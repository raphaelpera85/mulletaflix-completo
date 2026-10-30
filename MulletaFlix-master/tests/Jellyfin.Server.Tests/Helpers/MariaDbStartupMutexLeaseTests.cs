using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Server.Helpers;
using Xunit;

namespace Jellyfin.Server.Tests.Helpers;

public class MariaDbStartupMutexLeaseTests
{
    [Fact]
    public void Dispose_ReleasesMutexOnTheOwningThread()
    {
        var name = $"MulletaFlix.Test.{Guid.NewGuid():N}";
        using (new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2)))
        {
        }

        using var reacquired = new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task LifecycleOperations_AreSerializedWithinTheProcess()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = MariaDbProcessManager.ExecuteLifecycleOperationAsync(
            async () =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            },
            CancellationToken.None);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var second = MariaDbProcessManager.ExecuteLifecycleOperationAsync(
            () =>
            {
                secondEntered.SetResult();
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.False(secondEntered.Task.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(secondEntered.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task LifecycleOperation_CanBeCanceledWhileWaitingForTheGate()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = MariaDbProcessManager.ExecuteLifecycleOperationAsync(
            async () =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            },
            CancellationToken.None);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        var waiting = MariaDbProcessManager.ExecuteLifecycleOperationAsync(
            () => Task.CompletedTask,
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        releaseFirst.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await MariaDbProcessManager.ExecuteLifecycleOperationAsync(
            () => Task.CompletedTask,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Constructor_TimesOutWhenAnotherOwnerHoldsMutex()
    {
        var name = $"MulletaFlix.Test.{Guid.NewGuid():N}";
        using var mutex = new Mutex(false, name);

        try
        {
            Assert.True(mutex.WaitOne());
            Assert.Throws<TimeoutException>(() =>
                new MariaDbStartupMutexLease(name, TimeSpan.FromMilliseconds(50)));
        }
        finally
        {
            mutex.ReleaseMutex();
        }

        using var reacquired = new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_ObservesCancellationWhenMutexIsAvailable()
    {
        var name = $"MulletaFlix.Test.{Guid.NewGuid():N}";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2), cancellation.Token));

        using var reacquired = new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Constructor_CancelsWhileWaitingForAnotherOwner()
    {
        var name = $"MulletaFlix.Test.{Guid.NewGuid():N}";
        var ownerAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseOwner = new ManualResetEventSlim();
        var owner = new Thread(() =>
        {
            using var mutex = new Mutex(false, name);
            var ownsMutex = false;
            try
            {
                ownsMutex = mutex.WaitOne();
                if (!ownsMutex)
                {
                    ownerAcquired.TrySetException(new TimeoutException("Could not acquire the test mutex."));
                    return;
                }

                ownerAcquired.SetResult();
                releaseOwner.Wait();
            }
            catch (Exception ex)
            {
                ownerAcquired.TrySetException(ex);
            }
            finally
            {
                if (ownsMutex)
                {
                    mutex.ReleaseMutex();
                }
            }
        });
        owner.Start();

        try
        {
            await ownerAcquired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            using var cancellationTokenSource = new CancellationTokenSource();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var acquisition = Task.Run(() =>
            {
                started.SetResult();
                return new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(10), cancellationTokenSource.Token);
            });
            _ = acquisition.ContinueWith(
                task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result.Dispose();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
            Assert.False(acquisition.IsCompleted);
            cancellationTokenSource.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await acquisition.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        }
        finally
        {
            releaseOwner.Set();
            Assert.True(owner.Join(TimeSpan.FromSeconds(2)));
        }

        using var reacquired = new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Constructor_AcceptsAnAbandonedMutex()
    {
        var name = $"MulletaFlix.Test.{Guid.NewGuid():N}";
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mutex? abandonedMutex = null;
        var owner = new Thread(() =>
        {
            abandonedMutex = new Mutex(false, name);
#pragma warning disable MT1012 // Intentionally abandon this test mutex to exercise owner recovery.
            abandonedMutex.WaitOne();
#pragma warning restore MT1012
            acquired.SetResult();
        });

        owner.Start();
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(owner.Join(TimeSpan.FromSeconds(2)));
        abandonedMutex!.Dispose();

        using var recovered = new MariaDbStartupMutexLease(name, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void BuildStartupMutexName_IsStableForSameDataDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mulletaflix", "mariadb_data");
        var name = MariaDbProcessManager.BuildStartupMutexName(path);
        var normalizedPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
        {
            normalizedPath = normalizedPath.ToUpperInvariant();
        }

        var expectedName = $"Global\\MulletaFlix.EmbeddedMariaDb.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)))}";
        Assert.Equal(expectedName, name);
        Assert.Equal(
            name,
            MariaDbProcessManager.BuildStartupMutexName(Path.Combine(Path.GetTempPath(), ".", "mulletaflix", "mariadb_data")));
    }

    [Fact]
    public void BuildStartupMutexName_IsDifferentForDifferentDataDirectories()
    {
        Assert.NotEqual(
            MariaDbProcessManager.BuildStartupMutexName(Path.Combine(Path.GetTempPath(), "mulletaflix-a", "mariadb_data")),
            MariaDbProcessManager.BuildStartupMutexName(Path.Combine(Path.GetTempPath(), "mulletaflix-b", "mariadb_data")));
    }

    [Fact]
    public void BuildPortStartupMutexName_IsStableForSamePort()
    {
        Assert.Equal(
            MariaDbProcessManager.BuildPortStartupMutexName(3306),
            MariaDbProcessManager.BuildPortStartupMutexName(3306));
    }

    [Fact]
    public void BuildPortStartupMutexName_IsDifferentForDifferentPorts()
    {
        Assert.NotEqual(
            MariaDbProcessManager.BuildPortStartupMutexName(3306),
            MariaDbProcessManager.BuildPortStartupMutexName(3307));
    }

    [Fact]
    public void IsExpectedDataDirectory_AcceptsEquivalentNormalizedPaths()
    {
        var configuredPath = Path.Combine(Path.GetTempPath(), "mulletaflix", "db");
        var equivalentPath = Path.Combine(Path.GetTempPath(), "mulletaflix", "temporary", "..", "db");

        Assert.True(MariaDbProcessManager.IsExpectedDataDirectory(configuredPath, equivalentPath));
    }

    [Fact]
    public void IsExpectedDataDirectory_RejectsDifferentDirectories()
    {
        var configuredPath = Path.Combine(Path.GetTempPath(), "mulletaflix-a", "db");
        var actualPath = Path.Combine(Path.GetTempPath(), "mulletaflix-b", "db");

        Assert.False(MariaDbProcessManager.IsExpectedDataDirectory(configuredPath, actualPath));
    }
}
