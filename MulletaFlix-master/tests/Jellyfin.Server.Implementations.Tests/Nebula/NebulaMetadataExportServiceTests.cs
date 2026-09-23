using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public class NebulaMetadataExportServiceTests
{
    /// <summary>
    /// A scan raises one event per indexed file, so the follow-up of every item used to run at the
    /// same time. Each one refreshes metadata, which is what saturated the MariaDB connection pool.
    /// </summary>
    [Fact]
    public async Task RunGuardedExportAsync_NeverRunsMoreExportsThanTheBoundedSlots()
    {
        var service = CreateService();
        var concurrent = 0;
        var peak = 0;
        var started = 0;

        var exports = Enumerable.Range(0, NebulaMetadataExportService.MaxConcurrentExports * 5)
            .Select(_ => service.RunGuardedExportAsync(
                async token =>
                {
                    var running = Interlocked.Increment(ref concurrent);
                    Interlocked.Increment(ref started);
                    UpdatePeak(ref peak, running);
                    await Task.Delay(15, token).ConfigureAwait(false);
                    Interlocked.Decrement(ref concurrent);
                },
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(exports).ConfigureAwait(false);

        Assert.Equal(NebulaMetadataExportService.MaxConcurrentExports * 5, started);
        Assert.Equal(0, concurrent);

        // The gate must actually be used: all slots get taken, and never one more.
        Assert.Equal(NebulaMetadataExportService.MaxConcurrentExports, peak);
    }

    [Fact]
    public async Task RunGuardedExportAsync_ReleasesTheSlotWhenTheExportThrows()
    {
        var service = CreateService();
        var attempts = 0;

        for (var i = 0; i < NebulaMetadataExportService.MaxConcurrentExports + 3; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunGuardedExportAsync(
                _ =>
                {
                    attempts++;
                    throw new InvalidOperationException("export failed");
                },
                CancellationToken.None)).ConfigureAwait(false);
        }

        // Every slot was handed back, so the next export still has room to run.
        Assert.Equal(NebulaMetadataExportService.MaxConcurrentExports + 3, attempts);

        var ran = false;
        await service.RunGuardedExportAsync(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            CancellationToken.None).ConfigureAwait(false);

        Assert.True(ran);
    }

    [Fact]
    public async Task RunGuardedExportAsync_DoesNotRunWhenTheSlotWaitIsCancelled()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunGuardedExportAsync(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            cts.Token)).ConfigureAwait(false);

        Assert.False(ran);
    }

    private static void UpdatePeak(ref int peak, int value)
    {
        var current = Volatile.Read(ref peak);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref peak, value, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private static NebulaMetadataExportService CreateService()
    {
        return new NebulaMetadataExportService(
            new Mock<ILibraryManager>().Object,
            new Mock<IProviderManager>().Object,
            new Mock<IServerConfigurationManager>().Object,
            NullLogger<NebulaMetadataExportService>.Instance);
    }
}
