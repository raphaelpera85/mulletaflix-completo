using System;
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

    private static int GetQueuedItemCount(LimitedConcurrencyLibraryScheduler scheduler)
    {
        var channel = typeof(LimitedConcurrencyLibraryScheduler)
            .GetField("_tasks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scheduler)!;
        var reader = channel.GetType().GetProperty("Reader")!.GetValue(channel)!;
        return (int)reader.GetType().GetProperty("Count")!.GetValue(reader)!;
    }
}
