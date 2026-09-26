using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxExternalId : IExternalId
{
    public string ProviderName => "ShortMax";
    public string Key => ShortMaxSeriesProvider.ProviderKey;
    public ExternalIdMediaType? Type => null;
    public bool Supports(IHasProviderIds item) => item is Series;
}
