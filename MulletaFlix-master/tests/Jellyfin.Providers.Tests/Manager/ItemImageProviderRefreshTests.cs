using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager
{
    public partial class ItemImageProviderTests
    {
        [Theory]
        [InlineData(ImageType.Primary, 1, false)]
        [InlineData(ImageType.Backdrop, 2, false)]
        [InlineData(ImageType.Primary, 1, true)]
        [InlineData(ImageType.Backdrop, 2, true)]
        public async Task RefreshImages_PopulatedItemPopulatedProviderDynamic_UpdatesImagesIfForced(ImageType imageType, int imageCount, bool forceRefresh)
        {
            var item = GetItemWithImages(imageType, imageCount, false);

            var libraryOptions = GetLibraryOptions(item, imageType, imageCount);

            var imageResponse = new DynamicImageResponse
            {
                HasImage = true,
                Format = ImageFormat.Jpg,
                Path = "url path",
                Protocol = MediaProtocol.Http
            };

            var dynamicProvider = new Mock<IDynamicImageProvider>(MockBehavior.Strict);
            dynamicProvider.Setup(rp => rp.Name).Returns("MockDynamicProvider");
            dynamicProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });
            dynamicProvider.Setup(rp => rp.GetImage(item, imageType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            var refreshOptions = forceRefresh
                ? new ImageRefreshOptions(Mock.Of<IDirectoryService>())
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllImages = true
                }
                : new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            var itemImageProvider = GetItemImageProvider(null, new Mock<IFileSystem>());
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { dynamicProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.Equal(forceRefresh, result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            if (forceRefresh)
            {
                // replaces multi-types
                Assert.Single(item.GetImages(imageType));
            }
            else
            {
                // adds to multi-types if room
                Assert.Equal(imageCount, item.GetImages(imageType).Count());
            }
        }

        [Theory]
        [InlineData(ImageType.Primary, 1, true, MediaProtocol.Http)]
        [InlineData(ImageType.Backdrop, 2, true, MediaProtocol.Http)]
        [InlineData(ImageType.Primary, 1, true, MediaProtocol.File)]
        [InlineData(ImageType.Backdrop, 2, true, MediaProtocol.File)]
        [InlineData(ImageType.Primary, 1, false, MediaProtocol.File)]
        [InlineData(ImageType.Backdrop, 2, false, MediaProtocol.File)]
        public async Task RefreshImages_EmptyItemPopulatedProviderDynamic_AddsImages(ImageType imageType, int imageCount, bool responseHasPath, MediaProtocol protocol)
        {
            // Has to exist for querying DateModified time on file, results stored but not checked so not populating
            BaseItem.FileSystem = Mock.Of<IFileSystem>();

            var item = new Video();

            var libraryOptions = GetLibraryOptions(item, imageType, imageCount);

            // Path must exist if set: is read in as a stream by AsyncFile.OpenRead
            var imageResponse = new DynamicImageResponse
            {
                HasImage = true,
                Format = ImageFormat.Jpg,
                Path = responseHasPath ? string.Format(CultureInfo.InvariantCulture, _testDataImagePath, 0) : null,
                Protocol = protocol
            };

            var dynamicProvider = new Mock<IDynamicImageProvider>(MockBehavior.Strict);
            dynamicProvider.Setup(rp => rp.Name).Returns("MockDynamicProvider");
            dynamicProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });
            dynamicProvider.Setup(rp => rp.GetImage(item, imageType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(pm => pm.SaveImage(item, It.IsAny<Stream>(), It.IsAny<string>(), imageType, null, It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((callbackItem, _, _, callbackType, _, _) => callbackItem.SetImagePath(callbackType, 0, new FileSystemMetadata()))
                .Returns(Task.CompletedTask);
            providerManager.Setup(pm => pm.SaveImage(item, It.IsAny<string>(), It.IsAny<string>(), imageType, null, null, It.IsAny<CancellationToken>()))
                .Callback<BaseItem, string, string, ImageType, int?, bool?, CancellationToken>((callbackItem, _, _, callbackType, _, _, _) => callbackItem.SetImagePath(callbackType, 0, new FileSystemMetadata()))
                .Returns(Task.CompletedTask);
            var itemImageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { dynamicProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            // dynamic provider unable to return multiple images
            Assert.Single(item.GetImages(imageType));
            if (protocol == MediaProtocol.Http)
            {
                Assert.Equal(imageResponse.Path, item.GetImagePath(imageType, 0));
            }
        }

        [Theory]
        [InlineData(ImageType.Primary, 1, false)]
        [InlineData(ImageType.Backdrop, 1, false)]
        [InlineData(ImageType.Backdrop, 2, false)]
        [InlineData(ImageType.Primary, 1, true)]
        [InlineData(ImageType.Backdrop, 1, true)]
        [InlineData(ImageType.Backdrop, 2, true)]
        public async Task RefreshImages_PopulatedItemPopulatedProviderRemote_UpdatesImagesIfForced(ImageType imageType, int imageCount, bool forceRefresh)
        {
            var item = GetItemWithImages(imageType, imageCount, false);

            var libraryOptions = GetLibraryOptions(item, imageType, imageCount);

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(rp => rp.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });

            var refreshOptions = forceRefresh
                ? new ImageRefreshOptions(Mock.Of<IDirectoryService>())
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllImages = true
                }
                : new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            var remoteInfo = new RemoteImageInfo[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                remoteInfo[i] = new RemoteImageInfo
                {
                    Type = imageType,
                    Url = "image url " + i
                };
            }

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(pm => pm.GetAvailableRemoteImages(It.IsAny<BaseItem>(), It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(remoteInfo);
            var itemImageProvider = GetItemImageProvider(providerManager.Object, new Mock<IFileSystem>());
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { remoteProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.Equal(forceRefresh, result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(imageCount, item.GetImages(imageType).Count());
            foreach (var image in item.GetImages(imageType))
            {
                if (forceRefresh)
                {
                    Assert.Matches("image url [0-9]", image.Path);
                }
                else
                {
                    Assert.DoesNotMatch("image url [0-9]", image.Path);
                }
            }
        }

        [Theory]
        [InlineData(ImageType.Primary, 0, false)] // singular type only fetches if type is missing from item, no caching
        [InlineData(ImageType.Backdrop, 0, false)] // empty item, no cache to check
        [InlineData(ImageType.Backdrop, 1, false)] // populated item, cached so no download
        [InlineData(ImageType.Backdrop, 1, true)] // populated item, forced to download
        public async Task RefreshImages_NonStubItemPopulatedProviderRemote_DownloadsIfNecessary(ImageType imageType, int initialImageCount, bool fullRefresh)
        {
            var targetImageCount = 1;

            // Set path and media source manager so images will be downloaded (EnableImageStub will return false)
            var item = GetItemWithImages(imageType, initialImageCount, false);
            item.Path = "non-empty path";
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();

            // seek 2 so it won't short-circuit out of downloading when populated
            var libraryOptions = GetLibraryOptions(item, imageType, 2);

            const string Content = "Content";
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(rp => rp.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });
            remoteProvider.Setup(rp => rp.GetImageResponse(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string url, CancellationToken _) => new HttpResponseMessage
                {
                    ReasonPhrase = url,
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(Content, Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var refreshOptions = fullRefresh
                ? new ImageRefreshOptions(Mock.Of<IDirectoryService>())
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllImages = true
                }
                : new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            var remoteInfo = new RemoteImageInfo[targetImageCount];
            for (int i = 0; i < targetImageCount; i++)
            {
                remoteInfo[i] = new RemoteImageInfo
                {
                    Type = imageType,
                    Url = "image url " + i
                };
            }

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(pm => pm.GetAvailableRemoteImages(It.IsAny<BaseItem>(), It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(remoteInfo);
            providerManager.Setup(pm => pm.SaveImage(item, It.IsAny<Stream>(), It.IsAny<string>(), imageType, null, It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((callbackItem, _, _, callbackType, _, _) =>
                    callbackItem.SetImagePath(callbackType, callbackItem.AllowsMultipleImages(callbackType) ? callbackItem.GetImages(callbackType).Count() : 0, new FileSystemMetadata()))
                .Returns(Task.CompletedTask);
            var fileSystem = new Mock<IFileSystem>();
            // match reported file size to image content length - condition for skipping already downloaded multi-images
            fileSystem.Setup(fs => fs.GetFileInfo(It.IsAny<string>()))
                .Returns(new FileSystemMetadata { Length = Content.Length });
            var itemImageProvider = GetItemImageProvider(providerManager.Object, fileSystem);
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { remoteProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.Equal(initialImageCount == 0 || fullRefresh, result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(targetImageCount, item.GetImages(imageType).Count());
        }

        [Theory]
        [MemberData(nameof(GetImageTypesWithCount))]
        public async Task RefreshImages_EmptyItemPopulatedProviderRemoteExtras_LimitsImages(ImageType imageType, int imageCount)
        {
            var item = new Video();

            var libraryOptions = GetLibraryOptions(item, imageType, imageCount);

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(rp => rp.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            // populate remote with double the required images to verify count is trimmed to the library option count
            var remoteInfoCount = imageCount * 2;
            var remoteInfo = new RemoteImageInfo[remoteInfoCount];
            for (int i = 0; i < remoteInfoCount; i++)
            {
                remoteInfo[i] = new RemoteImageInfo
                {
                    Type = imageType,
                    Url = "image url " + i
                };
            }

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(pm => pm.GetAvailableRemoteImages(It.IsAny<BaseItem>(), It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(remoteInfo);
            var itemImageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { remoteProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            var actualImages = item.GetImages(imageType).ToList();
            Assert.Equal(imageCount, actualImages.Count);
            // images from the provider manager are sorted by preference (earlier images are higher priority) so we can verify that low url numbers are chosen
            foreach (var image in actualImages)
            {
                var index = int.Parse(NumbersRegex().Match(image.Path).ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture);
                Assert.True(index < imageCount);
            }
        }

        [Theory]
        [MemberData(nameof(GetImageTypesWithCount))]
        public async Task RefreshImages_PopulatedItemEmptyProviderRemoteFullRefresh_DoesntClearImages(ImageType imageType, int imageCount)
        {
            var item = GetItemWithImages(imageType, imageCount, false);

            var libraryOptions = GetLibraryOptions(item, imageType, imageCount);

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(rp => rp.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>())
            {
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllImages = true
            };

            var itemImageProvider = GetItemImageProvider(Mock.Of<IProviderManager>(), null);
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { remoteProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.False(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(imageCount, item.GetImages(imageType).Count());
        }

        [Theory]
        [InlineData(9, false)]
        [InlineData(10, true)]
        [InlineData(null, true)]
        public async Task RefreshImages_ProviderRemote_FiltersByWidth(int? remoteImageWidth, bool expectedToUpdate)
        {
            var imageType = ImageType.Primary;

            var item = new Video();

            var libraryOptions = new LibraryOptions
            {
                TypeOptions = new[]
                {
                    new TypeOptions
                    {
                        Type = item.GetType().Name,
                        ImageOptions = new[]
                        {
                            new ImageOption
                            {
                                Type = imageType,
                                MinWidth = 10
                            }
                        }
                    }
                }
            };

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(rp => rp.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(rp => rp.GetSupportedImages(item))
                .Returns(new[] { imageType });

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>());

            // set width on image from remote
            var remoteInfo = new[]
            {
                new RemoteImageInfo()
                {
                    Type = imageType,
                    Url = "image url",
                    Width = remoteImageWidth
                }
            };

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(pm => pm.GetAvailableRemoteImages(It.IsAny<BaseItem>(), It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(remoteInfo);
            var itemImageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await itemImageProvider.RefreshImages(item, libraryOptions, new List<IImageProvider> { remoteProvider.Object }, refreshOptions, CancellationToken.None);

            Assert.Equal(expectedToUpdate, result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
        }


    }
}
