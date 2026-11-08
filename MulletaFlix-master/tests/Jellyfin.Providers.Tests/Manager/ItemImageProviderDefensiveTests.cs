using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager
{
    [Collection("LibraryManagerTests")]
    public class ItemImageProviderDefensiveTests
    {
        private static ItemImageProvider CreateItemImageProvider(
            IProviderManager providerManager,
            IFileSystem fileSystem)
        {
            return new ItemImageProvider(
                new NullLogger<ItemImageProvider>(),
                providerManager,
                fileSystem);
        }

        private static LibraryOptions CreateLibraryOptions(ImageType imageType, string type = "Video")
        {
            return new LibraryOptions
            {
                TypeOptions =
                [
                    new TypeOptions
                    {
                        Type = type,
                        ImageFetcherOrder = ["MockRemoteProvider"],
                        ImageOptions =
                        [
                            new ImageOption
                            {
                                Type = imageType,
                                Limit = 1
                            }
                        ]
                    }
                ]
            };
        }

        [Fact]
        public async Task RefreshImages_NullRequestMessageAndOctetStream_ResolvesFromCandidateUrlWithoutNullReference()
        {
            var item = new Video { Name = "Defensive Test Video", Path = "/media/movies/video.mkv", PreferredMetadataLanguage = "en" };
            var candidateUrl = "https://images.example.com/posters/movie_artwork.png";

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(p => p.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(p => p.GetSupportedImages(item)).Returns([ImageType.Primary]);

            // HttpResponseMessage with RequestMessage = null, ContentType = application/octet-stream
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = null,
                Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

            remoteProvider.Setup(p => p.GetImageResponse(candidateUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(m => m.GetAvailableRemoteImages(item, It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Type = ImageType.Primary,
                        Url = candidateUrl
                    }
                ]);

            string? savedMimeType = null;
            providerManager.Setup(m => m.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((_, _, mime, _, _, _) =>
                {
                    savedMimeType = mime;
                    item.SetImage(new ItemImageInfo { Path = "/data/primary.png", Type = ImageType.Primary }, 0);
                })
                .Returns(Task.CompletedTask);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            var imageProvider = CreateItemImageProvider(providerManager.Object, fileSystem.Object);

            var result = await imageProvider.RefreshImages(
                item,
                CreateLibraryOptions(ImageType.Primary),
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>())
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllImages = true
                },
                CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(0, result.Failures);
            Assert.Equal("image/png", savedMimeType);
        }

        [Fact]
        public async Task RefreshImages_ExistingRemoteUrlImage_DoesNotThrowOrAbortCandidateEvaluation()
        {
            var item = new Video { Name = "Defensive Existing Remote Image Video", Path = "/media/movies/video.mkv" };
            // Item already has an image with a non-local URL path (IsLocalFile evaluates to false)
            item.SetImage(
                new ItemImageInfo
                {
                    Path = "https://cdn.example.com/old_poster.jpg",
                    Type = ImageType.Primary
                },
                0);

            var candidateUrl = "https://images.example.com/posters/new_poster.jpg";

            var remoteProvider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
            remoteProvider.Setup(p => p.Name).Returns("MockRemoteProvider");
            remoteProvider.Setup(p => p.GetSupportedImages(item)).Returns([ImageType.Primary]);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, candidateUrl),
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Image.Jpeg);
            response.Content.Headers.ContentLength = 6;

            remoteProvider.Setup(p => p.GetImageResponse(candidateUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(m => m.GetAvailableRemoteImages(item, It.IsAny<RemoteImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new RemoteImageInfo
                    {
                        ProviderName = "MockRemoteProvider",
                        Type = ImageType.Primary,
                        Url = candidateUrl
                    }
                ]);

            providerManager.Setup(m => m.SaveImage(
                    item,
                    It.IsAny<Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((_, _, _, _, _, _) =>
                {
                    item.SetImage(new ItemImageInfo { Path = "/data/new_primary.jpg", Type = ImageType.Primary }, 0);
                })
                .Returns(Task.CompletedTask);

            // Mock FileSystem: should never be queried with GetFileInfo for a non-local http path
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

            var imageProvider = CreateItemImageProvider(providerManager.Object, fileSystem.Object);

            var result = await imageProvider.RefreshImages(
                item,
                CreateLibraryOptions(ImageType.Primary),
                [remoteProvider.Object],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>())
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllImages = true
                },
                CancellationToken.None);

            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Equal(0, result.Failures);
        }
    }
}