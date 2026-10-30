#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Manager
{
    /// <summary>
    /// Utilities for managing images attached to items.
    /// </summary>
    public class ItemImageProvider
    {
        private readonly ILogger _logger;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private static readonly ImageType[] AllImageTypes = Enum.GetValues<ImageType>();

        /// <summary>
        /// Image types that are only one per item.
        /// </summary>
        private static readonly ImageType[] _singularImages =
        {
            ImageType.Primary,
            ImageType.Art,
            ImageType.Banner,
            ImageType.Box,
            ImageType.BoxRear,
            ImageType.Disc,
            ImageType.Logo,
            ImageType.Menu,
            ImageType.Thumb
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemImageProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="providerManager">The provider manager for interacting with provider image references.</param>
        /// <param name="fileSystem">The filesystem.</param>
        public ItemImageProvider(ILogger logger, IProviderManager providerManager, IFileSystem fileSystem)
        {
            _logger = logger;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Removes all existing images from the provided item.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to remove images from.</param>
        /// <param name="canDeleteLocal">Whether removing images outside metadata folder is allowed.</param>
        /// <returns><c>true</c> if changes were made to the item; otherwise <c>false</c>.</returns>
        public bool RemoveImages(BaseItem item, bool canDeleteLocal = false)
        {
            var singular = new List<ItemImageInfo>();
            var itemMetadataPath = item.GetInternalMetadataPath();
            for (var i = 0; i < _singularImages.Length; i++)
            {
                var currentImage = item.GetImageInfo(_singularImages[i], 0);
                if (currentImage is not null)
                {
                    var imageInMetadataFolder = currentImage.Path.StartsWith(itemMetadataPath, StringComparison.OrdinalIgnoreCase);
                    if (imageInMetadataFolder || canDeleteLocal || item.IsSaveLocalMetadataEnabled())
                    {
                        singular.Add(currentImage);
                    }
                }
            }

            foreach (var backdrop in item.GetImages(ImageType.Backdrop))
            {
                var imageInMetadataFolder = backdrop.Path.StartsWith(itemMetadataPath, StringComparison.OrdinalIgnoreCase);
                if (imageInMetadataFolder || canDeleteLocal || item.IsSaveLocalMetadataEnabled())
                {
                    singular.Add(backdrop);
                }
            }

            PruneImages(item, singular);

            return singular.Count > 0;
        }

        /// <summary>
        /// Verifies existing images have valid paths and adds any new local images provided.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to validate images for.</param>
        /// <param name="providers">The providers to use, must include <see cref="ILocalImageProvider"/>(s) for local scanning.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <returns><c>true</c> if changes were made to the item; otherwise <c>false</c>.</returns>
        public bool ValidateImages(BaseItem item, IEnumerable<IImageProvider> providers, ImageRefreshOptions refreshOptions)
        {
            var hasChanges = false;
            var directoryService = refreshOptions?.DirectoryService;

            if (item is not Photo)
            {
                var images = providers.OfType<ILocalImageProvider>()
                    .SelectMany(i => i.GetImages(item, directoryService))
                    .ToList();

                if (MergeImages(item, images, refreshOptions))
                {
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        /// <summary>
        /// Refreshes from the providers according to the given options.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to gather images for.</param>
        /// <param name="libraryOptions">The library options.</param>
        /// <param name="providers">The providers to query for images.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The refresh result.</returns>
        public async Task<RefreshResult> RefreshImages(
            BaseItem item,
            LibraryOptions libraryOptions,
            IEnumerable<IImageProvider> providers,
            ImageRefreshOptions refreshOptions,
            CancellationToken cancellationToken)
        {
            var oldBackdropImages = Array.Empty<ItemImageInfo>();
            if (refreshOptions.IsReplacingImage(ImageType.Backdrop))
            {
                oldBackdropImages = item.GetImages(ImageType.Backdrop).ToArray();
            }

            var result = new RefreshResult { UpdateType = ItemUpdateType.None };

            var typeName = item.GetType().Name;
            var typeOptions = libraryOptions.GetTypeOptions(typeName) ?? new TypeOptions { Type = typeName };
            var imageProviders = providers.ToList();

            var preferredImageLanguage = GetPreferredImageLanguage(
                item,
                libraryOptions,
                BaseItem.LibraryManager,
                BaseItem.ConfigurationManager?.Configuration);
            var remoteProviders = imageProviders.OfType<IRemoteImageProvider>().ToList();
            var refreshedRemoteProviders = false;

            // track library limits, adding buffer to allow lazy replacing of current images
            var backdropLimit = typeOptions.GetLimit(ImageType.Backdrop) + oldBackdropImages.Length;
            var downloadedImages = new List<ImageType>();

            foreach (var provider in imageProviders)
            {
                if (provider is IRemoteImageProvider remoteProvider)
                {
                    if (!refreshedRemoteProviders)
                    {
                        await RefreshFromProviders(item, remoteProviders, refreshOptions, typeOptions, preferredImageLanguage, backdropLimit, downloadedImages, result, cancellationToken).ConfigureAwait(false);
                        refreshedRemoteProviders = true;
                    }

                    continue;
                }

                if (provider is IDynamicImageProvider dynamicImageProvider)
                {
                    await RefreshFromProvider(item, dynamicImageProvider, refreshOptions, typeOptions, downloadedImages, result, cancellationToken).ConfigureAwait(false);
                }
            }

            // Only delete existing multi-images if new ones were added
            if (oldBackdropImages.Length > 0 && oldBackdropImages.Length < item.GetImages(ImageType.Backdrop).Count())
            {
                PruneImages(item, oldBackdropImages);
            }

            return result;
        }

        /// <summary>
        /// Refreshes from a dynamic provider.
        /// </summary>
        private async Task RefreshFromProvider(
            BaseItem item,
            IDynamicImageProvider provider,
            ImageRefreshOptions refreshOptions,
            TypeOptions savedOptions,
            List<ImageType> downloadedImages,
            RefreshResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                var images = provider.GetSupportedImages(item);

                foreach (var imageType in images)
                {
                    if (!savedOptions.IsEnabled(imageType))
                    {
                        continue;
                    }

                    if (!item.HasImage(imageType) || (refreshOptions.IsReplacingImage(imageType) && !downloadedImages.Contains(imageType)))
                    {
                        _logger.LogDebug("Running {Provider} for {Item}", provider.GetType().Name, item.Path ?? item.Name);

                        var response = await provider.GetImage(item, imageType, cancellationToken).ConfigureAwait(false);

                        if (response.HasImage)
                        {
                            if (string.IsNullOrEmpty(response.Path))
                            {
                                var mimeType = response.Format.GetMimeType();

                                var index = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
                                await _providerManager.SaveImage(item, response.Stream, mimeType, imageType, index, cancellationToken).ConfigureAwait(false);
                                item.GetImageInfo(imageType, index)!.ProviderName = provider.Name;
                            }
                            else
                            {
                                if (response.Protocol == MediaProtocol.Http)
                                {
                                    _logger.LogDebug("Setting image url into item {Item}", item.Id);
                                    var index = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
                                    item.SetImage(
                                        new ItemImageInfo
                                        {
                                            Path = response.Path,
                                            Type = imageType,
                                            ProviderName = provider.Name
                                        },
                                        index);
                                }
                                else
                                {
                                    var mimeType = MimeTypes.GetMimeType(response.Path);

                                    var index = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
                                    await _providerManager.SaveImage(item, response.Path, mimeType, imageType, index, null, cancellationToken).ConfigureAwait(false);
                                    item.GetImageInfo(imageType, index)!.ProviderName = provider.Name;
                                }
                            }

                            downloadedImages.Add(imageType);
                            result.UpdateType |= ItemUpdateType.ImageUpdate;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Failures++;
                _logger.LogError(ex, "Error in {Provider} for {Item}", provider.Name, item.Path ?? item.Name);
            }
        }

        /// <summary>
        /// Refreshes from remote providers.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="providers">The remote providers.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <param name="savedOptions">The saved options.</param>
        /// <param name="preferredImageLanguage">The effective preferred image language.</param>
        /// <param name="backdropLimit">The backdrop limit.</param>
        /// <param name="downloadedImages">The downloaded images.</param>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task RefreshFromProviders(
            BaseItem item,
            IReadOnlyList<IRemoteImageProvider> providers,
            ImageRefreshOptions refreshOptions,
            TypeOptions savedOptions,
            string preferredImageLanguage,
            int backdropLimit,
            List<ImageType> downloadedImages,
            RefreshResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!item.SupportsRemoteImageDownloading || providers.Count == 0)
                {
                    return;
                }

                var supportedProviders = new List<IRemoteImageProvider>(providers.Count);
                var supportedImages = new List<ImageType>();
                foreach (var provider in providers)
                {
                    try
                    {
                        supportedImages.AddRange(provider.GetSupportedImages(item));
                        supportedProviders.Add(provider);
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = ex.Message;
                        result.Failures++;
                        _logger.LogError(ex, "Error in {Provider} for {Item}", provider.Name, item.Path ?? item.Name);
                    }
                }

                supportedImages = supportedImages.Distinct().ToList();
                if (!refreshOptions.ReplaceAllImages &&
                    refreshOptions.ReplaceImages.Count == 0 &&
                    ContainsImages(item, supportedImages, savedOptions, backdropLimit))
                {
                    return;
                }

                var providersByImage = new Dictionary<RemoteImageInfo, IRemoteImageProvider>();
                var list = new List<RemoteImageInfo>();
                var providerResults = await Task.WhenAll(supportedProviders.Select(async provider =>
                {
                    try
                    {
                        var images = await _providerManager.GetAvailableRemoteImages(
                            item,
                            new RemoteImageQuery(provider.Name)
                            {
                                IncludeAllLanguages = true,
                                IncludeDisabledProviders = false,
                            },
                            cancellationToken).ConfigureAwait(false);

                        return (Provider: provider, Images: images.ToList(), Error: (Exception?)null);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        return (Provider: provider, Images: new List<RemoteImageInfo>(), Error: ex);
                    }
                })).ConfigureAwait(false);

                foreach (var providerResult in providerResults)
                {
                    if (providerResult.Error is not null)
                    {
                        result.ErrorMessage = providerResult.Error.Message;
                        result.Failures++;
                        _logger.LogError(providerResult.Error, "Error in {Provider} for {Item}", providerResult.Provider.Name, item.Path ?? item.Name);
                        continue;
                    }

                    foreach (var image in providerResult.Images)
                    {
                        image.ProviderName ??= providerResult.Provider.Name;
                        providersByImage[image] = providerResult.Provider;
                        list.Add(image);
                    }
                }

                list = list.OrderByLanguageDescending(preferredImageLanguage).ToList();
                int minWidth;

                foreach (var imageType in _singularImages)
                {
                    if (!savedOptions.IsEnabled(imageType))
                    {
                        continue;
                    }

                    if (!item.HasImage(imageType) || (refreshOptions.IsReplacingImage(imageType) && !downloadedImages.Contains(imageType)))
                    {
                        minWidth = savedOptions.GetMinWidth(imageType);
                        var downloaded = await DownloadImage(item, providersByImage, result, list, minWidth, imageType, cancellationToken).ConfigureAwait(false);

                        if (downloaded)
                        {
                            downloadedImages.Add(imageType);
                        }
                    }
                }

                minWidth = savedOptions.GetMinWidth(ImageType.Backdrop);
                var localizedBackdrops = list.OrderByLanguageDescending(preferredImageLanguage);
                await DownloadMultiImages(
                    item,
                    ImageType.Backdrop,
                    refreshOptions,
                    backdropLimit,
                    providersByImage,
                    result,
                    localizedBackdrops,
                    minWidth,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Failures++;
                _logger.LogError(ex, "Error in remote image providers for {Item}", item.Path ?? item.Name);
            }
        }

        /// <summary>
        /// Determines if an item already contains the given images.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="images">The images.</param>
        /// <param name="savedOptions">The saved options.</param>
        /// <param name="backdropLimit">The backdrop limit.</param>
        /// <returns><c>true</c> if the specified item contains images; otherwise, <c>false</c>.</returns>
        private bool ContainsImages(BaseItem item, List<ImageType> images, TypeOptions savedOptions, int backdropLimit)
        {
            // Using .Any causes the creation of a DisplayClass aka. variable capture
            for (var i = 0; i < _singularImages.Length; i++)
            {
                var type = _singularImages[i];
                if (images.Contains(type) && !item.HasImage(type) && savedOptions.GetLimit(type) > 0)
                {
                    return false;
                }
            }

            if (images.Contains(ImageType.Backdrop) && item.GetImages(ImageType.Backdrop).Count() < backdropLimit)
            {
                return false;
            }

            return true;
        }

        private void PruneImages(BaseItem item, IReadOnlyList<ItemImageInfo> images)
        {
            foreach (var image in images)
            {
                if (image.IsLocalFile)
                {
                    try
                    {
                        _fileSystem.DeleteFile(image.Path);
                    }
                    catch (FileNotFoundException)
                    {
                        // Nothing to do, already gone
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Nothing to do, already gone
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Unable to delete {Image}", image.Path);
                    }
                }
            }

            item.RemoveImages(images);

            // Cleanup old metadata directory for episodes if empty, as long as it's not a virtual item
            if (item is Episode && !item.IsVirtualItem)
            {
                var oldLocalMetadataDirectory = Path.Combine(item.ContainingFolderPath, "metadata");
                if (_fileSystem.DirectoryExists(oldLocalMetadataDirectory) && !_fileSystem.GetFiles(oldLocalMetadataDirectory).Any())
                {
                    Directory.Delete(oldLocalMetadataDirectory);
                }
            }
        }

        /// <summary>
        /// Merges a list of images into the provided item, validating existing images and replacing them or adding new images as necessary.
        /// </summary>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <param name="dontReplaceImages">List of imageTypes to remove from ReplaceImages.</param>
        public void UpdateReplaceImages(ImageRefreshOptions refreshOptions, ICollection<ImageType> dontReplaceImages)
        {
            if (refreshOptions is not null)
            {
                if (refreshOptions.ReplaceAllImages)
                {
                    refreshOptions.ReplaceAllImages = false;
                    refreshOptions.ReplaceImages = AllImageTypes.ToList();
                }

                refreshOptions.ReplaceImages = refreshOptions.ReplaceImages.Except(dontReplaceImages).ToList();
            }
        }

        /// <summary>
        /// Merges a list of images into the provided item, validating existing images and replacing them or adding new images as necessary.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to modify.</param>
        /// <param name="images">The new images to place in <c>item</c>.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <returns><c>true</c> if changes were made to the item; otherwise <c>false</c>.</returns>
        public bool MergeImages(BaseItem item, IReadOnlyList<LocalImageInfo> images, ImageRefreshOptions refreshOptions)
        {
            var changed = item.ValidateImages();
            var foundImageTypes = new List<ImageType>();
            for (var i = 0; i < _singularImages.Length; i++)
            {
                var type = _singularImages[i];
                var image = GetFirstLocalImageInfoByType(images, type);
                if (image is not null)
                {
                    var currentImage = item.GetImageInfo(type, 0);
                    // if image file is stored with media, don't replace that later
                    if (item.ContainingFolderPath is not null && item.ContainingFolderPath.Contains(Path.GetDirectoryName(image.FileInfo.FullName), StringComparison.OrdinalIgnoreCase))
                    {
                        foundImageTypes.Add(type);
                    }

                    if (currentImage is null || !string.Equals(currentImage.Path, image.FileInfo.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.SetImagePath(type, image.FileInfo);
                        changed = true;
                    }
                    else
                    {
                        var newDateModified = _fileSystem.GetLastWriteTimeUtc(image.FileInfo);

                        // If date changed then we need to reset saved image dimensions
                        if (currentImage.DateModified != newDateModified && (currentImage.Width > 0 || currentImage.Height > 0))
                        {
                            currentImage.Width = 0;
                            currentImage.Height = 0;
                            changed = true;
                        }

                        currentImage.DateModified = newDateModified;
                    }
                }
            }

            bool hasBackdrop = false;
            bool backdropStoredWithMedia = false;

            foreach (var image in images)
            {
                if (image.Type != ImageType.Backdrop)
                {
                    continue;
                }

                hasBackdrop = true;

                if (item.ContainingFolderPath is not null && item.ContainingFolderPath.Contains(Path.GetDirectoryName(image.FileInfo.FullName), StringComparison.OrdinalIgnoreCase))
                {
                    backdropStoredWithMedia = true;
                    break;
                }
            }

            if (hasBackdrop)
            {
                if (UpdateMultiImages(item, images, ImageType.Backdrop))
                {
                    changed = true;
                }

                if (backdropStoredWithMedia)
                {
                    foundImageTypes.Add(ImageType.Backdrop);
                }
            }

            if (foundImageTypes.Count > 0)
            {
                UpdateReplaceImages(refreshOptions, foundImageTypes);
            }

            return changed;
        }

        private static LocalImageInfo GetFirstLocalImageInfoByType(IReadOnlyList<LocalImageInfo> images, ImageType type)
        {
            var len = images.Count;
            for (var i = 0; i < len; i++)
            {
                var image = images[i];
                if (image.Type == type)
                {
                    return image;
                }
            }

            return null;
        }

        private bool UpdateMultiImages(BaseItem item, IReadOnlyList<LocalImageInfo> images, ImageType type)
        {
            var changed = false;

            var newImageFileInfos = images
                .Where(i => i.Type == type)
                .Select(i => i.FileInfo)
                .ToList();

            if (item.AddImages(type, newImageFileInfos))
            {
                changed = true;
            }

            return changed;
        }

        private async Task<bool> DownloadImage(
            BaseItem item,
            IReadOnlyDictionary<RemoteImageInfo, IRemoteImageProvider> providers,
            RefreshResult result,
            IEnumerable<RemoteImageInfo> images,
            int minWidth,
            ImageType type,
            CancellationToken cancellationToken)
        {
            var eligibleImages = images
                .Where(i => i.Type == type && (i.Width is null || i.Width >= minWidth))
                .ToList();

            if (EnableImageStub(item) && eligibleImages.Count > 0)
            {
                var imageIndex = item.AllowsMultipleImages(type) ? item.GetImages(type).Count() : 0;
                SaveImageStub(item, type, eligibleImages.Select(i => i.Url), imageIndex);
                SetImageSourceMetadata(item, type, imageIndex, eligibleImages[0], eligibleImages[0].ProviderName);
                result.UpdateType |= ItemUpdateType.ImageUpdate;
                return true;
            }

            var failedCandidates = 0;
            string? lastCandidateError = null;
            foreach (var image in eligibleImages)
            {
                var url = image.Url;
                if (!providers.TryGetValue(image, out var provider))
                {
                    continue;
                }

                try
                {
                    using var response = await provider.GetImageResponse(url, cancellationToken).ConfigureAwait(false);

                    // Sometimes providers send back bad urls. Just move to the next image
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogDebug("{Url} returned {StatusCode}, ignoring", url, response.StatusCode);
                        failedCandidates++;
                        lastCandidateError = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("{Url} returned {StatusCode}, trying the next image candidate", url, response.StatusCode);
                        failedCandidates++;
                        lastCandidateError = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        continue;
                    }

                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    var imageIndex = item.AllowsMultipleImages(type) ? item.GetImages(type).Count() : 0;
                    await using (stream.ConfigureAwait(false))
                    {
                        var mimetype = response.Content.Headers.ContentType?.MediaType;
                        if (mimetype is null || mimetype.Equals(MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
                        {
                            mimetype = MimeTypes.GetMimeType(response.RequestMessage.RequestUri.GetLeftPart(UriPartial.Path));
                        }

                        if (!IsImageMimeType(mimetype))
                        {
                            throw new HttpRequestException(
                                $"Request returned '{mimetype ?? "unknown"}' instead of an image type",
                                null,
                                HttpStatusCode.NotFound);
                        }

                        await _providerManager.SaveImage(
                            item,
                            stream,
                            mimetype,
                            type,
                            imageIndex,
                            cancellationToken).ConfigureAwait(false);
                        SetImageSourceMetadata(item, type, imageIndex, image, provider.Name);
                    }

                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                    return true;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve image candidate from {Provider}; trying the next candidate", provider.Name);
                    failedCandidates++;
                    lastCandidateError = ex.Message;
                }
            }

            if (failedCandidates > 0)
            {
                result.ErrorMessage = lastCandidateError;
                result.Failures++;
            }

            return false;
        }

        private bool EnableImageStub(BaseItem item)
        {
            if (item is LiveTvProgram)
            {
                return true;
            }

            if (!item.IsFileProtocol)
            {
                return true;
            }

            if (item is IItemByName and not MusicArtist)
            {
                var hasDualAccess = item as IHasDualAccess;
                if (hasDualAccess is null || hasDualAccess.IsAccessedByName)
                {
                    return true;
                }
            }

            // We always want to use prefetched images
            return false;
        }

        internal static string GetPreferredImageLanguage(
            BaseItem item,
            LibraryOptions libraryOptions,
            ILibraryManager? libraryManager,
            ServerConfiguration? serverConfiguration)
        {
            var language = item.PreferredMetadataLanguage;
            var countryCode = item.PreferredMetadataCountryCode;

            foreach (var parent in item.GetParents())
            {
                if (string.IsNullOrWhiteSpace(language) && !string.IsNullOrWhiteSpace(parent.PreferredMetadataLanguage))
                {
                    language = parent.PreferredMetadataLanguage;
                }

                if (string.IsNullOrWhiteSpace(countryCode) && !string.IsNullOrWhiteSpace(parent.PreferredMetadataCountryCode))
                {
                    countryCode = parent.PreferredMetadataCountryCode;
                }

                if (!string.IsNullOrWhiteSpace(language) && !string.IsNullOrWhiteSpace(countryCode))
                {
                    break;
                }
            }

            if ((string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(countryCode)) && libraryManager is not null)
            {
                foreach (var folder in libraryManager.GetCollectionFolders(item))
                {
                    if (string.IsNullOrWhiteSpace(language) && !string.IsNullOrWhiteSpace(folder.PreferredMetadataLanguage))
                    {
                        language = folder.PreferredMetadataLanguage;
                    }

                    if (string.IsNullOrWhiteSpace(countryCode) && !string.IsNullOrWhiteSpace(folder.PreferredMetadataCountryCode))
                    {
                        countryCode = folder.PreferredMetadataCountryCode;
                    }

                    if (!string.IsNullOrWhiteSpace(language) && !string.IsNullOrWhiteSpace(countryCode))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                language = libraryOptions.PreferredMetadataLanguage;
            }

            if (string.IsNullOrWhiteSpace(countryCode))
            {
                countryCode = libraryOptions.MetadataCountryCode;
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                language = serverConfiguration?.PreferredMetadataLanguage;
            }

            if (string.IsNullOrWhiteSpace(countryCode))
            {
                countryCode = serverConfiguration?.MetadataCountryCode;
            }

            if (string.Equals(countryCode, "BR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase))
            {
                return "pt-BR";
            }

            return language ?? string.Empty;
        }

        private void SaveImageStub(BaseItem item, ImageType imageType, IEnumerable<string> urls)
        {
            var newIndex = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;

            SaveImageStub(item, imageType, urls, newIndex);
        }

        private void SaveImageStub(BaseItem item, ImageType imageType, IEnumerable<string> urls, int newIndex)
        {
            var path = string.Join('|', urls.Take(1));

            item.SetImage(
                new ItemImageInfo
                {
                    Path = path,
                    Type = imageType
                },
                newIndex);
        }

        private async Task DownloadMultiImages(BaseItem item, ImageType imageType, ImageRefreshOptions refreshOptions, int limit, IReadOnlyDictionary<RemoteImageInfo, IRemoteImageProvider> providers, RefreshResult result, IEnumerable<RemoteImageInfo> images, int minWidth, CancellationToken cancellationToken)
        {
            foreach (var image in images.Where(i => i.Type == imageType))
            {
                if (item.GetImages(imageType).Count() >= limit)
                {
                    break;
                }

                if (image.Width.HasValue && image.Width.Value < minWidth)
                {
                    continue;
                }

                var url = image.Url;
                if (!providers.TryGetValue(image, out var provider))
                {
                    continue;
                }

                if (EnableImageStub(item))
                {
                    var imageIndex = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
                    SaveImageStub(item, imageType, new[] { url }, imageIndex);
                    SetImageSourceMetadata(item, imageType, imageIndex, image, provider.Name);
                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                    continue;
                }

                try
                {
                    using var response = await provider.GetImageResponse(url, cancellationToken).ConfigureAwait(false);

                    // Sometimes providers send back bad urls. Just move to the next image
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogDebug("{Url} returned {StatusCode}, ignoring", url, response.StatusCode);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("{Url} returned {StatusCode}, trying the next image candidate", url, response.StatusCode);
                        result.ErrorMessage = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        result.Failures++;
                        continue;
                    }

                    // If there's already an image of the same file size, skip it unless doing a full refresh
                    if (response.Content.Headers.ContentLength.HasValue && !refreshOptions.IsReplacingImage(imageType))
                    {
                        try
                        {
                            if (item.GetImages(imageType).Any(i => _fileSystem.GetFileInfo(i.Path).Length == response.Content.Headers.ContentLength.Value))
                            {
                                response.Content.Dispose();
                                continue;
                            }
                        }
                        catch (IOException ex)
                        {
                            _logger.LogError(ex, "Error examining images");
                        }
                    }

                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using (stream.ConfigureAwait(false))
                    {
                        var mimetype = response.Content.Headers.ContentType?.MediaType;
                        if (mimetype is null || mimetype.Equals(MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
                        {
                            mimetype = MimeTypes.GetMimeType(response.RequestMessage.RequestUri.GetLeftPart(UriPartial.Path));
                        }

                        if (!IsImageMimeType(mimetype))
                        {
                            throw new HttpRequestException(
                                $"Request returned '{mimetype ?? "unknown"}' instead of an image type",
                                null,
                                HttpStatusCode.NotFound);
                        }

                        var imageIndex = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
                        await _providerManager.SaveImage(
                            item,
                            stream,
                            mimetype,
                            imageType,
                            imageIndex,
                            cancellationToken).ConfigureAwait(false);
                        SetImageSourceMetadata(item, imageType, imageIndex, image, provider.Name);
                    }

                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve image candidate from {Provider}; trying the next candidate", provider.Name);
                    result.ErrorMessage = ex.Message;
                    result.Failures++;
                }
            }
        }

        private static void SetImageSourceMetadata(BaseItem item, ImageType imageType, int imageIndex, RemoteImageInfo image, string providerName)
        {
            var imageInfo = item.GetImageInfo(imageType, imageIndex);
            if (imageInfo is not null)
            {
                imageInfo.ProviderName = string.IsNullOrWhiteSpace(image.ProviderName) ? providerName : image.ProviderName;
                imageInfo.Language = image.Language?.ToLowerInvariant();
            }
        }

        private static bool IsImageMimeType(string? mimeType)
            => !string.IsNullOrWhiteSpace(mimeType)
                && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
