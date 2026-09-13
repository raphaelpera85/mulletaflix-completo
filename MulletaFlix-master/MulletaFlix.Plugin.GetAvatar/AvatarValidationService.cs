using System;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Plugin.GetAvatar.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Plugin.GetAvatar
{
    /// <summary>
    /// Hosted service that validates user avatars at startup.
    /// This ensures that profile images are repaired if they were deleted or lost.
    /// </summary>
    public class AvatarValidationService : IHostedService, IDisposable
    {
        private readonly AvatarService _avatarService;
        private readonly OnlinePackService _onlinePackService;
        private readonly ILogger<AvatarValidationService> _logger;
        private readonly CancellationTokenSource _lifecycleCts = new();
        private Task? _validationTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvatarValidationService"/> class.
        /// </summary>
        /// <param name="avatarService">The avatar service.</param>
        /// <param name="onlinePackService">The online avatar pack service.</param>
        /// <param name="logger">The logger instance.</param>
        public AvatarValidationService(
            AvatarService avatarService,
            OnlinePackService onlinePackService,
            ILogger<AvatarValidationService> logger)
        {
            _avatarService = avatarService;
            _onlinePackService = onlinePackService;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Avatar catalog/bootstrap may access the network. It must not delay
            // the server from accepting requests during host startup.
            _validationTask = Task.Run(() => ValidateAsync(_lifecycleCts.Token), _lifecycleCts.Token);
            return Task.CompletedTask;
        }

        private async Task ValidateAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("GetAvatar validation service starting...");

                // Keep isolated smoke/E2E environments hermetic. The normal
                // production path remains unchanged, while CI can opt out of
                // downloading the online catalog through an explicit flag.
                if (IsExternalBootstrapDisabled())
                {
                    _logger.LogInformation("GetAvatar online catalog bootstrap is disabled for this environment.");
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                await EnsureDefaultAvatarCatalogAsync().ConfigureAwait(false);

                var assignedCount = await _avatarService.AssignMissingAvatarsAsync().ConfigureAwait(false);
                if (assignedCount > 0)
                {
                    _logger.LogInformation("Auto-assigned avatars to {Count} user(s) without one.", assignedCount);
                }

                var repairedCount = await _avatarService.ValidateUserAvatarsAsync().ConfigureAwait(false);

                if (repairedCount > 0)
                {
                    _logger.LogInformation("Avatar validation completed. Repaired {Count} missing avatar(s).", repairedCount);
                }
                else
                {
                    _logger.LogInformation("Avatar validation completed. All avatars are valid.");
                }

                var deletedCount = _avatarService.CleanOrphanedProfileImages();
                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} orphaned profile image(s).", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during avatar validation at startup");
            }
        }

        private static bool IsExternalBootstrapDisabled()
        {
            var value = Environment.GetEnvironmentVariable("MFLX_DISABLE_EXTERNAL_BOOTSTRAP");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _lifecycleCts.Cancel();
            if (_validationTask is not null)
            {
                try
                {
                    await _validationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifecycleCts.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("GetAvatar validation service stopping...");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lifecycleCts.Dispose();
            }
        }

        private async Task EnsureDefaultAvatarCatalogAsync()
        {
            if (_avatarService.GetAvailableAvatars().Count > 0)
            {
                return;
            }

            try
            {
                var packs = await _onlinePackService.GetAvailablePacksAsync(forceRefresh: true).ConfigureAwait(false);
                if (packs.Count == 0)
                {
                    _logger.LogWarning("No online avatar packs were available during startup bootstrap.");
                    return;
                }

                var result = await _onlinePackService.ImportPacksAsync(packs).ConfigureAwait(false);
                _logger.LogInformation(
                    "Bootstrapped {ImportedCount} avatar(s) from {PackCount} online pack(s) at startup.",
                    result.ImportedCount,
                    result.PackResults.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bootstrap default avatar catalog at startup");
            }
        }
    }
}
