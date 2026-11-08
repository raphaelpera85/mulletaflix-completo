using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.ModelBinders;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Model.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// The collection controller.
/// </summary>
[Route("Collections")]
[Authorize(Policy = Policies.CollectionManagement)]
public class CollectionController : BaseMulletaFlixApiController
{
    private readonly ICollectionManager _collectionManager;
    private readonly IDtoService _dtoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionController"/> class.
    /// </summary>
    /// <param name="collectionManager">Instance of <see cref="ICollectionManager"/> interface.</param>
    /// <param name="dtoService">Instance of <see cref="IDtoService"/> interface.</param>
    public CollectionController(
        ICollectionManager collectionManager,
        IDtoService dtoService)
    {
        _collectionManager = collectionManager;
        _dtoService = dtoService;
    }

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="ids">Item Ids to add to the collection.</param>
    /// <param name="parentId">Optional. Create the collection within a specific folder.</param>
    /// <param name="isLocked">Whether or not to lock the new collection.</param>
    /// <response code="200">Collection created.</response>
    /// <returns>A <see cref="CollectionCreationOptions"/> with information about the new collection.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CollectionCreationResult>> CreateCollection(
        [FromQuery] string? name,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] string[] ids,
        [FromQuery] Guid? parentId,
        [FromQuery] bool isLocked = false)
    {
        if (parentId.HasValue && parentId.Value == Guid.Empty)
        {
            return BadRequest("Parent id must not be empty.");
        }

        var userId = User.GetUserId();

        var item = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
        {
            IsLocked = isLocked,
            Name = name,
            ParentId = parentId,
            ItemIdList = ids,
            UserIds = new[] { userId }
        }).ConfigureAwait(false);

        var dtoOptions = new DtoOptions();

        var dto = await _dtoService.GetBaseItemDtoAsync(item, dtoOptions).ConfigureAwait(false);

        return new CollectionCreationResult
        {
            Id = dto.Id
        };
    }

    /// <summary>
    /// Adds items to a collection.
    /// </summary>
    /// <param name="collectionId">The collection id.</param>
    /// <param name="ids">Item ids, comma delimited.</param>
    /// <response code="204">Items added to collection.</response>
    /// <response code="400">Invalid collection id or item ids.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("{collectionId}/Items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddToCollection(
        [FromRoute, Required] Guid collectionId,
        [FromQuery, Required, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] Guid[] ids)
    {
        if (collectionId == Guid.Empty)
        {
            return BadRequest("Collection id must not be empty.");
        }

        if (ids is null || ids.Length == 0)
        {
            return BadRequest("Item ids must not be empty.");
        }

        await _collectionManager.AddToCollectionAsync(collectionId, ids).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Removes items from a collection.
    /// </summary>
    /// <param name="collectionId">The collection id.</param>
    /// <param name="ids">Item ids, comma delimited.</param>
    /// <response code="204">Items removed from collection.</response>
    /// <response code="400">Invalid collection id or item ids.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpDelete("{collectionId}/Items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveFromCollection(
        [FromRoute, Required] Guid collectionId,
        [FromQuery, Required, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] Guid[] ids)
    {
        if (collectionId == Guid.Empty)
        {
            return BadRequest("Collection id must not be empty.");
        }

        if (ids is null || ids.Length == 0)
        {
            return BadRequest("Item ids must not be empty.");
        }

        await _collectionManager.RemoveFromCollectionAsync(collectionId, ids).ConfigureAwait(false);
        return NoContent();
    }
}

