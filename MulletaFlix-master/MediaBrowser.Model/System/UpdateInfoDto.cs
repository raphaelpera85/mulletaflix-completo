using System;

namespace MediaBrowser.Model.System
{
    /// <summary>
    /// Update availability information for the dashboard update center.
    /// </summary>
    public class UpdateInfoDto
    {
        /// <summary>
        /// Gets or sets the currently installed server version.
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the latest available version, or null when unknown.
        /// </summary>
        public string? AvailableVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an update is available.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// Gets or sets the changelog/release notes, or null when unavailable.
        /// </summary>
        public string? Changelog { get; set; }

        /// <summary>
        /// Gets or sets the download URL of the update archive.
        /// </summary>
        public string? ArchiveUrl { get; set; }

        /// <summary>
        /// Gets or sets the size of the update package in bytes.
        /// </summary>
        public long? PackageSize { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last successful check, or null when never checked.
        /// </summary>
        public DateTime? LastCheckedAt { get; set; }

        /// <summary>
        /// Gets or sets the current installation state.
        /// </summary>
        public string? InstallState { get; set; }

        /// <summary>
        /// Gets or sets the current install/download progress percentage (0-100).
        /// </summary>
        public int? InstallProgress { get; set; }

        /// <summary>
        /// Gets or sets any error message from the last update attempt.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
