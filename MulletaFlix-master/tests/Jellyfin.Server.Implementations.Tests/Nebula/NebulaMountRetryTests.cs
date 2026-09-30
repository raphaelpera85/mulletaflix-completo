using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMountRetryTests
{
    [Fact]
    public async Task DisposingManager_CancelsQueuedMountBeforeReadingConfiguration()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        var manager = new NebulaFtpManager(configuration.Object, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(NebulaFtpManager).GetField("_isEnvioRunning", flags)!.SetValue(manager, true);
        typeof(NebulaFtpManager).GetMethod("ScheduleMountRetry", flags)!.Invoke(manager, null);
        var retry = (NebulaMountRetry)typeof(NebulaFtpManager).GetField("_mountRetry", flags)!.GetValue(manager)!;
        manager.Dispose();
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        configuration.Verify(m => m.GetConfiguration("nebulaftp"), Times.Never);
        Assert.False(await manager.MountDriveNAsync(CancellationToken.None));
        configuration.Verify(m => m.GetConfiguration("nebulaftp"), Times.Never);
    }

    [Fact]
    public async Task CancelPendingAttempt_DoesNotMountAndAllowsLaterScheduling()
    {
        var calls = 0;
        using var retry = new NebulaMountRetry(
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            },
            NullLogger<NebulaMountRetry>.Instance);
        retry.Schedule(TimeSpan.FromMinutes(1));
        retry.Cancel();
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, calls);
        retry.Schedule(TimeSpan.Zero);
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DisposePreventsMountAndFurtherScheduling()
    {
        var calls = 0;
        var retry = new NebulaMountRetry(
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            },
            NullLogger<NebulaMountRetry>.Instance);
        retry.Schedule(TimeSpan.FromMinutes(1));
        retry.Dispose();
        retry.Schedule(TimeSpan.Zero);
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CancelInFlightAttempt_CancelsItsTokenAndCoalescesDuplicates()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var retry = new NebulaMountRetry(
            async token =>
            {
                Interlocked.Increment(ref calls);
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            NullLogger<NebulaMountRetry>.Instance);
        retry.Schedule(TimeSpan.Zero);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var index = 0; index < 5; index++)
        {
            retry.Schedule(TimeSpan.Zero);
        }

        retry.Cancel();
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ScheduleDuringAttempt_QueuesOneRetryAndWaitForIdleDrainsBoth()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var retry = new NebulaMountRetry(
            async _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.SetResult();
                    await release.Task;
                }
            },
            NullLogger<NebulaMountRetry>.Instance);

        retry.Schedule(TimeSpan.Zero);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        retry.Schedule(TimeSpan.Zero);
        for (var index = 0; index < 5; index++)
        {
            retry.Schedule(TimeSpan.Zero);
        }

        release.SetResult();
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CancelDuringAttempt_DropsQueuedRetry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var retry = new NebulaMountRetry(
            async token =>
            {
                Interlocked.Increment(ref calls);
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            NullLogger<NebulaMountRetry>.Instance);

        retry.Schedule(TimeSpan.Zero);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        retry.Schedule(TimeSpan.Zero);
        retry.Cancel();
        await retry.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
    }
}
