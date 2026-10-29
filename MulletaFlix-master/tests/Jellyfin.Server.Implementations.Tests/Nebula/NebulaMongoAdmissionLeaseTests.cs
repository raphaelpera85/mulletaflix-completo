using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMongoAdmissionLeaseTests
{
    [Fact]
    public async Task HeartbeatReturningFalse_CancelsLeaseLostToken()
    {
        var renewalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCount = 0;
        await using var lease = new NebulaMongoAdmissionLease(
            _ =>
            {
                renewalStarted.TrySetResult();
                return Task.FromResult(false);
            },
            _ =>
            {
                Interlocked.Increment(ref releaseCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(10));

        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lease.LeaseLostToken.Register(() => lost.TrySetResult());
        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(lease.LeaseLostToken.IsCancellationRequested);
        await lease.DisposeAsync();
        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public async Task HeartbeatThrowing_CancelsLeaseLostToken()
    {
        var renewalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var lease = new NebulaMongoAdmissionLease(
            _ =>
            {
                renewalStarted.TrySetResult();
                throw new InvalidOperationException("simulated Mongo renewal failure");
            },
            _ => Task.CompletedTask,
            TimeSpan.FromMilliseconds(10));

        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lease.LeaseLostToken.Register(() => lost.TrySetResult());
        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(lease.LeaseLostToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DisposeAsync_StopsHeartbeatAndReleasesOnlyOnce()
    {
        var renewalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewalCount = 0;
        var releaseCount = 0;
        var lease = new NebulaMongoAdmissionLease(
            _ =>
            {
                Interlocked.Increment(ref renewalCount);
                renewalStarted.TrySetResult();
                return Task.FromResult(true);
            },
            _ =>
            {
                Interlocked.Increment(ref releaseCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(10));

        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(1, renewalCount);
        Assert.Equal(1, releaseCount);
    }
}
