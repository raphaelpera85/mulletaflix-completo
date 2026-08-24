using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Unified Search Controller - provides comprehensive search across all content types.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Route("Search")]
public class UnifiedSearchController : BaseMulletaFlixApiController
{
    private readonly ILibraryManager _libraryManager;
    private readonly ISearchEngine _searchEngine;
    private readonly IDtoService _dtoService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IUserManager _userManager;

    public UnifiedSearchController(
        ILibraryManager libraryManager,
        ISearchEngine searchEngine,
        IDtoService dtoService,
        IImageProcessor imageProcessor,
        IUserManager userManager)
    {
        _libraryManager = libraryManager;
        _searchEngine = searchEngine;
        _dtoService = dtoService;
        _imageProcessor = imageProcessor;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets unified search results across all content types.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="userId">Optional user ID to scope search.</param>
    /// <param name="parentId">Optional parent ID to scope search.</param>
    /// <param name="collectionType">Optional collection type filter.</param>
    /// <param name="includeItemTypes">Optional item types to include.</param>
    /// <param name="excludeItemTypes">Optional item types to exclude.</param>
    /// <param name="mediaTypes">Optional media types filter.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="startIndex">Starting index for pagination.</param>
    /// <param name="includePeople">Include people in results.</param>
    /// <param name="includeMedia">Include media items in results.</param>
    /// <param name="includeGenres">Include genres in results.</param>
    /// <param name="includeStudios">Include studios in results.</param>
    /// <param name="includeArtists">Include artists in results.</param>
    /// <param name="sortBy">Sort fields.</param>
    /// <param name="sortOrder">Sort order.</param>
    /// <response code="200">Unified search results returned.</response>
    [HttpGet("Unified")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UnifiedSearchResult> GetUnifiedSearch(
        [FromQuery, Required] string searchTerm,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? parentId,
        [FromQuery] CollectionType? collectionType,
        [FromQuery] BaseItemKind[]? includeItemTypes,
        [FromQuery] BaseItemKind[]? excludeItemTypes,
        [FromQuery] MediaType[]? mediaTypes,
        [FromQuery] int? limit,
        [FromQuery] int? startIndex,
        [FromQuery] ItemSortBy[]? sortBy,
        [FromQuery] MulletaFlix.Database.Implementations.Enums.SortOrder sortOrder = MulletaFlix.Database.Implementations.Enums.SortOrder.Ascending,
        [FromQuery] bool includePeople = true,
        [FromQuery] bool includeMedia = true,
        [FromQuery] bool includeGenres = true,
        [FromQuery] bool includeStudios = true,
        [FromQuery] bool includeArtists = true)
    {
        userId = RequestHelpers.GetUserId(User, userId);

        var searchQuery = new SearchQuery
        {
            Limit = limit,
            SearchTerm = searchTerm,
            StartIndex = startIndex,
            UserId = userId.Value,
            IncludeItemTypes = includeItemTypes,
            ExcludeItemTypes = excludeItemTypes,
            MediaTypes = mediaTypes,
            ParentId = parentId,
            IncludeArtists = includeArtists,
            IncludeGenres = includeGenres,
            IncludeMedia = includeMedia,
            IncludePeople = includePeople,
            IncludeStudios = includeStudios
        };

        var searchResult = _searchEngine.GetSearchHints(searchQuery);

        // Also get detailed items for the search term
        var user = _userManager.GetUserById(userId!.Value);
        var itemsQuery = new InternalItemsQuery(user)
        {
            SearchTerm = searchTerm,
            Limit = limit ?? 100,
            StartIndex = startIndex,
            IncludeItemTypes = includeItemTypes,
            ExcludeItemTypes = excludeItemTypes,
            MediaTypes = mediaTypes,
            ParentId = parentId ?? Guid.Empty,
            OrderBy = sortBy != null && sortBy.Length > 0
                ? sortBy.Select<ItemSortBy, (ItemSortBy OrderBy, MulletaFlix.Database.Implementations.Enums.SortOrder SortOrder)>(s => (s, sortOrder)).ToArray()
                : [(ItemSortBy.SortName, sortOrder)],
            Recursive = true
        };

        var itemsResult = _libraryManager.GetItemList(itemsQuery);

        var sections = new List<UnifiedSearchSection>();

        // Group items by type
        var itemsByType = itemsResult
            .GroupBy(i => i.GetBaseItemKind())
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in itemsByType.OrderBy(x => x.Key.ToString()))
        {
            var typeName = kvp.Key.ToString();
            var items = kvp.Value.Select(GetSearchHintResult).ToArray();

            sections.Add(new UnifiedSearchSection
            {
                Name = typeName,
                Items = items,
                CardOptions = GetCardOptionsForType(kvp.Key)
            });
        }

        // Add search hints if available
        if (searchResult.Items.Any())
        {
            var hintItems = searchResult.Items
                .Select(h => GetSearchHintResult(h.Item))
                .Where(h => !sections.Any(s => s.Items.Any(i => i.Id == h.Id)))
                .ToArray();

            if (hintItems.Length > 0)
            {
                sections.Insert(0, new UnifiedSearchSection
                {
                    Name = "Suggestions",
                    Items = hintItems,
                    CardOptions = new { showQuickView = true }
                });
            }
        }

        return Ok(new UnifiedSearchResult
        {
            Items = sections.SelectMany(s => s.Items).ToArray(),
            TotalRecordCount = itemsResult.Count,
            Sections = sections.ToArray()
        });
    }

    /// <summary>
    /// Gets search statistics for the dashboard.
    /// </summary>
    /// <param name="userId">User ID to scope stats.</param>
    /// <response code="200">Search statistics returned.</response>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SearchStatsDto> GetSearchStats(
        [FromQuery, Required] Guid userId)
    {
        userId = RequestHelpers.GetUserId(User, userId);

        var user = _userManager.GetUserById(userId);
        var itemsQuery = new InternalItemsQuery(user)
        {
            Recursive = true,
            EnableTotalRecordCount = true
        };

        var items = _libraryManager.GetItemList(itemsQuery);

        var stats = new SearchStatsDto
        {
            TotalMovies = items.Count(i => i is Movie),
            TotalSeries = items.Count(i => i is Series),
            TotalEpisodes = items.Count(i => i is Episode),
            TotalArtists = items.Count(i => i is MusicArtist),
            TotalAlbums = items.Count(i => i is MusicAlbum),
            TotalSongs = items.Count(i => i is Audio),
            TotalChannels = items.Count(i => i.ChannelId != Guid.Empty),
            TotalPrograms = items.Count(i => i is LiveTvProgram)
        };

        return Ok(stats);
    }

