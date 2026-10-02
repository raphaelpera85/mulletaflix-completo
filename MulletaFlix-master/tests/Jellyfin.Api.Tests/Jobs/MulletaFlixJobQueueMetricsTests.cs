using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using MulletaFlix.Api.Jobs;
using Xunit;

namespace MulletaFlix.Api.Tests.Jobs;

public sealed class MulletaFlixJobQueueMetricsTests
{
    [Fact]
    public void LifecycleMetrics_AreAggregatedAndContainNoJobDetails()
    {
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == MulletaFlixJobQueueMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        MulletaFlixJobQueueMetrics.RecordEnqueued();
        MulletaFlixJobQueueMetrics.RecordStarted();
        MulletaFlixJobQueueMetrics.RecordCompleted();
        MulletaFlixJobQueueMetrics.RecordFailed();
        MulletaFlixJobQueueMetrics.RecordCancelled();

        Assert.Equal(
            ["mulletaflix.jobs.enqueued", "mulletaflix.jobs.started", "mulletaflix.jobs.completed", "mulletaflix.jobs.failed", "mulletaflix.jobs.cancelled"],
            measurements.Select(measurement => measurement.Name));
        Assert.All(measurements, measurement =>
        {
            Assert.Equal(1, measurement.Value);
            Assert.Empty(measurement.Tags);
        });
    }
}
