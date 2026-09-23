using System;
using System.Collections.Generic;
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
using MediaBrowser.Providers.Plugins.DramaBox;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Enums;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks
{
    /// <summary>
    /// Crawls the DramaBox catalogue and identifies the library series that came from the platform.
    /// </summary>
    /// <remarks>
    /// DramaBox content has no TMDb, TVDB or MyDramaList entry, so it stays unidentified: before
    /// this task the library held series with zero provider ids and zero images. The task works in
    /// two phases because DramaBox exposes no name search — phase one crawls the public category
    /// listings into a local index, phase two matches each unidentified series against that index
    /// and queues a refresh for the confident matches only.
    /// </remarks>
    public class DramaBoxMatchTask : IScheduledTask
    {
        /// <summary>
        /// Minimum similarity for an automatic match. Chosen from measured behaviour: the exact
        /// real case "3.2.1, Adeus e Ponto Final" scores 0.9357 against the DramaBox title once the
        /// separator characters are normalized, while unrelated titles stay well below 0.5.
        /// </summary>
        private const double AutoMatchThreshold = 0.92;

        private const int PageSize = 200;

        private readonly DramaBoxClient _dramaBoxClient;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<DramaBoxMatchTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxMatchTask"/> class.
        /// </summary>
        /// <param name="dramaBoxClient">The DramaBox client.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="providerManager">The provider manager, used to queue refreshes.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="logger">The logger.</param>
        public DramaBoxMatchTask(
            DramaBoxClient dramaBoxClient,
            ILibraryManager libraryManager,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILogger<DramaBoxMatchTask> logger)
        {
            _dramaBoxClient = dramaBoxClient;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "DramaBox: indexar catálogo e identificar séries";

        /// <inheritdoc />
        public string Description => "Baixa o catálogo do DramaBox (títulos, capas, sinopses, gêneros e episódios) e identifica automaticamente as séries da biblioteca que vieram da plataforma.";

        /// <inheritdoc />
        public string Category => "Biblioteca";

        /// <inheritdoc />
        public string Key => "DramaBoxIndexAndMatch";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // The catalogue grows daily and the crawl is 111 requests, so a daily refresh is cheap.
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

            var indexProgress = new Progress<double>(value => progress.Report(value * 0.5));
            var index = await _dramaBoxClient.RebuildIndexAsync(indexProgress, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("DramaBox index holds {Count} books; starting library match", index.Books.Count);

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
                        // Only ever touch a series nothing else has identified. The DramaBox provider
                        // replaces the series name with the platform title, so matching a series that
                        // already has, say, a TMDb identity and a coincidentally equal title would
                        // silently rewrite the name of an unrelated show.
                        if (item.ProviderIds is { Count: > 0 })
                        {
                            continue;
                        }

                        var match = await _dramaBoxClient.MatchByTitleAsync(item.Name, AutoMatchThreshold, cancellationToken).ConfigureAwait(false);
                        if (match is null)
                        {
                            continue;
                        }

                        _logger.LogInformation(
                            "DramaBox matched '{LibraryName}' to '{DramaBoxName}' (id {BookId}, score {Score})",
                            item.Name,
                            match.Book.Name,
                            match.Book.BookId,
                            match.Score);

                        // Identify through the refresh options instead of writing the provider id here.
                        // Setting the id and calling UpdateToRepositoryAsync persisted the item straight
                        // from a task context and hit, on every single series,
                        // "The instance of entity type 'BaseItemEntity' cannot be tracked because another
                        // instance with the same key value for {'Id'} is already being tracked", thrown
                        // from AddRange inside the persistence service. The task then matched nothing at
                        // all: 71 series logged as errors in one run. Passing the match as SearchResult
                        // is the path the Identify dialog already uses — the metadata service applies
                        // the provider ids itself (MetadataService.ApplySearchResult), so no direct save
                        // is needed and the whole class of tracking conflicts disappears.
                        var searchResult = new RemoteSearchResult
                        {
                            Name = match.Book.Name,
                            ImageUrl = DramaBoxTitleMatcher.BuildCoverUrl(match.Book.Cover, 360, 640),
                            Overview = match.Book.Overview
                        };
                        searchResult.SetProviderId(DramaBoxSeriesProvider.ProviderKey, match.Book.BookId);

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
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DramaBox could not process series {ItemId} {ItemName}", item.Id, item.Name);
                    }
                }

                startIndex += page.Count;
                progress.Report(0.5 + (0.5 * Math.Min(1.0, (double)startIndex / Math.Max(1, total))));
            }

            progress.Report(1);
            _logger.LogInformation(
                "DramaBox match finished: {Scanned} series scanned, {Matched} identified",
                scanned,
                matched);
        }
    }
}
