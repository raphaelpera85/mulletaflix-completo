using System;
using System.Globalization;
using System.Threading.Tasks;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Models.UserFeedbackDtos;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
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
    private readonly INebulaFtpManager _nebulaFtpManager;

    /// <summary>Initializes a new instance of the <see cref="UserFeedbackController"/> class.</summary>
    public UserFeedbackController(IActivityManager activityManager, ILibraryManager libraryManager, INebulaFtpManager nebulaFtpManager)
    {
        _activityManager = activityManager;
        _libraryManager = libraryManager;
        _nebulaFtpManager = nebulaFtpManager;
    }

    /// <summary>Searches STRM titles for the media-request autocomplete.</summary>
    [HttpGet("MediaSuggestions")]
    [ProducesResponseType(typeof(System.Collections.Generic.IReadOnlyList<MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto>), StatusCodes.Status200OK)]
    public IActionResult GetMediaSuggestions([FromQuery] string query, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new OkObjectResult(Array.Empty<MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto>());
        }

        return new OkObjectResult(_nebulaFtpManager.SearchMediaSuggestions(query, limit));
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

        // Register the requested title with both Nebula queues. The downloader
        // applies the title match as soon as it encounters a matching STRM, and
        // the staging watcher promotes an already queued upload immediately.
        _nebulaFtpManager.PrioritizeMedia(string.Empty, seriesName: title);

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
