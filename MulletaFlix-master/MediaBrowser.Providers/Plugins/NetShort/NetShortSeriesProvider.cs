using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Provides series metadata from NetShort, the short drama platform whose catalogue is not
    /// covered by TMDb, TVDB or MyDramaList.
    /// </summary>
    /// <remarks>
    /// Unlike the DramaBox sibling this provider does not need a crawled index: NetShort exposes a
    /// keyword search endpoint once the request body is encrypted, so a library title is resolved by
    /// asking the platform directly. A pasted URL or a bare numeric id still short-circuits the
    /// search, because it identifies the series exactly.
    /// </remarks>
    public class NetShortSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        /// <summary>
        /// The provider key used in provider id dictionaries.
        /// </summary>
        public const string ProviderKey = "NetShort";

        private const int MaxSearchResults = 12;

        private readonly NetShortClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetShortSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetShortSeriesProvider"/> class.
        /// </summary>
        /// <param name="client">The NetShort client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NetShortSeriesProvider(
            NetShortClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<NetShortSeriesProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var providerId = searchInfo.GetProviderId(ProviderKey);
            var term = !string.IsNullOrWhiteSpace(providerId) ? providerId : searchInfo.Name;

            var matches = await _client.SearchAsync(term, MaxSearchResults, cancellationToken).ConfigureAwait(false);
            if (matches.Count == 0)
            {
                _logger.LogInformation("NetShort found no candidate for {Term}", term);
                return results;
            }

            foreach (var match in matches)
            {
                var result = new RemoteSearchResult
                {
                    Name = match.Series.Name,
                    SearchProviderName = Name,
                    ImageUrl = match.Series.Cover,
                    Overview = match.Series.Overview,
                    PremiereDate = match.Series.PremiereDate
                };

                result.SetProviderId(ProviderKey, match.Series.SeriesId);
                results.Add(result);
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                QueriedById = false
            };

            var seriesId = NetShortClient.ResolveSeriesId(info.GetProviderId(ProviderKey));
            if (string.IsNullOrEmpty(seriesId))
            {
                var matches = await _client.SearchAsync(info.Name, 1, cancellationToken).ConfigureAwait(false);
                seriesId = matches.FirstOrDefault()?.Series.SeriesId;
            }

            if (string.IsNullOrEmpty(seriesId))
            {
                return result;
            }

            var series = await _client.GetSeriesAsync(seriesId, cancellationToken).ConfigureAwait(false);
            if (series is null)
            {
                return result;
            }

            result.QueriedById = true;

            var item = new Series();
            item.SetProviderId(ProviderKey, series.SeriesId);
            item.Name = series.Name;
            item.Overview = series.Overview;

            if (series.Genres.Count > 0)
            {
                item.Genres = series.Genres.ToArray();
            }

            if (series.PremiereDate.HasValue)
            {
                item.PremiereDate = series.PremiereDate.Value;
                item.ProductionYear = series.PremiereDate.Value.Year;
            }

            result.Item = item;
            result.HasMetadata = true;

            _logger.LogInformation(
                "NetShort filled metadata for {Name} (id {SeriesId}, {Episodes} episodes)",
                series.Name,
                series.SeriesId,
                series.EpisodeCount);

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
