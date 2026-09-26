namespace MediaBrowser.Model.Nebula;

/// <summary>A safe, path-free suggestion from the Nebula STRM catalog.</summary>
public sealed class NebulaMediaSuggestionDto
{
    /// <summary>Gets or sets the display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the media category.</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the release year when it can be inferred.</summary>
    public int? Year { get; set; }
}
