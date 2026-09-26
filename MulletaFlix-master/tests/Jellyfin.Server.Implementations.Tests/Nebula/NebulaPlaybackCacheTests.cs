using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public sealed class NebulaPlaybackCacheTests
{
    [Fact]
    public async Task FetchesEachChunkOnceAndServesItFromDisk()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var lease = cache.Acquire("media-1");
            var calls = 0;

            var first = await cache.GetOrFetchChunkAsync(
                "media-1",
                0,
                0,
                4,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new byte[] { 1, 2, 3, 4 });
                },
                CancellationToken.None);
            var second = await cache.GetOrFetchChunkAsync(
                "media-1",
                0,
                0,
                4,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new byte[] { 9 });
                },
                CancellationToken.None);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, first);
            Assert.Equal(first, second);
            Assert.Equal(1, calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupDoesNotRemoveAnActivePlayback()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var lease = cache.Acquire("media-2");
            await cache.GetOrFetchChunkAsync("media-2", 0, 0, 1, _ => Task.FromResult(new byte[] { 7 }), CancellationToken.None);

            cache.CleanupExpiredEntries(DateTime.UtcNow.AddHours(2));

            Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupRemovesInactivePlaybackAfterOneHour()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-3", 0, 0, 1, _ => Task.FromResult(new byte[] { 8 }), CancellationToken.None);

            cache.CleanupExpiredEntries(DateTime.UtcNow.AddHours(2));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nebula-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
