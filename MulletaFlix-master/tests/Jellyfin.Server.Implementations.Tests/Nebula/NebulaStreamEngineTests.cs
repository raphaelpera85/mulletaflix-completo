using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaStreamEngineTests
{
    [Fact]
    public async Task ChunkedStream_ReadsFromLocalPath_WhenAvailable()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var expectedBytes = new byte[256];
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                expectedBytes[i] = (byte)i;
            }

            await File.WriteAllBytesAsync(tempFile, expectedBytes);

            var part = new NebulaStreamPart
            {
                PartIndex = 0,
                FileOffset = 0,
                Size = expectedBytes.Length,
                LocalPath = tempFile
            };

            await using var stream = new NebulaChunkedStream(null!, new List<NebulaStreamPart> { part }, expectedBytes.Length, NullLogger.Instance);

            Assert.Equal(expectedBytes.Length, stream.Length);
            Assert.Equal(0, stream.Position);
            Assert.True(stream.CanRead);
            Assert.True(stream.CanSeek);
            Assert.False(stream.CanWrite);

            // Test seeking
            stream.Seek(10, SeekOrigin.Begin);
            Assert.Equal(10, stream.Position);

            var buffer = new byte[20];
            var read = await stream.ReadAsync(buffer.AsMemory(0, 20));
            Assert.Equal(20, read);
            Assert.Equal(30, stream.Position);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(10 + i, buffer[i]);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("movie.mkv", "video/x-matroska")]
    [InlineData("clip.webm", "video/webm")]
    [InlineData("song.mp3", "audio/mpeg")]
    [InlineData("subtitles.srt", "text/plain; charset=utf-8")]
    [InlineData("subtitles.vtt", "text/vtt; charset=utf-8")]
    [InlineData("unknown.xyz", "application/octet-stream")]
    public void HttpStreamServer_GuessesContentTypeCorrectly(string fileName, string expectedContentType)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "GuessContentType",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = (string?)method.Invoke(null, [fileName]);
        Assert.Equal(expectedContentType, result);
    }

    [Theory]
    [InlineData(null, 100, 0, 99, false, true)]
    [InlineData("bytes=0-9", 100, 0, 9, true, true)]
    [InlineData("bytes=90-", 100, 90, 99, true, true)]
    [InlineData("bytes=-10", 100, 90, 99, true, true)]
    [InlineData("bytes=-200", 100, 0, 99, true, true)]
    [InlineData("bytes=100-", 100, 0, 0, false, false)]
    [InlineData("bytes=20-10", 100, 0, 0, false, false)]
    [InlineData("bytes=0-1,4-5", 100, 0, 0, false, false)]
    [InlineData("bytes=-0", 100, 0, 0, false, false)]
    public void HttpStreamServer_ParsesSingleByteRangesSafely(
        string? header,
        long totalSize,
        long expectedStart,
        long expectedEnd,
        bool expectedRange,
        bool expectedValid)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "TryParseRange",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var arguments = new object?[] { header, totalSize, 0L, 0L, false };
        var result = Assert.IsType<bool>(method.Invoke(null, arguments));

        Assert.Equal(expectedValid, result);
        if (result)
        {
            Assert.Equal(expectedStart, arguments[2]);
            Assert.Equal(expectedEnd, arguments[3]);
            Assert.Equal(expectedRange, arguments[4]);
        }
    }

    [Theory]
    [InlineData("movie\"name.mkv", "inline; filename=\"moviename.mkv\"; filename*=UTF-8''moviename.mkv")]
    [InlineData("episode\r\nX-Injected: true.mkv", "inline; filename=\"episodeX-Injected: true.mkv\"; filename*=UTF-8''episodeX-Injected%3A%20true.mkv")]
    [InlineData("", "inline; filename=\"media.bin\"; filename*=UTF-8''media.bin")]
    public void HttpStreamServer_SanitizesContentDispositionFileNames(string fileName, string expectedHeader)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "BuildContentDisposition",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = Assert.IsType<string>(method.Invoke(null, [fileName]));

        Assert.Equal(expectedHeader, result);
    }
}
