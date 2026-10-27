using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public sealed class MidiaStorageOnlineSyncGateTests
{
    [Fact]
    public async Task AcquireAsync_SerializesSyncsForSameManifestPath()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        using var firstLease = await MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, TestContext.Current.CancellationToken);

        var secondLeaseTask = MidiaStorageOnlineSyncGate.AcquireAsync(
            Path.Combine(Path.GetDirectoryName(manifestPath)!, ".", Path.GetFileName(manifestPath)),
            TestContext.Current.CancellationToken);

        Assert.False(secondLeaseTask.IsCompleted);

        firstLease.Dispose();
        using var secondLease = await secondLeaseTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AcquireAsync_DoesNotSerializeDifferentManifestPaths()
    {
        var firstPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        var secondPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");

        using var firstLease = await MidiaStorageOnlineSyncGate.AcquireAsync(firstPath, TestContext.Current.CancellationToken);
        using var secondLease = await MidiaStorageOnlineSyncGate.AcquireAsync(secondPath, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AcquireAsync_CancellationDoesNotKeepTheGateLocked()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        using var firstLease = await MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, TestContext.Current.CancellationToken);
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waitingLeaseTask = MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, cancellationSource.Token);

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waitingLeaseTask);
        firstLease.Dispose();
        using var nextLease = await MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AcquireAsync_TimeoutDoesNotKeepTheGateLocked()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        using var firstLease = await MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, TestContext.Current.CancellationToken);

        var waitingLeaseTask = MidiaStorageOnlineSyncGate.AcquireAsync(
            manifestPath,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAsync<TimeoutException>(async () => await waitingLeaseTask);
        firstLease.Dispose();
        using var nextLease = await MidiaStorageOnlineSyncGate.AcquireAsync(manifestPath, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
