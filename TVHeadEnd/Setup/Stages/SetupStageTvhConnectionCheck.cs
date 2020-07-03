namespace TVHeadEnd.Setup.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Emby.TV.Model.Common.Enums;
    using Emby.TV.Model.ProviderData;
    using Emby.TV.Model.Providers.Config;
    using Emby.TV.Model.Providers.Tuners;
    using Emby.TV.Model.Providers.Tuners.Interfaces;
    using Emby.TV.Model.Setup;
    using Emby.Web.GenericEdit.Elements;

    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;
    using TVHeadEnd.Model;
    using TVHeadEnd.Setup.UiData;

    public class SetupStageTvhConnectionCheck : ProviderStageWizardBase
    {
        public const string ShowScanResult = nameof(ShowScanResult);

        private readonly TunerProviderTvHeadend tunerProvider;
        private readonly ITunerSetupManager tunerSetupManager;
        private readonly ILocalizationManager localizationManager;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly Task connectionCheckTask;
        private readonly TvHeadendSetupManager setupManager;
        private readonly RemoteSetupHelper setupHelper;
        private List<ChannelInfo> channelInfos;

        public SetupStageTvhConnectionCheck(
            TunerProviderTvHeadend tunerProvider,
            ITunerSetupManager tunerSetupManager,
            ILogger logger,
            ILocalizationManager localizationManager,
            TvHeadendSetupManager setupManager)
            : base(tunerProvider.ProviderId, logger)
        {
            this.tunerProvider = tunerProvider;
            this.tunerSetupManager = tunerSetupManager;
            this.localizationManager = localizationManager;
            this.setupManager = setupManager;
            this.Caption = localizationManager.GetLocalizedString("Connect Remote Tuner");
            this.SubCaption = localizationManager.GetLocalizedString("Checking SSH connection");
            this.ContentData = new TvhConnectionCheckUi(this.ProviderId);
            this.setupHelper = new RemoteSetupHelper(logger);
            this.AllowBack = true;
            this.AllowNext = false;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.connectionCheckTask = Task.Run(this.PerformChecks, this.cancellationTokenSource.Token);
        }

        protected TvhConnectionCheckUi ConnectionCheck => this.ContentData as TvhConnectionCheckUi;

        public override async Task Cancel()
        {
            if (this.connectionCheckTask?.Status == TaskStatus.Running)
            {
                this.cancellationTokenSource.Cancel();
                await this.connectionCheckTask.ConfigureAwait(false);
            }
        }

        public override bool IsCommandAllowed(string commandKey)
        {
            if (commandKey == ShowScanResult)
            {
                return true;
            }

            return base.IsCommandAllowed(commandKey);
        }

        public override Task<IProviderSetupStage> RunCommand(string itemId, string commandId, string data)
        {
            if (commandId != ShowScanResult)
            {
                return base.RunCommand(itemId, commandId, data);
            }

            var channelScan = this.CreateProviderChannelScan();

            var stage = this.setupManager.TunerSetupManager.GetShowScanResultsDialog(this, channelScan, this.tunerProvider.Capabilities);
            return Task.FromResult(stage);
        }

        public override async Task<IProviderSetupStage> OnNextCommand(string providerId, string commandId, string data)
        {
            var channelScan = this.CreateProviderChannelScan();

            var config = this.setupManager.TunerConfig;
            var tunerName = this.tunerProvider.CreateTunerName(config);
            var tunerKey = this.tunerProvider.CreateTunerGroupKey(config);
            var node = new TunerNodeConfiguration
            {
                IsEnabled = true,
                NodeName = "Node 1",
                Description = string.Empty,
                TunerNodeKey = tunerKey,
            };

            var tunerConfig = new TunerConfiguration()
            {
                CustomConfiguration = config,
                TunerGroupKey = tunerKey,
                ProviderId = this.tunerProvider.ProviderId,
                Name = tunerName,
                Description = this.setupManager.ServerInfo.Servername + " " + this.setupManager.ServerInfo.Serverversion,
                IsEnabled = true,
                Nodes = new List<TunerNodeConfiguration> { node },
            };

            return await this.tunerSetupManager.GetProcessChannelScanStage(this, this.tunerProvider, tunerConfig, channelScan).ConfigureAwait(false);
        }

        private ProviderChannelScan CreateProviderChannelScan()
        {
            var channelScan = new ProviderChannelScan
            {
                Duration = TimeSpan.MinValue,
                LogFilePath = null,
                ScanDate = DateTime.Now.ToUniversalTime(),
                TunerProviderId = this.ProviderId,
                ScannedStreams = this.CreateChannelsList(),
            };

            return channelScan;
        }

        private List<ProviderScannedStream> CreateChannelsList()
        {
            var result = new List<ProviderScannedStream>();

            foreach (var channelInfo in this.channelInfos)
            {
                var tuningData = channelInfo.Id.ToString();
                string providerName = null;

                if (!string.IsNullOrEmpty(channelInfo.ServiceName))
                {
                    var parts = channelInfo.ServiceName.Split('/');

                    if (parts.Length > 1)
                    {
                        tuningData = parts[1];
                        providerName = parts[0];
                    }
                }

                var source = new ProviderScannedStream()
                {
                    TuningDataHash = Math.Abs(tuningData.GetHashCode()),
                    TuningData = tuningData,
                    TuningInfo = tuningData,
                    TuningInfoShort = tuningData,
                };

                var channel = new ProviderScannedChannel()
                {
                    Name = channelInfo.Name,
                    ServiceId = channelInfo.Id,
                    ScannedStreamId = source.ScannedStreamId,
                    ScannedStream = source,
                    ChannelType = channelInfo.ChannelType,
                    ProviderName = providerName,
                    ChannelNumber = channelInfo.ChannelNumber,
                    ChannelNumMinor = channelInfo.ChannelNumberMinor,
                    ImageUrl = channelInfo.ImageUrl,
                };

                source.ScannedChannels = new List<ProviderScannedChannel> { channel };

                result.Add(source);
            }

            return result;
        }

        private async Task<bool> PerformChecks()
        {
            // Network location
            this.ConnectionCheck.StatusCheckNetworkLocation.Status = ItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = "Checking network location...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            this.RaiseInfoChanged();

            var result2 = await this.setupHelper.TestNetworkLocation(this.setupManager.TunerConfig, this.cancellationTokenSource.Token).ConfigureAwait(false);

            if (!result2.Success)
            {
                this.ConnectionCheck.StatusCheckNetworkLocation.Status = ItemStatus.Failed;
                this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = result2.Message;
                this.RaiseInfoChanged();
                return false;
            }

            this.ConnectionCheck.StatusCheckNetworkLocation.Status = ItemStatus.Succeeded;
            this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = result2.Message;

            // Ping Time
            this.ConnectionCheck.StatusCheckPingTime.Status = ItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckPingTime.StatusText = "Checking ping time...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            var result3 = await this.setupHelper.TestNetworkLatency(this.setupManager.TunerConfig, this.cancellationTokenSource.Token).ConfigureAwait(false);

            if (!result3.Success)
            {
                this.ConnectionCheck.StatusCheckPingTime.Status = ItemStatus.Failed;
                this.ConnectionCheck.StatusCheckPingTime.StatusText = result3.Message;
                this.RaiseInfoChanged();
                return false;
            }

            this.ConnectionCheck.StatusCheckPingTime.Status = ItemStatus.Succeeded;
            this.ConnectionCheck.StatusCheckPingTime.StatusText = result3.Message;

            // Connection
            this.ConnectionCheck.StatusCheckConnection.Status = ItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckConnection.StatusText = "Trying to connect to host...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            using (var htsConnectionHandler = new HtsConnectionHandler(this.Logger, this.setupManager.TunerConfig))
            {
                try
                {
                    if (!await htsConnectionHandler.Connect(0, this.cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        this.ConnectionCheck.StatusCheckConnection.Status = ItemStatus.Failed;
                        this.ConnectionCheck.StatusCheckConnection.StatusText = "Connection failed";
                        this.RaiseInfoChanged();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusCheckConnection.Status = ItemStatus.Failed;
                    this.ConnectionCheck.StatusCheckConnection.StatusText = "Connection failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }

                var status = "No Server Info";

                var serverInfo = htsConnectionHandler.ServerInfo;
                if (serverInfo != null)
                {
                    this.setupManager.ServerInfo = serverInfo;
                    int usedHtsPversion = serverInfo.ServerProtocolVersion < (int)HtsMessage.HTSP_VERSION ? serverInfo.ServerProtocolVersion : (int)HtsMessage.HTSP_VERSION;

                    status = string.Format(
                        "Server: {0} {1}\nHTSP Version: {2}",
                        serverInfo.Servername,
                        serverInfo.Serverversion,
                        usedHtsPversion);
                }

                this.ConnectionCheck.StatusCheckConnection.Status = ItemStatus.Succeeded;
                this.ConnectionCheck.StatusCheckConnection.StatusText = status;

                // Authenticate
                this.ConnectionCheck.StatusCheckAuthenticate.Status = ItemStatus.InProgress;
                this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Trying to authenticate...";

                this.RaiseInfoChanged();

                await Task.Delay(2000).ConfigureAwait(false);

                try
                {
                    if (!htsConnectionHandler.Authenticate())
                    {
                        this.ConnectionCheck.StatusCheckAuthenticate.Status = ItemStatus.Failed;
                        this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Authentication failed";
                        this.RaiseInfoChanged();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusCheckAuthenticate.Status = ItemStatus.Failed;
                    this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Authentication failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }

                this.ConnectionCheck.StatusCheckAuthenticate.Status = ItemStatus.Succeeded;
                this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Successfully authenticated";

                // Channel Download
                this.ConnectionCheck.StatusDownloadChannels.Status = ItemStatus.InProgress;
                this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading Channels...";

                this.RaiseInfoChanged();

                await Task.Delay(2000).ConfigureAwait(false);

                try
                {
                    if (!await htsConnectionHandler.WaitForInitialLoadAsync(TimeSpan.FromSeconds(30), this.cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        this.ConnectionCheck.StatusDownloadChannels.Status = ItemStatus.Failed;
                        this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading channnels failed";
                        this.RaiseInfoChanged();
                        return false;
                    }

                    var result = await htsConnectionHandler.BuildChannelInfos(this.cancellationTokenSource.Token).ConfigureAwait(false);
                    this.channelInfos = result.ToList();

                    this.ConnectionCheck.StatusDownloadChannels.Status = ItemStatus.Succeeded;
                    this.ConnectionCheck.StatusDownloadChannels.StatusText = string.Format("Downloaded {0} channels", this.channelInfos.Count);
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusDownloadChannels.Status = ItemStatus.Failed;
                    this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading channnels failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }
            }

            this.AllowBack = true;
            this.AllowNext = true;

            this.RaiseInfoChanged();

            return true;
        }
    }
}