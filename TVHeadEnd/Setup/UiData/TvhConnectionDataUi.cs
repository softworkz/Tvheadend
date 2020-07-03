namespace TVHeadEnd.Setup.UiData
{
    using System.ComponentModel;

    using Emby.TV.Model.Providers.Config;

    using MediaBrowser.Model.Attributes;

    using TVHeadEnd.Configuration;

    public class TvhConnectionDataUi : EditableConfigurationBase
    {
        public TvhConnectionDataUi()
        {
        }

        public TvhConnectionDataUi(TvHeadendTunerConfig tvhTunerConfig)
        {
            this.TvhHost = tvhTunerConfig.TvhServerName;
            this.HttpPort = tvhTunerConfig.HttpPort;
            this.HtspPort = tvhTunerConfig.HtspPort;
            this.UserName = tvhTunerConfig.Username;
            this.Password = tvhTunerConfig.Password;
        }

        public void ApplyToConfig(TvHeadendTunerConfig tvhTunerConfig)
        {
            tvhTunerConfig.TvhServerName = this.TvhHost;
            tvhTunerConfig.HttpPort = this.HttpPort;
            tvhTunerConfig.HtspPort = this.HtspPort;
            tvhTunerConfig.Username = this.UserName;
            tvhTunerConfig.Password = this.Password;
        }

        [DisplayName("Tvheadend Host")]
        [Description("Host name or IP address of the Tvheadend server")]
        [Required]
        public string TvhHost { get; set; }

        [DisplayName("HTTP Port")]
        [Required]
        [MinValue(10), MaxValue(65535)]
        public int HttpPort { get; set; }

        [DisplayName("HTSP Port")]
        [Required]
        [MinValue(10), MaxValue(65535)]
        public int HtspPort { get; set; }

        [DisplayName("User Name")]
        [Required]
        public string UserName { get; set; }

        [DisplayName("Password")]
        [Required]
        [IsPassword]
        public string Password { get; set; }

        /// <summary>Gets the editor title.</summary>
        /// <value>The editor title.</value>
        public override string EditorTitle { get; } = "TV Headend Connection";

        /// <summary>Gets the editor description.</summary>
        /// <value>The editor description.</value>
        public override string EditorDescription { get; } = "HTTP digest authentication must be disabled in Tvheadend in order for the Emby plugin to connect";
    }
}