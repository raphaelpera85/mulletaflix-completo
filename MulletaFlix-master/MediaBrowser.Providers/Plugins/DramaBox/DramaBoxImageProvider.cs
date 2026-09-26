using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Supplies DramaBox artwork for series and for their episodes.
    /// </summary>
    /// <remarks>
    /// Episode artwork is not addressed by any id of its own: DramaBox serves one thumbnail per
    /// chapter inside the series payload, so an episode image is resolved by going back to the
    /// parent series provider id and picking the chapter by index.
    /// </remarks>
    public class DramaBoxImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly DramaBoxClient _client;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaBoxImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxImageProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaBox client.</param>
        /// <param name="libraryManager">The library manager, used to reach the parent series of an episode.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaBoxImageProvider(
            DramaBoxClient client,
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaBoxImageProvider> logger)
        {
            _client = client;
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => DramaBoxSeriesProvider.ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public bool Supports(BaseItem item) => item is Series || item is Episode;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var results = new List<RemoteImageInfo>();

            var url = item switch
            {
                Series series => await GetSeriesImageAsync(series, cancellationToken).ConfigureAwait(false),
                Episode episode => await GetEpisodeImageAsync(episode, cancellationToken).ConfigureAwait(false),
                _ => null
            };

            if (!string.IsNullOrEmpty(url))
            {
                results.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = url
                });
            }

            return results;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        private async Task<string?> GetSeriesImageAsync(Series series, CancellationToken cancellationToken)
        {
            var bookId = series.GetProviderId(DramaBoxSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(bookId))
            {
                return null;
            }

            var book = await _client.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
            if (book is null || string.IsNullOrWhiteSpace(book.Cover))
            {
                return null;
            }

            return DramaBoxTitleMatcher.BuildCoverUrl(book.Cover, 720, 1280);
        }

        private async Task<string?> GetEpisodeImageAsync(Episode episode, CancellationToken cancellationToken)
        {
            var seriesId = episode.SeriesId;
            if (seriesId == Guid.Empty)
            {
                return null;
            }

            if (_libraryManager.GetItemById(seriesId) is not Series series)
            {
                return null;
            }

            var bookId = series.GetProviderId(DramaBoxSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(bookId))
            {
                return null;
            }

            var book = await _client.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
            if (book is null || book.Chapters.Count == 0)
            {
                return null;
            }

            // DramaBox indexes chapters from zero, MulletaFlix episode numbers start at one.
            var episodeNumber = episode.IndexNumber ?? 1;
            var chapter = book.Chapters.FirstOrDefault(candidate => candidate.Index == episodeNumber - 1);
            if (chapter is null || string.IsNullOrWhiteSpace(chapter.Cover))
            {
                _logger.LogDebug("No DramaBox chapter cover for episode {Episode} of series {SeriesId}", episodeNumber, seriesId);
                return null;
            }

            return DramaBoxTitleMatcher.BuildCoverUrl(chapter.Cover, 480, 640);
        }
    }
}
