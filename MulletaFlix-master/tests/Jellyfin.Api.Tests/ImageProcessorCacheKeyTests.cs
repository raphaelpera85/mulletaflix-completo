using System;
using System.IO;
using System.Reflection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Drawing;
using Xunit;

namespace MulletaFlix.Api.Tests;

public sealed class ImageProcessorCacheKeyTests
{
    [Fact]
    public void GetCacheFilePath_ProgressAndUnwatchedCountDoNotCollide()
    {
        var applicationPaths = new Mock<IServerApplicationPaths>();
        applicationPaths.SetupGet(paths => paths.ImageCachePath).Returns(Path.GetTempPath());

        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
        {
            ParallelImageEncodingLimit = 1
        });

        using var processor = new ImageProcessor(
            Mock.Of<ILogger<ImageProcessor>>(),
            applicationPaths.Object,
            Mock.Of<IFileSystem>(),
            Mock.Of<IImageEncoder>(),
            configuration.Object);

        var progressPath = InvokeGetCacheFilePath(processor, percentPlayed: 50, unwatchedCount: null);
        var unwatchedPath = InvokeGetCacheFilePath(processor, percentPlayed: 0, unwatchedCount: 50);

        Assert.NotEqual(progressPath, unwatchedPath);
    }

    private static string InvokeGetCacheFilePath(ImageProcessor processor, double percentPlayed, int? unwatchedCount)
    {
        var method = typeof(ImageProcessor).GetMethod("GetCacheFilePath", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GetCacheFilePath was not found.");

        return (string)method.Invoke(processor,
        [
            "cover.jpg",
            null,
            null,
            null,
            null,
            null,
            null,
            90,
            DateTime.UnixEpoch,
            ImageFormat.Jpg,
            percentPlayed,
            unwatchedCount,
            null,
            null,
            null
        ])!;
    }
}
