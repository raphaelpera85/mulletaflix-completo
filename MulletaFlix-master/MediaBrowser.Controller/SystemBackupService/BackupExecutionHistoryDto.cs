using System;
using MediaBrowser.Model.Tasks;

namespace MediaBrowser.Controller.SystemBackupService;

/// <summary>
/// Represents a backup execution history entry.
/// </summary>
public class BackupExecutionHistoryDto
{
    /// <summary>
    /// Gets or sets the backup path.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the start time UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the end time UTC.
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public TaskCompletionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the long error message.
    /// </summary>
    public string LongErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    public double DurationSeconds => (EndTimeUtc - StartTimeUtc).TotalSeconds;
}