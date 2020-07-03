namespace TVHeadEnd.Model
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Emby.TV.Model.Providers.Config;
    using Emby.TV.Model.Providers.Tuners.Interfaces;

    using MediaBrowser.Model.Entities;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.Setup.UiData;

    public class TvhTuner : ITuner
    {
        public TvhTuner(
            TunerProviderTvHeadend provider,
            ITunerCaps capabilities,
            TunerConfiguration tunerConfiguration)
        {
            this.TunerId = tunerConfiguration.TunerId;
            this.TunerGroupKey = tunerConfiguration.TunerGroupKey;
            this.Provider = provider;
            this.Name = tunerConfiguration.Name;
            this.Description = tunerConfiguration.Description;
            this.IsEnabled = tunerConfiguration.IsEnabled;
            this.Capabilities = capabilities;
            this.Nodes = null;
            this.Configuration = tunerConfiguration;
            this.TvhTunerConfig = (TvHeadendTunerConfig)tunerConfiguration.CustomConfiguration;
        }

        public Luid TunerId { get; }

        public string TunerGroupKey { get; }

        public ITunerProvider Provider { get; }

        public string Name { get; }

        public string Description { get; }

        public bool IsEnabled { get; private set; }

        public ITunerCaps Capabilities { get; }

        public TvHeadendTunerConfig TvhTunerConfig { get; private set; }

        public TunerConfiguration Configuration { get; }

        public Dictionary<Luid, ITunerNode> Nodes { get; }

        /// <summary>Gets the UI data object for tuner configuration.</summary>
        /// <returns>An instance of <see cref="EditableConfigurationBase"/>.</returns>
        public virtual EditableConfigurationBase GetTunerConfigurationUi()
        {
            return new TvhConnectionDataUi(this.TvhTunerConfig);
        }

        /// <summary>Applies the tuner configuration.</summary>
        /// <param name="configurationData">The configueration data.</param>
        /// <param name="tunerSetupManager"></param>
        /// <param name="token"></param>
        /// <returns>True, if the tuner requires to be re-loaded to activate the configuration change.</returns>
        public virtual async Task<bool> ApplyTunerConfigurationFromUi(EditableConfigurationBase configurationData, ITunerSetupManager tunerSetupManager, CancellationToken token)
        {
            var configUi = (TvhConnectionDataUi)configurationData;

            configUi.ApplyToConfig(this.TvhTunerConfig);

            await tunerSetupManager.UpdateConfguration(this, configurationData, token).ConfigureAwait(false);

            return true;
        }

        /// <summary>Shuts down the tuner.</summary>
        /// <returns>A task.</returns>
        /// <remarks>
        ///     <note type="warning">
        ///         <para>
        ///             ShutDown should take no longer than 3 seconds, after that time, the tuner will get disposed and unloaded.
        ///         </para>
        ///     </note>
        /// </remarks>
        public Task ShutDown()
        {
            return Task.CompletedTask;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
        }
    }
}