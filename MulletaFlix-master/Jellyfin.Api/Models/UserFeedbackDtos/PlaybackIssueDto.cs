using System;
using System.ComponentModel.DataAnnotations;

namespace MulletaFlix.Api.Models.UserFeedbackDtos;

/// <summary>Describes a playback or media issue reported by a user.</summary>
public class PlaybackIssueDto
{
    /// <summary>Gets or sets the identifier of the affected library item.</summary>
    [Required]
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the issue category.</summary>
    [Required, StringLength(80, MinimumLength = 1)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets additional details provided by the user.</summary>
    [StringLength(1000)]
    public string? Description { get; set; }
}
