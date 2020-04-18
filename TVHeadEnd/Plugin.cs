namespace TVHeadEnd
{
    using System;
    using System.IO;

    using MediaBrowser.Common.Configuration;
    using MediaBrowser.Common.Plugins;
    using MediaBrowser.Model.Drawing;
    using MediaBrowser.Model.Plugins;

    /// <summary>TV Headend Emby Plugin</summary>
    public class Plugin : IPlugin, IHasThumbImage
    {
        public static readonly Guid PluginId = new Guid("95732bbe-15ed-4293-bab2-e056ccc50159");

        /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
        /// <param name="applicationPaths">The application paths.</param>
        public Plugin(IApplicationPaths applicationPaths)
        {
            var assembly = this.GetType().Assembly;
            var assemblyName = assembly.GetName();

            this.Version = assemblyName.Version;
            this.AssemblyFilePath = assembly.Location;
            this.DataFolderPath = Path.Combine(applicationPaths.PluginsPath, assemblyName.Name);
        }

        /// <summary>Gets the path to the assembly file</summary>
        /// <value>The assembly file path.</value>
        public string AssemblyFilePath { get; }

        /// <summary>Gets the full path to the data folder, where the plugin can store any miscellaneous files needed</summary>
        /// <value>The data folder path.</value>
        public string DataFolderPath { get; }

        /// <summary>Gets the description.</summary>
        /// <value>The description.</value>
        public string Description => TunerProviderTvHeadend.ProvDescription;

        /// <summary>Gets the unique id.</summary>
        /// <value>The unique id.</value>
        public Guid Id => PluginId;

        /// <summary>Gets the name of the plugin</summary>
        /// <value>The name.</value>
        public string Name => TunerProviderTvHeadend.ProvName;

        /// <summary>Gets the thumb image format.</summary>
        /// <value>The thumb image format.</value>
        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }

        /// <summary>Gets the plugin version</summary>
        /// <value>The version.</value>
        public Version Version { get; }

        /// <summary>Gets the plugin info.</summary>
        /// <returns>Plugin Info.</returns>
        public PluginInfo GetPluginInfo()
        {
            return new PluginInfo
                       {
                           Name = TunerProviderTvHeadend.ProvName,
                           Version = this.Version.ToString(),
                           Description = TunerProviderTvHeadend.ProvDescription,
                           Id = TunerProviderTvHeadend.ProvId,
                       };
        }

        /// <summary>Gets the thumb image.</summary>
        /// <returns>An image stream.</returns>
        public Stream GetThumbImage()
        {
            var type = this.GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Images.TVHeadEnd.png");
        }

        internal static Stream GetThumbImageCore()
        {
            var type = typeof(Plugin);
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Images.TVHeadEnd.png");
        }

        /// <summary>Called when just before the plugin is uninstalled from the server.</summary>
        public virtual void OnUninstalling()
        {
        }
    }
}