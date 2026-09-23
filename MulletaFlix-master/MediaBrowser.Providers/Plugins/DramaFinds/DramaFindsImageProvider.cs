using System;
using System.Collections.Generic;
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

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Supplies DramaFinds artwork for series and for their episodes.
    /// </summary>
    /// <remarks>
    /// The platform publishes exactly one artwork per drama. Its own episode pages confirm this:
    /// the og:image of episodes 1, 2 and 3 of a drama is byte-for-byte the same cover URL, with the
    /// signature re-issued. So the episode path resolves back to the parent series and reuses the
    /// series cover, which is what the platform itself shows on an episode page. The cover is
    /// always returned with its expiring auth_key query removed.
    /// </remarks>
    public class DramaFindsImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly DramaFindsClient _client;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaFindsImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsImageProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaFinds client.</param>
        /// <param name="libraryManager">The library manager, used to reach the parent series of an episode.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaFindsImageProvider(
            DramaFindsClient client,
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaFindsImageProvider> logger)
        {
            _client = client;
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => DramaFindsSeriesProvider.ProviderKey;

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
            var dramaId = series.GetProviderId(DramaFindsSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(dramaId))
            {
                return null;
            }

            var drama = await _client.GetDramaAsync(dramaId, cancellationToken).ConfigureAwait(false);
            if (drama is null || string.IsNullOrWhiteSpace(drama.Cover))
            {
                return null;
            }

            return drama.Cover;
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

            var dramaId = series.GetProviderId(DramaFindsSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(dramaId))
            {
                return null;
            }

            var drama = await _client.GetDramaAsync(dramaId, cancellationToken).ConfigureAwait(false);
            if (drama is null || string.IsNullOrWhiteSpace(drama.Cover))
            {
                _logger.LogDebug("No DramaFinds cover for episode {Episode} of series {SeriesId}", episode.IndexNumber ?? 0, seriesId);
                return null;
            }

            return drama.Cover;
        }
    }
}
