using System;
using MulletaFlix.Database.Implementations.Entities;

namespace MulletaFlix.Data.Events.Users
{
    /// <summary>
    /// An event that occurs when a user license is created or updated.
    /// </summary>
    public class UserLicenseChangedEventArgs : GenericEventArgs<User>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLicenseChangedEventArgs"/> class.
        /// </summary>
        /// <param name="arg">The user.</param>
        /// <param name="isNewLicense">Whether this is a new license or a renewal.</param>
        /// <param name="durationHours">Duration in hours, or null for unlimited.</param>
        /// <param name="adminNotes">Optional admin notes.</param>
        /// <param name="grantedByUserId">The admin who granted the license.</param>
        public UserLicenseChangedEventArgs(
            User arg,
            bool isNewLicense,
            int? durationHours,
            string? adminNotes,
            Guid grantedByUserId) : base(arg)
        {
            IsNewLicense = isNewLicense;
            DurationHours = durationHours;
            AdminNotes = adminNotes;
            GrantedByUserId = grantedByUserId;
        }

        /// <summary>
        /// Gets a value indicating whether this is a new license or a renewal.
        /// </summary>
        public bool IsNewLicense { get; }

        /// <summary>
        /// Gets the duration in hours, or null for unlimited.
        /// </summary>
        public int? DurationHours { get; }

        /// <summary>
        /// Gets optional admin notes.
        /// </summary>
        public string? AdminNotes { get; }

        /// <summary>
        /// Gets the admin who granted the license.
        /// </summary>
        public Guid GrantedByUserId { get; }
    }
}