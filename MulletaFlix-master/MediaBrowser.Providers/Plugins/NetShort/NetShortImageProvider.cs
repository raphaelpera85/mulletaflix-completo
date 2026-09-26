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

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Supplies NetShort artwork for series and for their episodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Episode artwork is not addressed by an id of its own: NetShort serves one thumbnail per
    /// episode inside the series payload, so an episode image is resolved by going back to the parent
    /// series provider id and picking the episode by number.
    /// </para>
    /// <para>
    /// The URLs are handed back exactly as NetShort returned them. The CDN only serves the transform
    /// variants the site itself asked for — rewriting the suffix to another size answers 404, which
    /// was verified against the live CDN — so there is no size to request and nothing to gain from
    /// guessing one. The series cover the API returns is the full-size original.
    /// </para>
    /// </remarks>
    public class NetShortImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly NetShortClient _client;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetShortImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetShortImageProvider"/> class.
        /// </summary>
        /// <param name="client">The NetShort client.</param>
        /// <param name="libraryManager">The library manager, used to reach the parent series of an episode.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NetShortImageProvider(
            NetShortClient client,
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            ILogger<NetShortImageProvider> logger)
        {
            _client = client;
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => NetShortSeriesProvider.ProviderKey;

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
            var seriesId = series.GetProviderId(NetShortSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return null;
            }

            var resolved = await _client.GetSeriesAsync(seriesId, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(resolved?.Cover) ? null : resolved.Cover;
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

            var providerId = series.GetProviderId(NetShortSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return null;
            }

            var resolved = await _client.GetSeriesAsync(providerId, cancellationToken).ConfigureAwait(false);
            if (resolved is null || resolved.Episodes.Count == 0)
            {
                return null;
            }

            var episodeNumber = episode.IndexNumber ?? 1;
            var match = resolved.Episodes.FirstOrDefault(candidate => candidate.Number == episodeNumber);
            if (match is null || string.IsNullOrWhiteSpace(match.Cover))
            {
                _logger.LogDebug("No NetShort episode cover for episode {Episode} of series {SeriesId}", episodeNumber, seriesId);
                return null;
            }

            return match.Cover;
        }
    }
}
