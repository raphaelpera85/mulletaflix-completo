using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Declares the DramaBox external id so the value is editable and shown in the dashboard.
    /// </summary>
    public class DramaBoxExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "DramaBox";

        /// <inheritdoc />
        public string Key => "DramaBox";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Series;
    }
}
