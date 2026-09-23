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

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Fills episode titles from the NartoDrama series page.
    /// </summary>
    /// <remarks>
    /// The page publishes each episode as an anchor whose label is the number the platform counts by
    /// ("001") and whose text is just "EP 1", and it publishes no duration and no per-episode
    /// artwork: an episode URL serves the same og:image as its series. The title is therefore derived
    /// from the episode number, and the runtime is left to the media file itself.
    /// </remarks>
    public class NartoDramaEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private readonly NartoDramaClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NartoDramaEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NartoDramaEpisodeProvider"/> class.
        /// </summary>
        /// <param name="client">The NartoDrama client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NartoDramaEpisodeProvider(
            NartoDramaClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<NartoDramaEpisodeProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => NartoDramaSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // NartoDrama exposes no per-episode lookup: an episode is always reached through its series.
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (info.SeriesProviderIds is null
                || !info.SeriesProviderIds.TryGetValue(NartoDramaSeriesProvider.ProviderKey, out var slug)
                || string.IsNullOrWhiteSpace(slug))
            {
                return result;
            }

            var episodeNumber = info.IndexNumber ?? 0;
            if (episodeNumber <= 0)
            {
                return result;
            }

            var series = await _client.GetSeriesAsync(slug, cancellationToken).ConfigureAwait(false);
            if (series is null || series.Episodes.Count == 0)
            {
                return result;
            }

            var episode = series.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (episode is null)
            {
                // Distinguish "this series has no such episode" from "the page did not list that
                // far": the second case is a truncated episode list, not a real absence.
                if (series.EpisodesAreComplete)
                {
                    _logger.LogDebug("NartoDrama has no episode {Episode} in series {Slug}", episodeNumber, slug);
                }
                else
                {
                    _logger.LogWarning(
                        "NartoDrama listed only {Listed} of {Reported} episodes for series {Slug}, so episode {Episode} could not be filled",
                        series.Episodes.Count,
                        series.EpisodeCount,
                        slug,
                        episodeNumber);
                }

                return result;
            }

            result.Item = new Episode
            {
                Name = string.Format(CultureInfo.InvariantCulture, "Episódio {0}", episodeNumber)
            };

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
