using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks;

/// <summary>
/// Aggregate metrics for scheduled activity log cleanup.
/// </summary>
public static class ActivityLogCleanupMetrics
{
    public const string MeterName = "MulletaFlix.Server.ActivityLogCleanup";

    private static readonly Meter CleanupMeter = new(MeterName);
    private static readonly Counter<long> Runs = CleanupMeter.CreateCounter<long>("mulletaflix.activity_log_cleanup.runs");
    private static readonly Counter<long> ExpiredCandidates = CleanupMeter.CreateCounter<long>("mulletaflix.activity_log_cleanup.entries.expired_candidates");
    private static readonly Counter<long> DeletedEntries = CleanupMeter.CreateCounter<long>("mulletaflix.activity_log_cleanup.entries.deleted");
    private static readonly Histogram<double> Duration = CleanupMeter.CreateHistogram<double>("mulletaflix.activity_log_cleanup.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = CleanupMeter.CreateUpDownCounter<long>("mulletaflix.activity_log_cleanup.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds, long expiredCandidates, long deletedEntries)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ExpiredCandidates.Add(expiredCandidates);
        DeletedEntries.Add(deletedEntries);
    }
}
