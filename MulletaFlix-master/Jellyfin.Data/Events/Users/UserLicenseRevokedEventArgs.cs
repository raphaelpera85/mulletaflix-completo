using System;
using MulletaFlix.Database.Implementations.Entities;

namespace MulletaFlix.Data.Events.Users
{
    /// <summary>
    /// An event that occurs when a user license is revoked.
    /// </summary>
    public class UserLicenseRevokedEventArgs : GenericEventArgs<User>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLicenseRevokedEventArgs"/> class.
        /// </summary>
        /// <param name="arg">The user.</param>
        /// <param name="revokedByUserId">The admin who revoked the license.</param>
        public UserLicenseRevokedEventArgs(User arg, Guid revokedByUserId) : base(arg)
        {
            RevokedByUserId = revokedByUserId;
        }

        /// <summary>
        /// Gets the admin who revoked the license.
        /// </summary>
        public Guid RevokedByUserId { get; }
    }
}