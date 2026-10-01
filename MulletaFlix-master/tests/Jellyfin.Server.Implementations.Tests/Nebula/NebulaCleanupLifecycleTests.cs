using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaCleanupLifecycleTests
{
    [Fact]
    public async Task DisposeAsync_RetriesAfterOwnedProcessStopFailure()
    {
        var manager = CreateManager();
        using var unstarted = new System.Diagnostics.Process();
        var refreshQueue = GetField(manager, "_directoryRefreshQueue");
        SetField(manager, "_rcloneProcess", unstarted);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DisposeAsync().AsTask());
        Assert.Same(unstarted, GetField(manager, "_rcloneProcess"));
        Assert.Same(refreshQueue, GetField(manager, "_directoryRefreshQueue"));

        SetField(manager, "_rcloneProcess", null);
        await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(GetField(manager, "_mongoContext"));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForSharedRuntimeMaintenance()
    {
        var manager = CreateManager();
        var maintenance = (SemaphoreSlim)GetField(manager, "_maintenanceLock")!;
        await maintenance.WaitAsync();
        var mount = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(manager, "_automaticMountTask", mount.Task);

        try
        {
            var disposal = manager.DisposeAsync().AsTask();
            mount.SetResult();
            await Task.Delay(50);
            Assert.False(disposal.IsCompleted);
            Assert.Same(maintenance, GetField(manager, "_maintenanceLock"));
            maintenance.Release();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            mount.TrySetResult();
            if (maintenance.CurrentCount == 0)
            {
                maintenance.Release();
            }
        }
    }

    [Fact]
    public async Task DisposeAsync_WaitsForAutomaticMountAndRejectsNewCleanup()
    {
        var manager = CreateManager();
        var mount = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(manager, "_automaticMountTask", mount.Task);
        var disposal = manager.DisposeAsync().AsTask();
        var repeatedDisposal = manager.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposal.IsCompleted);
            Assert.False(repeatedDisposal.IsCompleted);
            var method = typeof(NebulaFtpManager).GetMethod("StartContinuousCleanup", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(manager, [new NebulaFtpConfiguration()]));
            Assert.IsType<ObjectDisposedException>(error.InnerException);
            Assert.Null(GetField(manager, "_cleanupTask"));
        }
        finally
        {
            mount.TrySetResult();
            await Task.WhenAll(disposal, repeatedDisposal).WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(await manager.StartEnvioAsync(false, CancellationToken.None));
        Assert.False(await manager.StartDownloaderAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposeDuringCleanup_PreservesLocksUntilCleanupFinishes()
    {
        var manager = CreateManager();
        using var cancellation = new CancellationTokenSource();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(manager, "_cleanupCts", cancellation);
        SetField(manager, "_cleanupTask", cleanup.Task);
        var transition = (SemaphoreSlim)GetField(manager, "_envioLock")!;
        try
        {
            manager.Dispose();
            Assert.True(cancellation.IsCancellationRequested);
            Assert.True(await transition.WaitAsync(0));
            transition.Release();
            Assert.Same(cleanup.Task, GetField(manager, "_cleanupTask"));
        }
        finally
        {
            cleanup.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Null(GetField(manager, "_cleanupTask"));
        Assert.Null(GetField(manager, "_cleanupCts"));
    }

    [Fact]
    public async Task CleanupTimeout_PreservesResourcesUntilTheTaskCompletes()
    {
        var manager = CreateManager();
        using var cancellation = new CancellationTokenSource();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(manager, "_cleanupCts", cancellation);
        SetField(manager, "_cleanupTask", cleanup.Task);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => DisposeRuntimeAsync(manager));
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Same(cleanup.Task, GetField(manager, "_cleanupTask"));
            Assert.Same(cancellation, GetField(manager, "_cleanupCts"));
        }
        finally
        {
            cleanup.TrySetResult();
            await DisposeRuntimeAsync(manager);
            await DisposeLocalQueueAsync(manager);
        }

        Assert.Null(GetField(manager, "_cleanupTask"));
        Assert.Null(GetField(manager, "_cleanupCts"));
    }

    [Fact]
    public async Task Startup_DoesNotReplaceACancelledCleanupStillRunning()
    {
        var manager = CreateManager();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(manager, "_cleanupCts", cancellation);
        SetField(manager, "_cleanupTask", cleanup.Task);
        var method = typeof(NebulaFtpManager).GetMethod("StartContinuousCleanup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(manager, [new NebulaFtpConfiguration()]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Same(cleanup.Task, GetField(manager, "_cleanupTask"));
        cleanup.SetResult();
        await DisposeLocalQueueAsync(manager);
    }

    [Fact]
    public async Task StopEnvioAsync_DisposesPlaybackCacheAndClearsAccessor()
    {
        var manager = CreateManager();
        var cachePath = Path.Combine(Path.GetTempPath(), "test-stop-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cachePath);
        try
        {
            var cache = new NebulaPlaybackCache(cachePath, NullLogger<NebulaPlaybackCache>.Instance);
            SetField(manager, "_playbackCache", cache);
            var accessor = (NebulaPlaybackCacheAccessor)GetField(manager, "_playbackCacheAccessor")!;
            accessor.Set(cache);

            Assert.Same(cache, accessor.Current);

            await manager.StopEnvioAsync(CancellationToken.None);

            Assert.Null(accessor.Current);
            Assert.Null(GetField(manager, "_playbackCache"));
        }
        finally
        {
            await DisposeLocalQueueAsync(manager);
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }

    private static NebulaFtpManager CreateManager() => new(
        Mock.Of<IServerConfigurationManager>(),
        NullLogger<NebulaFtpManager>.Instance,
        NullLoggerFactory.Instance);

    private static object? GetField(NebulaFtpManager manager, string name) =>
        typeof(NebulaFtpManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager);

    private static void SetField(NebulaFtpManager manager, string name, object value) =>
        typeof(NebulaFtpManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);

    private static Task DisposeRuntimeAsync(NebulaFtpManager manager) =>
        (Task)typeof(NebulaFtpManager).GetMethod("DisposeSharedRuntimeResourcesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null)!;

    // These fixtures own only the queue and explicitly injected cleanup task.
    private static ValueTask DisposeLocalQueueAsync(NebulaFtpManager manager) =>
        ((IAsyncDisposable)GetField(manager, "_directoryRefreshQueue")!).DisposeAsync();
}
