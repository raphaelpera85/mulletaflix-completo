using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled user license expiration.
/// </summary>
public static class LicenseExpirationMetrics
{
    public const string MeterName = "MulletaFlix.Server.LicenseExpiration";

    private static readonly Meter ExpirationMeter = new(MeterName);
    private static readonly Counter<long> Runs = ExpirationMeter.CreateCounter<long>("mulletaflix.license_expiration.runs");
    private static readonly Histogram<long> DisabledUsersPerRun = ExpirationMeter.CreateHistogram<long>("mulletaflix.license_expiration.users.disabled_per_run", "users");
    private static readonly Histogram<double> Duration = ExpirationMeter.CreateHistogram<double>("mulletaflix.license_expiration.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = ExpirationMeter.CreateUpDownCounter<long>("mulletaflix.license_expiration.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds, long disabledUsers)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        DisabledUsersPerRun.Record(disabledUsers);
    }
}
