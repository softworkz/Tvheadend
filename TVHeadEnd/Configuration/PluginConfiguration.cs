using System;
using MediaBrowser.Model.Plugins;

namespace TVHeadEnd.Configuration
{
    /// <summary>
    /// Class PluginConfiguration
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string TvhServerName { get; set; }
        public int HttpPort { get; set; }
        public int HtspPort { get; set; }
        public string WebRoot { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int Priority { get; set; }
        public string Profile { get; set; }
        public string ChannelType { get; set; }
        public bool EnableSubsMaudios { get; set; }
        public bool ForceDeinterlace { get; set; }

        public PluginConfiguration()
        {
            this.TvhServerName = "localhost";
            this.HttpPort = 9981;
			this.HtspPort = 9982;
            WebRoot = "/";
            Username = string.Empty;
            Password = string.Empty;
            Priority = 5;
            Profile = string.Empty;
            ChannelType = "Ignore";
            EnableSubsMaudios = false;
            ForceDeinterlace = false;
        }
    }
}