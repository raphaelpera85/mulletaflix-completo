using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled media segment extraction.
/// </summary>
public static class MediaSegmentExtractionMetrics
{
    public const string MeterName = "MulletaFlix.Server.MediaSegmentExtraction";

    private static readonly Meter ExtractionMeter = new(MeterName);
    private static readonly Counter<long> Runs = ExtractionMeter.CreateCounter<long>("mulletaflix.segment_extraction.runs");
    private static readonly Counter<long> ScannedItems = ExtractionMeter.CreateCounter<long>("mulletaflix.segment_extraction.items.scanned");
    private static readonly Counter<long> ProcessedItems = ExtractionMeter.CreateCounter<long>("mulletaflix.segment_extraction.items.processed");
    private static readonly Counter<long> SkippedItems = ExtractionMeter.CreateCounter<long>("mulletaflix.segment_extraction.items.skipped");
    private static readonly Histogram<double> Duration = ExtractionMeter.CreateHistogram<double>("mulletaflix.segment_extraction.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = ExtractionMeter.CreateUpDownCounter<long>("mulletaflix.segment_extraction.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(string result, double durationSeconds, long scanned, long processed, long skipped)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedItems.Add(scanned);
        ProcessedItems.Add(processed);
        SkippedItems.Add(skipped);
    }
}
