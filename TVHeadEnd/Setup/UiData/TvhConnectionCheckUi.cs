namespace TVHeadEnd.Setup.UiData
{
    using System.ComponentModel;

    using Emby.Web.GenericEdit;
    using Emby.Web.GenericEdit.Elements;

    using MediaBrowser.Model.Attributes;

    using TVHeadEnd.Setup.Stages;

    public class TvhConnectionCheckUi : EditableObjectBase
    {
        public TvhConnectionCheckUi(string providerId)
            : this()
        {
            this.ProviderId = providerId;
        }

        public TvhConnectionCheckUi()
        {
            this.StatusCheckNetworkLocation = new StatusItem("Check Network Location", null);
            this.StatusCheckPingTime = new StatusItem("Check Ping Time", null);
            this.StatusCheckConnection = new StatusItem("Connect to TV Headend Server", null);
            this.StatusCheckAuthenticate = new StatusItem("Authenticate", null);
            this.StatusDownloadChannels = new StatusItem("Channel Download", null);
        }

        [Browsable(false)]
        public string ProviderId { get; set; }

        /// <summary>Gets the editor title.</summary>
        /// <value>The editor title.</value>
        public override string EditorTitle { get; } = "Connection Check";

        /// <summary>Gets the editor description.</summary>
        /// <value>The editor description.</value>
        public override string EditorDescription { get; } = null;

        public StatusItem StatusCheckNetworkLocation { get; set; }

        public StatusItem StatusCheckPingTime { get; set; }

        public StatusItem StatusCheckConnection { get; set; }

        public StatusItem StatusCheckAuthenticate { get; set; }

        public StatusItem StatusDownloadChannels { get; set; }

        [VisibleCondition("StatusDownloadChannels.Status", ValueCondition.IsEqual, ItemStatus.Succeeded)]
        public ButtonItem ShowTunerChannels
        {
            get
            {
                return new ButtonItem("Show Scan Results")
                           {
                               Data1 = SetupStageTvhConnectionCheck.ShowScanResult,
                               Data2 = this.ProviderId,
                               Icon = IconNames.view_list,
                           };
            }
        }


    }
}