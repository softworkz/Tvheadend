namespace TVHeadEnd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.Drawing;
    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Model.Drawing;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.DataHelper;
    using TVHeadEnd.HTSP;

    internal class HtsConnectionHandler : IDisposable
    {
        private readonly AutorecDataHelper autorecDataHelper;

        // Data helpers
        private readonly ChannelDataHelper channelDataHelper;
        private readonly DvrDataHelper dvrDataHelper;

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();

        private readonly ILogger logger;
        private readonly TvHeadendTunerConfig tunerConfig;

        private HtsConnection htsConnection;

        private volatile bool initialLoadFinished;

        public HtsConnectionHandler(ILogger logger, TvHeadendTunerConfig tunerConfig)
        {
            this.logger = logger;
            this.tunerConfig = tunerConfig;

            this.logger.Info("[TVHclient] HTSConnectionHandler()");

            this.channelDataHelper = new ChannelDataHelper(logger);
            this.dvrDataHelper = new DvrDataHelper(logger);
            this.autorecDataHelper = new AutorecDataHelper(logger);

            this.Init();

            this.channelDataHelper.SetChannelType4Other(this.tunerConfig.ChannelType);
        }

        public bool IsConnected
        {
            get
            {
                return this.htsConnection?.IsConnected ?? false;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                return this.htsConnection != null && this.htsConnection.IsConnected && this.htsConnection.IsAuthenticated;
            }
        }

        public Task<IEnumerable<SeriesTimerInfo>> BuildAutorecInfos(CancellationToken cancellationToken)
        {
            return this.autorecDataHelper.BuildAutorecInfos(cancellationToken);
        }

        public Task<IEnumerable<ChannelInfo>> BuildChannelInfos(CancellationToken cancellationToken)
        {
            return this.channelDataHelper.BuildChannelInfos(cancellationToken);
        }

        public Task<IEnumerable<MyRecordingInfo>> BuildDvrInfos(CancellationToken cancellationToken)
        {
            return this.dvrDataHelper.BuildDvrInfos(cancellationToken);
        }

        public Task<IEnumerable<TimerInfo>> BuildPendingTimersInfos(CancellationToken cancellationToken)
        {
            return this.dvrDataHelper.BuildPendingTimersInfos(cancellationToken);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            this.htsConnection?.Stop();
            this.htsConnection = null;
        }

        public ImageStream GetChannelImage(string channelId, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() channelId: " + channelId);

                string channelIcon = this.channelDataHelper.GetChannelIcon4ChannelId(channelId);

                this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() channelIcon: " + channelIcon);

                WebRequest request;

                if (channelIcon.StartsWith("http"))
                {
                    request = WebRequest.Create(channelIcon);

                    this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() WebRequest: " + channelIcon);
                }
                else
                {
                    string requestStr = "http://" + this.tunerConfig.TvhServerName + ":" + this.tunerConfig.HttpPort + this.WebRoot + "/" + channelIcon;
                    request = WebRequest.Create(requestStr);
                    request.Headers["Authorization"] = this.headers["Authorization"];

                    this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() WebRequest: " + requestStr);
                }

                HttpWebResponse httpWebReponse = (HttpWebResponse)request.GetResponse();
                Stream stream = httpWebReponse.GetResponseStream();

                ImageStream imageStream = new ImageStream { Stream = stream };

                int lastDot = channelIcon.LastIndexOf('.');
                if (lastDot > -1)
                {
                    string suffix = channelIcon.Substring(lastDot + 1);
                    suffix = suffix.ToLower();

                    this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() image suffix: " + suffix);

                    if (Enum.TryParse(suffix, true, out ImageFormat imgFormat))
                    {
                        imageStream.Format = imgFormat;
                        this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() using fix image type {0}.", suffix.ToUpper());
                    }
                    else
                    {
                        this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() unkown image type '" + suffix + "' - return as PNG");
                        imageStream.Format = ImageFormat.Png;
                    }
                }
                else
                {
                    this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() no image type in suffix of channelImage name '" + channelIcon + "' found - return as PNG.");
                    imageStream.Format = ImageFormat.Png;
                }

                return imageStream;
            }
            catch (Exception ex)
            {
                this.logger.Error("[TVHclient] HTSConnectionHandler.GetChannelImage() caught exception: " + ex.Message);
                return null;
            }
        }

        public string GetChannelImageUrl(string channelId)
        {
            this.logger.Info("[TVHclient] HTSConnectionHandler.GetChannelImage() channelId: " + channelId);

            string channelIcon = this.channelDataHelper.GetChannelIcon4ChannelId(channelId);

            if (string.IsNullOrEmpty(channelIcon))
            {
                return null;
            }

            if (channelIcon.StartsWith("http"))
            {
                return this.channelDataHelper.GetChannelIcon4ChannelId(channelId);
            }
            else
            {
                return "http://" + this.tunerConfig.Username + ":" + this.tunerConfig.Password + "@" + this.tunerConfig.TvhServerName + ":" + this.tunerConfig.HttpPort + this.WebRoot + "/" + channelIcon;
            }
        }

        public TvHeadendTunerConfig Configuration
        {
            get
            {
                return this.tunerConfig;
            }
        }

        public Dictionary<string, string> GetHeaders()
        {
            return new Dictionary<string, string>(this.headers);
        }

        public HtsServerInfo ServerInfo
        {
            get
            {
                if (this.IsConnected)
                {
                    return this.htsConnection?.ServerInfo;
                }

                return null;
            }
        }

        public string HttpBaseUrl { get; private set; }

        private string WebRoot
        {
            get
            {
                return this.htsConnection?.ServerInfo?.WebRoot ?? string.Empty;
            }
        }

        private void CloseConnection()
        {
            var connection = this.htsConnection;
            if (connection != null)
            {
                connection.ConnectionError -= this.HtsConnection_ConnectionError;
                connection.MessageReceived -= this.HtsConnection_MessageReceived;

                try
                {
                    connection.Stop();
                }
                catch
                {
                    // Ignore
                }

                this.htsConnection = null;
                this.initialLoadFinished = false;
            }

            this.htsConnection = null;
        }

        public void SendMessage(HtsMessage message, IHtsResponseHandler responseHandler)
        {
            if (!this.IsAuthenticated)
            {
                throw new InvalidOperationException("Not connected");
            }

            this.htsConnection.SendMessage(message, responseHandler);
        }

        public async Task<bool> WaitForInitialLoadAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();

            while (this.IsAuthenticated && !this.initialLoadFinished && !cancellationToken.IsCancellationRequested && watch.Elapsed < timeout)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }

            return this.IsAuthenticated && this.initialLoadFinished;
        }

        // private static Stream ImageToPNGStream(Image image)
        // {
        // Stream stream = new System.IO.MemoryStream();
        // image.Save(stream, ImageFormat.Png);
        // stream.Position = 0;
        // return stream;
        // }

        public async Task<bool> Connect(int retries, CancellationToken cancellationToken)
        {
            // _logger.Info("[TVHclient] HTSConnectionHandler.ensureConnection()");
            if (this.htsConnection?.NeedsRestart ?? false)
            {
                this.CloseConnection();
            }

            if (this.htsConnection == null)
            {
                this.logger.Info("[TVHclient] HTSConnectionHandler.Connect() : create new HTS-Connection");
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                this.htsConnection = new HtsConnection("TVHclient4Emby-" + version, string.Empty + HtsMessage.HTSP_VERSION, this.logger);
                this.htsConnection.ConnectionError += this.HtsConnection_ConnectionError;
                this.htsConnection.MessageReceived += this.HtsConnection_MessageReceived;
            }

            if (this.htsConnection.IsConnected)
            {
                return true;
            }

            this.logger.Info(
                    "[TVHclient] HTSConnectionHandler.Connect: Used connection parameters: " +
                    "TVH Server = '" + this.tunerConfig.TvhServerName + "'; " +
                    "HTTP Port = '" + this.tunerConfig.HttpPort + "'; " +
                    "HTSP Port = '" + this.tunerConfig.HtspPort + "'; ");

            var connected = await this.htsConnection.Open(this.tunerConfig.TvhServerName, this.tunerConfig.HtspPort, this.tunerConfig.Username, retries, cancellationToken).ConfigureAwait(false);

            this.logger.Info("[TVHclient] HTSConnectionHandler.Connect: connection established: " + connected);

            return connected;
        }

        public bool Authenticate()
        {
            if (!this.IsConnected)
            {
                throw new InvalidOperationException("Not connected");
            }

            if (this.IsAuthenticated)
            {
                return true;
            }

            this.logger.Info(
                    "[TVHclient] HTSConnectionHandler.Authenticate: Used parameters: " +
                    "User = '" + this.tunerConfig.Username + "'; " +
                    "Password set = '" + (this.tunerConfig.Password?.Length > 0) + "'");

            var authenticated = this.htsConnection.Authenticate(this.tunerConfig.Username, this.tunerConfig.Password);

            this.logger.Info("[TVHclient] HTSConnectionHandler.Authenticate: authenticated: " + authenticated);

            return authenticated;
        }

        private async Task<bool> EnsureConnection(int retries = 3)
        {
            if (this.IsAuthenticated)
            {
                return true;
            }

            if (!await this.Connect(retries, CancellationToken.None).ConfigureAwait(false))
            {
                return false;
            }

            return this.Authenticate();
        }

        private void HtsConnection_MessageReceived(object sender, HtsMessage response)
        {
            if (response != null)
            {
                System.Diagnostics.Debug.Print("  Message: {0}", response.Method);

                switch (response.Method)
                {
                    case "tagAdd":
                    case "tagUpdate":
                    case "tagDelete":
                        // _logger.Fatal("[TVHclient] tad add/update/delete" + response.ToString());
                        break;

                    case "channelAdd":
                    case "channelUpdate":
                        this.channelDataHelper.Add(response);
                        break;

                    case "dvrEntryAdd":
                        this.dvrDataHelper.DvrEntryAdd(response);
                        break;
                    case "dvrEntryUpdate":
                        this.dvrDataHelper.DvrEntryUpdate(response);
                        break;
                    case "dvrEntryDelete":
                        this.dvrDataHelper.DvrEntryDelete(response);
                        break;

                    case "autorecEntryAdd":
                        this.autorecDataHelper.AutorecEntryAdd(response);
                        break;
                    case "autorecEntryUpdate":
                        this.autorecDataHelper.AutorecEntryUpdate(response);
                        break;
                    case "autorecEntryDelete":
                        this.autorecDataHelper.AutorecEntryDelete(response);
                        break;

                    case "eventAdd":
                    case "eventUpdate":
                    case "eventDelete":
                        // should not happen as we don't subscribe for this events.
                        break;

                    // case "subscriptionStart":
                    // case "subscriptionGrace":
                    // case "subscriptionStop":
                    // case "subscriptionSkip":
                    // case "subscriptionSpeed":
                    // case "subscriptionStatus":
                    // _logger.Fatal("[TVHclient] subscription events " + response.ToString());
                    // break;

                    // case "queueStatus":
                    // _logger.Fatal("[TVHclient] queueStatus event " + response.ToString());
                    // break;

                    // case "signalStatus":
                    // _logger.Fatal("[TVHclient] signalStatus event " + response.ToString());
                    // break;

                    // case "timeshiftStatus":
                    // _logger.Fatal("[TVHclient] timeshiftStatus event " + response.ToString());
                    // break;

                    // case "muxpkt": // streaming data
                    // _logger.Fatal("[TVHclient] muxpkt event " + response.ToString());
                    // break;
                    case "initialSyncCompleted":
                        this.initialLoadFinished = true;
                        break;

                    default:
                        // _logger.Fatal("[TVHclient] Method '" + response.Method + "' not handled in LiveTvService.cs");
                        break;
                }
            }
        }

        private void HtsConnection_ConnectionError(object sender, Exception ex)
        {
            this.logger.ErrorException("[TVHclient] HTSConnectionHandler recorded a HTSP error: " + ex.Message, ex);
            this.CloseConnection();

            // _liveTvService.sendDataSourceChanged();
            this.EnsureConnection();
        }

        private void Init()
        {
            var config = this.tunerConfig;

            if (string.IsNullOrEmpty(config.TvhServerName))
            {
                string message = "[TVHclient] HTSConnectionHandler.ensureConnection: TVH-Server name must be configured.";
                this.logger.Error(message);
                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrEmpty(config.Username))
            {
                string message = "[TVHclient] HTSConnectionHandler.ensureConnection: Username must be configured.";
                this.logger.Error(message);
                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrEmpty(config.Password))
            {
                string message = "[TVHclient] HTSConnectionHandler.ensureConnection: Password must be configured.";
                this.logger.Error(message);
                throw new InvalidOperationException(message);
            }

            if (config.EnableSubsMaudios)
            {
                // Use HTTP basic auth instead of TVH ticketing system for authentication to allow the users to switch subs or audio tracks at any time
                this.HttpBaseUrl = "http://" + config.Username + ":" + config.Password + "@" + config.TvhServerName + ":" + config.HttpPort + this.WebRoot;
            }
            else
            {
                this.HttpBaseUrl = "http://" + config.TvhServerName + ":" + config.HttpPort + this.WebRoot;
            }

            string authInfo = config.Username + ":" + config.Password;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
            this.headers["Authorization"] = "Basic " + authInfo;
        }
    }
}