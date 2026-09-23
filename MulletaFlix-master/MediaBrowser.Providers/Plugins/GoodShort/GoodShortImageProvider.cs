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

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Supplies GoodShort artwork for series and for their episodes.
    /// </summary>
    /// <remarks>
    /// Episode artwork is not addressed by any id of its own: GoodShort serves one image per chapter
    /// inside the series payload, so an episode image is resolved by going back to the parent
    /// series provider id and picking the episode by number.
    /// </remarks>
    public class GoodShortImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly GoodShortClient _client;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoodShortImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoodShortImageProvider"/> class.
        /// </summary>
        /// <param name="client">The GoodShort client.</param>
        /// <param name="libraryManager">The library manager, used to reach the parent series of an episode.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public GoodShortImageProvider(
            GoodShortClient client,
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            ILogger<GoodShortImageProvider> logger)
        {
            _client = client;
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => GoodShortSeriesProvider.ProviderKey;

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
            var seriesId = series.GetProviderId(GoodShortSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return null;
            }

            var record = await _client.GetSeriesAsync(seriesId, null, cancellationToken).ConfigureAwait(false);
            if (record is null || string.IsNullOrWhiteSpace(record.Cover))
            {
                return null;
            }

            // GoodShort serves this URL unsigned and without an expiry, so it is handed out as is.
            return record.Cover;
        }

        private async Task<string?> GetEpisodeImageAsync(Episode episode, CancellationToken cancellationToken)
        {
            var id = episode.SeriesId;
            if (id == Guid.Empty)
            {
                return null;
            }

            if (_libraryManager.GetItemById(id) is not Series series)
            {
                return null;
            }

            var seriesId = series.GetProviderId(GoodShortSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return null;
            }

            var record = await _client.GetSeriesAsync(seriesId, null, cancellationToken).ConfigureAwait(false);
            if (record is null || record.Episodes.Count == 0)
            {
                return null;
            }

            var episodeNumber = episode.IndexNumber ?? 1;
            var match = record.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (match is null || string.IsNullOrWhiteSpace(match.Thumbnail))
            {
                _logger.LogDebug("No GoodShort thumbnail for episode {Episode} of series {SeriesId}", episodeNumber, id);
                return null;
            }

            return match.Thumbnail;
        }
    }
}
