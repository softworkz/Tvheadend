namespace TVHeadEnd.Setup
{
    using System;
    using System.Threading.Tasks;

    using Emby.TV.Model.Setup;

    using MediaBrowser.Model.Events;
    using MediaBrowser.Model.GenericEdit;
    using MediaBrowser.Model.Logging;

    public abstract class ProviderStageBase : IProviderSetupStage
    {
        protected ProviderStageBase(string providerId, ILogger logger)
        {
            this.ProviderId = providerId;
            this.Logger = logger;
        }

        public string Caption { get; protected set; }

        public string SubCaption { get; protected set; }

        public string ProviderId { get; }

        protected ILogger Logger { get; }

        public IEditableObject ContentData { get; set; }

        public event EventHandler<GenericEventArgs<IProviderSetupStage>> SetupStageInfoChanged;

        public virtual bool IsCommandAllowed(string commandKey)
        {
            return false;
        }

        public virtual Task<IProviderSetupStage> RunCommand(string itemId, string commandId, string data)
        {
            return Task.FromResult<IProviderSetupStage>(null);
        }

        public abstract Task Cancel();

        public virtual void OnDialogResult(IProviderSetupStage dialogStage, bool completedOk, object data)
        {
        }

        protected void RaiseInfoChanged()
        {
            this.SetupStageInfoChanged?.Invoke(this, new GenericEventArgs<IProviderSetupStage>(this));
        }
    }
}