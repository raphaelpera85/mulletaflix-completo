using System;

namespace MediaBrowser.Model.Nebula;

/// <summary>Path-free status of the STRM media-suggestion index.</summary>
public sealed class NebulaMediaSuggestionIndexStatusDto
{
    /// <summary>Gets or sets the index state: NotStarted, Indexing, Ready, ReadyWithWarnings, or Error.</summary>
    public string State { get; set; } = "NotStarted";

    /// <summary>Gets or sets whether a filesystem scan is currently running.</summary>
    public bool IsIndexing { get; set; }

    /// <summary>Gets or sets the number of indexed titles in the last successful catalog.</summary>
    public int IndexedTitleCount { get; set; }

    /// <summary>Gets or sets the number of configured roots scanned.</summary>
    public int RootCount { get; set; }

    /// <summary>Gets or sets the number of roots that could not be read in the last scan.</summary>
    public int FailedRootCount { get; set; }

    /// <summary>Gets or sets the UTC time of the last successful scan.</summary>
    public DateTime? LastIndexedAtUtc { get; set; }
}
