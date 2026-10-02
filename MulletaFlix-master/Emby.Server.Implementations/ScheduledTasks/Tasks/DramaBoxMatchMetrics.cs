using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for DramaBox catalog indexing and library matching.
/// </summary>
public static class DramaBoxMatchMetrics
{
    public const string MeterName = "MulletaFlix.Server.DramaBoxMatch";

    private static readonly Meter MatchMeter = new(MeterName);
    private static readonly Counter<long> Runs = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.runs");
    private static readonly Counter<long> CatalogBooks = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.catalog.books");
    private static readonly Counter<long> ScannedSeries = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.series.scanned");
    private static readonly Counter<long> Candidates = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.series.candidates");
    private static readonly Counter<long> Matches = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.series.matched");
    private static readonly Counter<long> FailedSeries = MatchMeter.CreateCounter<long>("mulletaflix.dramabox_match.series.failed");
    private static readonly Histogram<double> Duration = MatchMeter.CreateHistogram<double>("mulletaflix.dramabox_match.duration", "s");
    private static readonly Histogram<double> IndexDuration = MatchMeter.CreateHistogram<double>("mulletaflix.dramabox_match.index.duration", "s");
    private static readonly Histogram<double> LibraryMatchDuration = MatchMeter.CreateHistogram<double>("mulletaflix.dramabox_match.library_match.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = MatchMeter.CreateUpDownCounter<long>("mulletaflix.dramabox_match.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordCatalogBooks(long count) => CatalogBooks.Add(count);

    public static void RecordIndexDuration(double durationSeconds) => IndexDuration.Record(durationSeconds);

    public static void RecordMatchDuration(double durationSeconds) => LibraryMatchDuration.Record(durationSeconds);

    public static void RecordRun(string result, double durationSeconds, long scanned, long candidates, long matches, long failures)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedSeries.Add(scanned);
        Candidates.Add(candidates);
        Matches.Add(matches);
        FailedSeries.Add(failures);
    }
}
