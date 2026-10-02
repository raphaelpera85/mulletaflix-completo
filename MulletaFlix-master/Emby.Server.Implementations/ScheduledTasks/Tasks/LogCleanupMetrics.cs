using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled log file cleanup.
/// </summary>
public static class LogCleanupMetrics
{
    public const string MeterName = "MulletaFlix.Server.LogCleanup";

    private static readonly Meter CleanupMeter = new(MeterName);
    private static readonly Counter<long> Runs = CleanupMeter.CreateCounter<long>("mulletaflix.log_cleanup.runs");
    private static readonly Counter<long> ScannedFiles = CleanupMeter.CreateCounter<long>("mulletaflix.log_cleanup.files.scanned");
    private static readonly Counter<long> ExpiredFiles = CleanupMeter.CreateCounter<long>("mulletaflix.log_cleanup.files.expired");
    private static readonly Counter<long> DeleteAttempts = CleanupMeter.CreateCounter<long>("mulletaflix.log_cleanup.files.delete_attempts");
    private static readonly Counter<long> DeleteFailures = CleanupMeter.CreateCounter<long>("mulletaflix.log_cleanup.files.delete_failures");
    private static readonly Histogram<double> Duration = CleanupMeter.CreateHistogram<double>("mulletaflix.log_cleanup.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = CleanupMeter.CreateUpDownCounter<long>("mulletaflix.log_cleanup.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(
        string result,
        double durationSeconds,
        long scanned,
        long expired,
        long deleteAttempts,
        long deleteFailures)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedFiles.Add(scanned);
        ExpiredFiles.Add(expired);
        DeleteAttempts.Add(deleteAttempts);
        DeleteFailures.Add(deleteFailures);
    }
}
