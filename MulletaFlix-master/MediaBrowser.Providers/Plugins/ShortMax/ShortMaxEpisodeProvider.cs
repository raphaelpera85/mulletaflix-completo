using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
{
    private readonly ShortMaxClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShortMaxEpisodeProvider> _logger;

    public ShortMaxEpisodeProvider(ShortMaxClient client, IHttpClientFactory httpClientFactory, ILogger<ShortMaxEpisodeProvider> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ShortMaxSeriesProvider.ProviderKey;
    public int Order => 3;
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Episode>();
        if (info.SeriesProviderIds is null || !info.SeriesProviderIds.TryGetValue(Name, out var seriesId) || info.IndexNumber is not int number || number <= 0) return result;
        var series = await _client.GetSeriesAsync(seriesId, cancellationToken).ConfigureAwait(false);
        if (series is null || !series.Episodes.Any(item => item.Number == number)) return result;
        result.Item = new Episode { Name = string.Format(CultureInfo.InvariantCulture, "Episódio {0}", number) };
        result.HasMetadata = true;
        result.QueriedById = true;
        _logger.LogDebug("ShortMax filled episode {Episode} for series {SeriesId}", number, seriesId);
        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
}