    private SearchHint GetSearchHintResult(BaseItem item)
    {
        var result = new SearchHint
        {
            Name = item.Name,
            IndexNumber = item.IndexNumber,
            ParentIndexNumber = item.ParentIndexNumber,
            Id = item.Id,
            Type = item.GetBaseItemKind(),
            MediaType = item.MediaType,
            RunTimeTicks = item.RunTimeTicks,
            ProductionYear = item.ProductionYear,
            ChannelId = item.ChannelId,
            EndDate = item.EndDate
        };

#pragma warning disable CS0618
        result.ItemId = result.Id;
#pragma warning restore CS0618

        if (item.IsFolder)
        {
            result.IsFolder = true;
        }

        var primaryTag = _imageProcessor.GetImageCacheTag(item, ImageType.Primary);
        if (primaryTag is not null)
        {
            result.PrimaryImageTag = primaryTag;
            result.PrimaryImageAspectRatio = _dtoService.GetPrimaryImageAspectRatio(item);
        }

        var thumbItem = item.HasImage(ImageType.Thumb) ? item : null;
        if (thumbItem is null && item is Episode)
        {
            thumbItem = item.GetParents().OfType<Series>().FirstOrDefault(i => i.HasImage(ImageType.Thumb));
        }
        thumbItem ??= item.GetParents().OfType<BaseItem>().FirstOrDefault(i => i.HasImage(ImageType.Thumb));
        if (thumbItem is not null)
        {
            var thumbTag = _imageProcessor.GetImageCacheTag(thumbItem, ImageType.Thumb);
            if (thumbTag is not null)
            {
                result.ThumbImageTag = thumbTag;
                result.ThumbImageItemId = thumbItem.Id.ToString("N");
            }
        }

        var backdropItem = item.HasImage(ImageType.Backdrop) ? item : item.GetParents().OfType<BaseItem>().FirstOrDefault(i => i.HasImage(ImageType.Backdrop));
        if (backdropItem is not null)
        {
            var backdropTag = _imageProcessor.GetImageCacheTag(backdropItem, ImageType.Backdrop);
            if (backdropTag is not null)
            {
                result.BackdropImageTag = backdropTag;
                result.BackdropImageItemId = backdropItem.Id.ToString("N");
            }
        }

        switch (item)
        {
            case IHasSeries hasSeries:
                result.Series = hasSeries.SeriesName;
                break;
            case LiveTvProgram program:
                result.StartDate = program.StartDate;
                break;
            case Series series:
                if (series.Status.HasValue)
                {
                    result.Status = series.Status.Value.ToString();
                }
                break;
            case MusicAlbum album:
                result.Artists = album.Artists;
                result.AlbumArtist = album.AlbumArtist;
                break;
            case Audio song:
                result.AlbumArtist = song.AlbumArtists?.FirstOrDefault();
                result.Artists = song.Artists;
                if (song.AlbumEntity is MusicAlbum musicAlbum)
                {
                    result.Album = musicAlbum.Name;
                    result.AlbumId = musicAlbum.Id;
                }
                else
                {
                    result.Album = song.Album;
                }
                break;
        }

        if (item.ChannelId != Guid.Empty)
        {
            var channel = _libraryManager.GetItemById<BaseItem>(item.ChannelId);
            result.ChannelName = channel?.Name;
        }

        return result;
    }

