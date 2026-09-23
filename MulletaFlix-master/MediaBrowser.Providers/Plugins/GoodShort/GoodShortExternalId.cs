using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Declares the GoodShort external id so the value is editable and shown in the dashboard.
    /// </summary>
    public class GoodShortExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "GoodShort";

        /// <inheritdoc />
        public string Key => "GoodShort";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Series;
    }
}
