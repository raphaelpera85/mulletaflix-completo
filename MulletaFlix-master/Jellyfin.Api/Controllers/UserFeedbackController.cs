using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Models.UserFeedbackDtos;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Common.Api;
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

    /// <summary>Returns indexed STRM titles for the administration request status grids.</summary>
    [HttpGet("MediaRequestCatalog")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(typeof(System.Collections.Generic.IReadOnlyList<MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto>), StatusCodes.Status200OK)]
    public IActionResult GetMediaRequestCatalog()
    {
        return new OkObjectResult(_nebulaFtpManager.GetMediaSuggestionCatalog());
    }

    /// <summary>Returns the media requests submitted by the current user, most recent first.</summary>
    [HttpGet("MediaRequests")]
    [ProducesResponseType(typeof(MediaRequestQueryResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMediaRequests([FromQuery] int limit = 100, [FromQuery] int startIndex = 0)
    {
        var result = await _activityManager.GetPagedResultAsync(new MulletaFlix.Data.Queries.ActivityLogQuery
        {
            UserId = User.GetUserId(),
            Type = "MediaRequest",
            Limit = Math.Clamp(limit, 1, 500),
            Skip = Math.Max(startIndex, 0),
            OrderBy = new[] { (MulletaFlix.Data.Enums.ActivityLogSortBy.DateCreated, MulletaFlix.Database.Implementations.Enums.SortOrder.Descending) }
        }).ConfigureAwait(false);

        var requestedTitles = result.Items.Select(entry => GetCatalogTitleKey(GetRequestTitle(entry)))
            .Where(title => title.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var catalog = _nebulaFtpManager.GetMediaSuggestionCatalog();

        return new OkObjectResult(new MediaRequestQueryResultDto
        {
            StartIndex = result.StartIndex,
            TotalRecordCount = result.TotalRecordCount,
            Items = result.Items,
            Catalog = (catalog ?? Array.Empty<MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto>())
                .Where(item => requestedTitles.Contains(GetCatalogTitleKey(item.Title)))
                .ToArray(),
            PriorityRequestIds = result.Items
                .Where(entry => _nebulaFtpManager.IsMediaRequestPrioritized(GetRequestTitle(entry)))
                .Select(entry => entry.Id)
                .ToArray()
        });
    }

    private static string GetCatalogTitleKey(string title)
    {
        // This only selects candidates, not the final included status. Keep all
        // category/year variants so the client can reject ambiguous identities.
        var withoutYear = Regex.Replace(title.Trim(), @"\s*[\[(]?\s*(?:19|20)\d{2}\s*[\])]?\s*$", string.Empty);
        return new string(withoutYear.Normalize(NormalizationForm.FormD)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string GetRequestTitle(MediaBrowser.Model.Activity.ActivityLogEntry entry)
    {
        const string prefix = "Solicitação de mídia:";
        return entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? entry.Name[prefix.Length..].Trim()
            : entry.Name.Trim();
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
