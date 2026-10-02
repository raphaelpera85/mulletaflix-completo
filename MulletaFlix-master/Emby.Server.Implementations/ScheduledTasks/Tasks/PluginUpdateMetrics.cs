using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled plugin updates.
/// </summary>
public static class PluginUpdateMetrics
{
    public const string MeterName = "MulletaFlix.Server.PluginUpdates";

    private static readonly Meter UpdateMeter = new(MeterName);
    private static readonly Counter<long> Runs = UpdateMeter.CreateCounter<long>("mulletaflix.plugin_updates.runs");
    private static readonly Histogram<long> AvailablePackagesPerRun = UpdateMeter.CreateHistogram<long>("mulletaflix.plugin_updates.packages.available_per_run", "packages");
    private static readonly Counter<long> AttemptedPackages = UpdateMeter.CreateCounter<long>("mulletaflix.plugin_updates.packages.attempted");
    private static readonly Counter<long> FailedPackages = UpdateMeter.CreateCounter<long>("mulletaflix.plugin_updates.packages.failed");
    private static readonly Histogram<double> Duration = UpdateMeter.CreateHistogram<double>("mulletaflix.plugin_updates.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = UpdateMeter.CreateUpDownCounter<long>("mulletaflix.plugin_updates.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(
        string result,
        double durationSeconds,
        long availablePackages,
        long attemptedPackages,
        long failedPackages)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        AvailablePackagesPerRun.Record(availablePackages);
        AttemptedPackages.Add(attemptedPackages);
        FailedPackages.Add(failedPackages);
        Duration.Record(durationSeconds, tags);
    }
}
