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

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Fills episode titles, runtimes and thumbnails from the DramaBox series payload.
    /// </summary>
    /// <remarks>
    /// DramaBox episode names are always Chinese ("第一集") even on the Portuguese pages, so the
    /// title is derived from the episode number instead of being copied. One hundred nanoseconds
    /// per millisecond is the conversion from the millisecond duration DramaBox reports to the
    /// tick count MulletaFlix stores.
    /// </remarks>
    public class DramaBoxEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private const long TicksPerMillisecond = 10_000L;

        private readonly DramaBoxClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaBoxEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxEpisodeProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaBox client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaBoxEpisodeProvider(
            DramaBoxClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaBoxEpisodeProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => DramaBoxSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // DramaBox exposes no per-episode lookup: an episode is always reached through its series.
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (info.SeriesProviderIds is null
                || !info.SeriesProviderIds.TryGetValue(DramaBoxSeriesProvider.ProviderKey, out var bookId)
                || string.IsNullOrWhiteSpace(bookId))
            {
                return result;
            }

            var book = await _client.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
            if (book is null || book.Chapters.Count == 0)
            {
                return result;
            }

            var episodeNumber = info.IndexNumber ?? 0;
            if (episodeNumber <= 0)
            {
                return result;
            }

            var chapter = book.Chapters.FirstOrDefault(candidate => candidate.Index == episodeNumber - 1);
            if (chapter is null)
            {
                _logger.LogDebug("DramaBox has no chapter {Episode} in book {BookId}", episodeNumber, bookId);
                return result;
            }

            var episode = new Episode
            {
                Name = string.Format(CultureInfo.InvariantCulture, "Episódio {0}", episodeNumber)
            };

            if (chapter.DurationMs > 0)
            {
                episode.RunTimeTicks = chapter.DurationMs * TicksPerMillisecond;
            }

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
