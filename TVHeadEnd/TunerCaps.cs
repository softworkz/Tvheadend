namespace TVHeadEnd
{
    using Emby.TV.Model.Providers.Tuners.Interfaces;

    /// <summary>
    /// Capabilities of the tuner.
    /// </summary>
    /// <seealso cref="ITunerCaps" />
    public class TunerCaps : ITunerCaps
    {
        /// <summary>Gets a value indicating whether the provider can be configured.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsConfigurationUi => true;
    }
}