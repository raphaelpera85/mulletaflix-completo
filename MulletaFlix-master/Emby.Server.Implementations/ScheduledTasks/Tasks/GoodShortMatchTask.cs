using System;
using System.Collections.Generic;
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
using MediaBrowser.Providers.Plugins.GoodShort;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Enums;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Identifica automaticamente séries de dramas curtos que existem no GoodShort.
/// </summary>
public sealed class GoodShortMatchTask : IScheduledTask
{
    private const double AutoMatchThreshold = 0.92;
    private const int PageSize = 200;
    private readonly GoodShortClient _client;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<GoodShortMatchTask> _logger;

    public GoodShortMatchTask(
        GoodShortClient client,
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger<GoodShortMatchTask> logger)
    {
        _client = client;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string Name => "GoodShort: identificar dramas curtos";

    public string Description => "Procura no GoodShort séries ainda não identificadas e agenda o preenchimento automático de título, capa, episódios e metadados.";

    public string Category => "Biblioteca";

    public string Key => "GoodShortMatch";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromDays(1).Ticks
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            Recursive = true,
            IsVirtualItem = false,
            Limit = PageSize,
            DtoOptions = new DtoOptions
            {
                EnableImages = false,
                Fields = new[] { ItemFields.ProviderIds }
            }
        };

        var startIndex = 0;
        var scanned = 0;
        var matched = 0;
        var total = _libraryManager.GetCount(query);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            query.StartIndex = startIndex;
            var page = _libraryManager.GetItemList(query);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var item in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;
                try
                {
                    if (item.ProviderIds is { Count: > 0 })
                    {
                        continue;
                    }

                    var matches = await _client.SearchAsync(item.Name, 5, cancellationToken).ConfigureAwait(false);
                    var match = matches.FirstOrDefault(candidate => candidate.Score >= AutoMatchThreshold);
                    if (match is null)
                    {
                        continue;
                    }

                    var searchResult = new RemoteSearchResult
                    {
                        Name = match.Series.Name,
                        ImageUrl = match.Series.Cover,
                        Overview = match.Series.Overview,
                        PremiereDate = match.Series.PremiereDate
                    };
                    searchResult.SetProviderId(GoodShortSeriesProvider.ProviderKey, match.Series.SeriesId);

                    _providerManager.QueueRefresh(
                        item.Id,
                        new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                        {
                            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                            ReplaceAllImages = false,
                            IsAutomated = true,
                            SearchResult = searchResult
                        },
                        RefreshPriority.Normal);

                    matched++;
                    _logger.LogInformation(
                        "GoodShort matched '{LibraryName}' to '{GoodShortName}' (id {SeriesId}, score {Score})",
                        item.Name,
                        match.Series.Name,
                        match.Series.SeriesId,
                        match.Score);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GoodShort could not process series {ItemId} {ItemName}", item.Id, item.Name);
                }
            }

            startIndex += page.Count;
            progress.Report(Math.Min(1.0, (double)startIndex / Math.Max(1, total)));
        }

        progress.Report(1);
        _logger.LogInformation("GoodShort match finished: {Scanned} series scanned, {Matched} identified", scanned, matched);
    }
}
