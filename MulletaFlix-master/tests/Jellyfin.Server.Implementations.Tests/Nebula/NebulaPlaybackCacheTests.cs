using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public sealed class NebulaPlaybackCacheTests
{
    [Fact]
    public async Task StartPrefetch_LimitsConcurrentMediaDownloadsWithoutDroppingQueuedMedia()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var firstTwoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allAdmittedStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allAdmittedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseDownloads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedMedia = new ConcurrentBag<string>();
            var active = 0;
            var maximumActive = 0;

            void TrackMaximum(int value)
            {
                var observed = Volatile.Read(ref maximumActive);
                while (value > observed)
                {
                    var prior = Interlocked.CompareExchange(ref maximumActive, value, observed);
                    if (prior == observed)
                    {
                        return;
                    }

                    observed = prior;
                }
            }

            for (var index = 1; index <= 10; index++)
            {
                var mediaKey = $"media-{index}";
                cache.StartPrefetch(mediaKey, async cancellationToken =>
                {
                    var current = Interlocked.Increment(ref active);
                    TrackMaximum(current);
                    startedMedia.Add(mediaKey);
                    if (startedMedia.Count == 2)
                    {
                        firstTwoStarted.TrySetResult();
                    }

                    if (startedMedia.Count == 6)
                    {
                        allAdmittedStarted.TrySetResult();
                    }

                    try
                    {
                        await releaseDownloads.Task.WaitAsync(cancellationToken);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref active);
                        if (startedMedia.Count == 6 && Volatile.Read(ref active) == 0)
                        {
                            allAdmittedCompleted.TrySetResult();
                        }
                    }
                });
            }

            await firstTwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(100);
            Assert.Equal(2, Volatile.Read(ref maximumActive));
            Assert.Equal(2, startedMedia.Count);
            Assert.Equal(6, cache.PendingPrefetchCount);
            Assert.Equal(2, cache.ActivePrefetchCount);
            Assert.Equal(4, cache.QueuedPrefetchCount);

            releaseDownloads.TrySetResult();
            await allAdmittedStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await allAdmittedCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(6, startedMedia.Count);
            Assert.Equal(0, cache.PendingPrefetchCount);
            Assert.Equal(0, cache.ActivePrefetchCount);
            Assert.Equal(0, cache.QueuedPrefetchCount);
            Assert.Equal(2, Volatile.Read(ref maximumActive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelPrefetch_StopsOnlyTheRequestedMediaTask()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cache.StartPrefetch("media-to-stop", async token =>
            {
                started.TrySetResult();
                using var registration = token.Register(() => canceled.TrySetResult());
                await Task.Delay(Timeout.Infinite, token);
            });
            cache.StartPrefetch("media-to-keep", async token => await Task.Delay(Timeout.Infinite, token));

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(cache.CancelPrefetch("media-to-stop"));
            Assert.False(cache.CancelPrefetch("media-missing"));
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => cache.PendingPrefetchCount == 1);

            Assert.Equal(1, cache.PendingPrefetchCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelPrefetch_WaitsForActivePlaybackLeaseToCloseThenCancelsImmediately()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var lease = cache.Acquire("active-media");
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cache.StartPrefetch("active-media", async token =>
            {
                started.TrySetResult();
                using var registration = token.Register(() => canceled.TrySetResult());
                await Task.Delay(Timeout.Infinite, token);
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(cache.CancelPrefetch("active-media"));
            Assert.False(canceled.Task.IsCompleted);
            Assert.Equal(1, cache.PendingPrefetchCount);

            lease.Dispose();

            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => cache.PendingPrefetchCount == 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelPrefetch_IsRevokedWhenANewPlaybackLeaseStartsBeforeTheLastLeaseCloses()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var firstLease = cache.Acquire("resumed-media");
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cache.StartPrefetch("resumed-media", async token =>
            {
                started.TrySetResult();
                using var registration = token.Register(() => canceled.TrySetResult());
                await Task.Delay(Timeout.Infinite, token);
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(cache.CancelPrefetch("resumed-media"));

            var resumedLease = cache.Acquire("resumed-media");
            firstLease.Dispose();
            Assert.False(canceled.Task.IsCompleted);

            resumedLease.Dispose();
            Assert.False(canceled.Task.IsCompleted);
            Assert.Equal(1, cache.PendingPrefetchCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelPrefetch_RunsReentrantTokenCallbacksOutsideCacheLocks()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cache.StartPrefetch("callback-media", async token =>
            {
                using var registration = token.Register(() =>
                {
                    using var lease = cache.Acquire("callback-media");
                    cache.StartPrefetch("callback-media", _ => Task.CompletedTask);
                    callbackCompleted.TrySetResult();
                });
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(cache.CancelPrefetch("callback-media"));
            await callbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => cache.PendingPrefetchCount == 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelPrefetch_DoesNotWaitForBlockingTokenCallbacksBeforeFinishingPrefetch()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            cache.StartPrefetch("blocking-callback-media", async token =>
            {
                using var registration = token.Register(() =>
                {
                    callbackStarted.TrySetResult();
                    releaseCallback.Task.GetAwaiter().GetResult();
                    callbackCompleted.TrySetResult();
                });
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    await callbackStarted.Task;
                }
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Assert.True(await Task.Run(() => cache.CancelPrefetch("blocking-callback-media"))
                    .WaitAsync(TimeSpan.FromSeconds(5)));
                await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, cache.PendingPrefetchCount);
                Assert.False(callbackCompleted.Task.IsCompleted);
            }
            finally
            {
                releaseCallback.TrySetResult();
            }

            await callbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => cache.PendingPrefetchCount == 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AheadPrefetch_IsBoundedAndPersistsFetchedChunks()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var firstTwoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseDownloads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = 0;
            var active = 0;
            var maximumActive = 0;

            void TrackMaximum(int value)
            {
                var observed = Volatile.Read(ref maximumActive);
                while (value > observed)
                {
                    var prior = Interlocked.CompareExchange(ref maximumActive, value, observed);
                    if (prior == observed)
                    {
                        return;
                    }

                    observed = prior;
                }
            }

            var downloads = Enumerable.Range(1, 3).Select(index => cache.GetOrFetchAheadChunkAsync(
                $"ahead-{(index == 3 ? 1 : index)}",
                0,
                0,
                1,
                async cancellationToken =>
                {
                    var current = Interlocked.Increment(ref active);
                    TrackMaximum(current);
                    if (Interlocked.Increment(ref started) == 2)
                    {
                        firstTwoStarted.TrySetResult();
                    }

                    try
                    {
                        await releaseDownloads.Task.WaitAsync(cancellationToken);
                        return new[] { (byte)index };
                    }
                    finally
                    {
                        Interlocked.Decrement(ref active);
                    }
                },
                CancellationToken.None)).ToArray();

            await firstTwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(100);
            Assert.Equal(2, Volatile.Read(ref maximumActive));
            Assert.Equal(2, Volatile.Read(ref started));

            var foregroundChunk = await cache.GetOrFetchChunkAsync(
                "foreground",
                0,
                0,
                1,
                _ => Task.FromResult(new byte[] { 9 }),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(new byte[] { 9 }, foregroundChunk);
            var sharedReader = cache.GetOrFetchChunkAsync(
                "ahead-1",
                0,
                0,
                1,
                _ => Task.FromResult(new byte[] { 8 }),
                CancellationToken.None);

            releaseDownloads.TrySetResult();
            var results = await Task.WhenAll(downloads).WaitAsync(TimeSpan.FromSeconds(2));
            var sharedResult = await sharedReader.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(2, Volatile.Read(ref maximumActive));
            Assert.Equal(3, results.Length);
            Assert.Equal(new byte[] { 1 }, results[0]);
            Assert.Equal(new byte[] { 2 }, results[1]);
            Assert.Empty(results[2]);
            Assert.Equal(results[0], sharedResult);
            Assert.Equal(3, cache.GetCacheSizeBytes());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChunkedStream_ReadPersistsPlaybackChunkInSharedCache()
    {
        var root = CreateTempDirectory();
        try
        {
            var mediaPath = Path.Combine(root, "media.bin");
            var expected = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(mediaPath, expected);
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var accessor = new NebulaPlaybackCacheAccessor();
            accessor.Set(cache);
            var part = new NebulaStreamPart
            {
                PartIndex = 0,
                FileOffset = 0,
                Size = expected.Length,
                LocalPath = mediaPath
            };
            await using var stream = new NebulaChunkedStream(
                null,
                new List<NebulaStreamPart> { part },
                expected.Length,
                NullLogger<NebulaChunkedStream>.Instance,
                accessor,
                "shared-media");

            var actual = new byte[expected.Length];
            var read = await stream.ReadAsync(actual.AsMemory());

            Assert.Equal(expected.Length, read);
            Assert.Equal(expected, actual);
            Assert.Equal(expected.Length, cache.GetCacheSizeBytes());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChunkedStream_DeliversFirstChunkWhileWholeMediaPrefetchWaitsOnNextChunk()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var accessor = new NebulaPlaybackCacheAccessor();
            accessor.Set(cache);
            var nextChunkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseNextChunk = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstChunk = Enumerable.Repeat((byte)0x5A, 1024 * 1024).ToArray();
            var part = new NebulaStreamPart { PartIndex = 0, FileOffset = 0, Size = firstChunk.Length + 1 };
            await using var stream = new NebulaChunkedStream(
                null,
                new[] { part },
                part.Size,
                NullLogger<NebulaChunkedStream>.Instance,
                accessor,
                "first-byte-before-prefetch",
                async (_, chunkIndex, cancellationToken) =>
                {
                    if (chunkIndex == 0)
                    {
                        return firstChunk;
                    }

                    nextChunkStarted.TrySetResult();
                    await releaseNextChunk.Task.WaitAsync(cancellationToken);
                    return new byte[] { 0x6B };
                });

            var buffer = new byte[firstChunk.Length];
            var readTask = stream.ReadAsync(buffer.AsMemory()).AsTask();
            await nextChunkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(readTask, completed);
            Assert.Equal(firstChunk.Length, await readTask);
            Assert.Equal(firstChunk, buffer);
            Assert.Equal(firstChunk.Length, cache.GetCacheSizeBytes());

            releaseNextChunk.TrySetResult();
            await WaitUntilAsync(() => cache.PendingPrefetchCount == 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PlaybackSessionMonitor_CancelsPrefetchOnlyAfterLastSessionStopsUsingMedia()
    {
        var cancelledPaths = new List<string>();
        var manager = new Mock<INebulaFtpManager>();
        manager
            .Setup(mock => mock.CancelPlaybackPrefetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) =>
            {
                cancelledPaths.Add(path);
                return Task.FromResult(true);
            });
        var monitor = new NebulaPlaybackSessionMonitor(
            Mock.Of<ISessionManager>(),
            manager.Object,
            NullLogger<NebulaPlaybackSessionMonitor>.Instance);

        await monitor.TrackPlaybackStartAsync("session-a", "play-a", "N:/Series/Example.mkv");
        await monitor.TrackPlaybackStartAsync("session-b", "play-b", "N:/Series/Example.mkv");
        await monitor.TrackPlaybackStoppedAsync("session-a", "play-a");

        Assert.Empty(cancelledPaths);

        await monitor.TrackPlaybackStoppedAsync("session-b", "play-b");

        Assert.Equal(new[] { "N:/Series/Example.mkv" }, cancelledPaths);
    }

    [Fact]
    public async Task PlaybackSessionMonitor_CancelsPreviousMediaOnSwitchAndIgnoresStaleStop()
    {
        var cancelledPaths = new List<string>();
        var manager = new Mock<INebulaFtpManager>();
        manager
            .Setup(mock => mock.CancelPlaybackPrefetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) =>
            {
                cancelledPaths.Add(path);
                return Task.FromResult(true);
            });
        var monitor = new NebulaPlaybackSessionMonitor(
            Mock.Of<ISessionManager>(),
            manager.Object,
            NullLogger<NebulaPlaybackSessionMonitor>.Instance);

        await monitor.TrackPlaybackStartAsync("session-a", "play-old", "N:/Series/Old.mkv");
        await monitor.TrackPlaybackStartAsync("session-a", "play-new", "N:/Series/New.mkv");
        await monitor.TrackPlaybackStoppedAsync("session-a", null);
        await monitor.TrackPlaybackStoppedAsync("session-a", "play-old");
        Assert.Equal(new[] { "N:/Series/Old.mkv" }, cancelledPaths);

        await monitor.TrackPlaybackStoppedAsync("session-a", "play-new");

        Assert.Equal(new[] { "N:/Series/Old.mkv", "N:/Series/New.mkv" }, cancelledPaths);
    }

    [Fact]
    public async Task PlaybackSessionMonitor_UsesItemPathWhenPlaySessionIdIsMissing()
    {
        var cancelledPaths = new List<string>();
        var manager = new Mock<INebulaFtpManager>();
        manager
            .Setup(mock => mock.CancelPlaybackPrefetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) =>
            {
                cancelledPaths.Add(path);
                return Task.FromResult(true);
            });
        var monitor = new NebulaPlaybackSessionMonitor(
            Mock.Of<ISessionManager>(),
            manager.Object,
            NullLogger<NebulaPlaybackSessionMonitor>.Instance);

        await monitor.TrackPlaybackStartAsync("session-a", null, "N:/Series/Current.mkv");
        await monitor.TrackPlaybackStoppedAsync("session-a", null, "N:/Series/Previous.mkv");
        Assert.Empty(cancelledPaths);

        await monitor.TrackPlaybackStoppedAsync("session-a", null, "N:/Series/Current.mkv");

        Assert.Equal(new[] { "N:/Series/Current.mkv" }, cancelledPaths);
    }

    [Fact]
    public async Task PlaybackSessionMonitor_RestartsPrefetchIfSameMediaResumesDuringCancellation()
    {
        var cancelStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeCancel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new Mock<INebulaFtpManager>();
        manager
            .Setup(mock => mock.CancelPlaybackPrefetchAsync("N:/Series/Resumed.mkv", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken __) =>
            {
                cancelStarted.TrySetResult();
                await completeCancel.Task;
                return true;
            });
        manager
            .Setup(mock => mock.StartPlaybackPrefetchAsync("N:/Series/Resumed.mkv", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(true));
        var monitor = new NebulaPlaybackSessionMonitor(
            Mock.Of<ISessionManager>(),
            manager.Object,
            NullLogger<NebulaPlaybackSessionMonitor>.Instance);

        await monitor.TrackPlaybackStartAsync("session-a", "play-old", "N:/Series/Resumed.mkv");
        var stopTask = monitor.TrackPlaybackStoppedAsync("session-a", "play-old");
        await cancelStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await monitor.TrackPlaybackStartAsync("session-a", "play-new", "N:/Series/Resumed.mkv");
        completeCancel.TrySetResult();
        await stopTask;

        manager.Verify(
            mock => mock.StartPlaybackPrefetchAsync("N:/Series/Resumed.mkv", It.IsAny<CancellationToken>()),
            Times.Once);
    }

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
            Assert.Equal(1, cache.CacheHits);
            Assert.Equal(1, cache.CacheMisses);
            Assert.Equal(1, cache.TelegramFetchCount);
            Assert.Equal(0, cache.TelegramFetchFailures);
            Assert.True(cache.AverageTelegramFetchLatencyMs >= 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReportsTelegramFetchFailuresAndKeepsCacheMissVisible()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);

            await Assert.ThrowsAsync<IOException>(() => cache.GetOrFetchChunkAsync(
                "failed-media",
                0,
                0,
                4,
                _ => Task.FromException<byte[]>(new IOException("Telegram indisponível")),
                CancellationToken.None));

            Assert.Equal(1, cache.CacheMisses);
            Assert.Equal(1, cache.TelegramFetchCount);
            Assert.Equal(1, cache.TelegramFetchFailures);
            Assert.True(cache.AverageTelegramFetchLatencyMs >= 0);
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
    public async Task PeriodicCleanupEnforcesReducedCacheLimit()
    {
        var root = CreateTempDirectory();
        try
        {
            using (var originalCache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: 6,
                minimumFreeSpaceBytes: 0))
            {
                await originalCache.GetOrFetchChunkAsync("older-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 1, 1, 1 }), CancellationToken.None);
                await originalCache.GetOrFetchChunkAsync("newer-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 2, 2, 2 }), CancellationToken.None);
            }

            using var reducedCache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: 3,
                minimumFreeSpaceBytes: 0);
            reducedCache.CleanupExpiredEntries(DateTime.UtcNow);

            Assert.Equal(3, reducedCache.GetCacheSizeBytes());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatingCacheLimitEvictsImmediately()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                maxCacheBytes: 6,
                minimumFreeSpaceBytes: 0);
            await cache.GetOrFetchChunkAsync("first-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 1, 1, 1 }), CancellationToken.None);
            await cache.GetOrFetchChunkAsync("second-media", 0, 0, 3, _ => Task.FromResult(new byte[] { 2, 2, 2 }), CancellationToken.None);

            cache.UpdateLimits(3, 0);

            Assert.Equal(3, cache.GetCacheSizeBytes());
            Assert.Equal(3, cache.MaxCacheBytes);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CacheAccessorSwitchesNewStreamsWithoutInvalidatingExistingLeases()
    {
        var root = CreateTempDirectory();
        var replacementRoot = CreateTempDirectory();
        try
        {
            using var originalCache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var replacementCache = new NebulaPlaybackCache(replacementRoot, NullLogger<NebulaPlaybackCache>.Instance);
            var accessor = new NebulaPlaybackCacheAccessor();
            accessor.Set(originalCache);

            var (original, existingLease) = accessor.Acquire("active-media");
            accessor.Set(replacementCache);
            var (replacement, replacementLease) = accessor.Acquire("next-media");

            Assert.Same(originalCache, original);
            Assert.Same(replacementCache, replacement);
            Assert.Equal(1, originalCache.ActiveLeasesCount);
            Assert.Equal(1, replacementCache.ActiveLeasesCount);
            existingLease?.Dispose();
            replacementLease?.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(replacementRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ClearCachePreservesMediaWithInflightChunkFetch()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var fetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completeFetch = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = cache.GetOrFetchChunkAsync(
                "clear-inflight-media",
                0,
                0,
                3,
                async _ =>
                {
                    fetchStarted.TrySetResult(true);
                    return await completeFetch.Task;
                },
                CancellationToken.None);

            await fetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.ClearCache();
            Assert.Equal(1, cache.CacheCleanupRuns);
            Assert.Equal(0, cache.CacheCleanupFailures);
            Assert.NotNull(cache.LastCacheCleanupUtc);
            Assert.True(cache.LastCacheCleanupDurationMs >= 0);
            Assert.Single(Directory.GetDirectories(Path.Combine(root, "nebula-playback")));

            completeFetch.TrySetResult(new byte[] { 4, 4, 4 });
            Assert.Equal(new byte[] { 4, 4, 4 }, await request);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CleanupExpiredEntries_ReportsContentionAsSkipped()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var storageGate = (SemaphoreSlim)typeof(NebulaPlaybackCache)
                .GetField("_storageGate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(cache)!;
            Assert.True(storageGate.Wait(0));
            try
            {
                cache.CleanupExpiredEntries();
            }
            finally
            {
                storageGate.Release();
            }

            Assert.Equal(1, cache.CacheCleanupSkipped);
            Assert.Equal(1, cache.CacheCleanupRuns);
            Assert.Equal(0, cache.CacheCleanupFailures);
            Assert.NotNull(cache.LastCacheCleanupUtc);

            cache.CleanupExpiredEntries();
            Assert.Equal(2, cache.CacheCleanupRuns);
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
            await WaitUntilAsync(() => cache.TelegramFetchCancellations == 1);
            Assert.Equal(1, cache.TelegramFetchCancellations);
            Assert.Equal(0, cache.TelegramFetchFailures);
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
    public async Task DisposingCacheDoesNotWaitForBlockingSharedFetchCancellationCallback()
    {
        var root = CreateTempDirectory();
        using var releaseCallback = new ManualResetEventSlim();
        try
        {
            var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            var fetchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = cache.GetOrFetchChunkAsync(
                "blocked-fetch-callback",
                0,
                0,
                3,
                async token =>
                {
                    using var registration = token.Register(() =>
                    {
                        callbackStarted.TrySetResult();
                        releaseCallback.Wait();
                    });
                    fetchStarted.TrySetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return Array.Empty<byte>();
                },
                CancellationToken.None);

            await fetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var dispose = Task.Run(cache.Dispose);
            await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(dispose.IsCompleted);

            releaseCallback.Set();
            await dispose.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        }
        finally
        {
            releaseCallback.Set();
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "A operação assíncrona não terminou dentro do prazo esperado.");
    }
}
