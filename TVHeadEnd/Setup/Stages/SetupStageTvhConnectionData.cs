namespace TVHeadEnd.Setup.Stages
{
    using System;
    using System.Threading.Tasks;

    using Emby.TV.Model.Providers.Tuners.Interfaces;
    using Emby.TV.Model.Setup;

    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Setup.UiData;

    public class SetupStageTvhConnectionData : ProviderStageWizardBase
    {
        private readonly TunerProviderTvHeadend tunerProvider;
        private readonly ITunerSetupManager tunerSetupManager;
        private readonly ILocalizationManager localizationManager;
        private readonly TvHeadendSetupManager setupManager;

        public SetupStageTvhConnectionData(
            TunerProviderTvHeadend tunerProvider,
            ILogger logger,
            ITunerSetupManager tunerSetupManager,
            ILocalizationManager localizationManager,
            TvHeadendSetupManager setupManager)
            : base(tunerProvider.ProviderId, logger)
        {
            this.tunerProvider = tunerProvider;
            this.tunerSetupManager = tunerSetupManager;
            this.localizationManager = localizationManager;
            this.setupManager = setupManager;
            this.Caption = localizationManager.GetLocalizedString("TV Headend");
            this.SubCaption = null;
            this.ContentData = TvHeadendSetupManager.ConvertToConnectionDataUi(setupManager.TunerConfig);
        }

        protected TvhConnectionDataUi ConnectionData => this.ContentData as TvhConnectionDataUi;

        public override Task Cancel()
        {
            return Task.CompletedTask;
        }

        public override Task<IProviderSetupStage> OnNextCommand(string providerId, string commandId, string data)
        {
            if (string.IsNullOrWhiteSpace(this.ConnectionData.UserName))
            {
                throw new ApplicationException("Please specify a user name");
            }

            if (string.IsNullOrWhiteSpace(this.ConnectionData.TvhHost))
            {
                throw new ApplicationException("Please specify a host name");
            }

            TvHeadendSetupManager.ApplyToConfig(this.setupManager.TunerConfig, this.ConnectionData);

            var nextStage = new SetupStageTvhConnectionCheck(this.tunerProvider, this.tunerSetupManager, this.Logger, this.localizationManager, this.setupManager);
            return Task.FromResult<IProviderSetupStage>(nextStage);
        }
    }
}