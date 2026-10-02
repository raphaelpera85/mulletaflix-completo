using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Model.Globalization;
using MulletaFlix.Database.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class OptimizeDatabaseTaskTests
{
    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "failure")]
    public async Task ExecuteAsync_RecordsOutcomeAndDuration(bool fail, string expectedResult)
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == OptimizeDatabaseMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, CopyTags(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, CopyTags(tags))));
        listener.Start();

        var provider = new Mock<IMulletaFlixDatabaseProvider>();
        if (fail)
        {
            provider.Setup(database => database.RunScheduledOptimisation(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));
        }

        var task = new OptimizeDatabaseTask(
            NullLogger<OptimizeDatabaseTask>.Instance,
            Mock.Of<ILocalizationManager>(),
            provider.Object);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), CancellationToken.None);

        Assert.Contains(measurements, item => item.Name == "mulletaflix.database_optimization.runs"
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, expectedResult)));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.database_optimization.duration");
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.database_optimization.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, item => Assert.All(item.Tags, tag => Assert.Equal("result", tag.Key)));
        provider.Verify(database => database.RunScheduledOptimisation(It.IsAny<CancellationToken>()), Times.Once);
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
}
