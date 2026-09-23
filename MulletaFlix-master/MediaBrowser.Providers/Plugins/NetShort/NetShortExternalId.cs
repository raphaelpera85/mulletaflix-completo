using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Declares the NetShort external id so the value is editable and shown in the dashboard.
    /// </summary>
    public class NetShortExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "NetShort";

        /// <inheritdoc />
        public string Key => "NetShort";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Series;
    }
}
