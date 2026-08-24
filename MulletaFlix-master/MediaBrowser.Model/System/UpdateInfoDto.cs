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
        /// Gets or sets the timestamp of the last successful check, or null when never checked.
        /// </summary>
        public DateTime? LastCheckedAt { get; set; }
    }
}
