using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MediaBrowser.Providers.Lyric;

/// <summary>
/// Aggregate metrics for automated lyric discovery and download.
/// </summary>
public static class LyricScheduledTaskMetrics
{
    public const string MeterName = "MulletaFlix.Providers.LyricsScheduledTask";

    private static readonly Meter LyricMeter = new(MeterName);
    private static readonly Counter<long> Runs = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.runs");
    private static readonly Counter<long> ScannedItems = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.items.scanned");
    private static readonly Counter<long> SearchCandidates = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.items.missing");
    private static readonly Counter<long> Searches = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.searches");
    private static readonly Counter<long> DownloadAttempts = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.downloads");
    private static readonly Counter<long> Failures = LyricMeter.CreateCounter<long>("mulletaflix.lyrics.failures");
    private static readonly Histogram<double> Duration = LyricMeter.CreateHistogram<double>("mulletaflix.lyrics.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = LyricMeter.CreateUpDownCounter<long>("mulletaflix.lyrics.active_runs");

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
    /// <param name="scannedItems">Number of audio items scanned.</param>
    /// <param name="searchCandidates">Number of items without lyrics.</param>
    /// <param name="searches">Number of provider searches.</param>
    /// <param name="downloadAttempts">Number of lyric downloads attempted.</param>
    /// <param name="failures">Number of task or item failures.</param>
    public static void RecordRun(
        string result,
        double durationSeconds,
        long scannedItems,
        long searchCandidates,
        long searches,
        long downloadAttempts,
        long failures)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        ScannedItems.Add(scannedItems);
        SearchCandidates.Add(searchCandidates);
        Searches.Add(searches);
        DownloadAttempts.Add(downloadAttempts);
        Failures.Add(failures);
    }
}
