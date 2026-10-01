using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaDirectoryRefreshQueueTests
{
    [Fact]
    public async Task Enqueue_CoalescesRapidRefreshesForSameDirectory()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new ConcurrentQueue<string>();
        await using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            (directory, _) =>
            {
                refreshed.Enqueue(directory);
                completed.TrySetResult();
                return Task.CompletedTask;
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(100));

        queue.Enqueue("/Series/One");
        await Task.Delay(25);
        queue.Enqueue("/Series/One");
        queue.Enqueue("/Series/One");

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(150);

        Assert.Equal(new[] { "/Series/One" }, refreshed.ToArray());
    }

    [Fact]
    public async Task Enqueue_RefreshesManyDirectoriesWithOnlyOneRefreshActive()
    {
        const int directoryCount = 32;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var activeRefreshes = 0;
        var maximumActiveRefreshes = 0;
        await using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            async (directory, cancellationToken) =>
            {
                var active = Interlocked.Increment(ref activeRefreshes);
                UpdateMaximum(ref maximumActiveRefreshes, active);
                try
                {
                    await Task.Delay(5, cancellationToken);
                    refreshed.TryAdd(directory, 0);
                    if (refreshed.Count == directoryCount)
                    {
                        completed.TrySetResult();
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeRefreshes);
                }
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(10));

        for (var index = 0; index < directoryCount; index++)
        {
            queue.Enqueue($"/Series/{index}");
        }

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(directoryCount, refreshed.Count);
        Assert.Equal(1, maximumActiveRefreshes);
    }

    [Fact]
    public async Task Enqueue_DuringRefreshSchedulesOnlyOneFollowUp()
    {
        var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoRefreshesCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;
        await using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            async (_, cancellationToken) =>
            {
                var count = Interlocked.Increment(ref refreshCount);
                if (count == 1)
                {
                    firstRefreshStarted.TrySetResult();
                    await releaseFirstRefresh.Task.WaitAsync(cancellationToken);
                }

                if (count == 2)
                {
                    twoRefreshesCompleted.TrySetResult();
                }
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(25));

        queue.Enqueue("/Series/One");
        await firstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Enqueue("/Series/One");
        queue.Enqueue("/Series/One");
        releaseFirstRefresh.TrySetResult();

        await twoRefreshesCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public async Task RefreshFailure_DoesNotStopFollowingDirectories()
    {
        var firstFailureObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRefreshCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new ConcurrentQueue<string>();
        await using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            (directory, _) =>
            {
                refreshed.Enqueue(directory);
                if (directory == "/Series/One")
                {
                    firstFailureObserved.TrySetResult();
                    throw new InvalidOperationException("simulated rclone failure");
                }

                secondRefreshCompleted.TrySetResult();
                return Task.CompletedTask;
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(10));

        queue.Enqueue("/Series/One");
        await firstFailureObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Enqueue("/Series/Two");

        await secondRefreshCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, refreshed.Count);
    }

    [Fact]
    public async Task DisposeAsync_CancelsActiveRefreshAndWaitsForWorker()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            async (_, cancellationToken) =>
            {
                refreshStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(10));

        queue.Enqueue("/Series/One");
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await queue.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        queue.Enqueue("/Series/Two");
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (value <= current || Interlocked.CompareExchange(ref maximum, value, current) == current)
            {
                return;
            }
        }
    }
}
