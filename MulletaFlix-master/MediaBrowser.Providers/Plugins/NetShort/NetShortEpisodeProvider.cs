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

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Fills episode titles and thumbnails from the NetShort series payload.
    /// </summary>
    /// <remarks>
    /// NetShort episode payloads carry a number, an id, a thumbnail and a paywall flag, but no title
    /// and no duration, so the title is derived from the episode number exactly as the DramaBox
    /// provider derives its own. Run time is left alone: nothing in the payload reports it, and a
    /// fabricated value would be worse than none.
    /// </remarks>
    public class NetShortEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private readonly NetShortClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetShortEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetShortEpisodeProvider"/> class.
        /// </summary>
        /// <param name="client">The NetShort client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NetShortEpisodeProvider(
            NetShortClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<NetShortEpisodeProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => NetShortSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // NetShort exposes no per-episode lookup: an episode is always reached through its series.
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (info.SeriesProviderIds is null
                || !info.SeriesProviderIds.TryGetValue(NetShortSeriesProvider.ProviderKey, out var seriesId)
                || string.IsNullOrWhiteSpace(seriesId))
            {
                return result;
            }

            var episodeNumber = info.IndexNumber ?? 0;
            if (episodeNumber <= 0)
            {
                return result;
            }

            var series = await _client.GetSeriesAsync(seriesId, cancellationToken).ConfigureAwait(false);
            if (series is null || series.Episodes.Count == 0)
            {
                return result;
            }

            var episode = series.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (episode is null)
            {
                _logger.LogDebug("NetShort has no episode {Episode} in series {SeriesId}", episodeNumber, seriesId);
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
