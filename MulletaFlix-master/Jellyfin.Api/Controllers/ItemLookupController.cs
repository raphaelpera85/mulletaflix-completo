using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Helpers;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Item lookup controller.
/// </summary>
[Route("")]
[Authorize]
public class ItemLookupController : BaseMulletaFlixApiController
{
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ItemLookupController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemLookupController"/> class.
    /// </summary>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ItemLookupController}"/> interface.</param>
    public ItemLookupController(
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILibraryManager libraryManager,
        ILogger<ItemLookupController> logger)
    {
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Get the item's external id info.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <response code="200">External id info retrieved.</response>
    /// <response code="404">Item not found.</response>
    /// <returns>List of external id info.</returns>
    [HttpGet("Items/{itemId}/ExternalIdInfos")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IEnumerable<ExternalIdInfo>> GetExternalIdInfos([FromRoute, Required] Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest("Item id must not be empty.");
        }

        var item = _libraryManager.GetItemById<BaseItem>(itemId, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        return Ok(_providerManager.GetExternalIdInfos(item));
    }

    /// <summary>
    /// Get movie remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Movie remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Movie")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetMovieRemoteSearchResults([FromBody, Required] RemoteSearchQuery<MovieInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get trailer remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Trailer remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Trailer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetTrailerRemoteSearchResults([FromBody, Required] RemoteSearchQuery<TrailerInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<Trailer, TrailerInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get music video remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Music video remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/MusicVideo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetMusicVideoRemoteSearchResults([FromBody, Required] RemoteSearchQuery<MusicVideoInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<MusicVideo, MusicVideoInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get series remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Series remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetSeriesRemoteSearchResults([FromBody, Required] RemoteSearchQuery<SeriesInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<Series, SeriesInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get box set remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Box set remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/BoxSet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetBoxSetRemoteSearchResults([FromBody, Required] RemoteSearchQuery<BoxSetInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<BoxSet, BoxSetInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get music artist remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Music artist remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/MusicArtist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetMusicArtistRemoteSearchResults([FromBody, Required] RemoteSearchQuery<ArtistInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<MusicArtist, ArtistInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get music album remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Music album remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/MusicAlbum")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetMusicAlbumRemoteSearchResults([FromBody, Required] RemoteSearchQuery<AlbumInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<MusicAlbum, AlbumInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get person remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Person remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Person")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetPersonRemoteSearchResults([FromBody, Required] RemoteSearchQuery<PersonLookupInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<Person, PersonLookupInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Get book remote search.
    /// </summary>
    /// <param name="query">Remote search query.</param>
    /// <response code="200">Book remote search executed.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="OkResult"/> containing the list of remote search results.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Book")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RemoteSearchResult>>> GetBookRemoteSearchResults([FromBody, Required] RemoteSearchQuery<BookInfo> query)
    {
        if (query is null)
        {
            return BadRequest("Search query is required.");
        }

        var results = await _providerManager.GetRemoteSearchResults<Book, BookInfo>(query, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Applies search criteria to an item and refreshes metadata.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="searchResult">The remote search result.</param>
    /// <param name="replaceAllImages">Optional. Whether or not to replace all images. Default: True.</param>
    /// <response code="204">Item metadata refreshed.</response>
    /// <response code="400">Search result is invalid or missing.</response>
    /// <response code="404">Item not found.</response>
    /// <returns>
    /// A <see cref="Task" /> that represents the asynchronous operation to get the remote search results.
    /// The task result contains an <see cref="NoContentResult"/>.
    /// </returns>
    [HttpPost("Items/RemoteSearch/Apply/{itemId}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApplySearchCriteria(
        [FromRoute, Required] Guid itemId,
        [FromBody, Required] RemoteSearchResult searchResult,
        [FromQuery] bool replaceAllImages = true)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest("Item id must not be empty.");
        }

        if (searchResult is null)
        {
            return BadRequest("Search result is required.");
        }

        var item = _libraryManager.GetItemById<BaseItem>(itemId, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        var providerIds = searchResult.ProviderIds != null
            ? new Dictionary<string, string>(searchResult.ProviderIds, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Setting provider id's to item {ItemId}-{ItemName}: {@ProviderIds}",
            item.Id,
            item.Name,
            providerIds);

        // Since the refresh process won't erase provider Ids, we need to set this explicitly now.
        item.ProviderIds = providerIds;
        await _providerManager.RefreshFullItem(
            item,
            new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllMetadata = true,
                ReplaceAllImages = replaceAllImages,
                SearchResult = searchResult,
                RemoveOldMetadata = true
            },
            HttpContext.RequestAborted).ConfigureAwait(false);

        if (replaceAllImages
            && !item.HasImage(ImageType.Primary)
            && !string.IsNullOrWhiteSpace(searchResult.ImageUrl))
        {
            try
            {
                await _providerManager.SaveImage(item, searchResult.ImageUrl, ImageType.Primary, null, HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                var savedImage = item.GetImageInfo(ImageType.Primary, 0);
                if (savedImage is not null && !string.IsNullOrWhiteSpace(searchResult.SearchProviderName))
                {
                    savedImage.ProviderName = searchResult.SearchProviderName;
                }

                await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, HttpContext.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to save primary image fallback for item {ItemId}-{ItemName}.",
                    item.Id,
                    item.Name);
            }
        }

        return NoContent();
    }
}
