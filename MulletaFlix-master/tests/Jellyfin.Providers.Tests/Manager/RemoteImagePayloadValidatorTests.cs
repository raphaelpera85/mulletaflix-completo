using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Providers.Manager;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public class RemoteImagePayloadValidatorTests
{
    [Fact]
    public async Task PrepareAsync_ValidImage_ReturnsValidatedSeekablePayloadAndCleansTemporaryFile()
    {
        var image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Test Data", "Images", "blank0.jpg"));
        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(true);
        encoder.SetupGet(value => value.SupportedInputFormats).Returns(new[] { "jpeg", "jpg" });
        encoder.Setup(value => value.IsImageDecodable(It.IsAny<string>(), RemoteImagePayloadValidator.MaximumPixelCount))
            .Returns((string path, long _) =>
            {
                Assert.EndsWith(".jpg", path, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(image, File.ReadAllBytes(path));
                return true;
            });
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            await using (var prepared = await RemoteImagePayloadValidator.PrepareAsync(
                new MemoryStream(image),
                "image/jpeg",
                encoder.Object,
                temporaryDirectory,
                CancellationToken.None))
            {
                Assert.True(prepared.CanSeek);
                using var actual = new MemoryStream();
                await prepared.CopyToAsync(actual, TestContext.Current.CancellationToken);
                Assert.Equal(image, actual.ToArray());
            }

            Assert.Empty(Directory.GetFiles(temporaryDirectory));
            encoder.VerifyAll();
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_InvalidBytesWithImageMime_RejectsAndRemovesStagedPayload()
    {
        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(true);
        encoder.SetupGet(value => value.SupportedInputFormats).Returns(new[] { "jpeg", "jpg" });
        encoder.Setup(value => value.IsImageDecodable(It.IsAny<string>(), RemoteImagePayloadValidator.MaximumPixelCount))
            .Returns(false);
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => RemoteImagePayloadValidator.PrepareAsync(
                new MemoryStream(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C }),
                "image/jpeg",
                encoder.Object,
                temporaryDirectory,
                CancellationToken.None));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
            Assert.Empty(Directory.GetFiles(temporaryDirectory));
            encoder.VerifyAll();
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, "image/jpeg")]
    [InlineData(true, "image/svg+xml")]
    [InlineData(true, "image/tiff")]
    public async Task PrepareAsync_WhenDecoderUnavailableForFormat_StagesPayloadWithoutRequiringDecoder(bool decoderAvailable, string mimeType)
    {
        var image = new byte[] { 1, 2, 3 };
        var source = new MemoryStream(image);
        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(decoderAvailable);
        if (decoderAvailable && !string.Equals(mimeType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
        {
            encoder.SetupGet(value => value.SupportedInputFormats).Returns(new[] { "jpeg", "jpg", "svg" });
        }

        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            await using (var prepared = await RemoteImagePayloadValidator.PrepareAsync(
                source,
                mimeType,
                encoder.Object,
                temporaryDirectory,
                TestContext.Current.CancellationToken))
            {
                Assert.NotSame(source, prepared);
                using var actual = new MemoryStream();
                await prepared.CopyToAsync(actual, TestContext.Current.CancellationToken);
                Assert.Equal(image, actual.ToArray());
            }

            Assert.False(source.CanRead);
            Assert.Empty(Directory.GetFiles(temporaryDirectory));
            encoder.VerifyAll();
        }
        finally
        {
            source.Dispose();
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, "image/jpeg")]
    [InlineData(true, "image/tiff")]
    public async Task PrepareAsync_RejectsOversizedPayloadEvenWhenDecoderIsUnavailable(bool decoderAvailable, string mimeType)
    {
        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(decoderAvailable);
        if (decoderAvailable)
        {
            encoder.SetupGet(value => value.SupportedInputFormats).Returns(new[] { "jpeg", "jpg" });
        }

        var temporaryDirectory = CreateTemporaryDirectory();
        var source = new OversizedSeekableStream(RemoteImagePayloadValidator.MaximumPayloadBytes + 1);
        try
        {
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => RemoteImagePayloadValidator.PrepareAsync(
                source,
                mimeType,
                encoder.Object,
                temporaryDirectory,
                TestContext.Current.CancellationToken));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, exception.StatusCode);
            Assert.False(source.CanRead);
            Assert.Empty(Directory.GetFiles(temporaryDirectory));
            encoder.VerifyAll();
        }
        finally
        {
            source.Dispose();
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_WhenTemporaryDirectoryCannotBeCreated_DisposesSource()
    {
        var source = new MemoryStream(new byte[] { 1, 2, 3 });
        var encoder = new Mock<IImageEncoder>(MockBehavior.Strict);
        encoder.SetupGet(value => value.SupportsImageEncoding).Returns(false);
        var parentFile = Path.Combine(Path.GetTempPath(), $"mulletaflix-image-validation-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(parentFile, Array.Empty<byte>(), TestContext.Current.CancellationToken);

        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() => RemoteImagePayloadValidator.PrepareAsync(
                source,
                "image/jpeg",
                encoder.Object,
                Path.Combine(parentFile, "not-a-directory"),
                TestContext.Current.CancellationToken));

            Assert.False(source.CanRead);
            encoder.VerifyAll();
        }
        finally
        {
            source.Dispose();
            File.Delete(parentFile);
        }
    }

    [Fact]
    public async Task ReadBoundedAsync_RejectsContentLengthAboveLimitBeforeReading()
    {
        using var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentLength = RemoteImagePayloadValidator.MaximumPayloadBytes + 1;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            RemoteImagePayloadValidator.ReadBoundedAsync(content, CancellationToken.None));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, exception.StatusCode);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mulletaflix-image-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class OversizedSeekableStream(long length) : Stream
    {
        public bool IsDisposed { get; private set; }

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }

        public override bool CanRead => !IsDisposed;
    }
}
