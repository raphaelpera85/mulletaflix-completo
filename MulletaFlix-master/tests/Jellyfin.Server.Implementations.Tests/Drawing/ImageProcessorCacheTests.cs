using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Drawing;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Drawing;

public sealed class ImageProcessorCacheTests
{
    [Fact]
    public async Task ProcessImage_DoesNotExposeCacheUntilEncodingCompletes()
    {
        using var temporary = new TemporaryDirectory();
        var sourcePath = Path.Combine(temporary.Path, "poster.jpg");
        var cachePath = Path.Combine(temporary.Path, "cache");
        await File.WriteAllBytesAsync(sourcePath, "source"u8.ToArray());

        using var encodingStarted = new ManualResetEventSlim();
        using var finishEncoding = new ManualResetEventSlim();
        var encoder = CreateEncoder();
        var encodeCount = 0;
        encoder.Setup(value => value.EncodeImage(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ImageOrientation?>(),
                It.IsAny<int>(),
                It.IsAny<ImageProcessingOptions>(),
                ImageFormat.Jpg))
            .Returns((string _, DateTime _, string outputPath, bool _, ImageOrientation? _, int _, ImageProcessingOptions _, ImageFormat _) =>
            {
                Interlocked.Increment(ref encodeCount);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, "partial"u8.ToArray());
                encodingStarted.Set();
                if (!finishEncoding.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release the image encoder.");
                }

                File.WriteAllBytes(outputPath, "complete image"u8.ToArray());
                return outputPath;
            });

        using var processor = CreateProcessor(cachePath, encoder.Object);
        var options = CreateOptions(sourcePath);
        var firstRequest = Task.Run(() => processor.ProcessImage(options));
        Assert.True(encodingStarted.Wait(TimeSpan.FromSeconds(5)));

        var secondRequest = Task.Run(() => processor.ProcessImage(options));
        try
        {
            await Task.Delay(100);
            Assert.False(secondRequest.IsCompleted);
            var filesWhileEncoding = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
            Assert.NotEmpty(filesWhileEncoding);
            Assert.All(filesWhileEncoding, path => Assert.EndsWith(".tmp", path, StringComparison.Ordinal));
        }
        finally
        {
            finishEncoding.Set();
        }

        var results = await Task.WhenAll(firstRequest, secondRequest).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, encodeCount);
        Assert.Equal(results[0].Path, results[1].Path);
        Assert.Equal("complete image", await File.ReadAllTextAsync(results[0].Path));
        Assert.Single(Directory.GetFiles(cachePath, "*.jpg", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(cachePath, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ProcessImage_WhenEncodingFailsDoesNotLeaveCacheEntry()
    {
        using var temporary = new TemporaryDirectory();
        var sourcePath = Path.Combine(temporary.Path, "poster.jpg");
        var cachePath = Path.Combine(temporary.Path, "cache");
        await File.WriteAllBytesAsync(sourcePath, "source"u8.ToArray());

        var encoder = CreateEncoder();
        encoder.Setup(value => value.EncodeImage(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ImageOrientation?>(),
                It.IsAny<int>(),
                It.IsAny<ImageProcessingOptions>(),
                ImageFormat.Jpg))
            .Returns((string _, DateTime _, string outputPath, bool _, ImageOrientation? _, int _, ImageProcessingOptions _, ImageFormat _) =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, "partial"u8.ToArray());
                throw new IOException("Synthetic image encoding failure.");
            });

        using var processor = CreateProcessor(cachePath, encoder.Object);
        var result = await processor.ProcessImage(CreateOptions(sourcePath));

        Assert.Equal(sourcePath, result.Path);
        Assert.Empty(Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories));
    }

    private static Mock<IImageEncoder> CreateEncoder()
    {
        var encoder = new Mock<IImageEncoder>();
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(true);
        encoder.SetupGet(value => value.SupportedOutputFormats).Returns([ImageFormat.Jpg]);
        return encoder;
    }

    private static ImageProcessor CreateProcessor(string cachePath, IImageEncoder encoder)
    {
        var appPaths = new Mock<IServerApplicationPaths>();
        appPaths.SetupGet(value => value.ImageCachePath).Returns(cachePath);
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(value => value.Configuration).Returns(new ServerConfiguration());
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.GetLastWriteTimeUtc(It.IsAny<string>()))
            .Returns((string path) => File.GetLastWriteTimeUtc(path));

        return new ImageProcessor(
            NullLogger<ImageProcessor>.Instance,
            appPaths.Object,
            fileSystem.Object,
            encoder,
            configuration.Object);
    }

    private static ImageProcessingOptions CreateOptions(string sourcePath)
        => new()
        {
            Item = new Movie { Path = sourcePath },
            Image = new ItemImageInfo
            {
                Path = sourcePath,
                DateModified = File.GetLastWriteTimeUtc(sourcePath),
                Width = 200,
                Height = 100
            },
            Quality = 80,
            MaxWidth = 100,
            SupportedOutputFormats = [ImageFormat.Jpg]
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory().FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
