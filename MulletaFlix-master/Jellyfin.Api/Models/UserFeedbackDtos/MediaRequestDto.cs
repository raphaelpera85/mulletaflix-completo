using System.ComponentModel.DataAnnotations;

namespace MulletaFlix.Api.Models.UserFeedbackDtos;

/// <summary>Describes a media title requested by a user.</summary>
public class MediaRequestDto
{
    /// <summary>Gets or sets the requested title.</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested content category.</summary>
    [Required, StringLength(40, MinimumLength = 1)]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the release year, when known.</summary>
    [Range(1888, 2200)]
    public int? Year { get; set; }

    /// <summary>Gets or sets optional notes such as a provider URL or season.</summary>
    [StringLength(1000)]
    public string? Notes { get; set; }
}
