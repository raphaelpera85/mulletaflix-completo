using System;
using System.Globalization;
using System.Threading.Tasks;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Models.UserFeedbackDtos;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Activity;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>Accepts authenticated user media requests and playback issue reports.</summary>
[Authorize]
[Tags("UserFeedback")]
public class UserFeedbackController : BaseMulletaFlixApiController
{
    private readonly IActivityManager _activityManager;
    private readonly ILibraryManager _libraryManager;

    /// <summary>Initializes a new instance of the <see cref="UserFeedbackController"/> class.</summary>
    public UserFeedbackController(IActivityManager activityManager, ILibraryManager libraryManager)
    {
        _activityManager = activityManager;
        _libraryManager = libraryManager;
    }

    /// <summary>Creates a request for a title to be added to the server library.</summary>
    [HttpPost("MediaRequests")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CreateMediaRequest([FromBody] MediaRequestDto request)
    {
        var title = request.Title.Trim();
        var mediaType = request.MediaType.Trim();
        if (title.Length == 0 || mediaType.Length == 0)
        {
            return BadRequest();
        }

        var details = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await _activityManager.CreateAsync(new ActivityLog(
            string.Format(CultureInfo.InvariantCulture, "Solicitação de mídia: {0}", title),
            "MediaRequest",
            User.GetUserId())
        {
            Overview = request.Year.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} · {1}", mediaType, request.Year.Value)
                : mediaType,
            ShortOverview = details
        }).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>Creates a report about playback or data for an accessible library item.</summary>
    [HttpPost("PlaybackIssues")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePlaybackIssue([FromBody] PlaybackIssueDto request)
    {
        var userId = User.GetUserId();
        var item = _libraryManager.GetItemById<MediaBrowser.Controller.Entities.BaseItem>(request.ItemId, userId);
        if (item is null)
        {
            return NotFound();
        }

        var category = request.Category.Trim();
        if (category.Length == 0)
        {
            return BadRequest();
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await _activityManager.CreateAsync(new ActivityLog(
            string.Format(CultureInfo.InvariantCulture, "Problema reportado: {0}", item.Name),
            "PlaybackIssue",
            userId)
        {
            ItemId = item.Id.ToString("N", CultureInfo.InvariantCulture),
            Overview = category,
            ShortOverview = description
        }).ConfigureAwait(false);

        return NoContent();
    }
}
