using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Providers.Plugins.ShortMax;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Enums;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

public sealed class ShortMaxMatchTask : IScheduledTask
{
    private const double AutoMatchThreshold = 0.92;
    private readonly ShortMaxClient _client;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ShortMaxMatchTask> _logger;

    public ShortMaxMatchTask(ShortMaxClient client, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem, ILogger<ShortMaxMatchTask> logger)
    {
        _client = client;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string Name => "ShortMax: identificar dramas curtos";
    public string Description => "Procura no ShortMax séries ainda não identificadas e agenda capa, metadados e episódios.";
    public string Category => "Biblioteca";
    public string Key => "ShortMaxMatch";
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() { yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromDays(1).Ticks }; }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "success";
        long scanned = 0;
        long candidates = 0;
        long matched = 0;
        ShortMaxMatchMetrics.RecordActive(1);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                Recursive = true,
                IsVirtualItem = false,
                Limit = 200,
                DtoOptions = new DtoOptions { EnableImages = false, Fields = new[] { ItemFields.ProviderIds } }
            };
            var start = 0;
            var total = _libraryManager.GetCount(query);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                query.StartIndex = start;
                var page = _libraryManager.GetItemList(query);
                if (page.Count == 0)
                {
                    break;
                }

                foreach (var item in page)
                {
                    scanned++;
                    if (item.ProviderIds is { Count: > 0 })
                    {
                        continue;
                    }
                    candidates++;

                    var matches = await _client.SearchAsync(item.Name, 5, cancellationToken).ConfigureAwait(false);
                    var match = matches.FirstOrDefault(candidate => candidate.Score >= AutoMatchThreshold);
                    if (match is null)
                    {
                        continue;
                    }

                    var searchResult = new RemoteSearchResult { Name = match.Series.Name, ImageUrl = match.Series.Cover, Overview = match.Series.Overview };
                    searchResult.SetProviderId(ShortMaxSeriesProvider.ProviderKey, match.Series.SeriesId);
                    _providerManager.QueueRefresh(item.Id, new MetadataRefreshOptions(new DirectoryService(_fileSystem)) { MetadataRefreshMode = MetadataRefreshMode.FullRefresh, ImageRefreshMode = MetadataRefreshMode.FullRefresh, ReplaceAllMetadata = true, ReplaceAllImages = true, IsAutomated = true, SearchResult = searchResult }, RefreshPriority.Normal);
                    matched++;
                    _logger.LogInformation("ShortMax matched '{LibraryName}' to '{ShortMaxName}' (id {SeriesId}, score {Score})", item.Name, match.Series.Name, match.Series.SeriesId, match.Score);
                }

                start += page.Count;
                progress.Report(Math.Min(1d, (double)start / Math.Max(1, total)));
            }

            result = scanned == 0 ? "no_items" : candidates == 0 ? "no_candidates" : "success";
            progress.Report(1d);
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
            ShortMaxMatchMetrics.RecordRun(result, Stopwatch.GetElapsedTime(startedAt).TotalSeconds, scanned, candidates, matched);
            ShortMaxMatchMetrics.RecordActive(-1);
        }
    }
}
