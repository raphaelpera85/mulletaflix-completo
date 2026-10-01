using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Branding;
using MediaBrowser.Model.IO;
using MulletaFlix.Drawing.Skia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ImageControllerSplashscreenTests
{
    [Fact]
    public void DeleteCustomSplashscreen_RemovesManagedFileAfterConfigurationCommit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var managedFile = Path.Combine(directory, "splashscreen-upload-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(managedFile, [1, 2, 3]);
            BrandingOptions current = new() { SplashscreenLocation = managedFile, IntroEnabled = true };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()))
                .Returns((string _, Func<object, object> update) =>
                {
                    current = (BrandingOptions)update(current);
                    return current;
                });
            var controller = CreateController(directory, configuration);

            var result = controller.DeleteCustomSplashscreen();

            Assert.IsType<NoContentResult>(result);
            Assert.Null(current.SplashscreenLocation);
            Assert.True(current.IntroEnabled);
            Assert.False(File.Exists(managedFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteCustomSplashscreen_DoesNotDeleteUserManagedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var userFile = Path.Combine(directory, "user-selected.png");
            File.WriteAllBytes(userFile, [1, 2, 3]);
            var dataPath = Path.Combine(directory, "server-data");
            Directory.CreateDirectory(dataPath);
            var branding = new BrandingOptions { SplashscreenLocation = userFile };
            BrandingOptions current = branding;
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.GetConfiguration("branding")).Returns(() => current);
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()))
                .Returns((string _, Func<object, object> update) =>
                {
                    current = (BrandingOptions)update(current);
                    return current;
                });
            var controller = CreateController(dataPath, configuration);

            var result = controller.DeleteCustomSplashscreen();

            Assert.IsType<NoContentResult>(result);
            Assert.True(File.Exists(userFile));
            Assert.Null(current.SplashscreenLocation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteCustomSplashscreen_ConfigurationFailurePreservesManagedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var managedFile = Path.Combine(directory, "splashscreen-upload-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(managedFile, [1, 2, 3]);
            var branding = new BrandingOptions { SplashscreenLocation = managedFile };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.GetConfiguration("branding")).Returns(branding);
            configuration.Setup(manager => manager.SaveConfiguration("branding", It.IsAny<object>())).Throws(new IOException("save failed"));
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>())).Throws(new IOException("save failed"));
            var controller = CreateController(directory, configuration);

            Assert.Throws<IOException>(() => controller.DeleteCustomSplashscreen());

            Assert.True(File.Exists(managedFile));
            Assert.Equal(managedFile, branding.SplashscreenLocation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OversizedDeclaredBody_IsRejectedBeforeWriting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new Mock<IServerConfigurationManager>();
            var controller = CreateController(directory, configuration);
            controller.Request.ContentLength = (28L * 1024 * 1024) + 1;
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes("AQID"));

            var result = await controller.UploadCustomSplashscreen();

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);
            Assert.Empty(Directory.GetFiles(directory));
            configuration.Verify(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()), Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidUpload_PublishesOnlyCompleteFileAndPreservesOtherBrandingFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var previousPath = Path.Combine(directory, "splashscreen-upload-" + Guid.NewGuid().ToString("N") + ".png");
            await File.WriteAllBytesAsync(previousPath, [4, 5, 6], TestContext.Current.CancellationToken);
            BrandingOptions current = new() { SplashscreenLocation = previousPath, CustomCss = ".custom{color:red}", IntroEnabled = true };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()))
                .Returns((string _, Func<object, object> update) =>
                {
                    current = (BrandingOptions)update(current);
                    return current;
                });
            var imageProcessor = new Mock<IImageProcessor>();
            imageProcessor.Setup(processor => processor.IsImageDecodable(It.IsAny<string>(), It.IsAny<long>())).Returns(true);
            var controller = CreateController(directory, configuration, imageProcessor.Object);
            using var bitmap = new SKBitmap(1, 1);
            bitmap.Erase(SKColors.Red);
            using var encodedImage = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            var image = encodedImage.ToArray();
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(image)));

            var result = await controller.UploadCustomSplashscreen();

            Assert.IsType<NoContentResult>(result);
            Assert.NotEqual(previousPath, current.SplashscreenLocation);
            Assert.Equal(image, await File.ReadAllBytesAsync(current.SplashscreenLocation!, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(previousPath));
            Assert.Equal(".custom{color:red}", current.CustomCss);
            Assert.True(current.IntroEnabled);
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigurationFailure_RemovesUnpublishedFileAndPreservesPreviousImage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var previousPath = Path.Combine(directory, "previous.png");
            await File.WriteAllBytesAsync(previousPath, [1, 2, 3], TestContext.Current.CancellationToken);
            var current = new BrandingOptions { SplashscreenLocation = previousPath };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()))
                .Throws(new IOException("simulated configuration failure"));
            var imageProcessor = new Mock<IImageProcessor>();
            imageProcessor.Setup(processor => processor.IsImageDecodable(It.IsAny<string>(), It.IsAny<long>())).Returns(true);
            var controller = CreateController(directory, configuration, imageProcessor.Object);
            var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR4kAAAAASUVORK5CYII=");
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(image)));

            await Assert.ThrowsAsync<IOException>(() => controller.UploadCustomSplashscreen());

            Assert.Equal(previousPath, current.SplashscreenLocation);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(previousPath, TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidUpload_PreservesExistingConfigurationAndFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var previousPath = Path.Combine(directory, "previous.png");
            await File.WriteAllBytesAsync(previousPath, [1, 2, 3], TestContext.Current.CancellationToken);
            var branding = new BrandingOptions { SplashscreenLocation = previousPath };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.GetConfiguration("branding")).Returns(branding);
            var appPaths = new Mock<IApplicationPaths>();
            appPaths.Setup(paths => paths.DataPath).Returns(directory);
            var controller = new ImageController(
                Mock.Of<IUserManager>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<IProviderManager>(),
                Mock.Of<IImageProcessor>(),
                Mock.Of<IFileSystem>(),
                NullLogger<ImageController>.Instance,
                configuration.Object,
                appPaths.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.Request.ContentType = "image/png";
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes("!invalid-base64!"));

            var result = await controller.UploadCustomSplashscreen();

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(previousPath, branding.SplashscreenLocation);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(previousPath, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(directory, "splashscreen-upload.png")));
            configuration.Verify(manager => manager.SaveConfiguration("branding", It.IsAny<object>()), Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("image/png", "AQIDBAUGBwgJCgsM")]
    [InlineData("image/png", "/9j/4AAQSkZJRgABAQAA")]
    [InlineData("image/svg+xml", "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=")]
    public async Task WrongImageContent_IsRejectedWithoutReplacingPreviousImage(string contentType, string encodedImage)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var previousPath = Path.Combine(directory, "previous.png");
            await File.WriteAllBytesAsync(previousPath, [1, 2, 3], TestContext.Current.CancellationToken);
            BrandingOptions current = new() { SplashscreenLocation = previousPath };
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.Setup(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()))
                .Returns((string _, Func<object, object> update) =>
                {
                    current = (BrandingOptions)update(current);
                    return current;
                });
            var controller = CreateController(directory, configuration);
            controller.Request.ContentType = contentType;
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(encodedImage));

            var result = await controller.UploadCustomSplashscreen();

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(previousPath, current.SplashscreenLocation);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(previousPath, TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFiles(directory));
            configuration.Verify(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()), Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageThatCannotBeDecoded_IsRejectedBeforeConfigurationCommit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new Mock<IServerConfigurationManager>();
            var imageProcessor = new Mock<IImageProcessor>();
            var controller = CreateController(directory, configuration, imageProcessor.Object);
            var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR4kAAAAASUVORK5CYII=");
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(image)));

            var result = await controller.UploadCustomSplashscreen();

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(Directory.GetFiles(directory));
            configuration.Verify(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()), Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task TruncatedPngWithValidHeader_IsRejectedBeforeConfigurationCommit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-splashscreen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new Mock<IServerConfigurationManager>();
            var imageProcessor = new Mock<IImageProcessor>();
            imageProcessor.Setup(processor => processor.IsImageDecodable(It.IsAny<string>(), It.IsAny<long>())).Returns(false);
            var controller = CreateController(directory, configuration, imageProcessor.Object);
            var validImage = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR4kAAAAASUVORK5CYII=");
            var truncatedImage = validImage[..33]; // PNG signature and IHDR are intact; image data is missing.
            controller.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(truncatedImage)));

            var result = await controller.UploadCustomSplashscreen();

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(Directory.GetFiles(directory));
            configuration.Verify(manager => manager.UpdateConfiguration("branding", It.IsAny<Func<object, object>>()), Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SkiaDecoder_RejectsTruncatedPngWithValidHeader()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mulletaflix-image-decode-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var appPaths = new Mock<IApplicationPaths>();
            appPaths.Setup(paths => paths.TempDirectory).Returns(directory);
            var encoder = new SkiaEncoder(NullLogger<SkiaEncoder>.Instance, appPaths.Object);
            using var bitmap = new SKBitmap(1, 1);
            bitmap.Erase(SKColors.Red);
            using var encodedImage = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            var image = encodedImage.ToArray();
            var validPath = Path.Combine(directory, "valid.png");
            var truncatedPath = Path.Combine(directory, "truncated.png");
            File.WriteAllBytes(validPath, image);
            File.WriteAllBytes(truncatedPath, image[..33]);

            using var codec = SKCodec.Create(validPath, out var createResult);
            Assert.NotNull(codec);
            Assert.Equal(SKCodecResult.Success, createResult);
            Assert.True(encoder.IsImageDecodable(validPath, 16_777_216));
            Assert.False(encoder.IsImageDecodable(truncatedPath, 16_777_216));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ImageController CreateController(string directory, Mock<IServerConfigurationManager> configuration, IImageProcessor? imageProcessor = null)
    {
        var appPaths = new Mock<IApplicationPaths>();
        appPaths.Setup(paths => paths.DataPath).Returns(directory);
        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IProviderManager>(),
            imageProcessor ?? Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            NullLogger<ImageController>.Instance,
            configuration.Object,
            appPaths.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.ContentType = "image/png";
        return controller;
    }
}
