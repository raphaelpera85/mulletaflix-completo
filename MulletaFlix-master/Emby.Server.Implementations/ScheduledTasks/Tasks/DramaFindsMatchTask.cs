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
using MediaBrowser.Providers.Plugins.DramaFinds;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Enums;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks
{
    /// <summary>
    /// Identifies the library series that came from DramaFinds.
    /// </summary>
    /// <remarks>
    /// DramaFinds content has no TMDb, TVDB or MyDramaList entry, so it stays unidentified unless
    /// something resolves it by title. Unlike the DramaBox equivalent this task has no crawl phase:
    /// DramaFinds exposes a real search endpoint, so each candidate is resolved with one request
    /// against the title the library already holds. Only series with no provider ids are touched,
    /// and only matches that clear the automatic threshold are queued for a refresh, so a run
    /// converges and never rewrites an identity another provider established.
    /// </remarks>
    public class DramaFindsMatchTask : IScheduledTask
    {
        /// <summary>
        /// Minimum similarity for an automatic match. Same value the DramaBox task uses, and chosen
        /// the same way: the genuine partial-title case measures 0.9357 while the unrelated hits the
        /// fuzzy search endpoint returns for the same query measure 0.25 and 0.2857.
        /// </summary>
        private const double AutoMatchThreshold = 0.92;

        /// <summary>
        /// How many search rows to ask for per series. The endpoint is fuzzy by substring, so the
        /// rows next to the real title are neighbours rather than candidates, and a larger page
        /// only costs bandwidth.
        /// </summary>
        private const int SearchResultsPerSeries = 5;

        private const int PageSize = 200;

        private readonly DramaFindsClient _dramaFindsClient;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<DramaFindsMatchTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsMatchTask"/> class.
        /// </summary>
        /// <param name="dramaFindsClient">The DramaFinds client.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="providerManager">The provider manager, used to queue refreshes.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="logger">The logger.</param>
        public DramaFindsMatchTask(
            DramaFindsClient dramaFindsClient,
            ILibraryManager libraryManager,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILogger<DramaFindsMatchTask> logger)
        {
            _dramaFindsClient = dramaFindsClient;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "DramaFinds: identificar séries";

        /// <inheritdoc />
        public string Description => "Consulta o catálogo do DramaFinds pelo título de cada série ainda não identificada da biblioteca e identifica automaticamente as que vieram da plataforma.";

        /// <inheritdoc />
        public string Category => "Biblioteca";

        /// <inheritdoc />
        public string Key => "DramaFindsMatch";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // The catalogue grows daily, and a run only queries the series that are still
            // unidentified, so after the first pass a daily refresh costs a handful of requests.
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(1).Ticks
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

                if (_dramaFindsClient.IsInFailureCooldown)
                {
                    // The platform refused a request, so every remaining search would be answered
                    // from the cooldown without a round trip. Stop instead of walking the rest of
                    // the library for nothing.
                    _logger.LogWarning(
                        "DramaFinds is in failure cooldown; stopping the match run early after {Scanned} series",
                        scanned);
                    break;
                }

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
                        // Only ever touch a series nothing else has identified. The DramaFinds
                        // provider replaces the series name with the platform title, so matching a
                        // series that already has, say, a TMDb identity and a coincidentally equal
                        // title would silently rewrite the name of an unrelated show.
                        if (item.ProviderIds is { Count: > 0 })
                        {
                            continue;
                        }

                        var matches = await _dramaFindsClient
                            .SearchAsync(item.Name, SearchResultsPerSeries, cancellationToken)
                            .ConfigureAwait(false);

                        // The candidates arrive ranked, so the first one over the threshold is the
                        // best one. Anything the fuzzy search returned for a shared word stays in
                        // the list and is discarded here.
                        var match = matches.FirstOrDefault(candidate => candidate.Score >= AutoMatchThreshold);
                        if (match is null)
                        {
                            continue;
                        }

                        // The platform keeps duplicate ids for one drama. The ranking is
                        // deterministic, so the choice is stable, but it is logged because picking
                        // silently between equally named editions is how two series end up sharing
                        // one identity.
                        if (matches.Count > 1 && matches[1].Score >= AutoMatchThreshold)
                        {
                            _logger.LogInformation(
                                "DramaFinds holds {Count} equally strong editions of '{LibraryName}'; chose drama {DramaId} (released {ReleaseDate}) as the most recent",
                                matches.Count,
                                item.Name,
                                match.Drama.DramaId,
                                match.Drama.ReleaseDate);
                        }

                        _logger.LogInformation(
                            "DramaFinds matched '{LibraryName}' to '{DramaFindsName}' (id {DramaId}, score {Score})",
                            item.Name,
                            match.Drama.Title,
                            match.Drama.DramaId,
                            match.Score);

                        // Identify through the refresh options instead of writing the provider id
                        // here. Setting the id and calling UpdateToRepositoryAsync persisted the
                        // item straight from a task context and hit, on every single series,
                        // "The instance of entity type 'BaseItemEntity' cannot be tracked because
                        // another instance with the same key value for {'Id'} is already being
                        // tracked", thrown from AddRange inside the persistence service: the task
                        // then matched nothing at all. Passing the match as SearchResult is the path
                        // the Identify dialog already uses — the metadata service applies the
                        // provider ids itself (MetadataService.ApplySearchResult), so no direct save
                        // is needed and the whole class of tracking conflicts disappears.
                        var searchResult = new RemoteSearchResult
                        {
                            Name = match.Drama.Title,
                            ImageUrl = match.Drama.Cover,
                            Overview = match.Drama.Overview
                        };
                        searchResult.SetProviderId(DramaFindsSeriesProvider.ProviderKey, match.Drama.DramaId);

                        _providerManager.QueueRefresh(
                            item.Id,
                            new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                            {
                                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                                ReplaceAllMetadata = true,
                                ReplaceAllImages = true,
                                IsAutomated = true,
                                SearchResult = searchResult
                            },
                            RefreshPriority.Normal);

                        matched++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DramaFinds could not process series {ItemId} {ItemName}", item.Id, item.Name);
                    }
                }

                startIndex += page.Count;
                progress.Report(Math.Min(1.0, (double)startIndex / Math.Max(1, total)));
            }

            progress.Report(1);
            _logger.LogInformation(
                "DramaFinds match finished: {Scanned} series scanned, {Matched} identified",
                scanned,
                matched);
        }
    }
}
