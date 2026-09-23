using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Supplies NartoDrama artwork for series.
    /// </summary>
    /// <remarks>
    /// Only series artwork exists. A series page publishes exactly one image, the portrait poster,
    /// and an episode page serves that same poster as its og:image, so supporting episodes here would
    /// cost one request per episode to return the series cover again. A Primary image is offered
    /// rather than a Backdrop because the poster is portrait (the site renders it at 300x400) and
    /// MulletaFlix renders backdrops in landscape.
    /// </remarks>
    public class NartoDramaImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly NartoDramaClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NartoDramaImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NartoDramaImageProvider"/> class.
        /// </summary>
        /// <param name="client">The NartoDrama client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NartoDramaImageProvider(
            NartoDramaClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<NartoDramaImageProvider> logger)
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
        public bool Supports(BaseItem item) => item is Series;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var results = new List<RemoteImageInfo>();

            if (item is not Series series)
            {
                return results;
            }

            var slug = series.GetProviderId(NartoDramaSeriesProvider.ProviderKey);
            if (string.IsNullOrWhiteSpace(slug))
            {
                return results;
            }

            var seriesInfo = await _client.GetSeriesAsync(slug, cancellationToken).ConfigureAwait(false);
            if (seriesInfo is null || string.IsNullOrWhiteSpace(seriesInfo.Cover))
            {
                _logger.LogDebug("NartoDrama has no poster for series {Slug}", slug);
                return results;
            }

            results.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Type = ImageType.Primary,
                Url = seriesInfo.Cover
            });

            return results;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
