using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Provides series metadata from DramaFinds, the short drama platform whose catalogue is not
    /// covered by TMDb, TVDB or MyDramaList.
    /// </summary>
    /// <remarks>
    /// Resolution happens in two ways: an exact drama id extracted from a pasted public URL, or the
    /// platform's own search endpoint. The search is fuzzy by substring, so the results are scored
    /// and filtered locally before they are offered, and the top candidate is only used when it
    /// clears the same automatic threshold the DramaBox provider uses.
    /// </remarks>
    public class DramaFindsSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        /// <summary>
        /// The provider key used in provider id dictionaries.
        /// </summary>
        public const string ProviderKey = "DramaFinds";

        private const int MaxSearchResults = 12;

        private readonly DramaFindsClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaFindsSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsSeriesProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaFinds client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaFindsSeriesProvider(
            DramaFindsClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaFindsSeriesProvider> logger)
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

            // A search that matched nothing is a normal outcome on this platform, so it stays at
            // information level: many short dramas simply do not exist on DramaFinds.
            if (matches.Count == 0)
            {
                _logger.LogInformation("DramaFinds found no candidate for {Term}", term);
                return results;
            }

            // Every candidate is offered, including the duplicate ids the platform keeps for the
            // same title, so the user picks the edition instead of the provider guessing.
            foreach (var match in matches)
            {
                var result = new RemoteSearchResult
                {
                    Name = match.Drama.Title,
                    SearchProviderName = Name,
                    ImageUrl = match.Drama.Cover,
                    Overview = match.Drama.Overview,
                    PremiereDate = ParseReleaseDate(match.Drama.ReleaseDate)
                };

                result.SetProviderId(ProviderKey, match.Drama.DramaId);
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

            var dramaId = DramaFindsClient.ResolveDramaId(info.GetProviderId(ProviderKey));
            if (string.IsNullOrEmpty(dramaId))
            {
                var matches = await _client.SearchAsync(info.Name, MaxSearchResults, cancellationToken).ConfigureAwait(false);

                // The ranking is deterministic, so when the platform holds two ids for the same
                // drama the same edition is chosen on every run. The choice is logged because
                // silently picking between identical titles is exactly how two series end up
                // sharing one identity.
                var best = matches.FirstOrDefault();
                if (best is not null && matches.Count > 1)
                {
                    _logger.LogInformation(
                        "DramaFinds returned {Count} candidates for '{Name}'; picked drama {DramaId} (released {ReleaseDate}, rating {Rating}) as the most recent edition",
                        matches.Count,
                        info.Name,
                        best.Drama.DramaId,
                        best.Drama.ReleaseDate,
                        best.Drama.Rating);
                }

                dramaId = best?.Drama.DramaId;
            }

            if (string.IsNullOrEmpty(dramaId))
            {
                return result;
            }

            var drama = await _client.GetDramaAsync(dramaId, cancellationToken).ConfigureAwait(false);
            if (drama is null)
            {
                return result;
            }

            result.QueriedById = true;

            var series = new Series();
            series.SetProviderId(ProviderKey, drama.DramaId);
            series.Name = drama.Title;
            series.Overview = drama.Overview;

            // The detail payload carries no genre list, only a free-form tags field that the
            // platform reports as null, so genres are left untouched rather than invented.
            if (drama.Tags.Count > 0)
            {
                series.Tags = drama.Tags.ToArray();
            }

            if (drama.Rating.HasValue)
            {
                series.CommunityRating = (float)drama.Rating.Value;
            }

            var premiereDate = ParseReleaseDate(drama.ReleaseDate);
            if (premiereDate.HasValue)
            {
                series.PremiereDate = premiereDate.Value;
                series.ProductionYear = premiereDate.Value.Year;
            }

            result.Item = series;
            result.HasMetadata = true;

            _logger.LogInformation(
                "DramaFinds filled metadata for {Name} (id {DramaId}, {Episodes} episodes)",
                drama.Title,
                drama.DramaId,
                drama.EpisodeCount);

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// Parses the DramaFinds release date, which is an ISO calendar day such as 2025-12-22.
        /// </summary>
        /// <param name="releaseDate">The raw value.</param>
        /// <returns>The parsed timestamp, or null.</returns>
        internal static DateTime? ParseReleaseDate(string? releaseDate)
        {
            if (string.IsNullOrWhiteSpace(releaseDate))
            {
                return null;
            }

            const string Format = "yyyy-MM-dd";
            if (DateTime.TryParseExact(releaseDate, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            {
                return exact;
            }

            if (DateTime.TryParse(releaseDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var loose))
            {
                return loose;
            }

            return null;
        }
    }
}
