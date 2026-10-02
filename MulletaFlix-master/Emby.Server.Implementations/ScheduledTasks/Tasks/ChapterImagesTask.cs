using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Class ChapterImagesTask.
/// </summary>
public class ChapterImagesTask : IScheduledTask
{
    private readonly ILogger<ChapterImagesTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IApplicationPaths _appPaths;
    private readonly IChapterManager _chapterManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILocalizationManager _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterImagesTask" /> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="chapterManager">Instance of the <see cref="IChapterManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    public ChapterImagesTask(
        ILogger<ChapterImagesTask> logger,
        ILibraryManager libraryManager,
        IApplicationPaths appPaths,
        IChapterManager chapterManager,
        IFileSystem fileSystem,
        ILocalizationManager localization)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _appPaths = appPaths;
        _chapterManager = chapterManager;
        _fileSystem = fileSystem;
        _localization = localization;
    }

    /// <inheritdoc />
    public string Name => _localization.GetLocalizedString("TaskRefreshChapterImages");

    /// <inheritdoc />
    public string Description => _localization.GetLocalizedString("TaskRefreshChapterImagesDescription");

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc />
    public string Key => "RefreshChapterImages";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(2).Ticks,
            MaxRuntimeTicks = TimeSpan.FromHours(4).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "success";
        long totalVideos = 0;
        long scannedVideos = 0;
        long processedVideos = 0;
        long failedVideos = 0;
        ChapterImagesMetrics.RecordActive(1);

        try
        {
            const int PageSize = 100;
            var query = new InternalItemsQuery
            {
                MediaTypes = [MediaType.Video],
                IsFolder = false,
                Recursive = true,
                DtoOptions = new DtoOptions(false)
                {
                    EnableImages = false
                },
                SourceTypes = [SourceType.Library],
                IsVirtualItem = false,
                Limit = PageSize
            };

            // Count first, then page. This used to load every Video entity at once — 77,909 items on
            // this library — and that peak was one of the largest contributors to the host paging hard
            // enough to freeze the whole process for 50-100 seconds at a time (measured: 26 log silences
            // above 20 s, worst 102.6 s, with only ~1.9 GB of RAM free). The sibling tasks
            // (MediaSegmentExtractionTask, TrickplayImagesTask) already page this way.
            var numberOfVideos = _libraryManager.GetCount(query);
            totalVideos = numberOfVideos;
            if (numberOfVideos == 0)
            {
                result = "no_items";
            }

            var numComplete = 0;

            var failHistoryPath = Path.Combine(_appPaths.CachePath, "chapter-failures.txt");

            // HashSet, not List: this was an O(n) scan per item over a collection that grows with every
            // failure.
            HashSet<string> previouslyFailedImages;

            if (File.Exists(failHistoryPath))
            {
                try
                {
                    previouslyFailedImages = new HashSet<string>(
                        (await File.ReadAllTextAsync(failHistoryPath, cancellationToken).ConfigureAwait(false))
                            .Split('|', StringSplitOptions.RemoveEmptyEntries),
                        StringComparer.OrdinalIgnoreCase);
                }
                catch (IOException)
                {
                    previouslyFailedImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                previouslyFailedImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var directoryService = new DirectoryService(_fileSystem);

            var startIndex = 0;
            while (startIndex < numberOfVideos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                query.StartIndex = startIndex;
                var videos = _libraryManager.GetItemList(query).OfType<Video>().ToList();
                if (videos.Count == 0)
                {
                    break;
                }

                scannedVideos += videos.Count;

                foreach (var video in videos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var key = video.Path + video.DateModified.Ticks;

                    var extract = !previouslyFailedImages.Contains(key);

                    try
                    {
                        var chapters = _chapterManager.GetChapters(video.Id);

                        var success = await _chapterManager.RefreshChapterImages(video, directoryService, chapters, extract, true, cancellationToken).ConfigureAwait(false);

                        if (!success)
                        {
                            previouslyFailedImages.Add(key);

                            var parentPath = Path.GetDirectoryName(failHistoryPath);
                            if (parentPath is not null)
                            {
                                Directory.CreateDirectory(parentPath);
                            }

                            string text = string.Join('|', previouslyFailedImages);
                            await File.WriteAllTextAsync(failHistoryPath, text, cancellationToken).ConfigureAwait(false);
                            failedVideos++;
                        }
                        else
                        {
                            processedVideos++;
                        }

                        numComplete++;
                        double percent = numberOfVideos == 0 ? 100 : (double)numComplete / numberOfVideos;

                        progress.Report(100 * percent);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (ObjectDisposedException ex)
                    {
                        failedVideos++;
                        result = processedVideos > 0 ? "partial_failure" : "failure";
                        // TODO Investigate and properly fix.
                        _logger.LogError(ex, "Object Disposed");
                        return;
                    }
                    catch
                    {
                        failedVideos++;
                        throw;
                    }
                }

                startIndex += PageSize;
            }

            if (failedVideos > 0)
            {
                result = "partial_failure";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = "cancelled";
            throw;
        }
        catch
        {
            result = "failure";
            throw;
        }
        finally
        {
            ChapterImagesMetrics.RecordRun(
                result,
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                totalVideos,
                scannedVideos,
                processedVideos,
                failedVideos);
            ChapterImagesMetrics.RecordActive(-1);
        }
    }
}
