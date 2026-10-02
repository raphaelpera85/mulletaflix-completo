using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Aggregate metrics for scheduled chapter image refreshes.
/// </summary>
public static class ChapterImagesMetrics
{
    public const string MeterName = "MulletaFlix.Server.ChapterImages";

    private static readonly Meter ChapterImagesMeter = new(MeterName);
    private static readonly Counter<long> Runs = ChapterImagesMeter.CreateCounter<long>("mulletaflix.chapter_images.runs");
    private static readonly Histogram<long> TotalVideosPerRun = ChapterImagesMeter.CreateHistogram<long>("mulletaflix.chapter_images.videos.total_per_run", "videos");
    private static readonly Counter<long> ScannedVideos = ChapterImagesMeter.CreateCounter<long>("mulletaflix.chapter_images.videos.scanned");
    private static readonly Counter<long> ProcessedVideos = ChapterImagesMeter.CreateCounter<long>("mulletaflix.chapter_images.videos.processed");
    private static readonly Counter<long> FailedVideos = ChapterImagesMeter.CreateCounter<long>("mulletaflix.chapter_images.videos.failed");
    private static readonly Histogram<double> Duration = ChapterImagesMeter.CreateHistogram<double>("mulletaflix.chapter_images.duration", "s");
    private static readonly UpDownCounter<long> ActiveRuns = ChapterImagesMeter.CreateUpDownCounter<long>("mulletaflix.chapter_images.active_runs");

    public static void RecordActive(long delta) => ActiveRuns.Add(delta);

    public static void RecordRun(
        string result,
        double durationSeconds,
        long totalVideos,
        long scannedVideos,
        long processedVideos,
        long failedVideos)
    {
        var tags = new TagList { { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        TotalVideosPerRun.Record(totalVideos);
        ScannedVideos.Add(scannedVideos);
        ProcessedVideos.Add(processedVideos);
        FailedVideos.Add(failedVideos);
    }
}
