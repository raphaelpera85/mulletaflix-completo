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
using MediaBrowser.Controller.Configuration;
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
    [Collection("LibraryManagerTests")]
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

            var item = new Video { PreferredMetadataLanguage = "pt-BR" };

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

        [Fact]
        public async Task RefreshImages_RemoteProviders_SelectLocalizedCandidateAcrossProviders()
        {
            var item = new Video { PreferredMetadataLanguage = "pt-BR" };
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var englishProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            englishProvider.Setup(provider => provider.Name).Returns("EnglishProvider");
            englishProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            var portugueseProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            portugueseProvider.Setup(provider => provider.Name).Returns("PortugueseProvider");
            portugueseProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages && string.IsNullOrEmpty(query.ProviderName)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                {
                    ProviderName = "PortugueseProvider",
                    Language = "pt",
                    Type = ImageType.Primary,
                    Url = "localized-image"
                }
                ]);

            var itemImageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await itemImageProvider.RefreshImages(
                item,
                libraryOptions,
                [englishProvider.Object, portugueseProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            providerManager.Verify(
                manager => manager.GetAvailableRemoteImages(
                item,
                It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages && string.IsNullOrEmpty(query.ProviderName)),
                It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.True(item.HasImage(ImageType.Primary));
        }

        [Theory]
        [InlineData("en")]
        [InlineData(null)]
        public async Task RefreshImages_ReplacesKnownRemotePosterWithoutPreferredLanguageWhenPreferredLanguageIsAvailable(string existingLanguage)
        {
            var item = new Video
            {
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://images.example/english.jpg",
                    Type = ImageType.Primary,
                    ProviderName = "MockRemoteProvider",
                    Language = existingLanguage
                },
                0);
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item))
                .Returns([ImageType.Primary]);

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Language = "pt",
                        Type = ImageType.Primary,
                        Url = "https://images.example/portuguese.jpg"
                    }
                ]);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                libraryOptions,
                new[] { remoteProvider.Object },
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal("https://images.example/portuguese.jpg", item.GetImageInfo(ImageType.Primary, 0)!.Path);
            Assert.Equal("pt", item.GetImageInfo(ImageType.Primary, 0)!.Language);
            Assert.Equal("MockRemoteProvider", item.GetImageInfo(ImageType.Primary, 0)!.ProviderName);
        }

        [Fact]
        public async Task RefreshImages_DownloadsPreferredLanguageImageAndPreservesProvenance()
        {
            var item = new Video { Path = "non-empty path", PreferredMetadataLanguage = "pt-BR" };
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("PortugueseProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            remoteProvider.Setup(provider => provider.GetImageResponse(
                    "https://images.example/poster-pt.jpg",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/poster-pt.jpg"),
                    Content = new StringContent("image content", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RemoteImageInfo
                    {
                        ProviderName = "PortugueseProvider",
                        Language = "pt",
                        Type = ImageType.Primary,
                        Url = "https://images.example/poster-pt.jpg"
                    }
                });
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((imageItem, _, _, imageType, _, _) =>
                    imageItem.SetImage(new ItemImageInfo { Path = "C:\\metadata\\poster.jpg", Type = imageType }, 0))
                .Returns(Task.CompletedTask);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                libraryOptions,
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            var savedImage = item.GetImageInfo(ImageType.Primary, 0)!;
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal("C:\\metadata\\poster.jpg", savedImage.Path);
            Assert.Equal("PortugueseProvider", savedImage.ProviderName);
            Assert.Equal("pt", savedImage.Language);
            remoteProvider.Verify(
                provider => provider.GetImageResponse(
                    "https://images.example/poster-pt.jpg",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RefreshImages_ReplacesStaleRemoteBackdropAndPreservesManualBackdrop()
        {
            var item = new Video
            {
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR",
                Path = "non-empty path"
            };
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://images.example/english-backdrop.jpg",
                    Type = ImageType.Backdrop,
                    ProviderName = "MockRemoteProvider",
                    Language = "en"
                },
                0);
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "manual-backdrop.jpg",
                    Type = ImageType.Backdrop
                },
                1);
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Backdrop]);
            remoteProvider.Setup(provider => provider.GetImageResponse("portuguese-backdrop", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/portuguese-backdrop.jpg"),
                    Content = new StringContent("image", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.IsAny<RemoteImageQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Language = "pt",
                        Type = ImageType.Backdrop,
                        Url = "portuguese-backdrop"
                    }
                ]);
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Backdrop,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((savedItem, _, _, type, _, _) =>
                    savedItem.SetImage(
                        new ItemImageInfo
                        {
                            Path = "portuguese-backdrop.jpg",
                            Type = type
                        },
                        savedItem.GetImages(type).Count()))
                .Returns(Task.CompletedTask);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                GetLibraryOptions(item, ImageType.Backdrop, 2),
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(2, item.GetImages(ImageType.Backdrop).Count());
            Assert.Contains(item.GetImages(ImageType.Backdrop), image => image.Path == "manual-backdrop.jpg");
            Assert.Contains(item.GetImages(ImageType.Backdrop), image =>
                image.ProviderName == "MockRemoteProvider" && image.Language == "pt");
            Assert.DoesNotContain(item.GetImages(ImageType.Backdrop), image => image.Language == "en");
        }

        [Theory]
        [InlineData("en")]
        [InlineData(null)]
        public async Task RefreshImages_UsesGlobalPreferredLanguageWhenReplacingKnownRemotePosterWithoutLanguage(string existingLanguage)
        {
            var item = new Video();
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://images.example/english.jpg",
                    Type = ImageType.Primary,
                    ProviderName = "MockRemoteProvider",
                    Language = existingLanguage
                },
                0);
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var previousLibraryManager = BaseItem.LibraryManager;
            var previousConfigurationManager = BaseItem.ConfigurationManager;
            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetCollectionFolders(item)).Returns([]);
            libraryManager.Setup(manager => manager.GetLibraryOptions(item)).Returns(libraryOptions);
            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
            {
                PreferredMetadataLanguage = "pt-BR",
                MetadataCountryCode = "BR"
            });
            BaseItem.LibraryManager = libraryManager.Object;
            BaseItem.ConfigurationManager = configurationManager.Object;

            try
            {
                var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
                remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
                remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
                var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
                providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                        item,
                        It.IsAny<RemoteImageQuery>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync([
                        new RemoteImageInfo
                        {
                            ProviderName = "MockRemoteProvider",
                            Language = "pt",
                            Type = ImageType.Primary,
                            Url = "https://images.example/portuguese.jpg"
                        }
                    ]);

                var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                    item,
                    libraryOptions,
                    [remoteProvider.Object],
                    new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                    CancellationToken.None);

                Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
                Assert.Equal("https://images.example/portuguese.jpg", item.GetImageInfo(ImageType.Primary, 0)!.Path);
            }
            finally
            {
                BaseItem.LibraryManager = previousLibraryManager;
                BaseItem.ConfigurationManager = previousConfigurationManager;
            }
        }

        [Fact]
        public async Task RefreshImages_KeepsKnownEnglishPosterWhenPreferredLanguageIsUnavailable()
        {
            var item = new Video
            {
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://images.example/english.jpg",
                    Type = ImageType.Primary,
                    ProviderName = "MockRemoteProvider",
                    Language = "en"
                },
                0);
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.IsAny<RemoteImageQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Language = "en",
                        Type = ImageType.Primary,
                        Url = "https://images.example/english-new.jpg"
                    }
                ]);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                libraryOptions,
                new[] { remoteProvider.Object },
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.False(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal("https://images.example/english.jpg", item.GetImageInfo(ImageType.Primary, 0)!.Path);
        }

        [Fact]
        public async Task RefreshImages_ReplacesKnownNonPreferredPosterWithEnglishFallback()
        {
            var item = new Video
            {
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://images.example/korean.jpg",
                    Type = ImageType.Primary,
                    ProviderName = "MockRemoteProvider",
                    Language = "ko"
                },
                0);
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            remoteProvider.Setup(provider => provider.GetImageResponse("english image", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/english.jpg"),
                    Content = new StringContent("image content", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Language = "en",
                        Type = ImageType.Primary,
                        Url = "english image"
                    }
                ]);
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((savedItem, _, _, type, _, _) =>
                    savedItem.SetImage(new ItemImageInfo { Path = "english.jpg", Type = type }, 0))
                .Returns(Task.CompletedTask);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                libraryOptions,
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.NotEqual("https://images.example/korean.jpg", item.GetImageInfo(ImageType.Primary, 0)!.Path);
            Assert.Equal("en", item.GetImageInfo(ImageType.Primary, 0)!.Language);
            Assert.Equal("MockRemoteProvider", item.GetImageInfo(ImageType.Primary, 0)!.ProviderName);
        }

        [Fact]
        public async Task RefreshImages_PreservesExistingImageWithoutKnownRemoteProvenance()
        {
            var item = new Video();
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "manual-poster.jpg",
                    Type = ImageType.Primary
                },
                0);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                GetLibraryOptions(item, ImageType.Primary, 1),
                new[] { remoteProvider.Object },
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.False(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal("manual-poster.jpg", item.GetImageInfo(ImageType.Primary, 0)!.Path);
            providerManager.VerifyNoOtherCalls();
        }

        [Fact]
        public void SetImage_ClearsRemoteProvenanceWhenReplacingWithManualImage()
        {
            var item = new Video();
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "downloaded-poster.jpg",
                    Type = ImageType.Primary,
                    ProviderName = "TheMovieDb",
                    Language = "en"
                },
                0);

            item.SetImage(
                new ItemImageInfo
                {
                    Path = "manual-poster.jpg",
                    Type = ImageType.Primary
                },
                0);

            Assert.Null(item.GetImageInfo(ImageType.Primary, 0)!.ProviderName);
            Assert.Null(item.GetImageInfo(ImageType.Primary, 0)!.Language);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RefreshImages_RemoteImageDownloadFailure_IsReportedForRetry(bool throwRequestException)
        {
            var item = new Video { Path = "non-empty path" };
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns(new[] { ImageType.Primary });

            if (throwRequestException)
            {
                remoteProvider.Setup(provider => provider.GetImageResponse("image url", It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("Remote image request failed."));
            }
            else
            {
                remoteProvider.Setup(provider => provider.GetImageResponse("image url", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway));
            }

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.IsAny<RemoteImageQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new RemoteImageInfo { Type = ImageType.Primary, Url = "image url" } });

            var imageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await imageProvider.RefreshImages(
                item,
                libraryOptions,
                new List<IImageProvider> { remoteProvider.Object },
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.True(result.Failures > 0);
            Assert.False(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.False(item.HasImage(ImageType.Primary));
        }

        [Theory]
        [InlineData(ImageType.Primary, false)]
        [InlineData(ImageType.Primary, true)]
        [InlineData(ImageType.Backdrop, false)]
        [InlineData(ImageType.Backdrop, true)]
        public async Task RefreshImages_RemoteImageFailure_UsesNextCandidate(ImageType imageType, bool throwRequestException)
        {
            var item = new Video { Path = "non-empty path" };
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, imageType, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns(new[] { imageType });
            if (throwRequestException)
            {
                remoteProvider.Setup(provider => provider.GetImageResponse("unavailable image url", It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("Remote image request failed."));
            }
            else
            {
                remoteProvider.Setup(provider => provider.GetImageResponse("unavailable image url", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway));
            }

            remoteProvider.Setup(provider => provider.GetImageResponse("fallback image url", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/poster.jpg"),
                    Content = new StringContent("image content", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.IsAny<RemoteImageQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RemoteImageInfo { Type = imageType, Url = "unavailable image url" },
                    new RemoteImageInfo { Type = imageType, Url = "fallback image url" }
                });
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    imageType,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var imageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await imageProvider.RefreshImages(
                item,
                libraryOptions,
                new List<IImageProvider> { remoteProvider.Object },
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            remoteProvider.Verify(
                provider => provider.GetImageResponse("fallback image url", It.IsAny<CancellationToken>()),
                Times.Once);
            providerManager.Verify(
                manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    imageType,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(imageType == ImageType.Primary ? 0 : 1, result.Failures);
        }

        [Fact]
        public async Task RefreshImages_FailedPreferredLanguageImage_UsesEnglishFromAnotherProvider()
        {
            var item = new Video { Path = "non-empty path", PreferredMetadataLanguage = "pt-BR" };
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var englishProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            englishProvider.Setup(provider => provider.Name).Returns("EnglishProvider");
            englishProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            englishProvider.Setup(provider => provider.GetImageResponse("english image", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/english.jpg"),
                    Content = new StringContent("image content", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var portugueseProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            portugueseProvider.Setup(provider => provider.Name).Returns("PortugueseProvider");
            portugueseProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([ImageType.Primary]);
            portugueseProvider.Setup(provider => provider.GetImageResponse("portuguese image", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway));

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RemoteImageInfo { ProviderName = "EnglishProvider", Language = "en", Type = ImageType.Primary, Url = "english image" },
                    new RemoteImageInfo { ProviderName = "PortugueseProvider", Language = "pt", Type = ImageType.Primary, Url = "portuguese image" }
                });
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var imageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await imageProvider.RefreshImages(
                item,
                libraryOptions,
                [englishProvider.Object, portugueseProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            portugueseProvider.Verify(
                provider => provider.GetImageResponse("portuguese image", It.IsAny<CancellationToken>()),
                Times.Once);
            englishProvider.Verify(
                provider => provider.GetImageResponse("english image", It.IsAny<CancellationToken>()),
                Times.Once);
            providerManager.Verify(
                manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
        }

        [Theory]
        [InlineData(ImageType.Primary)]
        [InlineData(ImageType.Backdrop)]
        public async Task RefreshImages_RemoteResponseWithNonImageContentType_UsesNextCandidate(ImageType imageType)
        {
            var item = new Video { Path = "non-empty path" };
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, imageType, 1);
            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(provider => provider.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(provider => provider.GetSupportedImages(item)).Returns([imageType]);
            remoteProvider.Setup(provider => provider.GetImageResponse("html image url", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/error"),
                    Content = new StringContent("<html>not an image</html>", Encoding.UTF8, MediaTypeNames.Text.Html)
                });
            remoteProvider.Setup(provider => provider.GetImageResponse("valid image url", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://images.example/poster.jpg"),
                    Content = new StringContent("image content", Encoding.UTF8, MediaTypeNames.Image.Jpeg)
                });

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.IsAny<RemoteImageQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RemoteImageInfo { Type = imageType, Url = "html image url" },
                    new RemoteImageInfo { Type = imageType, Url = "valid image url" }
                });
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    imageType,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var imageProvider = GetItemImageProvider(providerManager.Object, null);
            var result = await imageProvider.RefreshImages(
                item,
                libraryOptions,
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            remoteProvider.Verify(
                provider => provider.GetImageResponse("valid image url", It.IsAny<CancellationToken>()),
                Times.Once);
            providerManager.Verify(
                manager => manager.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    imageType,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(imageType == ImageType.Primary ? 0 : 1, result.Failures);
        }
    }
}
