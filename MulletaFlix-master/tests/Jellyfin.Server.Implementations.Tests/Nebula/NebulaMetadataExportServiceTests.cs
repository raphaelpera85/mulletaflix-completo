using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
    [Fact]
    public async Task RunGuardedExportAsync_EmitsOnlyBoundedMetricsAndReleasesActiveGaugeOnFailure()
    {
        var measurements = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == NebulaMetadataExportService.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        using var service = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunGuardedExportAsync(
            _ => Task.FromException(new InvalidOperationException("private media path must not be exported")),
            CancellationToken.None));

        var operation = Assert.Single(measurements.Where(item =>
            item.Name == "mulletaflix.metadata_export.operations"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, "failure"))));
        Assert.Equal(1, operation.Value);
        Assert.Contains(measurements, item => item.Name == "mulletaflix.metadata_export.operation.duration" && item.Value >= 0);
        Assert.Equal([1d, -1d], measurements
            .Where(item => item.Name == "mulletaflix.metadata_export.active_operations")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, item => Assert.DoesNotContain(item.Tags, tag =>
            tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("media", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
    }

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
