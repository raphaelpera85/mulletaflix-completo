using System;
using System.IO;
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
    public void BuildStartupMutexName_IsStableForSamePort()
    {
        Assert.Equal(
            MariaDbProcessManager.BuildStartupMutexName(3306),
            MariaDbProcessManager.BuildStartupMutexName(3306));
    }

    [Fact]
    public void BuildStartupMutexName_IsDifferentForDifferentPorts()
    {
        Assert.NotEqual(
            MariaDbProcessManager.BuildStartupMutexName(3306),
            MariaDbProcessManager.BuildStartupMutexName(3307));
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
