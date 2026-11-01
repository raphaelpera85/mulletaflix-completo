using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public sealed class MidiaStorageOnlineSyncGateTests
{
    [Fact]
    public async Task RunAsync_SerializesScheduledAndManualSyncWriters()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        var scheduledEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseScheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequence = new List<string>();
        var scheduled = MidiaStorageOnlineSyncGate.RunAsync(
            manifestPath,
            async () =>
            {
                sequence.Add("scheduled");
                scheduledEntered.SetResult();
                await releaseScheduled.Task;
            },
            CancellationToken.None);
        await scheduledEntered.Task;

        var manual = MidiaStorageOnlineSyncGate.RunAsync(
            Path.Combine(Path.GetDirectoryName(manifestPath)!, ".", Path.GetFileName(manifestPath)),
            () =>
            {
                sequence.Add("manual");
                return Task.FromResult(1);
            },
            CancellationToken.None);
        var manualWasBlocked = !manual.IsCompleted;

        releaseScheduled.SetResult();
        await Task.WhenAll(scheduled, manual);

        Assert.True(manualWasBlocked);
        Assert.Equal(1, await manual);
        Assert.Equal(new[] { "scheduled", "manual" }, sequence);
    }

    [Fact]
    public async Task RunAsync_DoesNotSerializeDifferentManifestPaths()
    {
        var firstPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        var secondPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = MidiaStorageOnlineSyncGate.RunAsync(
            firstPath,
            async () =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
            },
            CancellationToken.None);
        await firstEntered.Task;

        var secondRan = false;
        await MidiaStorageOnlineSyncGate.RunAsync(
            secondPath,
            () =>
            {
                secondRan = true;
                return Task.FromResult(true);
            },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        releaseFirst.SetResult();
        await first;
        Assert.True(secondRan);
    }

    [Fact]
    public async Task RunAsync_CancelledWaitDoesNotKeepManifestLocked()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = MidiaStorageOnlineSyncGate.RunAsync(
            manifestPath,
            async () =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
            },
            CancellationToken.None);
        await firstEntered.Task;

        using var cancellationSource = new CancellationTokenSource();
        var cancelledWait = MidiaStorageOnlineSyncGate.RunAsync(
            manifestPath,
            () => Task.FromResult(true),
            cancellationSource.Token);
        cancellationSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWait);

        releaseFirst.SetResult();
        await first;
        await MidiaStorageOnlineSyncGate.RunAsync(
            manifestPath,
            () => Task.FromResult(true),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RunAsync_ReleasesGateAfterOperationThrows()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"mfx-sync-manifest-{Guid.NewGuid():N}", "sync_manifest.json");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MidiaStorageOnlineSyncGate.RunAsync(
                manifestPath,
                () => Task.FromException(new InvalidOperationException()),
                CancellationToken.None));

        var nextWriterRan = false;
        await MidiaStorageOnlineSyncGate.RunAsync(
            manifestPath,
            () =>
            {
                nextWriterRan = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(nextWriterRan);
    }
}
