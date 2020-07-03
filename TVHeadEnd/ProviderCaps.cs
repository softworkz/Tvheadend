namespace TVHeadEnd
{
    using System.Collections.Generic;
    using System.Linq;

    using Emby.TV.Model.ProviderData;
    using Emby.TV.Model.Providers.Tuners.Interfaces;

    /// <summary>
    /// Capabilities of the tuner provider.
    /// </summary>
    /// <seealso cref="ITunerProviderCaps" />
    public class ProviderCaps : ITunerProviderCaps
    {
        private static readonly List<string> ChannelFields = new List<string>
                                                                      {
                                                                          nameof(ProviderScannedStream.TuningInfo),
                                                                          nameof(ProviderScannedChannel.ServiceId),
                                                                          nameof(ProviderScannedChannel.ProviderName),
                                                                          nameof(ProviderScannedChannel.ChannelType),
                                                                          nameof(ProviderScannedChannel.ChannelNumber),
                                                                          nameof(ProviderScannedChannel.ChannelNumMinor),
                                                                          nameof(ProviderScannedChannel.ImageUrl),
                                                                      };

        private static readonly List<string> IdentityFields = new List<string>
                                                                      {
                                                                          nameof(ProviderScannedStream.TuningInfo),
                                                                          nameof(ProviderScannedChannel.ServiceId),
                                                                      };

        /// <summary>Gets a value indicating whether the provider can be configured.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsConfigurationUi => false;

        /// <summary>Gets a value indicating whether the provider supports adding a tuner by manual setup.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsManualSetup => true;

        /// <summary>Gets a value indicating whether the provider supports automatic detection of available tuners.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsAutoDetection => false;

        /// <summary>Gets a value indicating whether the provider supports detection of available tuners after some UI interaction.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsGuidedDetection => false;

        /// <summary>Gets a value indicating whether the provider supports streams including service information data.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsInbandChannelInfo => true;

        /// <summary>Gets a value indicating whether the provider supports streams including in-band epg (event information) data.</summary>
        /// <value><c>true</c> if supported otherwise, <c>false</c>.</value>
        public bool SupportsInbandEpg => true;

        /// <summary>Gets a custom caption for the guided detection button.</summary>
        /// <value>The guided detection caption.</value>
        /// <remarks>When no valid string is returned, the default caption is shown.</remarks>
        public string GuidedDetectionCaption => null;

        /// <summary>Gets a custom caption for the manual add button.</summary>
        /// <value>The manual add caption.</value>
        /// <remarks>When no valid string is returned, the default caption is shown.</remarks>
        public string ManualAddCaption
        {
            get
            {
                return "Connect to TV Headend";
            }
        }

        /// <summary>Gets a list of the supported fields for tuner channels.</summary>
        /// <value>The list of channel fields.</value>
        public IList<string> SupportedChannelFields => ChannelFields.ToList();

        /// <summary>Gets a list of the channel fields to uniquely identify a tuner channel.</summary>
        /// <value>The list of identity fields.</value>
        public IList<string> FixedIdentityFields => IdentityFields.ToList();

        /// <summary>Gets a list of channel fields which could be used to set up channel identity.</summary>
        /// <value>The list of identity candidate fields.</value>
        public IList<string> IdentityCandidateFields { get; } = null;

        /// <summary>Gets a list of suggested display names for tuner channel fields.</summary>
        /// <value>A list of <see cref="IChannelFieldName"/> items.</value>
        public IList<IChannelFieldName> ChannelFieldNames { get; } = null;

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return
                $"SupportsManualSetup: {this.SupportsManualSetup}, SupportsAutoDetection: {this.SupportsAutoDetection}, SupportsGuidedDetection: {this.SupportsGuidedDetection}, SupportsInbandChannelInfo: {this.SupportsInbandChannelInfo}, SupportsInbandEpg: {this.SupportsInbandEpg}";
        }
    }
}