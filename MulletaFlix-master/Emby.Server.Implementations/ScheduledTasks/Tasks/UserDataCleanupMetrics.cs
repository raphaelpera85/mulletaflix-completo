using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for detached user data cleanup.
/// </summary>
public static class UserDataCleanupMetrics
{
    public const string MeterName = "MulletaFlix.Server.UserDataCleanup";

    private static readonly Meter CleanupMeter = new(MeterName);
    private static readonly Counter<long> Runs = CleanupMeter.CreateCounter<long>("mulletaflix.user_data_cleanup.runs");
    private static readonly Histogram<long> DetachedEntriesPerRun = CleanupMeter.CreateHistogram<long>("mulletaflix.user_data_cleanup.entries.detached_per_run", "entries");
    private static readonly Histogram<long> ExpiredCandidatesPerRun = CleanupMeter.CreateHistogram<long>("mulletaflix.user_data_cleanup.entries.expired_candidates_per_run", "entries");
    private static readonly Histogram<long> DeletedEntriesPerRun = CleanupMeter.CreateHistogram<long>("mulletaflix.user_data_cleanup.entries.deleted_per_run", "entries");
    private static readonly Histogram<double> Duration = CleanupMeter.CreateHistogram<double>("mulletaflix.user_data_cleanup.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = CleanupMeter.CreateUpDownCounter<long>("mulletaflix.user_data_cleanup.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds, long detachedEntries, long expiredCandidates, long deletedEntries)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        DetachedEntriesPerRun.Record(detachedEntries);
        ExpiredCandidatesPerRun.Record(expiredCandidates);
        DeletedEntriesPerRun.Record(deletedEntries);
    }
}
