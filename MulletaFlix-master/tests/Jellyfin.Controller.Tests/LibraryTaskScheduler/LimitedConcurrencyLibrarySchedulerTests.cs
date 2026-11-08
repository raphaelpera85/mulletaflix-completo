using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.LibraryTaskScheduler;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Controller.Tests.LibraryTaskScheduler;

public sealed class LimitedConcurrencyLibrarySchedulerTests
{
    [Fact]
    public async Task CleanupWaitsForWorkerTaskAfterCancellationCallbacksComplete()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationTokenSource.Token.Register(() => cancellationObserved.TrySetResult());
        var workerCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Task> worker = Task.Factory.StartNew(
            () => workerCompletion.Task,
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);
        var runners = new[] { new KeyValuePair<CancellationTokenSource, Task>(cancellationTokenSource, worker) };

        var cleanup = LimitedConcurrencyLibraryScheduler.CancelAndWaitForRunnersAsync(runners);

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(cleanup.IsCompleted);
        workerCompletion.TrySetResult();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Enqueue_AppliesBackpressureWhenBoundedChannelIsFull()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var bothWorkersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorkers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedWorkers = 0;
        var enqueue = scheduler.Enqueue(
            Enumerable.Range(0, 7).ToArray(),
            async (_, _) =>
            {
                if (Interlocked.Increment(ref startedWorkers) == 2)
                {
                    bothWorkersStarted.TrySetResult();
                }

                await releaseWorkers.Task;
            },
            new Progress<double>(),
            CancellationToken.None);

