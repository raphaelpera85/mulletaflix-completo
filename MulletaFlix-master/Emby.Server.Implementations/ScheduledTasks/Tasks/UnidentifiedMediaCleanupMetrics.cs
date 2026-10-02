using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for the unidentified-media maintenance task.
/// </summary>
public static class UnidentifiedMediaCleanupMetrics
{
    public const string MeterName = "MulletaFlix.Server.UnidentifiedMediaCleanup";

    private static readonly Meter CleanupMeter = new(MeterName);
    private static readonly Counter<long> Runs = CleanupMeter.CreateCounter<long>("mulletaflix.unidentified_cleanup.runs");
    private static readonly Counter<long> ScannedItems = CleanupMeter.CreateCounter<long>("mulletaflix.unidentified_cleanup.items.scanned");
    private static readonly Counter<long> QueuedRefreshes = CleanupMeter.CreateCounter<long>("mulletaflix.unidentified_cleanup.refreshes.queued");
    private static readonly Counter<long> FailedRefreshes = CleanupMeter.CreateCounter<long>("mulletaflix.unidentified_cleanup.refreshes.failed");
    private static readonly Histogram<double> Duration = CleanupMeter.CreateHistogram<double>("mulletaflix.unidentified_cleanup.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = CleanupMeter.CreateUpDownCounter<long>("mulletaflix.unidentified_cleanup.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds, long scanned, long queued, long failed)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedItems.Add(scanned);
        QueuedRefreshes.Add(queued);
        FailedRefreshes.Add(failed);
    }
}
