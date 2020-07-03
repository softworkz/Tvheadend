namespace TVHeadEnd.Setup
{
    using System.Threading.Tasks;

    using Emby.TV.Model.Setup;

    using MediaBrowser.Model.Logging;

    public abstract class ProviderStageWizardBase : ProviderStageBase, IProviderSetupStageWizard
    {
        protected ProviderStageWizardBase(string providerId, ILogger logger)
            : base(providerId, logger)
        {
            this.AllowCancel = true;
            this.AllowNext = true;
        }

        public bool AllowCancel { get; set; }

        public bool AllowFinish { get; set; }

        public bool AllowBack { get; set; }

        public bool AllowNext { get; set; }

        public virtual Task OnCancelCommand()
        {
            return Task.CompletedTask;
        }

        public virtual Task<IProviderSetupStage> OnNextCommand(string providerId, string commandId, string data)
        {
            return Task.FromResult<IProviderSetupStage>(null);
        }

        public virtual Task OnFinishCommand(string providerId, string commandId, string data)
        {
            return Task.CompletedTask;
        }
    }
}