    private object GetCardOptionsForType(BaseItemKind type)
    {
        return type switch
        {
            BaseItemKind.Movie or BaseItemKind.Series => new { coverImage = true, showYear = true },
            BaseItemKind.Episode => new { showParentTitle = true },
            BaseItemKind.MusicArtist => new { coverImage = true, shape = "circle" },
            BaseItemKind.MusicAlbum => new { coverImage = true, showArtist = true },
            BaseItemKind.Audio => new { showAlbum = true, showArtist = true },
            BaseItemKind.LiveTvProgram => new { showChannel = true, showTime = true },
            _ => new { }
        };
    }
}

/// <summary>
/// Unified search result.
/// </summary>
public class UnifiedSearchResult
{
    public SearchHint[] Items { get; set; } = Array.Empty<SearchHint>();
    public int TotalRecordCount { get; set; }
    public UnifiedSearchSection[] Sections { get; set; } = Array.Empty<UnifiedSearchSection>();
}

/// <summary>
/// Unified search section.
/// </summary>
public class UnifiedSearchSection
{
    public string Name { get; set; } = string.Empty;
    public SearchHint[] Items { get; set; } = Array.Empty<SearchHint>();
    public object? CardOptions { get; set; }
}

/// <summary>
/// Search statistics.
/// </summary>
public class SearchStatsDto
{
    public int TotalMovies { get; set; }
    public int TotalSeries { get; set; }
    public int TotalEpisodes { get; set; }
    public int TotalArtists { get; set; }
    public int TotalAlbums { get; set; }
    public int TotalSongs { get; set; }
    public int TotalChannels { get; set; }
    public int TotalPrograms { get; set; }
}