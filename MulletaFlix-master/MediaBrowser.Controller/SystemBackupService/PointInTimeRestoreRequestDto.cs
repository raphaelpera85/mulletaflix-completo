using System;

namespace MediaBrowser.Controller.SystemBackupService;

/// <summary>
/// Request DTO for point-in-time restore.
/// </summary>
public class PointInTimeRestoreRequestDto
{
    /// <summary>
    /// Gets or sets the target date for point-in-time restore.
    /// </summary>
    public DateTimeOffset TargetDate { get; set; }
}