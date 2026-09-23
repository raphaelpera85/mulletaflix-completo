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

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Fills episode titles and runtimes from the GoodShort series payload.
    /// </summary>
    /// <remarks>
    /// GoodShort episode names are the chapter numbers ("001", "002", ...) even on the Portuguese
    /// pages and the JSON-LD carries a synthetic "&lt;series name&gt; - EP 1" instead, so the title is
    /// derived from the episode number rather than copied from either. The duration is reported by
    /// GoodShort in seconds and MulletaFlix stores ticks; there is no sub-second precision to
    /// preserve, so the conversion is a plain multiply.
    /// </remarks>
    public class GoodShortEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private const long TicksPerSecond = 10_000_000L;

        private readonly GoodShortClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoodShortEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoodShortEpisodeProvider"/> class.
        /// </summary>
        /// <param name="client">The GoodShort client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public GoodShortEpisodeProvider(
            GoodShortClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<GoodShortEpisodeProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => GoodShortSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // GoodShort exposes no per-episode lookup: an episode is always reached through its series.
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (info.SeriesProviderIds is null
                || !info.SeriesProviderIds.TryGetValue(GoodShortSeriesProvider.ProviderKey, out var seriesId)
                || string.IsNullOrWhiteSpace(seriesId))
            {
                return result;
            }

            var episodeNumber = info.IndexNumber ?? 0;
            if (episodeNumber <= 0)
            {
                return result;
            }

            var series = await _client.GetSeriesAsync(seriesId, null, cancellationToken).ConfigureAwait(false);
            if (series is null || series.Episodes.Count == 0)
            {
                return result;
            }

            var episode = series.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (episode is null)
            {
                // Distinguish "this series has no such episode" from "the source we could reach did
                // not list that far": the second case is a GoodShort truncation, not a real absence.
                if (series.EpisodesAreComplete)
                {
                    _logger.LogDebug("GoodShort has no episode {Episode} in series {SeriesId}", episodeNumber, seriesId);
                }
                else
                {
                    _logger.LogWarning(
                        "GoodShort listed only {Listed} of {Reported} episodes for series {SeriesId}, so episode {Episode} could not be filled",
                        series.Episodes.Count,
                        series.EpisodeCount,
                        seriesId,
                        episodeNumber);
                }

                return result;
            }

            var item = new Episode
            {
                Name = string.Format(CultureInfo.InvariantCulture, "Episódio {0}", episodeNumber)
            };

            if (episode.DurationSeconds > 0)
            {
                item.RunTimeTicks = episode.DurationSeconds * TicksPerSecond;
            }

            result.Item = item;
            result.HasMetadata = true;
            result.QueriedById = true;

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
