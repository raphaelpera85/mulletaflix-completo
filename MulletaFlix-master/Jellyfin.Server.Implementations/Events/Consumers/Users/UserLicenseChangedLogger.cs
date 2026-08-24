using System.Globalization;
using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Globalization;
using MulletaFlix.Data.Events.Users;
using MulletaFlix.Database.Implementations.Entities;

namespace MulletaFlix.Server.Implementations.Events.Consumers.Users
{
    /// <summary>
    /// Creates an entry in the activity log when a user license is created or updated.
    /// </summary>
    public class UserLicenseChangedLogger : IEventConsumer<UserLicenseChangedEventArgs>
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IActivityManager _activityManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLicenseChangedLogger"/> class.
        /// </summary>
        /// <param name="localizationManager">The localization manager.</param>
        /// <param name="activityManager">The activity manager.</param>
        public UserLicenseChangedLogger(ILocalizationManager localizationManager, IActivityManager activityManager)
        {
            _localizationManager = localizationManager;
            _activityManager = activityManager;
        }

        /// <inheritdoc />
        public async Task OnEvent(UserLicenseChangedEventArgs eventArgs)
        {
            var user = eventArgs.Argument;
            var key = eventArgs.IsNewLicense
                ? "UserLicenseCreatedWithName"
                : "UserLicenseUpdatedWithName";

            var entry = new ActivityLog(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        _localizationManager.GetServerLocalizedString(key),
                        user.Username),
                    eventArgs.IsNewLicense ? "UserLicenseCreated" : "UserLicenseUpdated",
                    user.Id);

            if (eventArgs.DurationHours.HasValue)
            {
                entry.Overview = string.Format(
                    CultureInfo.InvariantCulture,
                    _localizationManager.GetServerLocalizedString("UserLicenseDurationHours"),
                    eventArgs.DurationHours.Value);
            }
            else
            {
                entry.Overview = _localizationManager.GetServerLocalizedString("UserLicenseDurationUnlimited");
            }

            await _activityManager.CreateAsync(entry).ConfigureAwait(false);
        }
    }
}