        try
        {
            await bothWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var queueWait = Stopwatch.StartNew();
            while (GetQueuedItemCount(scheduler) < 2 && queueWait.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            Assert.Equal(2, GetQueuedItemCount(scheduler));
            Assert.False(enqueue.IsCompleted);

            releaseWorkers.TrySetResult();
            await enqueue.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseWorkers.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_RemovesStoppedWorkersFromRegistry()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);

        await scheduler.Enqueue([1], (_, _) => Task.CompletedTask, new Progress<double>(), CancellationToken.None);
        var runners = GetTaskRunners(scheduler);
        Assert.NotEmpty(runners);

        await scheduler.DisposeAsync();
        await Task.WhenAll(runners);

        var cleanupWait = Stopwatch.StartNew();
        while (GetTaskRunners(scheduler).Length > 0 && cleanupWait.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Empty(GetTaskRunners(scheduler));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForAdmittedEnqueueToDrain()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var bothWorkersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorkers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedWorkers = 0;
        var enqueue = scheduler.Enqueue(
            Enumerable.Range(0, 7).ToArray(),
            async (_, _) =>
            {
                if (Interlocked.Increment(ref startedWorkers) == 2)
                {
                    bothWorkersStarted.TrySetResult();
                }

                await releaseWorkers.Task;
            },
            new Progress<double>(),
            CancellationToken.None);

        try
        {
            await bothWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var dispose = scheduler.DisposeAsync().AsTask();
            var concurrentDispose = scheduler.DisposeAsync().AsTask();
            Assert.False(dispose.IsCompleted);
            Assert.False(concurrentDispose.IsCompleted);
            releaseWorkers.TrySetResult();
            await enqueue.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await dispose.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await concurrentDispose.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseWorkers.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task ApplicationStopping_UnblocksEnqueueWaitingOnFullChannel()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        using var applicationStopping = new CancellationTokenSource();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(applicationStopping.Token);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var bothWorkersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorkers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedWorkers = 0;
        var enqueue = scheduler.Enqueue(
            Enumerable.Range(0, 7).ToArray(),
            async (_, _) =>
            {
                if (Interlocked.Increment(ref startedWorkers) == 2)
                {
                    bothWorkersStarted.TrySetResult();
                }

                await releaseWorkers.Task;
            },
            new Progress<double>(),
            CancellationToken.None);

        try
        {
            await bothWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var queueWait = Stopwatch.StartNew();
            while (GetQueuedItemCount(scheduler) < 2 && queueWait.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            Assert.Equal(2, GetQueuedItemCount(scheduler));
            Assert.False(enqueue.IsCompleted);

            applicationStopping.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enqueue);

            releaseWorkers.TrySetResult();
            await scheduler.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseWorkers.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task ApplicationStopping_UnblocksEnqueueWaitingForCleanup()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        using var applicationStopping = new CancellationTokenSource();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(applicationStopping.Token);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var cleanupInProgress = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(LimitedConcurrencyLibraryScheduler)
            .GetField("_cleanupInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scheduler, cleanupInProgress);

        var enqueue = scheduler.Enqueue([1], (_, _) => Task.CompletedTask, new Progress<double>(), CancellationToken.None);
        try
        {
            Assert.False(enqueue.IsCompleted);
            applicationStopping.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enqueue);
        }
        finally
        {
            cleanupInProgress.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_UnblocksEnqueueWaitingForCleanup()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var cleanupInProgress = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(LimitedConcurrencyLibraryScheduler)
            .GetField("_cleanupInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scheduler, cleanupInProgress);

        var enqueue = scheduler.Enqueue([1], (_, _) => Task.CompletedTask, new Progress<double>(), CancellationToken.None);
        try
        {
            Assert.False(enqueue.IsCompleted);
            var dispose = scheduler.DisposeAsync().AsTask();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enqueue);
            await dispose.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            cleanupInProgress.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task ApplicationStopping_StopsSequentialEnqueueBeforeStartingNextItem()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 1 });
        using var applicationStopping = new CancellationTokenSource();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(applicationStopping.Token);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var firstItemStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstItem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondItemStarted = 0;
        var enqueue = scheduler.Enqueue(
            [1, 2],
            async (item, _) =>
            {
                if (item == 1)
                {
                    firstItemStarted.TrySetResult();
                    await releaseFirstItem.Task;
                }
                else
                {
                    Interlocked.Increment(ref secondItemStarted);
                }
            },
            new Progress<double>(),
            CancellationToken.None);

        try
        {
            await firstItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            applicationStopping.Cancel();
            releaseFirstItem.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enqueue);
            Assert.Equal(0, secondItemStarted);
        }
        finally
        {
            releaseFirstItem.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_FromWorkerCallbackFailsFastInsteadOfDeadlocking()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);
        var nestedDispose = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var enqueue = scheduler.Enqueue(
            [1],
            async (_, _) =>
            {
                nestedDispose.TrySetResult(scheduler.DisposeAsync().AsTask());
                await releaseWorker.Task;
            },
            new Progress<double>(),
            CancellationToken.None);

        try
        {
            var disposeTask = await nestedDispose.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(disposeTask.IsCompleted);
            releaseWorker.TrySetResult();
            await enqueue.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => disposeTask);
        }
        finally
        {
            releaseWorker.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    [Fact]
    public async Task EnqueueDuringCleanupStartsNewWorkersAndCompletes()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { LibraryScanFanoutConcurrency = 2 });
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var scheduler = new LimitedConcurrencyLibraryScheduler(
            applicationLifetime.Object,
            NullLogger<LimitedConcurrencyLibraryScheduler>.Instance,
            configuration.Object);

        var cleanupCallbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCleanupToContinue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await scheduler.Enqueue([1], (_, _) => Task.CompletedTask, new Progress<double>(), CancellationToken.None);
            using var cancellationCallback = GetTaskRunnerTokenSources(scheduler)[0].Token.Register(() =>
            {
                cleanupCallbackStarted.TrySetResult();
                allowCleanupToContinue.Task.GetAwaiter().GetResult();
            });
            await Task.Delay(TimeSpan.FromSeconds(65), TestContext.Current.CancellationToken);
            await cleanupCallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var processedAfterCleanup = 0;
            var enqueue = scheduler.Enqueue(
                [2],
                (_, _) =>
                {
                    Interlocked.Increment(ref processedAfterCleanup);
                    return Task.CompletedTask;
                },
                new Progress<double>(),
                CancellationToken.None);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(enqueue.IsCompleted);
            allowCleanupToContinue.TrySetResult();
            await enqueue.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(1, processedAfterCleanup);
        }
        finally
        {
            allowCleanupToContinue.TrySetResult();
            await scheduler.DisposeAsync();
        }
    }

    private static int GetQueuedItemCount(LimitedConcurrencyLibraryScheduler scheduler)
    {
        var channel = typeof(LimitedConcurrencyLibraryScheduler)
            .GetField("_tasks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        var reader = channel.GetType().GetProperty("Reader")!.GetValue(channel)!;
        return (int)reader.GetType().GetProperty("Count")!.GetValue(reader)!;
    }

    private static Task[] GetTaskRunners(LimitedConcurrencyLibraryScheduler scheduler)
    {
        var schedulerType = typeof(LimitedConcurrencyLibraryScheduler);
        var taskLock = (System.Threading.Lock)schedulerType
            .GetField("_taskLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        var runners = (System.Collections.Generic.Dictionary<CancellationTokenSource, Task>)schedulerType
            .GetField("_taskRunners", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        lock (taskLock)
        {
            return runners.Values
                .Select(runner => runner is Task<Task> worker ? worker.Unwrap() : runner)
                .ToArray();
        }
    }

    private static CancellationTokenSource[] GetTaskRunnerTokenSources(LimitedConcurrencyLibraryScheduler scheduler)
    {
        var schedulerType = typeof(LimitedConcurrencyLibraryScheduler);
        var taskLock = (System.Threading.Lock)schedulerType
            .GetField("_taskLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        var runners = (System.Collections.Generic.Dictionary<CancellationTokenSource, Task>)schedulerType
            .GetField("_taskRunners", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        lock (taskLock)
        {
            return runners.Keys.ToArray();
        }
    }
}
