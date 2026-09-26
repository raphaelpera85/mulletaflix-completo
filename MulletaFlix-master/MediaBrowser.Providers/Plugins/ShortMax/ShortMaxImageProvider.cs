using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly ShortMaxClient _client;
    private readonly IHttpClientFactory _httpClientFactory;

    public ShortMaxImageProvider(ShortMaxClient client, IHttpClientFactory httpClientFactory)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ShortMaxSeriesProvider.ProviderKey;
    public int Order => 3;
    public bool Supports(BaseItem item) => item is Series or Episode;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var series = item as Series;
        var id = series?.GetProviderId(Name);
        if (string.IsNullOrWhiteSpace(id)) return new List<RemoteImageInfo>();
        var resolved = await _client.GetSeriesAsync(id, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolved?.Cover)) return new List<RemoteImageInfo>();
        return new[] { new RemoteImageInfo { ProviderName = Name, Type = ImageType.Primary, Url = resolved.Cover } };
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
}
