using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Updates;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class PluginUpdateTaskTests
{
    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "partial_failure")]
    public async Task ExecuteAsync_RecordsPackageOutcomesWithoutSensitiveTags(bool failInstall, string expectedResult)
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var installation = CreateInstallation("Private plugin name");
        var manager = new Mock<IInstallationManager>();
        manager.Setup(service => service.GetAvailablePluginUpdates(It.IsAny<CancellationToken>()))
            .ReturnsAsync([installation]);
        manager.Setup(service => service.InstallPackage(installation, It.IsAny<CancellationToken>()))
            .Returns(failInstall ? Task.FromException(new HttpRequestException("private download url")) : Task.CompletedTask);
        var task = CreateTask(manager.Object);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertMetric(measurements, expectedResult);
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.available_per_run", 1L);
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.attempted", 1L);
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.failed", failInstall ? 1L : 0L);
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.plugin_updates.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, item => Assert.All(item.Tags, tag =>
        {
            Assert.Equal("result", tag.Key);
            Assert.DoesNotContain("Private plugin name", tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("private download url", tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }));
    }

    [Fact]
    public async Task ExecuteAsync_NoAvailablePackages_RecordsNoItems()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var manager = new Mock<IInstallationManager>();
        manager.Setup(service => service.GetAvailablePluginUpdates(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InstallationInfo>());

        await CreateTask(manager.Object).ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        AssertMetric(measurements, "no_items");
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.available_per_run", 0L);
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.attempted", 0L);
        AssertMetricValue(measurements, "mulletaflix.plugin_updates.packages.failed", 0L);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_RecordsCancellationAndRethrows()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var manager = new Mock<IInstallationManager>();
        manager.Setup(service => service.GetAvailablePluginUpdates(cancellation.Token))
            .Returns(Task.FromCanceled<IEnumerable<InstallationInfo>>(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTask(manager.Object).ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));

        AssertMetric(measurements, "cancelled");
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.plugin_updates.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    private static PluginUpdateTask CreateTask(IInstallationManager manager)
        => new(NullLogger<PluginUpdateTask>.Instance, manager, Mock.Of<ILocalizationManager>());

    private static InstallationInfo CreateInstallation(string name)
        => new() { Name = name, Version = new Version(1, 0) };

    private static MeterListener Listen(List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PluginUpdateMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, CopyTags(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, CopyTags(tags))));
        listener.Start();
        return listener;
    }

    private static KeyValuePair<string, object?>[] CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new KeyValuePair<string, object?>[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            copy[i] = tags[i];
        }

        return copy;
    }

    private static void AssertMetric(
        List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements,
        string expectedResult)
    {
        Assert.Contains(measurements, item => item.Name == "mulletaflix.plugin_updates.runs"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, expectedResult)));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.plugin_updates.duration");
    }

    private static void AssertMetricValue(
        List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements,
        string name,
        long expected)
        => Assert.Contains(measurements, item => item.Name == name && Equals(item.Value, expected));
}
