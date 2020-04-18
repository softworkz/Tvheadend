namespace TVHeadEnd.Configuration
{
    using Emby.TV.Model.Configuration;

    using MediaBrowser.Model.Attributes;

    public class TvHeadendTunerConfig : EditableConfigurationBase
    {
        public string TvhServerName { get; set; }
        public int HttpPort { get; set; }
        public int HtspPort { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        [MinValue(0), MaxValue(4)]
        public int Priority { get; set; }
        public string Profile { get; set; }
        public string ChannelType { get; set; }
        public bool EnableSubsMaudios { get; set; }
        public bool ForceDeinterlace { get; set; }

        public TvHeadendTunerConfig()
        {
            this.TvhServerName = "localhost";
            this.HttpPort = 9981;
            this.HtspPort = 9982;
            this.Username = string.Empty;
            this.Password = string.Empty;
            this.Priority = 5;
            this.Profile = string.Empty;
            this.ChannelType = "Ignore";
            this.EnableSubsMaudios = false;
            this.ForceDeinterlace = false;

            // Test
            this.TvhServerName = "192.168.25.200";
            this.Username = "somebody";
            this.Password = "somesome";
        }
    }
}
