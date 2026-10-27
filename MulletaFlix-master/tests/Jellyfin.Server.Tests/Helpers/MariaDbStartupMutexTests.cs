using System;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Server.Helpers;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public class MariaDbStartupMutexTests
{
    [Fact]
    public async Task StartupMutexLease_ReleasesFromOwnerThreadAfterAsyncContinuation()
    {
        var mutexName = $"MulletaFlix.StartupMutex.{Guid.NewGuid():N}";
        var fallbackName = $"{mutexName}.fallback";
        var lease = await MariaDbProcessManager.StartupMutexLease.AcquireAsync(
            mutexName,
            fallbackName,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        using var contender = new Mutex(false, mutexName);

        var acquiredByContender = false;
        try
        {
            Assert.False(contender.WaitOne(0));
            await Task.WhenAll(
                Task.Run(async () => await lease.DisposeAsync(), TestContext.Current.CancellationToken),
                Task.Run(async () => await lease.DisposeAsync(), TestContext.Current.CancellationToken));

            acquiredByContender = contender.WaitOne(TimeSpan.FromSeconds(2));
            Assert.True(acquiredByContender);
        }
        finally
        {
            if (acquiredByContender)
            {
                contender.ReleaseMutex();
            }
        }
    }

    [Fact]
    public async Task StartupMutexLease_ReportsBoundedTimeout()
    {
        var mutexName = $"MulletaFlix.StartupMutex.{Guid.NewGuid():N}";
        using var owner = new MutexOwner(mutexName);

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await MariaDbProcessManager.StartupMutexLease.AcquireAsync(
                mutexName,
                $"{mutexName}.fallback",
                TimeSpan.FromMilliseconds(50),
                TestContext.Current.CancellationToken));

        Assert.Contains("Timed out waiting", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupMutexLease_CancellationStopsPendingWait()
    {
        var mutexName = $"MulletaFlix.StartupMutex.{Guid.NewGuid():N}";
        using var owner = new MutexOwner(mutexName);
        using var cancellation = new CancellationTokenSource();

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await MariaDbProcessManager.StartupMutexLease.AcquireAsync(
                mutexName,
                $"{mutexName}.fallback",
                TimeSpan.FromSeconds(2),
                cancellation.Token));
    }

    [Fact]
    public async Task StartupMutexLease_RecoversAbandonedMutex()
    {
        var mutexName = $"MulletaFlix.StartupMutex.{Guid.NewGuid():N}";
        using var observer = new Mutex(false, mutexName);
        using var owner = new MutexOwner(mutexName, abandonOnExit: true);

        await using (await MariaDbProcessManager.StartupMutexLease.AcquireAsync(
            mutexName,
            $"{mutexName}.fallback",
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken))
        {
            var observerAcquired = false;
            try
            {
                observerAcquired = observer.WaitOne(0);
                Assert.False(observerAcquired);
            }
            finally
            {
                if (observerAcquired)
                {
                    observer.ReleaseMutex();
                }
            }
        }

        var acquiredByObserver = false;
        try
        {
            acquiredByObserver = observer.WaitOne(TimeSpan.FromSeconds(2));
            Assert.True(acquiredByObserver);
        }
        finally
        {
            if (acquiredByObserver)
            {
                observer.ReleaseMutex();
            }
        }
    }

    private sealed class MutexOwner : IDisposable
    {
        private readonly ManualResetEvent _release = new(false);
        private readonly ManualResetEvent _acquired = new(false);
        private readonly Thread _thread;
        private bool _ownsMutex;

        public MutexOwner(string mutexName, bool abandonOnExit = false)
        {
            _thread = new Thread(() =>
            {
                using var mutex = new Mutex(false, mutexName);
                var ownsMutex = false;
                try
                {
                    ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(2));
                    _ownsMutex = ownsMutex;
                    _acquired.Set();
                    if (ownsMutex && !abandonOnExit)
                    {
                        _release.WaitOne();
                    }
                }
                finally
                {
                    if (ownsMutex && !abandonOnExit)
                    {
                        mutex.ReleaseMutex();
                    }

                    if (!ownsMutex)
                    {
                        _acquired.Set();
                    }
                }
            });
            _thread.Start();
            Assert.True(_acquired.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.True(_ownsMutex);
        }

        public void Dispose()
        {
            _release.Set();
            try
            {
                Assert.True(_thread.Join(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                _release.Dispose();
                _acquired.Dispose();
            }
        }
    }
}
