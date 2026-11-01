using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Controller.Providers
{
    /// <summary>
    /// Interface IImageProvider.
    /// </summary>
    public interface IRemoteImageProvider : IImageProvider
    {
        /// <summary>
        /// Gets the supported images.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>IEnumerable{ImageType}.</returns>
        IEnumerable<ImageType> GetSupportedImages(BaseItem item);

        /// <summary>
        /// Gets the images.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{IEnumerable{RemoteImageInfo}}.</returns>
        Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the image response.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{HttpResponseInfo}.</returns>
        Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Remote image provider that accepts the locale selected for the current image query.
    /// </summary>
    public interface ILanguageAwareRemoteImageProvider : IRemoteImageProvider
    {
        /// <summary>
        /// Gets images using the requested metadata locale instead of the item's locale.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="metadataLanguage">The preferred metadata language.</param>
        /// <param name="metadataCountryCode">The preferred metadata country code.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The available images.</returns>
        Task<IEnumerable<RemoteImageInfo>> GetImages(
            BaseItem item,
            string metadataLanguage,
            string metadataCountryCode,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets images using the requested locale and language inclusion setting.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="metadataLanguage">The preferred metadata language.</param>
        /// <param name="metadataCountryCode">The preferred metadata country code.</param>
        /// <param name="includeAllLanguages">Whether images in every language should be returned.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The available images.</returns>
        Task<IEnumerable<RemoteImageInfo>> GetImages(
            BaseItem item,
            string metadataLanguage,
            string metadataCountryCode,
            bool includeAllLanguages,
            CancellationToken cancellationToken)
            => GetImages(item, metadataLanguage, metadataCountryCode, cancellationToken);
    }
}
