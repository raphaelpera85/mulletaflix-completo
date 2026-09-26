using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    public const string ProviderKey = "ShortMax";
    private readonly ShortMaxClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShortMaxSeriesProvider> _logger;

    public ShortMaxSeriesProvider(ShortMaxClient client, IHttpClientFactory httpClientFactory, ILogger<ShortMaxSeriesProvider> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ProviderKey;
    public int Order => 3;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var term = searchInfo.GetProviderId(ProviderKey) ?? searchInfo.Name;
        var matches = await _client.SearchAsync(term, 12, cancellationToken).ConfigureAwait(false);
        return matches.Select(match => new RemoteSearchResult
        {
            Name = match.Series.Name,
            SearchProviderName = Name,
            ImageUrl = match.Series.Cover,
            Overview = match.Series.Overview
        }).Select((result, index) =>
        {
            result.SetProviderId(ProviderKey, matches[index].Series.SeriesId);
            return result;
        }).ToList();
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();
        var id = ShortMaxTitleMatcher.ExtractSeriesId(info.GetProviderId(ProviderKey));
        var match = id is null ? (await _client.SearchAsync(info.Name, 1, cancellationToken).ConfigureAwait(false)).FirstOrDefault()?.Series : null;
        id ??= match?.SeriesId;
        var series = await _client.GetSeriesAsync(id, cancellationToken).ConfigureAwait(false);
        if (series is null) return result;

        result.Item = new Series { Name = series.Name, Overview = series.Overview };
        result.Item.SetProviderId(ProviderKey, series.SeriesId);
        result.HasMetadata = true;
        result.QueriedById = true;
        _logger.LogInformation("ShortMax filled metadata for {Name} (id {SeriesId}, {Episodes} episodes)", series.Name, series.SeriesId, series.Episodes.Count);
        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
}
