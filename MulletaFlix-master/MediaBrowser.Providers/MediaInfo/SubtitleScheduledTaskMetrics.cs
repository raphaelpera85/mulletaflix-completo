using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MediaBrowser.Providers.MediaInfo;

/// <summary>
/// Aggregate metrics for automated subtitle discovery and download.
/// </summary>
public static class SubtitleScheduledTaskMetrics
{
    public const string MeterName = "MulletaFlix.Providers.SubtitlesScheduledTask";

    private static readonly Meter SubtitleMeter = new(MeterName);
    private static readonly Counter<long> Runs = SubtitleMeter.CreateCounter<long>("mulletaflix.subtitles.runs");
    private static readonly Counter<long> ScannedItems = SubtitleMeter.CreateCounter<long>("mulletaflix.subtitles.items.scanned");
    private static readonly Counter<long> Failures = SubtitleMeter.CreateCounter<long>("mulletaflix.subtitles.failures");
    private static readonly Histogram<double> Duration = SubtitleMeter.CreateHistogram<double>("mulletaflix.subtitles.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = SubtitleMeter.CreateUpDownCounter<long>("mulletaflix.subtitles.active_runs");

    /// <summary>
    /// Records the change in active task executions.
    /// </summary>
    /// <param name="delta">The change in active executions.</param>
    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    /// <summary>
    /// Records aggregate outcome and work counts for one run.
    /// </summary>
    /// <param name="result">A bounded result value.</param>
    /// <param name="durationSeconds">Elapsed execution time.</param>
    /// <param name="scannedItems">Number of video items scanned.</param>
    /// <param name="failures">Number of task or item failures.</param>
    public static void RecordRun(string result, double durationSeconds, long scannedItems, long failures)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedItems.Add(scannedItems);
        Failures.Add(failures);
    }
}
