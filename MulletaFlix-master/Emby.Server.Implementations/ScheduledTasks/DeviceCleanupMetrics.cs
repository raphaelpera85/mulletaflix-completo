using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks;

/// <summary>
/// Aggregate metrics for scheduled cleanup of inactive devices.
/// </summary>
public static class DeviceCleanupMetrics
{
    public const string MeterName = "MulletaFlix.Server.DeviceCleanup";

    private static readonly Meter CleanupMeter = new(MeterName);
    private static readonly Counter<long> Runs = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.runs");
    private static readonly Counter<long> ActiveSessions = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.sessions.active");
    private static readonly Counter<long> ActiveDevices = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.devices.active");
    private static readonly Counter<long> ScannedDevices = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.devices.scanned");
    private static readonly Counter<long> KeptDevices = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.devices.kept");
    private static readonly Counter<long> DeletedDevices = CleanupMeter.CreateCounter<long>("mulletaflix.device_cleanup.devices.deleted");
    private static readonly Histogram<double> Duration = CleanupMeter.CreateHistogram<double>("mulletaflix.device_cleanup.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = CleanupMeter.CreateUpDownCounter<long>("mulletaflix.device_cleanup.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(
        string result,
        double durationSeconds,
        long sessions,
        long activeDevices,
        long scanned,
        long kept,
        long deleted)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ActiveSessions.Add(sessions);
        ActiveDevices.Add(activeDevices);
        ScannedDevices.Add(scanned);
        KeptDevices.Add(kept);
        DeletedDevices.Add(deleted);
    }
}
