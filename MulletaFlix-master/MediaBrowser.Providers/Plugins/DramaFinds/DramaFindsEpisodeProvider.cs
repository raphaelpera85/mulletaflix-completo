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

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Fills episode names from the DramaFinds series payload.
    /// </summary>
    /// <remarks>
    /// DramaFinds exposes no per-episode payload and no per-episode title: its own episode pages
    /// set og:title to exactly "Episódio N - &lt;drama&gt;". The episode list is therefore
    /// synthesized from the episode count, and the deterministic public watch URL is the only
    /// per-episode identifier the platform has, so it is stored as the episode's provider id.
    /// </remarks>
    public class DramaFindsEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private readonly DramaFindsClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaFindsEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsEpisodeProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaFinds client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaFindsEpisodeProvider(
            DramaFindsClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaFindsEpisodeProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => DramaFindsSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // DramaFinds exposes no per-episode lookup: an episode is always reached through its series.
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (info.SeriesProviderIds is null
                || !info.SeriesProviderIds.TryGetValue(DramaFindsSeriesProvider.ProviderKey, out var dramaId)
                || string.IsNullOrWhiteSpace(dramaId))
            {
                return result;
            }

            var drama = await _client.GetDramaAsync(dramaId, cancellationToken).ConfigureAwait(false);
            if (drama is null || drama.Episodes.Count == 0)
            {
                return result;
            }

            var episodeNumber = info.IndexNumber ?? 0;
            if (episodeNumber <= 0)
            {
                return result;
            }

            var synthesized = drama.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (synthesized is null)
            {
                _logger.LogDebug("DramaFinds has no episode {Episode} in drama {DramaId}", episodeNumber, dramaId);
                return result;
            }

            var episode = new Episode
            {
                Name = synthesized.Name
            };

            episode.SetProviderId(DramaFindsSeriesProvider.ProviderKey, synthesized.Url);

            result.Item = episode;
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
