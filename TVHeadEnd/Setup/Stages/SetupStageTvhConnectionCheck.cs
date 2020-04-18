namespace TVHeadEnd.Setup.Stages
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Emby.GenericEdit.Compound;
    using Emby.TV.Model.Setup;
    using Emby.TV.Model.Setup.Enums;
    using Emby.TV.Model.Setup.Interfaces;

    using MediaBrowser.Model.GenericEdit;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;
    using TVHeadEnd.Setup.UiData;

    public class SetupStageTvhConnectionCheck : WizardSetupStageBase
    {
        private readonly TunerProviderTvHeadend tunerProvider;
        private readonly ITunerSetupManager tunerSetupManager;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly Task connectionCheckTask;
        private readonly TvHeadendSetupManager setupManager;
        private RemoteSetupHelper setupHelper;

        public SetupStageTvhConnectionCheck(
            TunerProviderTvHeadend tunerProvider,
            ITunerSetupManager tunerSetupManager,
            ILogger logger,
            ILocalizationManager localizationManager,
            TvHeadendSetupManager setupManager,
            ISetupStage originalpreviousSetupStage,
            ISetupStage previousSetupStage)
            : base(SetupArea.Tuners, tunerProvider.ProviderId, logger, localizationManager, originalpreviousSetupStage, previousSetupStage)
        {
            this.tunerProvider = tunerProvider;
            this.tunerSetupManager = tunerSetupManager;
            this.setupManager = setupManager;
            this.Caption = localizationManager.GetLocalizedString("Connect Remote Tuner");
            this.SubCaption = localizationManager.GetLocalizedString("Checking SSH connection");
            this.ContentData = new TvhConnectionCheckUi();
            this.setupHelper = new RemoteSetupHelper(logger);
            this.AllowBack = true;
            this.AllowNext = false;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.connectionCheckTask = Task.Run(this.PerformChecks, this.cancellationTokenSource.Token);
        }

        public override string Caption { get; }

        public override string SubCaption { get; }

        public override sealed IEditableObject ContentData { get; set; }

        protected TvhConnectionCheckUi ConnectionCheck => this.ContentData as TvhConnectionCheckUi;

        public override async Task Cancel()
        {
            if (this.connectionCheckTask?.Status == TaskStatus.Running)
            {
                this.cancellationTokenSource.Cancel();
                await this.connectionCheckTask.ConfigureAwait(false);
            }
        }

        public override Task<ISetupStage> OnNextCommand(string providerId, string commandId, string data)
        {
            var nextStage = new SetupStageTvhConnectionCheck(this.tunerProvider, this.tunerSetupManager, this.Logger, this.LocalizationManager, this.setupManager, this.OriginalSetupStage, this);
            return Task.FromResult<ISetupStage>(nextStage);
        }

        private async Task<bool> PerformChecks()
        {
            // Network location
            this.ConnectionCheck.StatusCheckNetworkLocation.Status = StatusItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = "Checking network location...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            this.RaiseInfoChanged();

            var result2 = await this.setupHelper.TestNetworkLocation(this.setupManager.TunerConfig, this.cancellationTokenSource.Token).ConfigureAwait(false);

            if (!result2.Success)
            {
                this.ConnectionCheck.StatusCheckNetworkLocation.Status = StatusItemStatus.Failed;
                this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = result2.Message;
                this.RaiseInfoChanged();
                return false;
            }

            this.ConnectionCheck.StatusCheckNetworkLocation.Status = StatusItemStatus.Succeeded;
            this.ConnectionCheck.StatusCheckNetworkLocation.StatusText = result2.Message;

            // Ping Time
            this.ConnectionCheck.StatusCheckPingTime.Status = StatusItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckPingTime.StatusText = "Checking ping time...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            var result3 = await this.setupHelper.TestNetworkLatency(this.setupManager.TunerConfig, this.cancellationTokenSource.Token).ConfigureAwait(false);

            if (!result3.Success)
            {
                this.ConnectionCheck.StatusCheckPingTime.Status = StatusItemStatus.Failed;
                this.ConnectionCheck.StatusCheckPingTime.StatusText = result3.Message;
                this.RaiseInfoChanged();
                return false;
            }

            this.ConnectionCheck.StatusCheckPingTime.Status = StatusItemStatus.Succeeded;
            this.ConnectionCheck.StatusCheckPingTime.StatusText = result3.Message;

            // Connection
            this.ConnectionCheck.StatusCheckConnection.Status = StatusItemStatus.InProgress;
            this.ConnectionCheck.StatusCheckConnection.StatusText = "Trying to connect to host...";

            this.RaiseInfoChanged();

            await Task.Delay(2000).ConfigureAwait(false);

            using (var htsConnectionHandler = new HtsConnectionHandler(this.Logger, this.setupManager.TunerConfig))
            {
                try
                {
                    if (!await htsConnectionHandler.Connect(0, this.cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        this.ConnectionCheck.StatusCheckConnection.Status = StatusItemStatus.Failed;
                        this.ConnectionCheck.StatusCheckConnection.StatusText = "Connection failed";
                        this.RaiseInfoChanged();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusCheckConnection.Status = StatusItemStatus.Failed;
                    this.ConnectionCheck.StatusCheckConnection.StatusText = "Connection failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }

                var status = "No Server Info";

                var serverInfo = htsConnectionHandler.ServerInfo;
                if (serverInfo != null)
                {
                    int usedHtsPversion = serverInfo.ServerProtocolVersion < (int)HtsMessage.HTSP_VERSION ? serverInfo.ServerProtocolVersion : (int)HtsMessage.HTSP_VERSION;

                    status = string.Format("Server: {0} {1}\nHTSP Version: {2}\nFree Diskspace: {3}", serverInfo.Servername, serverInfo.Serverversion, usedHtsPversion, serverInfo.Diskspace);
                }

                this.ConnectionCheck.StatusCheckConnection.Status = StatusItemStatus.Succeeded;
                this.ConnectionCheck.StatusCheckConnection.StatusText = status;

                // Authenticate
                this.ConnectionCheck.StatusCheckAuthenticate.Status = StatusItemStatus.InProgress;
                this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Trying to authenticate...";

                this.RaiseInfoChanged();

                await Task.Delay(2000).ConfigureAwait(false);
           
                try
                {
                    if (!htsConnectionHandler.Authenticate())
                    {
                        this.ConnectionCheck.StatusCheckAuthenticate.Status = StatusItemStatus.Failed;
                        this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Authentication failed";
                        this.RaiseInfoChanged();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusCheckAuthenticate.Status = StatusItemStatus.Failed;
                    this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Authentication failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }

                this.ConnectionCheck.StatusCheckAuthenticate.Status = StatusItemStatus.Succeeded;
                this.ConnectionCheck.StatusCheckAuthenticate.StatusText = "Successfully authenticated";

                // Channel Download
                this.ConnectionCheck.StatusDownloadChannels.Status = StatusItemStatus.InProgress;
                this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading Channels...";

                this.RaiseInfoChanged();

                await Task.Delay(2000).ConfigureAwait(false);

                try
                {
                    if (!await htsConnectionHandler.WaitForInitialLoadAsync(TimeSpan.FromSeconds(30), this.cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        this.ConnectionCheck.StatusDownloadChannels.Status = StatusItemStatus.Failed;
                        this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading channnels failed";
                        this.RaiseInfoChanged();
                        return false;
                    }

                    var result = await htsConnectionHandler.BuildChannelInfos(this.cancellationTokenSource.Token).ConfigureAwait(false);
                    var list = result.ToList();

                    this.ConnectionCheck.StatusDownloadChannels.Status = StatusItemStatus.Succeeded;
                    this.ConnectionCheck.StatusDownloadChannels.StatusText = string.Format("Downloaded {0} channels", list.Count);
                }
                catch (Exception ex)
                {
                    this.ConnectionCheck.StatusDownloadChannels.Status = StatusItemStatus.Failed;
                    this.ConnectionCheck.StatusDownloadChannels.StatusText = "Downloading channnels failed" + "\n" + ex.Message;
                    this.RaiseInfoChanged();
                    return false;
                }
            }

            this.AllowBack = true;
            this.AllowFinish = true;

            this.RaiseInfoChanged();

            return true;
        }
    }
}