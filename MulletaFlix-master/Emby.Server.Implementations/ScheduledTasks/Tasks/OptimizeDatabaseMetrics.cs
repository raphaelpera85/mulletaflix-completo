using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled database optimization.
/// </summary>
public static class OptimizeDatabaseMetrics
{
    public const string MeterName = "MulletaFlix.Server.DatabaseOptimization";

    private static readonly Meter OptimizationMeter = new(MeterName);
    private static readonly Counter<long> Runs = OptimizationMeter.CreateCounter<long>("mulletaflix.database_optimization.runs");
    private static readonly Histogram<double> Duration = OptimizationMeter.CreateHistogram<double>("mulletaflix.database_optimization.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = OptimizationMeter.CreateUpDownCounter<long>("mulletaflix.database_optimization.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
    }
}
