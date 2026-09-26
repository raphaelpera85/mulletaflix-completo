using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Declares the DramaFinds external id so the value is editable and shown in the dashboard.
    /// </summary>
    public class DramaFindsExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "DramaFinds";

        /// <inheritdoc />
        public string Key => "DramaFinds";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Series;
    }
}
