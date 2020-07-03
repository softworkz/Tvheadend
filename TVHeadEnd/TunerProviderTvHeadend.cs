namespace TVHeadEnd
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Emby.TV.Model.Common.Enums;
    using Emby.TV.Model.Providers.Config;
    using Emby.TV.Model.Providers.Tuners.Interfaces;
    using Emby.TV.Model.Setup;

    using MediaBrowser.Common.Net;
    using MediaBrowser.Common.Security;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.System;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.Model;
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
            this.Capabilities = new ProviderCaps();
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

        /// <summary>Occurs when a tuner's availability has changed].</summary>
        public event EventHandler TunerAvailabilityChanged;

        /// <summary>Gets the provider capabilities.</summary>
        /// <value>The provider capabilities.</value>
        public ITunerProviderCaps Capabilities { get; }

        /// <summary>Shuts down the provider.</summary>
        /// <returns>A task.</returns>
        /// <remarks>
        ///     <note type="warning">
        ///         <para>
        ///             ShutDown should take no longer than 3 seconds, after that time, the provider will get disposed and unloaded.
        ///         </para>
        ///     </note>
        /// </remarks>
        public Task ShutDown()
        {
            return Task.CompletedTask;
        }

        /// <summary>Gets the type of the configuration object.</summary>
        /// <value>The type of the configuration object.</value>
        public Type ConfigurationObjectType => typeof(TvHeadendProviderConfig);

        /// <summary>Gets the UI data object for user configuration.</summary>
        /// <returns>An instance of <see cref="EditableConfigurationBase"/>.</returns>
        public EditableConfigurationBase GetProviderConfigurationUi()
        {
            return null;
        }

        /// <summary>Applies the user configuration.</summary>
        /// <param name="userConfigurationUi">The user configuration data.</param>
        /// <param name="tunerSetupManager"></param>
        /// <param name="token"></param>
        public Task<bool> ApplyProviderConfigurationFromUi(EditableConfigurationBase userConfigurationUi, ITunerSetupManager tunerSetupManager, CancellationToken token)
        {
            return null;
        }

        /// <summary>Gets the type of the tuner configuration object.</summary>
        /// <value>The type of the tuner configuration object.</value>
        public Type TunerConfigurationObjectType => typeof(TvHeadendTunerConfig);

        /// <summary>Gets the type of the tuner node configuration.</summary>
        /// <value>The type of the tuner node configuration.</value>
        /// <remarks>May be null if not required.</remarks>
        public virtual Type TunerNodeConfigurationObjectType => typeof(TvHeadendTunerConfig);

        public TvHeadendProviderConfig Configuration { get; private set; }

        public Task<bool> Initialize(EditableConfigurationBase editableConfiguration, CancellationToken token)
        {
            this.Configuration = (TvHeadendProviderConfig)editableConfiguration;

            return Task.FromResult(true);
        }

        /// <summary>Checks whether the provider is supported on the current platform..</summary>
        /// <param name="envInfo">The environment information.</param>
        /// <returns>A boolean value.</returns>
        public bool CheckIsSupported(IEnvironmentInfo envInfo)
        {
            return true;
        }

        /// <summary>Gets the static available tuners.</summary>
        /// <returns>A collection of <see cref="TunerConfiguration"/>s.</returns>
        /// <remarks>This will be called only once during startup.</remarks>
        public IList<TunerConfiguration> GetStaticAvailableTuners()
        {
            return null;
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

        /// <summary>Queries the tuner availability.</summary>
        /// <param name="tunerConfiguration">The tuner configuration.</param>
        /// <param name="tunerNodeConfiguration">The tuner node configuration.</param>
        /// <param name="forceRefresh">if set to <c>true</c> requery status, don't return a cached value.</param>
        /// <returns>The availability status of the tuner.</returns>
        public Task<TvResourceStatus> QueryTunerAvailability(TunerConfiguration tunerConfiguration, TunerNodeConfiguration tunerNodeConfiguration, bool forceRefresh)
        {
            // TODO:
            return Task.FromResult(TvResourceStatus.Available);
        }

        /// <summary>Called to create and initialize an <see cref="ITuner"/> instance.</summary>
        /// <param name="tunerConfiguration">
        ///     The editable configuration of the type indicated by the
        ///     <see cref="ITunerProvider.TunerConfigurationObjectType" /> property.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        ///     <note type="warning">
        ///         <para>
        ///             Initialization should take no longer than 10 seconds, otherwise initialization will be cancelled and the
        ///             tuner will get disposed.
        ///         </para>
        ///         <para>
        ///             Things like establishing remote connections or other longer running tasks should be performed async.
        ///         </para>
        ///     </note>
        /// </remarks>
        public Task<ITuner> CreateTuner(TunerConfiguration tunerConfiguration, CancellationToken token)
        {
            var tuner = new TvhTuner(this, new TunerCaps(), tunerConfiguration);

            return Task.FromResult<ITuner>(tuner);
        }

        /// <summary>Start the guided detection UI process.</summary>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the guided detection procedure.</returns>
        public Task<IProviderSetupStage> SetupStartGuidedDetection(ITunerSetupManager tunerSetupManager)
        {
            return null;
        }

        /// <summary>User has chosen to add one of the detected or statically advertised tuners.</summary>
        /// <param name="tunerInfo">The tuner information.</param>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the add-tuner procedure.</returns>
        public Task<IProviderSetupStage> SetupAddTuner(TunerConfiguration tunerInfo, ITunerSetupManager tunerSetupManager)
        {
            return null;
        }

        /// <summary>Start the UI process for manually adding a tuner.</summary>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.Interfaces.ISetupStage" /> implementing the first UI step for the procedure to manually add a tuner.</returns>
        public Task<IProviderSetupStage> SetupManualAddTuner(ITunerSetupManager tunerSetupManager)
        {
            var nativeSetupManager = new TvHeadendSetupManager(
                this,
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
                nativeSetupManager);
            return Task.FromResult<IProviderSetupStage>(newStage);
        }

        /// <summary>Start a new interactive channel scan.</summary>
        /// <param name="tuner">The tuner.</param>
        /// <param name="tunerSetupManager">The tuner setup manager.</param>
        /// <returns>An <see cref="T:Emby.TV.Model.Setup.IProviderSetupStage" /> implementing the first UI step for the channel scan procedure.</returns>
        public virtual Task<IProviderSetupStage> SetupRunChannelScan(ITuner tuner, ITunerSetupManager tunerSetupManager)
        {
            throw new NotImplementedException();
        }

        public string CreateTunerGroupKey(TvHeadendTunerConfig tunerConfig)
        {
            var tunerName = string.Format("TvHeadend:{0}:{1}", tunerConfig.TvhServerName, tunerConfig.HtspPort);
            return tunerName;
        }

        public string CreateTunerName(TvHeadendTunerConfig tunerConfig)
        {
            var tunerName = string.Format("TV Headend [{0}]", tunerConfig.TvhServerName);
            return tunerName;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
        }
    }
}