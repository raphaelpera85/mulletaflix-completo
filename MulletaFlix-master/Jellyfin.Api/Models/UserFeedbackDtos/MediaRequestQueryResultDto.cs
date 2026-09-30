using System.Collections.Generic;
using MediaBrowser.Model.Activity;

namespace MulletaFlix.Api.Models.UserFeedbackDtos;

/// <summary>Current user's media requests and the subset registered for Nebula priority.</summary>
public sealed class MediaRequestQueryResultDto
{
    /// <summary>Gets or sets the first result index.</summary>
    public int StartIndex { get; set; }

    /// <summary>Gets or sets the total number of matching requests.</summary>
    public int TotalRecordCount { get; set; }

    /// <summary>Gets or sets the current page of requests.</summary>
    public IReadOnlyList<ActivityLogEntry> Items { get; set; } = System.Array.Empty<ActivityLogEntry>();

    /// <summary>Gets or sets request IDs whose titles are registered for Nebula priority.</summary>
    public IReadOnlyList<long> PriorityRequestIds { get; set; } = System.Array.Empty<long>();
}
