using System;
using System.IO;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Drawing;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Drawing;

public sealed class ImageProcessorBlurHashTests
{
    [Fact]
    public void GetImageBlurHash_RetriesTransientFileLock()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows sharing violations are platform-specific.");

        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupSequence(value => value.GetImageBlurHash(It.IsAny<int>(), It.IsAny<int>(), "person.jpg"))
            .Throws(new IOException("Image is temporarily locked.", unchecked((int)0x80070020)))
            .Returns("blurhash");
        using var processor = CreateProcessor(encoder.Object);

        var result = processor.GetImageBlurHash("person.jpg", new ImageDimensions(100, 100));

        Assert.Equal("blurhash", result);
        encoder.Verify(value => value.GetImageBlurHash(It.IsAny<int>(), It.IsAny<int>(), "person.jpg"), Times.Exactly(2));
    }

    [Fact]
    public void GetImageBlurHash_StopsAfterBoundedRetries()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows sharing violations are platform-specific.");

        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.Setup(value => value.GetImageBlurHash(It.IsAny<int>(), It.IsAny<int>(), "person.jpg"))
            .Throws(new IOException("Image remains locked.", unchecked((int)0x80070020)));
        using var processor = CreateProcessor(encoder.Object);

        Assert.Throws<IOException>(() => processor.GetImageBlurHash("person.jpg", new ImageDimensions(100, 100)));

        encoder.Verify(value => value.GetImageBlurHash(It.IsAny<int>(), It.IsAny<int>(), "person.jpg"), Times.Exactly(4));
    }

    private static ImageProcessor CreateProcessor(IImageEncoder encoder)
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(value => value.Configuration).Returns(new ServerConfiguration());
        return new ImageProcessor(
            NullLogger<ImageProcessor>.Instance,
            Mock.Of<IServerApplicationPaths>(),
            Mock.Of<IFileSystem>(),
            encoder,
            configuration.Object);
    }
}
