using System.Diagnostics.Metrics;

namespace MulletaFlix.Api.Jobs;

/// <summary>
/// Low-cardinality lifecycle metrics for the in-process job queue.
/// </summary>
internal static class MulletaFlixJobQueueMetrics
{
    internal const string MeterName = "MulletaFlix.Api.JobQueue";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Enqueued = Meter.CreateCounter<long>("mulletaflix.jobs.enqueued");
    private static readonly Counter<long> Started = Meter.CreateCounter<long>("mulletaflix.jobs.started");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("mulletaflix.jobs.completed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("mulletaflix.jobs.failed");
    private static readonly Counter<long> Cancelled = Meter.CreateCounter<long>("mulletaflix.jobs.cancelled");

    internal static void RecordEnqueued() => Enqueued.Add(1);

    internal static void RecordStarted() => Started.Add(1);

    internal static void RecordCompleted() => Completed.Add(1);

    internal static void RecordFailed() => Failed.Add(1);

    internal static void RecordCancelled() => Cancelled.Add(1);
}
