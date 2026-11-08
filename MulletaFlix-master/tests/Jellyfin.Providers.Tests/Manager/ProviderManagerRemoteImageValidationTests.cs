using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Manager;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public partial class ProviderManagerTests
{
    [Fact]
    public async Task SaveImage_WhenRemoteFormatHasNoDecoder_PreservesExistingImageAndMetadata()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"mulletaflix-provider-image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var existingImagePath = Path.Combine(temporaryDirectory, "existing-poster.jpg");
        var existingImageBytes = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(existingImagePath, existingImageBytes, TestContext.Current.CancellationToken);

        try
        {
            var appPaths = new Mock<IServerApplicationPaths>(MockBehavior.Strict);
            appPaths.SetupGet(paths => paths.TempDirectory).Returns(temporaryDirectory);
            var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
            encoder.SetupGet(value => value.SupportsImageEncoding).Returns(false);
            var item = new Movie
            {
                ImageInfos = [new ItemImageInfo { Type = ImageType.Primary, Path = existingImagePath }]
            };

            using var providerManager = GetProviderManager(appPaths: appPaths.Object, imageEncoder: encoder.Object);
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => providerManager.SaveImage(
                item,
                new MemoryStream(new byte[] { 1, 2, 3 }),
                "image/jpeg",
                ImageType.Primary,
                null,
                TestContext.Current.CancellationToken));

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, exception.StatusCode);
            Assert.Equal(existingImagePath, item.GetImageInfo(ImageType.Primary, 0)?.Path);
            Assert.Equal(existingImageBytes, await File.ReadAllBytesAsync(existingImagePath, TestContext.Current.CancellationToken));
            Assert.Equal(existingImagePath, Assert.Single(Directory.GetFiles(temporaryDirectory)));
            encoder.VerifyAll();
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveImage_InvalidRemotePayload_PreservesExistingImageAndMetadata()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"mulletaflix-provider-image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var existingImagePath = Path.Combine(temporaryDirectory, "existing-poster.jpg");
        var existingImageBytes = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(existingImagePath, existingImageBytes, TestContext.Current.CancellationToken);

        try
        {
            var appPaths = new Mock<IServerApplicationPaths>(MockBehavior.Strict);
            appPaths.SetupGet(paths => paths.TempDirectory).Returns(temporaryDirectory);
            var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
            encoder.SetupGet(value => value.SupportsImageEncoding).Returns(true);
            encoder.SetupGet(value => value.SupportedInputFormats).Returns(new[] { "jpg", "jpeg" });
            encoder.Setup(value => value.IsImageDecodable(It.IsAny<string>(), RemoteImagePayloadValidator.MaximumPixelCount))
                .Returns(false);
            var item = new Movie
            {
                ImageInfos = [new ItemImageInfo { Type = ImageType.Primary, Path = existingImagePath }]
            };

            using var providerManager = GetProviderManager(appPaths: appPaths.Object, imageEncoder: encoder.Object);
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => providerManager.SaveImage(
                item,
                new MemoryStream(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C }),
                "image/jpeg",
                ImageType.Primary,
                null,
                TestContext.Current.CancellationToken));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
            Assert.Equal(existingImagePath, item.GetImageInfo(ImageType.Primary, 0)?.Path);
            Assert.Equal(existingImageBytes, await File.ReadAllBytesAsync(existingImagePath, TestContext.Current.CancellationToken));
            Assert.Equal(existingImagePath, Assert.Single(Directory.GetFiles(temporaryDirectory)));
            encoder.VerifyAll();
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveImage_NullOrWhiteSpaceUrl_ThrowsArgumentException(string? url)
    {
        var item = new Movie();
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => providerManager.SaveImage(
            item,
            url!,
            ImageType.Primary,
            null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveImage_NullItem_ThrowsArgumentNullException()
    {
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAsync<ArgumentNullException>(() => providerManager.SaveImage(
            null!,
            "https://example.com/poster.jpg",
            ImageType.Primary,
            null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRemoteSearchResults_NullSearchInfo_ThrowsArgumentNullException()
    {
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAsync<ArgumentNullException>(() => providerManager.GetRemoteSearchResults<Movie, MovieInfo>(
            null!,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveImage_Stream_NullItem_ThrowsArgumentNullException()
    {
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAsync<ArgumentNullException>(() => providerManager.SaveImage(
            null!,
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "image/jpeg",
            ImageType.Primary,
            null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveImage_Path_NullItem_ThrowsArgumentNullException()
    {
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAsync<ArgumentNullException>(() => providerManager.SaveImage(
            null!,
            "temp.jpg",
            "image/jpeg",
            ImageType.Primary,
            null,
            null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveImage_Path_NullSource_ThrowsArgumentNullException()
    {
        var item = new Movie();
        using var providerManager = GetProviderManager();
        await Assert.ThrowsAsync<ArgumentNullException>(() => providerManager.SaveImage(
            item,
            null!,
            "image/jpeg",
            ImageType.Primary,
            null,
            null,
            TestContext.Current.CancellationToken));
    }
}
