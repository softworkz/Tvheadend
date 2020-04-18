namespace TVHeadEnd.Setup.UiData
{
    using System.ComponentModel;

    using Emby.GenericEdit;

    using MediaBrowser.Model.Attributes;

    public class TvhConnectionDataUi : EditableObjectBase
    {
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