namespace TVHeadEnd.Setup.Stages
{
    using System;
    using System.Threading.Tasks;

    using Emby.TV.Model.Setup;
    using Emby.TV.Model.Setup.Enums;
    using Emby.TV.Model.Setup.Interfaces;

    using MediaBrowser.Model.GenericEdit;
    using MediaBrowser.Model.Globalization;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Setup.UiData;

    public class SetupStageTvhConnectionData : WizardSetupStageBase
    {
        private readonly TunerProviderTvHeadend tunerProvider;
        private readonly ITunerSetupManager tunerSetupManager;
        private readonly TvHeadendSetupManager setupManager;

        public SetupStageTvhConnectionData(
            TunerProviderTvHeadend tunerProvider,
            ILogger logger,
            ITunerSetupManager tunerSetupManager,
            ILocalizationManager localizationManager,
            TvHeadendSetupManager setupManager,
            ISetupStage previouSetupStage)
            : base(SetupArea.Tuners, tunerProvider.ProviderId, logger, localizationManager, previouSetupStage, null)
        {
            this.tunerProvider = tunerProvider;
            this.tunerSetupManager = tunerSetupManager;
            this.setupManager = setupManager;
            this.Caption = localizationManager.GetLocalizedString("TV Headend");
            this.SubCaption = null;
            this.ContentData = TvHeadendSetupManager.ConvertToConnectionDataUi(setupManager.TunerConfig);
        }

        public override string Caption { get; }

        public override string SubCaption { get; }

        public override sealed IEditableObject ContentData { get; set; }

        protected TvhConnectionDataUi ConnectionData => this.ContentData as TvhConnectionDataUi;

        public override Task Cancel()
        {
            return Task.CompletedTask;
        }

        public override Task<ISetupStage> OnNextCommand(string providerId, string commandId, string data)
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

            var nextStage = new SetupStageTvhConnectionCheck(this.tunerProvider, this.tunerSetupManager, this.Logger, this.LocalizationManager, this.setupManager, this.OriginalSetupStage, this);
            return Task.FromResult<ISetupStage>(nextStage);
        }
    }
}