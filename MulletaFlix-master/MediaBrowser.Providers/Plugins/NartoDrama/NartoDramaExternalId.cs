using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Declares the NartoDrama external id so the value is editable and shown in the dashboard.
    /// </summary>
    /// <remarks>
    /// The value is the series slug, for example "abandonei-o-rei-dos-deuses-no-altar". The numeric
    /// content id is deliberately not the key: it never appears in a page URL, so a user holding one
    /// could not turn it back into a series, while a slug addresses the page on its own.
    /// </remarks>
    public class NartoDramaExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => NartoDramaSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public string Key => NartoDramaSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Series;
    }
}
