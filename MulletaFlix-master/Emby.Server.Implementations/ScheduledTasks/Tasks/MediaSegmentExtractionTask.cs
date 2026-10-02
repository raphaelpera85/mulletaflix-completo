using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Task to obtain media segments.
/// </summary>
public class MediaSegmentExtractionTask : IScheduledTask
{
    /// <summary>
    /// The library manager.
    /// </summary>
    private readonly ILibraryManager _libraryManager;
    private readonly ILocalizationManager _localization;
    private readonly IMediaSegmentManager _mediaSegmentManager;
    private static readonly BaseItemKind[] _itemTypes = [BaseItemKind.Episode, BaseItemKind.Movie, BaseItemKind.Audio, BaseItemKind.AudioBook];

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSegmentExtractionTask" /> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="localization">The localization manager.</param>
    /// <param name="mediaSegmentManager">The segment manager.</param>
    public MediaSegmentExtractionTask(ILibraryManager libraryManager, ILocalizationManager localization, IMediaSegmentManager mediaSegmentManager)
    {
        _libraryManager = libraryManager;
        _localization = localization;
        _mediaSegmentManager = mediaSegmentManager;
    }

    /// <inheritdoc/>
    public string Name => _localization.GetLocalizedString("TaskExtractMediaSegments");

    /// <inheritdoc/>
    public string Description => _localization.GetLocalizedString("TaskExtractMediaSegmentsDescription");

    /// <inheritdoc/>
    public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc/>
    public string Key => "TaskExtractMediaSegments";

    /// <inheritdoc/>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "success";
        long scannedCount = 0;
        long processedCount = 0;
        long skippedCount = 0;
        MediaSegmentExtractionMetrics.RecordActive(1);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(0);

            const int pageSize = 100;

            var query = new InternalItemsQuery
            {
                MediaTypes = [MediaType.Video, MediaType.Audio],
                IsVirtualItem = false,
                IncludeItemTypes = _itemTypes,
                DtoOptions = new DtoOptions(true),
                SourceTypes = [SourceType.Library],
                Recursive = true,
                Limit = pageSize
            };

            var numberOfVideos = _libraryManager.GetCount(query);
            if (numberOfVideos == 0)
            {
                result = "no_items";
                progress.Report(100);
                return;
            }

            var startIndex = 0;
            var numComplete = 0;

            while (startIndex < numberOfVideos)
            {
                query.StartIndex = startIndex;

                var baseItems = _libraryManager.GetItemList(query);
                var currentPageCount = baseItems.Count;
                scannedCount += currentPageCount;
                // TODO parallelize with Parallel.ForEach?
                for (var i = 0; i < currentPageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = baseItems[i];
                    // Only local files supported
                    if (item.IsFileProtocol && File.Exists(item.Path))
                    {
                        var libraryOptions = _libraryManager.GetLibraryOptions(item);
                        await _mediaSegmentManager.RunSegmentPluginProviders(item, libraryOptions, false, cancellationToken).ConfigureAwait(false);
                        processedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }

                    // Update progress
                    numComplete++;
                    double percent = (double)numComplete / numberOfVideos;
                    progress.Report(100 * percent);
                }

                startIndex += pageSize;
            }

            progress.Report(100);
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
            MediaSegmentExtractionMetrics.RecordRun(
                result,
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                scannedCount,
                processedCount,
                skippedCount);
            MediaSegmentExtractionMetrics.RecordActive(-1);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(12).Ticks
        };
    }
}
