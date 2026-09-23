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

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Provides series metadata from GoodShort, the short drama platform whose catalogue is not
    /// covered by TMDb, TVDB or MyDramaList.
    /// </summary>
    /// <remarks>
    /// Resolution happens in two ways: an exact series id extracted from a pasted drama URL, or a
    /// match through GoodShort's own search endpoint ranked with the local title matcher.
    /// </remarks>
    public class GoodShortSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        /// <summary>
        /// The provider key used in provider id dictionaries.
        /// </summary>
        public const string ProviderKey = "GoodShort";

        private const int MaxSearchResults = 12;

        private readonly GoodShortClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoodShortSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoodShortSeriesProvider"/> class.
        /// </summary>
        /// <param name="client">The GoodShort client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public GoodShortSeriesProvider(
            GoodShortClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<GoodShortSeriesProvider> logger)
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
                _logger.LogInformation("GoodShort found no candidate for {Term}", term);
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

            var providerId = info.GetProviderId(ProviderKey);
            var seriesId = GoodShortClient.ResolveSeriesId(providerId);
            string? slug = GoodShortTitleMatcher.ExtractSlug(providerId);

            if (string.IsNullOrEmpty(seriesId))
            {
                var matches = await _client.SearchAsync(info.Name, 1, cancellationToken).ConfigureAwait(false);
                var best = matches.FirstOrDefault();
                if (best is null)
                {
                    return result;
                }

                seriesId = best.Series.SeriesId;
                slug = best.Series.Slug;
            }

            var series = await _client.GetSeriesAsync(seriesId, slug, cancellationToken).ConfigureAwait(false);
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

            if (series.Rating.HasValue)
            {
                item.CommunityRating = (float)series.Rating.Value;
            }

            if (series.PremiereDate.HasValue)
            {
                item.PremiereDate = series.PremiereDate.Value;
                item.ProductionYear = series.PremiereDate.Value.Year;
            }

            // GoodShort reports the episode total on the book payload, but a Series in MulletaFlix
            // has no episode-count field: the count is whatever episodes actually exist. The number
            // is only logged, plus used by the image provider to answer "does this series even have
            // an episode N" when the episode list came back short.
            result.Item = item;
            result.HasMetadata = true;

            _logger.LogInformation(
                "GoodShort filled metadata for {Name} (id {SeriesId}, {Episodes} of {Reported} episodes)",
                series.Name,
                series.SeriesId,
                series.Episodes.Count,
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
