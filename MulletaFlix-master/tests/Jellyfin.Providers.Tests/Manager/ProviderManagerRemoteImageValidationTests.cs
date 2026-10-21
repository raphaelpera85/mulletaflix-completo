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
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Manager;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public partial class ProviderManagerTests
{
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
}
