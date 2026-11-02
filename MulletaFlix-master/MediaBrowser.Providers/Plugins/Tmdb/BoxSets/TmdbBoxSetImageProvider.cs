using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.Tmdb.BoxSets
{
    /// <summary>
    /// BoxSet image provider powered by TMDb.
    /// </summary>
    public class TmdbBoxSetImageProvider : ILanguageAwareRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbBoxSetImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="tmdbClientManager">The <see cref="TmdbClientManager"/>.</param>
        public TmdbBoxSetImageProvider(IHttpClientFactory httpClientFactory, TmdbClientManager tmdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _tmdbClientManager = tmdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is BoxSet;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item) =>
        [
            ImageType.Primary,
            ImageType.Backdrop,
            ImageType.Thumb
        ];

        /// <inheritdoc />
        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            return GetImages(item, item.GetPreferredMetadataLanguage(), item.GetPreferredMetadataCountryCode(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, string metadataLanguage, string metadataCountryCode, CancellationToken cancellationToken)
        {
            return await GetImages(item, metadataLanguage, metadataCountryCode, false, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, string metadataLanguage, string metadataCountryCode, bool includeAllLanguages, CancellationToken cancellationToken)
        {
            var language = TmdbUtils.GetImageQueryLanguage(metadataLanguage, includeAllLanguages);
            var countryCode = metadataCountryCode;
            var tmdbId = Convert.ToInt32(item.GetProviderId(MetadataProvider.Tmdb), CultureInfo.InvariantCulture);

            if (tmdbId <= 0)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var collection = await _tmdbClientManager
                .GetCollectionAsync(
                    tmdbId,
                    language,
                    TmdbUtils.GetImageLanguagesParam(language, countryCode, includeAllLanguages),
                    countryCode,
                    cancellationToken)
                .ConfigureAwait(false);

            if (collection?.Images is null)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var posters = collection.Images.Posters;
            var backdrops = collection.Images.Backdrops;
            var remoteImages = new List<RemoteImageInfo>(posters?.Count ?? 0 + backdrops?.Count ?? 0);

            if (posters is not null)
            {
                remoteImages.AddRange(_tmdbClientManager.ConvertPostersToRemoteImageInfo(posters, metadataLanguage));
            }

            if (backdrops is not null)
            {
                remoteImages.AddRange(_tmdbClientManager.ConvertBackdropsToRemoteImageInfo(backdrops, metadataLanguage));
            }

            return remoteImages;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
