using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.AudioDb;
using Xunit;

namespace MediaBrowser.Providers.Tests.Plugins.AudioDb;

public sealed class AudioDbCacheFileTests
{
    [Fact]
    public async Task WriteResponseAsync_FailedCopy_PreservesExistingCacheAndRemovesTemporaryFile()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "artist.json");
        await File.WriteAllTextAsync(path, "old cache", TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new PartialFailureContent()
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => AudioDbCacheFile.WriteResponseAsync(response, path, TestContext.Current.CancellationToken));

        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal("old cache", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task WriteResponseAsync_SuccessfulCopy_ReplacesCacheAndRemovesTemporaryFile()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "album.json");
        await File.WriteAllTextAsync(path, "old cache", TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("new cache")
        };

        await AudioDbCacheFile.WriteResponseAsync(response, path, TestContext.Current.CancellationToken);

        Assert.Equal("new cache", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task WriteResponseAsync_UnsuccessfulResponse_DoesNotReplaceCache()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "album.json");
        await File.WriteAllTextAsync(path, "old cache", TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("error")
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => AudioDbCacheFile.WriteResponseAsync(response, path, TestContext.Current.CancellationToken));

        Assert.Equal("old cache", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        Directory.Delete(directory, recursive: true);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AudioDbCacheFileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class PartialFailureContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            await stream.WriteAsync("partial"u8.ToArray(), TestContext.Current.CancellationToken);
            throw new IOException("Simulated interrupted response copy.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
