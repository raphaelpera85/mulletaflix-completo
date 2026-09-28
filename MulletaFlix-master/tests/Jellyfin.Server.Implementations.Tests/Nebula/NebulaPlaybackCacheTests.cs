using System;
using System.IO;
using System.Linq;
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
    public async Task CacheEvictsLeastRecentlyUsedInactiveMediaToRespectLimit()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: 3,
                minimumFreeSpaceBytes: 0);
            await cache.GetOrFetchChunkAsync("old-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 1, 1, 1 }), CancellationToken.None);
            await cache.GetOrFetchChunkAsync("new-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 2, 2, 2 }), CancellationToken.None);

            Assert.Equal(3, cache.GetCacheSizeBytes());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
            Assert.Equal(new byte[] { 2, 2, 2 }, await cache.GetOrFetchChunkAsync(
                "new-media", 0, 0, 3, _ => Task.FromResult(Array.Empty<byte>()), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FullCacheDoesNotEvictActiveMediaOrFailPlayback()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: 3,
                minimumFreeSpaceBytes: 0);
            using var lease = cache.Acquire("active-media");
            await cache.GetOrFetchChunkAsync("active-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 1, 1, 1 }), CancellationToken.None);

            var result = await cache.GetOrFetchChunkAsync(
                "second-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 2, 2, 2 }), CancellationToken.None);

            Assert.Equal(new byte[] { 2, 2, 2 }, result);
            Assert.Equal(3, cache.GetCacheSizeBytes());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReservedFreeSpacePreventsCachingButStillReturnsFetchedBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: long.MaxValue,
                minimumFreeSpaceBytes: long.MaxValue);

            var result = await cache.GetOrFetchChunkAsync(
                "reserve-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 3, 3, 3 }), CancellationToken.None);

            Assert.Equal(new byte[] { 3, 3, 3 }, result);
            Assert.Equal(0, cache.GetCacheSizeBytes());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelingOneWaiterDoesNotCancelOrDuplicateSharedFetch()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var cancellation = new CancellationTokenSource();
            var fetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completeFetch = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;

            var first = cache.GetOrFetchChunkAsync(
                "concurrent-media",
                0,
                0,
                3,
                async token =>
                {
                    Interlocked.Increment(ref calls);
                    fetchStarted.TrySetResult(true);
                    return await completeFetch.Task.WaitAsync(token);
                },
                cancellation.Token);
            await fetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var second = cache.GetOrFetchChunkAsync(
                "concurrent-media",
                0,
                0,
                3,
                _ => Task.FromException<byte[]>(new InvalidOperationException("Fetch duplicado.")),
                CancellationToken.None);

            cancellation.Cancel();
            var firstError = await Record.ExceptionAsync(() => first);
            var third = cache.GetOrFetchChunkAsync(
                "concurrent-media",
                0,
                0,
                3,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new byte[] { 9, 9, 9 });
                },
                CancellationToken.None);

            completeFetch.TrySetResult(new byte[] { 1, 2, 3 });
            var secondError = await Record.ExceptionAsync(() => second);
            var thirdError = await Record.ExceptionAsync(() => third);

            Assert.IsAssignableFrom<OperationCanceledException>(firstError);
            Assert.Null(secondError);
            Assert.Null(thirdError);
            Assert.Equal(new byte[] { 1, 2, 3 }, await second);
            Assert.Equal(new byte[] { 1, 2, 3 }, await third);
            Assert.Equal(1, calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelingLastWaiterCancelsSharedFetch()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var cancellation = new CancellationTokenSource();
            var fetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fetchCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = cache.GetOrFetchChunkAsync(
                "cancel-all-media",
                0,
                0,
                3,
                async token =>
                {
                    fetchStarted.TrySetResult(true);
                    using var registration = token.Register(() => fetchCanceled.TrySetResult(true));
                    await Task.Delay(Timeout.Infinite, token);
                    return Array.Empty<byte>();
                },
                cancellation.Token);

            await fetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await fetchCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisposedCacheRejectsNewChunkFetches()
    {
        var root = CreateTempDirectory();
        try
        {
            var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            cache.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetOrFetchChunkAsync(
                "disposed-media",
                0,
                0,
                3,
                _ => Task.FromResult(new byte[] { 1, 2, 3 }),
                CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisposingCacheCancelsAnInflightFetch()
    {
        var root = CreateTempDirectory();
        try
        {
            var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var fetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fetchCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = cache.GetOrFetchChunkAsync(
                "dispose-inflight-media",
                0,
                0,
                3,
                async token =>
                {
                    fetchStarted.TrySetResult(true);
                    using var registration = token.Register(() => fetchCanceled.TrySetResult(true));
                    await Task.Delay(Timeout.Infinite, token);
                    return Array.Empty<byte>();
                },
                CancellationToken.None);

            await fetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.Dispose();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await fetchCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    public async Task CleanupDoesNotRemoveInactivePlaybackBeforeOneHour()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-3", 0, 0, 1, _ => Task.FromResult(new byte[] { 8 }), CancellationToken.None);

            // Simula passagem de 59 minutos (abaixo de 1 hora).
            cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(59));

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
            await cache.GetOrFetchChunkAsync("media-4", 0, 0, 1, _ => Task.FromResult(new byte[] { 8 }), CancellationToken.None);

            // Simula passagem de 61 minutos (acima de 1 hora).
            cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(61));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupDeletesExpiredEntriesAcrossMultipleDirectoriesEvenIfOneIsLocked()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-locked", 0, 0, 1, _ => Task.FromResult(new byte[] { 1 }), CancellationToken.None);
            await cache.GetOrFetchChunkAsync("media-free", 0, 0, 1, _ => Task.FromResult(new byte[] { 2 }), CancellationToken.None);

            var files = Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories);
            var lockedFile = files.First(f => f.Contains("000000-00000000.bin", StringComparison.Ordinal));

            // Abre o arquivo com FileShare.None para bloquear exclusão deste arquivo específico
            using (var fileLock = File.Open(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Limpeza executada após 61 minutos.
                cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(61));
            }

            // O diretório livre DEVE ter sido removido com sucesso mesmo com o outro bloqueado
            var remainingFiles = Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories);
            Assert.Contains(remainingFiles, f => f == lockedFile);
            Assert.DoesNotContain(remainingFiles, f => f != lockedFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CacheInspectionAndClearCacheWorkAsExpected()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            Assert.Equal(Path.Combine(root, "nebula-playback"), cache.CachePath);
            Assert.Equal(0, cache.ActiveLeasesCount);
            Assert.Equal(0, cache.GetCacheSizeBytes());
            Assert.Equal(0, cache.GetCachedFilesCount());

            using (var lease = cache.Acquire("active-media"))
            {
                Assert.Equal(1, cache.ActiveLeasesCount);
                await cache.GetOrFetchChunkAsync("active-media", 0, 0, 10, _ => Task.FromResult(new byte[10]), CancellationToken.None);
                await cache.GetOrFetchChunkAsync("idle-media", 0, 0, 20, _ => Task.FromResult(new byte[20]), CancellationToken.None);

                Assert.Equal(30, cache.GetCacheSizeBytes());
                Assert.Equal(2, cache.GetCachedFilesCount());

                cache.ClearCache();

                // active-media must NOT be deleted because it is leased; idle-media should be deleted
                Assert.Equal(10, cache.GetCacheSizeBytes());
                Assert.Equal(1, cache.GetCachedFilesCount());
            }

            Assert.Equal(0, cache.ActiveLeasesCount);
            cache.ClearCache();
            Assert.Equal(0, cache.GetCacheSizeBytes());
            Assert.Equal(0, cache.GetCachedFilesCount());
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
