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
using MediaBrowser.Providers.Helpers;
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
        /// <param name="canDeleteLocal">Whether detaching images outside the metadata folder is allowed.</param>
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
            var backdropCountBeforeRefresh = item.GetImages(ImageType.Backdrop).Count();
            if (refreshOptions.IsReplacingImage(ImageType.Backdrop))
            {
                oldBackdropImages = item.GetImages(ImageType.Backdrop).ToArray();
            }

            var result = new RefreshResult { UpdateType = ItemUpdateType.None };

            var typeName = item.GetType().Name;
            var typeOptions = libraryOptions.GetTypeOptions(typeName) ?? new TypeOptions { Type = typeName };
            var imageProviders = providers.ToList();

            // track library limits, adding buffer to allow lazy replacing of current images
            var backdropLimit = typeOptions.GetLimit(ImageType.Backdrop) + oldBackdropImages.Length;
            var remoteImageProviders = imageProviders.OfType<IRemoteImageProvider>().ToArray();
            var remoteProviderNames = remoteImageProviders
                .Select(provider => provider.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var preferredLanguage = string.IsNullOrWhiteSpace(item.PreferredMetadataLanguage)
                ? libraryOptions.PreferredMetadataLanguage
                : item.PreferredMetadataLanguage;
            if (string.IsNullOrWhiteSpace(preferredLanguage)
                && BaseItem.LibraryManager is not null
                && BaseItem.ConfigurationManager is not null)
            {
                preferredLanguage = item.GetPreferredMetadataLanguage();
            }
            var staleRemoteBackdrops = item.GetImages(ImageType.Backdrop)
                .Where(image => !string.IsNullOrWhiteSpace(image.ProviderName)
                    && remoteProviderNames.Contains(image.ProviderName)
                    && (string.IsNullOrWhiteSpace(image.Language)
                        || !EnumerableExtensions.MatchesLanguage(preferredLanguage, image.Language)))
                .ToArray();
            var staleRemoteImageTypes = _singularImages
                .Where(type =>
                {
                    var image = item.GetImageInfo(type, 0);
                    return image is not null
                        && !string.IsNullOrWhiteSpace(image.ProviderName)
                        && remoteProviderNames.Contains(image.ProviderName)
                        && (string.IsNullOrWhiteSpace(image.Language)
                            || !EnumerableExtensions.MatchesLanguage(preferredLanguage, image.Language));
                })
                .ToHashSet();
            if (staleRemoteBackdrops.Length > 0)
            {
                staleRemoteImageTypes.Add(ImageType.Backdrop);
            }
            var providersNeedingRefresh = remoteImageProviders
                .Where(provider => item.SupportsRemoteImageDownloading
                    && (refreshOptions.ReplaceAllImages
                        || refreshOptions.ReplaceImages.Count > 0
                        || provider.GetSupportedImages(item).Any(staleRemoteImageTypes.Contains)
                        || !ContainsImages(item, provider.GetSupportedImages(item).ToList(), typeOptions, backdropLimit)))
                .ToArray();
            var remoteImages = Array.Empty<RemoteImageInfo>();

            if (providersNeedingRefresh.Length > 0)
            {
                try
                {
                    remoteImages = (await _providerManager.GetAvailableRemoteImages(
                        item,
                        new RemoteImageQuery(string.Empty)
                        {
                            IncludeAllLanguages = true,
                            IncludeDisabledProviders = false,
                        },
                        cancellationToken).ConfigureAwait(false))
                        .Where(image => string.IsNullOrWhiteSpace(image.Language)
                            || EnumerableExtensions.MatchesLanguage(preferredLanguage, image.Language))
                        .OrderByLanguageDescending(preferredLanguage)
                        .ToArray();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Failures++;
                    result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
                    _logger.LogError(ex, "Error retrieving remote images for {Item}", item.Path ?? item.Name ?? "Unknown name");
                    return result;
                }
            }

            var preferredLanguageImageTypes = remoteImages
                .Where(image => staleRemoteImageTypes.Contains(image.Type)
                    && EnumerableExtensions.MatchesLanguage(preferredLanguage, image.Language))
                .Select(image => image.Type)
                .ToHashSet();
            var imageTypesToReplace = preferredLanguageImageTypes
                .Concat(remoteImages
                    .Where(image => staleRemoteImageTypes.Contains(image.Type)
                        && !preferredLanguageImageTypes.Contains(image.Type)
                    && item.GetImages(image.Type).Any(existing =>
                        remoteProviderNames.Contains(existing.ProviderName)
                        && (string.IsNullOrWhiteSpace(existing.Language)
                            || !EnumerableExtensions.MatchesLanguage(preferredLanguage, existing.Language))
                        && !EnumerableExtensions.MatchesLanguage("en", existing.Language))
                    && EnumerableExtensions.MatchesLanguage("en", image.Language))
                    .Select(image => image.Type))
                .ToHashSet();

            if (!refreshOptions.IsReplacingImage(ImageType.Backdrop)
                && imageTypesToReplace.Contains(ImageType.Backdrop))
            {
                oldBackdropImages = staleRemoteBackdrops;
                // Reserve only configured replacement slots before pruning stale remote backdrops.
                var existingNonStaleBackdropCount = item.GetImages(ImageType.Backdrop).Count() - oldBackdropImages.Length;
                var replacementSlots = existingNonStaleBackdropCount == 0
                    ? Math.Min(oldBackdropImages.Length, typeOptions.GetLimit(ImageType.Backdrop))
                    : oldBackdropImages.Length;
                backdropLimit = item.GetImages(ImageType.Backdrop).Count()
                    + replacementSlots;
            }

            var downloadedImages = new List<ImageType>();

            var preferredLanguageProviders = remoteImages
                .Where(image => EnumerableExtensions.MatchesLanguage(preferredLanguage, image.Language))
                .Select(image => image.ProviderName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in imageProviders.OrderBy(provider =>
                         provider is IRemoteImageProvider remoteProvider
                         && !preferredLanguageProviders.Contains(remoteProvider.Name) ? 1 : 0))
            {
                if (provider is IRemoteImageProvider remoteProvider)
                {
                    if (!providersNeedingRefresh.Contains(remoteProvider))
                    {
                        continue;
                    }

                    var providerImages = remoteImages.Where(image =>
                        string.Equals(image.ProviderName, remoteProvider.Name, StringComparison.OrdinalIgnoreCase)
                        || (remoteImageProviders.Length == 1 && string.IsNullOrWhiteSpace(image.ProviderName)));
                    await RefreshFromProvider(item, remoteProvider, providerImages, preferredLanguage, imageTypesToReplace, refreshOptions, typeOptions, backdropLimit, downloadedImages, result, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (provider is IDynamicImageProvider dynamicImageProvider)
                {
                    await RefreshFromProvider(item, dynamicImageProvider, refreshOptions, typeOptions, downloadedImages, result, cancellationToken).ConfigureAwait(false);
                }
            }

            // Only delete as many existing multi-images as replacements were added.
            var addedBackdropCount = item.GetImages(ImageType.Backdrop).Count() - backdropCountBeforeRefresh;
            if (oldBackdropImages.Length > 0 && addedBackdropCount > 0)
            {
                var imagesToPrune = refreshOptions.IsReplacingImage(ImageType.Backdrop)
                    ? oldBackdropImages
                    : oldBackdropImages
                        .OrderBy(image => EnumerableExtensions.MatchesLanguage("en", image.Language) ? 1 : 0)
                        .Take(Math.Min(oldBackdropImages.Length, addedBackdropCount))
                        .ToArray();
                PruneImages(item, imagesToPrune, deleteReplacedRemoteFiles: true);
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

                                await _providerManager.SaveImage(item, response.Stream, mimeType, imageType, null, cancellationToken).ConfigureAwait(false);
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

                                    await _providerManager.SaveImage(item, response.Path, mimeType, imageType, null, null, cancellationToken).ConfigureAwait(false);
                                }

                            }

                            var savedImageIndex = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() - 1 : 0;
                            var savedImage = item.GetImageInfo(imageType, savedImageIndex);
                            if (savedImage is not null)
                            {
                                savedImage.ProviderName = provider.Name;
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
                result.ErrorMessage = RemoteUrlLogRedactor.Redact(ex.Message);
                result.Failures++;
                _logger.LogError("Error in {Provider} for {Item}: {Error}", provider.Name, item.Path ?? item.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
            }
        }

        /// <summary>
        /// Refreshes from a remote provider.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="images">The language-filtered remote image candidates for this provider.</param>
        /// <param name="preferredLanguage">The preferred metadata language for image ordering.</param>
        /// <param name="imageTypesToReplace">Image types with a preferred-language candidate, or an English fallback, that may safely replace known remote images.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <param name="savedOptions">The saved options.</param>
        /// <param name="backdropLimit">The backdrop limit.</param>
        /// <param name="downloadedImages">The downloaded images.</param>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task RefreshFromProvider(
            BaseItem item,
            IRemoteImageProvider provider,
            IEnumerable<RemoteImageInfo> images,
            string preferredLanguage,
            IReadOnlySet<ImageType> imageTypesToReplace,
            ImageRefreshOptions refreshOptions,
            TypeOptions savedOptions,
            int backdropLimit,
            List<ImageType> downloadedImages,
            RefreshResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!item.SupportsRemoteImageDownloading)
                {
                    return;
                }

                if (!refreshOptions.ReplaceAllImages &&
                    refreshOptions.ReplaceImages.Count == 0 &&
                    !provider.GetSupportedImages(item).Any(imageTypesToReplace.Contains) &&
                    ContainsImages(item, provider.GetSupportedImages(item).ToList(), savedOptions, backdropLimit))
                {
                    return;
                }

                _logger.LogDebug("Running {Provider} for {Item}", provider.GetType().Name, item.Path ?? item.Name);

                var list = images.ToList();
                int minWidth;

                foreach (var imageType in _singularImages)
                {
                    if (!savedOptions.IsEnabled(imageType))
                    {
                        continue;
                    }

                    if (!item.HasImage(imageType)
                        || ((refreshOptions.IsReplacingImage(imageType) || imageTypesToReplace.Contains(imageType))
                            && !downloadedImages.Contains(imageType)))
                    {
                        minWidth = savedOptions.GetMinWidth(imageType);
                        var downloaded = await DownloadImage(item, provider, result, list, minWidth, imageType, cancellationToken).ConfigureAwait(false);

                        if (downloaded)
                        {
                            downloadedImages.Add(imageType);
                        }
                    }
                }

                minWidth = savedOptions.GetMinWidth(ImageType.Backdrop);
                var orderedBackdrops = list
                    .OrderByDescending(i => EnumerableExtensions.MatchesLanguage(preferredLanguage, i.Language))
                    .ThenByDescending(i => string.IsNullOrEmpty(i.Language));
                await DownloadMultiImages(item, ImageType.Backdrop, refreshOptions, backdropLimit, provider, result, orderedBackdrops, minWidth, imageTypesToReplace.Contains(ImageType.Backdrop), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = RemoteUrlLogRedactor.Redact(ex.Message);
                result.Failures++;
                _logger.LogError("Error in {Provider} for {Item}: {Error}", provider.Name, item.Path ?? item.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
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

        private void PruneImages(BaseItem item, IReadOnlyList<ItemImageInfo> images, bool deleteReplacedRemoteFiles = false)
        {
            if (deleteReplacedRemoteFiles)
            {
                foreach (var image in images)
                {
                    // Only delete locally saved remote images. Manual local images remain user-owned.
                    if (!image.IsLocalFile
                        || string.IsNullOrWhiteSpace(image.ProviderName)
                        || IsImageReferencedElsewhere(item, image))
                    {
                        continue;
                    }

                    try
                    {
                        _fileSystem.DeleteFile(image.Path);
                    }
                    catch (FileNotFoundException)
                    {
                        // Nothing to do, already gone.
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Nothing to do, already gone.
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Unable to delete replaced image {Image}", image.Path);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Unable to delete replaced image {Image}", image.Path);
                    }
                }
            }

            // Image paths may be shared; detach references without deleting files another item may use.
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

        private bool IsImageReferencedElsewhere(BaseItem item, ItemImageInfo image)
        {
            if (item.ImageInfos.Any(current =>
                    !ReferenceEquals(current, image)
                    && string.Equals(current.Path, image.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (BaseItem.LibraryManager is null)
            {
                _logger.LogWarning("Skipping deletion of replaced image {Image} because the library reference index is unavailable", image.Path);
                return true;
            }

            try
            {
                return BaseItem.LibraryManager
                    .GetItemList(new InternalItemsQuery { ImageTypes = AllImageTypes })
                    .Any(other => other.Id != item.Id
                        && other.ImageInfos.Any(current => string.Equals(current.Path, image.Path, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping deletion of replaced image {Image} because image references could not be checked", image.Path);
                return true;
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
                    if (IsImageStoredWithMedia(item, image))
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
                        if (Math.Abs((currentImage.DateModified - newDateModified).TotalSeconds) > 1 && (currentImage.Width > 0 || currentImage.Height > 0))
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

                if (IsImageStoredWithMedia(item, image))
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

        private static bool IsImageStoredWithMedia(BaseItem item, LocalImageInfo image)
        {
            var containingFolderPath = item.ContainingFolderPath;
            var imagePath = image?.FileInfo?.FullName;
            if (string.IsNullOrWhiteSpace(containingFolderPath) || string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            var imageDirectory = Path.GetDirectoryName(imagePath);
            if (string.IsNullOrWhiteSpace(imageDirectory))
            {
                return false;
            }

            var normalizedContaining = containingFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedImageDir = imageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedContaining, normalizedImageDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedImageDir.StartsWith(normalizedContaining + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedImageDir.StartsWith(normalizedContaining + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
            IRemoteImageProvider provider,
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
                SaveImageStub(item, type, provider, eligibleImages);
                result.UpdateType |= ItemUpdateType.ImageUpdate;
                return true;
            }

            var failedCandidates = 0;
            string? lastCandidateError = null;
            foreach (var image in eligibleImages)
            {
                var url = image.Url;

                try
                {
                    using var response = await provider.GetImageResponse(url, cancellationToken).ConfigureAwait(false);

                    // Sometimes providers send back bad urls. Just move to the next image
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogDebug("{Url} returned {StatusCode}, ignoring", RemoteUrlLogRedactor.Redact(url), response.StatusCode);
                        failedCandidates++;
                        lastCandidateError = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("{Url} returned {StatusCode}, trying the next image candidate", RemoteUrlLogRedactor.Redact(url), response.StatusCode);
                        failedCandidates++;
                        lastCandidateError = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        continue;
                    }

                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using (stream.ConfigureAwait(false))
                    {
                        var mimetype = response.Content.Headers.ContentType?.MediaType;
                        if (mimetype is null || mimetype.Equals(MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
                        {
                            var resolvedUri = response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path) ?? url;
                            mimetype = MimeTypes.GetMimeType(resolvedUri);
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
                            null,
                            cancellationToken).ConfigureAwait(false);
                    }

                    SetImageSource(item, type, 0, provider, image);

                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                    return true;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Failed to retrieve image candidate from {Provider}; trying the next candidate: {Error}", provider.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
                    failedCandidates++;
                    lastCandidateError = RemoteUrlLogRedactor.Redact(ex.Message);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to retrieve image candidate from {Provider}; trying the next candidate: {Error}", provider.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
                    failedCandidates++;
                    lastCandidateError = RemoteUrlLogRedactor.Redact(ex.Message);
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

        private void SaveImageStub(BaseItem item, ImageType imageType, IRemoteImageProvider provider, IEnumerable<RemoteImageInfo> images)
        {
            var newIndex = item.AllowsMultipleImages(imageType) ? item.GetImages(imageType).Count() : 0;
            var image = images.First();

            item.SetImage(
                new ItemImageInfo
                {
                    Path = image.Url,
                    Type = imageType,
                    ProviderName = image.ProviderName,
                    Language = image.Language
                },
                newIndex);
            SetImageSource(item, imageType, newIndex, provider, image);
        }

        private async Task DownloadMultiImages(BaseItem item, ImageType imageType, ImageRefreshOptions refreshOptions, int limit, IRemoteImageProvider provider, RefreshResult result, IEnumerable<RemoteImageInfo> images, int minWidth, bool replacingLocalizedImages, CancellationToken cancellationToken)
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

                if (EnableImageStub(item))
                {
                    SaveImageStub(item, imageType, provider, new[] { image });
                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                    continue;
                }

                try
                {
                    using var response = await provider.GetImageResponse(url, cancellationToken).ConfigureAwait(false);

                    // Sometimes providers send back bad urls. Just move to the next image
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogDebug("{Url} returned {StatusCode}, ignoring", RemoteUrlLogRedactor.Redact(url), response.StatusCode);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("{Url} returned {StatusCode}, trying the next image candidate", RemoteUrlLogRedactor.Redact(url), response.StatusCode);
                        result.ErrorMessage = $"{provider.Name} returned HTTP {(int)response.StatusCode}";
                        result.Failures++;
                        continue;
                    }

                    // If there's already an image of the same file size, skip it unless doing a full refresh
                    if (response.Content.Headers.ContentLength.HasValue && !refreshOptions.IsReplacingImage(imageType) && !replacingLocalizedImages)
                    {
                        try
                        {
                            var targetLength = response.Content.Headers.ContentLength.Value;
                            if (item.GetImages(imageType).Any(i => i.IsLocalFile
                                                                   && !string.IsNullOrWhiteSpace(i.Path)
                                                                   && _fileSystem.GetFileInfo(i.Path)?.Length == targetLength))
                            {
                                response.Content.Dispose();
                                continue;
                            }
                        }
                        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
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
                            var resolvedUri = response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path) ?? url;
                            mimetype = MimeTypes.GetMimeType(resolvedUri);
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
                            imageType,
                            null,
                            cancellationToken).ConfigureAwait(false);
                    }

                    var currentImages = item.GetImages(imageType).ToList();
                    if (currentImages.Count > 0)
                    {
                        SetImageSource(item, imageType, currentImages.Count - 1, provider, image);
                    }

                    result.UpdateType |= ItemUpdateType.ImageUpdate;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Failed to retrieve image candidate from {Provider}; trying the next candidate: {Error}", provider.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
                    result.ErrorMessage = RemoteUrlLogRedactor.Redact(ex.Message);
                    result.Failures++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to retrieve image candidate from {Provider}; trying the next candidate: {Error}", provider.Name, RemoteUrlLogRedactor.Redact(ex.ToString()));
                    result.ErrorMessage = RemoteUrlLogRedactor.Redact(ex.Message);
                    result.Failures++;
                }
            }
        }

        private static bool IsImageMimeType(string? mimeType)
            => !string.IsNullOrWhiteSpace(mimeType)
                && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        private static void SetImageSource(BaseItem item, ImageType imageType, int index, IRemoteImageProvider? provider, RemoteImageInfo image)
        {
            var imageInfo = item.GetImageInfo(imageType, index);
            if (imageInfo is not null)
            {
                imageInfo.ProviderName = image.ProviderName ?? provider?.Name;
                imageInfo.Language = image.Language;
            }
        }
    }
}
