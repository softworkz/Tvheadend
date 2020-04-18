namespace TVHeadEnd
{
    using System;
    using System.Threading.Tasks;

    using Emby.TV.Model.Setup.Interfaces;
    using Emby.TV.Model.Tuners;
    using Emby.TV.Model.Tuners.Interfaces;

    using MediaBrowser.Common.Net;
    using MediaBrowser.Common.Security;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.System;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.Setup;
    using TVHeadEnd.Setup.Stages;

    public class TunerProviderTvHeadend : ITunerProvider
    {
        public const string ProvDescription = "Provides live TV using Tvheadend as a back-end";
        public const string ProvId = "tvhclient2";
        public const string ProvName = "TV Headend";
        private readonly ILogger logger;
        private readonly ISecurityManager securityManager;
        private readonly IJsonSerializer jsonSerializer;
        private readonly IHttpClient httpClient;
        private readonly INetworkManager networkManager;
        private readonly IEnvironmentInfo environmentInfo;

        private static readonly TunerProviderCaps ProviderCapabilities = new TunerProviderCaps
                                                                             {
                                                                                 SupportsManualSetup = true,
                                                                                 SupportsAutoDetection = false,
                                                                                 SupportsGuidedDetection = false,
                                                                                 SupportsInbandChannelInfo = true,
                                                                                 SupportsInbandEpg = true,
                                                                             };

        private readonly IFileSystem fileSystem;

        public TunerProviderTvHeadend(
            ILogger logger,
            ILocalizationManager localizationManager,
            INetworkManager networkManager,
            ISecurityManager securityManager,
            IJsonSerializer jsonSerializer,
            IHttpClient httpClient,
            IEnvironmentInfo environmentInfo,
            IFileSystem fileSystem)
        {
            this.logger = logger;
            this.securityManager = securityManager;
            this.jsonSerializer = jsonSerializer;
            this.httpClient = httpClient;
            this.networkManager = networkManager;
            this.environmentInfo = environmentInfo;
            this.LocalizationManager = localizationManager;
            this.fileSystem = fileSystem;
        }

        /// <summary>Gets the provider identifier.</summary>
        /// <value>The provider identifier.</value>
        public string ProviderId => ProvId;

        /// <summary>Gets the provider name.</summary>
        /// <value>The provider name.</value>
        public string Name => ProvName;

        /// <summary>Gets the provider description.</summary>
        /// <value>The provider description.</value>
        public string Description => ProvDescription;

        /// <summary>Gets the provider capabilities.</summary>
        /// <value>The provider capabilities.</value>
        public ITunerProviderCaps Capabilities => ProviderCapabilities;

        /// <summary>Gets the type of the configuration object.</summary>
        /// <value>The type of the configuration object.</value>
        public Type ConfigurationObjectType => typeof(TvHeadendProviderConfig);

        /// <summary>Gets the type of the tuner configuration object.</summary>
        /// <value>The type of the tuner configuration object.</value>
        public Type TunerConfigurationObjectType => typeof(TvHeadendTunerConfig);

        /// <summary>Gets a custom caption for the guided detection button.</summary>
        /// <value>The guided detection caption.</value>
        /// <remarks>When no valid string is returned, the default caption is shown.</remarks>
        public string GuidedDetectionCaption => null;

        /// <summary>Gets a custom caption for the manual add button.</summary>
        /// <value>The manual add caption.</value>
        /// <remarks>When no valid string is returned, the default caption is shown.</remarks>
        public string ManualAddCaption
        {
            get
            {
                return "Connect to TV Headend";
            }
        }

        /// <summary>Checks whether the provider is supported on the current platform..</summary>
        /// <param name="environmentInfo">The environment information.</param>
        /// <returns>A boolean value.</returns>
        public bool CheckIsSupported(IEnvironmentInfo environmentInfo)
        {
            return true;
        }

        /// <summary>Gets the tuner detector for regular detection.</summary>
        /// <returns>An object implementing <see cref="T:Emby.TV.Model.Tuners.Interfaces.ITunerDetector" /> or null when <see cref="P:Emby.TV.Model.Tuners.Interfaces.ITunerProviderCaps.SupportsAutoDetection" /> is false.</returns>
        public ITunerDetector GetTunerDetector()
        {
            return null;
        }

        /// <summary>Gets the tuner detector for guided detection.</summary>
        /// <returns>An object implementing <see cref="T:Emby.TV.Model.Tuners.Interfaces.ITunerDetector" /> or null when <see cref="P:Emby.TV.Model.Tuners.Interfaces.ITunerProviderCaps.SupportsGuidedDetection" /> is false.</returns>
        public ITunerDetector GetGuidedTunerDetector()
        {
            return null;
        }

        protected ILocalizationManager LocalizationManager { get; }

        public void RemoveTuner(ITuner tuner)
        {
        }

        public Task StopTuner(ITuner tuner)
        {
            throw new NotImplementedException();
        }

        public Task StopTunerNode(ITunerNode tunerNode)
        {
            throw new NotImplementedException();
        }

        public Task EnableProvider(bool enable)
        {
            throw new NotImplementedException();
        }

        public Task EnableTuner(ITuner tuner, bool enable)
        {
            throw new NotImplementedException();
        }

        public Task EnableTunerNode(ITunerNode tunerNode, bool enable)
        {
            throw new NotImplementedException();
        }

        /// <summary>Start the guided detection UI process.</summary>
        /// <param name="previousSetupStage">The previous setup stage.</param>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the guided detection procedure.</returns>
        public Task<ISetupStage> SetupStartGuidedDetection(ISetupStage previousSetupStage, ITunerSetupManager tunerSetupManager)
        {
            return null;
        }

        /// <summary>User has chosen to add one of the detected or statically advertised tuners.</summary>
        /// <param name="previousSetupStage">The previous setup stage.</param>
        /// <param name="tunerInfo">The tuner information.</param>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the add-tuner procedure.</returns>
        public Task<ISetupStage> SetupAddTuner(ISetupStage previousSetupStage, TunerInfo tunerInfo, ITunerSetupManager tunerSetupManager)
        {
            return null;
        }

        /// <summary>Start the UI process for manually adding a tuner.</summary>
        /// <param name="previousSetupStage">The previous setup stage.</param>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the procedure to manually add a tuner.</returns>
        public Task<ISetupStage> SetupManualAddTuner(ISetupStage previousSetupStage, ITunerSetupManager tunerSetupManager)
        {
            var nativeSetupManager = new TvHeadendSetupManager(this,
                this.logger,
                this.LocalizationManager,
                this.fileSystem,
                this.environmentInfo,
                this.httpClient,
                this.jsonSerializer,
                tunerSetupManager,
                new TvHeadendTunerConfig());

            var newStage = new SetupStageTvhConnectionData(
                this,
                this.logger,
                tunerSetupManager,
                this.LocalizationManager,
                nativeSetupManager,
                previousSetupStage);
            return Task.FromResult<ISetupStage>(newStage);
        }
    }
}