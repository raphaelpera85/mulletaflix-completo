using System;

namespace MediaBrowser.Controller.SystemBackupService;

/// <summary>
/// Indicates that another backup or restore operation is already in progress.
/// </summary>
public sealed class BackupOperationInProgressException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackupOperationInProgressException"/> class.
    /// </summary>
    public BackupOperationInProgressException()
        : base("Another backup or restore operation is already in progress.")
    {
    }
}
