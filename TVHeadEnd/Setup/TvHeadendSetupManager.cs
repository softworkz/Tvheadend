namespace TVHeadEnd.Setup
{
    using System.Threading.Tasks;

    using Emby.TV.Model.Providers.Tuners.Interfaces;
    using Emby.TV.Model.Setup;

    using MediaBrowser.Common.Net;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.System;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.HTSP;
    using TVHeadEnd.Setup.UiData;

    public class TvHeadendSetupManager
    {
        private readonly TunerProviderTvHeadend tunerProvider;
        private readonly ILogger logger;
        private readonly ILocalizationManager localizationManager;
        private readonly IFileSystem fileSystem;
        private readonly IEnvironmentInfo environmentInfo;
        private readonly IHttpClient httpClient;

        public TvHeadendSetupManager(
            TunerProviderTvHeadend tunerProvider,
            ILogger logger,
            ILocalizationManager localizationManager,
            IFileSystem fileSystem,
            IEnvironmentInfo environmentInfo,
            IHttpClient httpClient,
            IJsonSerializer jsonSerializer,
            ITunerSetupManager tunerSetupManager,
            TvHeadendTunerConfig tunerConfig)
        {
            this.tunerProvider = tunerProvider;
            this.logger = logger;
            this.localizationManager = localizationManager;
            this.fileSystem = fileSystem;
            this.environmentInfo = environmentInfo;
            this.httpClient = httpClient;
            this.JsonSerializer = jsonSerializer;
            this.TunerSetupManager = tunerSetupManager;
            this.TunerConfig = tunerConfig;
        }

        public ITunerSetupManager TunerSetupManager { get; }

        public IJsonSerializer JsonSerializer { get; }

        public TvHeadendTunerConfig TunerConfig { get; }

        public HtsServerInfo ServerInfo { get; set; }

        internal Task<IProviderSetupStage> GetNextStage(
            ProviderStageWizardBase setupStage,
            ILocalizationManager localizationManager)
        {
            ////if (setupStage is SetupStageAddSimulatedTuner addSimulatedTunerStage)
            ////{
            ////        ISetupStage newStage = new SetupStageSimulatedTunerData(this, this.logger, localizationManager, addSimulatedTunerStage.SecurityManager, originalpreviousSetupStage, previousSetupStage);
            ////        return Task.FromResult(newStage);
            ////}
            return null;
        }

        public static TvhConnectionDataUi ConvertToConnectionDataUi(TvHeadendTunerConfig tunerConfig)
        {
            var dataUi = new TvhConnectionDataUi
                             {
                                 TvhHost = tunerConfig.TvhServerName?.Trim(),
                                 HttpPort = tunerConfig.HttpPort,
                                 HtspPort = tunerConfig.HtspPort,
                                 UserName = tunerConfig.Username?.Trim(),
                                 Password = tunerConfig.Password?.Trim(),
                             };

            return dataUi;
        }

        public static void ApplyToConfig(TvHeadendTunerConfig tunerConfig, TvhConnectionDataUi dataUi)
        {
            tunerConfig.TvhServerName = dataUi.TvhHost?.Trim();
            tunerConfig.HttpPort = dataUi.HttpPort;
            tunerConfig.HtspPort = dataUi.HtspPort;
            tunerConfig.Username = dataUi.UserName?.Trim();
            tunerConfig.Password = dataUi.Password?.Trim();
        }

        public class CheckResult
        {
            public CheckResult(bool success, string message)
            {
                this.Success = success;
                this.Message = message;
            }

            public bool Success { get; }

            public string Message { get; }
        }
    }
}