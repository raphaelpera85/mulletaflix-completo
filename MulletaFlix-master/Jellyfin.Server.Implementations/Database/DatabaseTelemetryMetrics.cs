using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Jellyfin.Server.Implementations.Database;

/// <summary>
/// Low-cardinality measurements for EF database commands and connection acquisition.
/// </summary>
public static class DatabaseTelemetryMetrics
{
    public const string MeterName = "MulletaFlix.Server.Database";

    private static readonly Meter DatabaseMeter = new(MeterName);
    private static readonly Histogram<double> CommandDuration = DatabaseMeter.CreateHistogram<double>("mulletaflix.database.command.duration", "s");
    private static readonly Counter<long> Commands = DatabaseMeter.CreateCounter<long>("mulletaflix.database.commands");
    private static readonly Histogram<double> ConnectionOpenDuration = DatabaseMeter.CreateHistogram<double>("mulletaflix.database.connection.open.duration", "s");
    private static readonly Counter<long> ConnectionOpens = DatabaseMeter.CreateCounter<long>("mulletaflix.database.connection.opens");

    internal static void RecordCommand(double durationSeconds, string operation, string context, string result)
    {
        var tags = new TagList
        {
            { "db.operation", operation },
            { "db.context", context },
            { "result", result }
        };

        CommandDuration.Record(durationSeconds, tags);
        Commands.Add(1, tags);
    }

    internal static void RecordConnectionOpen(double durationSeconds, string context, string result)
    {
        var tags = new TagList
        {
            { "db.context", context },
            { "result", result }
        };

        ConnectionOpenDuration.Record(durationSeconds, tags);
        ConnectionOpens.Add(1, tags);
    }
}
