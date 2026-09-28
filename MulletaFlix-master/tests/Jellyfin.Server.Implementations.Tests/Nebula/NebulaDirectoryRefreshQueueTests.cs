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
        using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
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
    public async Task Enqueue_RefreshesDifferentDirectoriesIndependently()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
            (directory, _) =>
            {
                refreshed.TryAdd(directory, 0);
                if (refreshed.Count == 2)
                {
                    completed.TrySetResult();
                }

                return Task.CompletedTask;
            },
            NullLogger<Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue>.Instance,
            TimeSpan.FromMilliseconds(25));

        queue.Enqueue("/Series/One");
        queue.Enqueue("/Movies/Two");

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains("/Series/One", refreshed.Keys);
        Assert.Contains("/Movies/Two", refreshed.Keys);
    }

    [Fact]
    public async Task Enqueue_DuringRefreshSchedulesOnlyOneFollowUp()
    {
        var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoRefreshesCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;
        using var queue = new Jellyfin.Server.Implementations.Nebula.NebulaDirectoryRefreshQueue(
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
}
