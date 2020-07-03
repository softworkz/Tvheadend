namespace TVHeadEnd.Setup
{
    using System;
    using System.Threading.Tasks;

    using Emby.TV.Model.Setup;

    using MediaBrowser.Model.Logging;

    public abstract class ProviderStageDialogBase : ProviderStageBase, IProviderSetupStageDialog
    {
        protected ProviderStageDialogBase(string providerId, ILogger logger)
            : base(providerId, logger)
        {
            this.AllowCancel = true;
            this.AllowOk = true;
        }

        public bool AllowCancel { get; set; }
        public bool AllowOk { get; set; }

        public virtual bool ShowDialogFullScreen { get; } = false;

        public virtual Task OnCancelCommand()
        {
            return Task.CompletedTask;
        }

        public virtual Task OnOkCommand(string providerId, string commandId, string data)
        {
            throw new NotImplementedException();
        }
    }
}