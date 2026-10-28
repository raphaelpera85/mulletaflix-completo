using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.IO;
using MulletaFlix.Plugin.GetAvatar.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Plugin.GetAvatar.Services
{
    /// <summary>
    /// Service for managing user avatars.
    /// </summary>
    public sealed class AvatarService : IDisposable
    {
        private static readonly SemaphoreSlim UserAvatarUpdateLock = new(1, 1);

        private readonly IUserManager _userManager;
        private readonly IProviderManager _providerManager;
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<AvatarService> _logger;
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly string _avatarDirectory;
        private readonly AvatarFileStore _avatarFileStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvatarService"/> class.
        /// </summary>
        /// <param name="userManager">The user manager.</param>
        /// <param name="providerManager">The provider manager.</param>
        /// <param name="appPaths">The application paths.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        public AvatarService(
            IUserManager userManager,
            IProviderManager providerManager,
            IApplicationPaths appPaths,
            ILogger<AvatarService> logger,
            IServerConfigurationManager serverConfigurationManager)
        {
            _userManager = userManager;
            _providerManager = providerManager;
            _appPaths = appPaths;
            _logger = logger;
            _serverConfigurationManager = serverConfigurationManager;

            _logger.LogInformation(
                "AvatarService constructor called. Plugin.Instance is {Status}",
                Plugin.Instance == null ? "NULL" : "initialized");

            // Store avatars in plugin data directory using Jellyfin's proper paths
            // This path is automatically adapted to the environment (Docker, Windows, Linux, etc.)
            var pluginDataPath = Path.Combine(
                _appPaths.PluginConfigurationsPath,
                "GetAvatar",
                "avatars");

            _avatarDirectory = pluginDataPath;

            _logger.LogInformation("Avatar directory path: {Path}", _avatarDirectory);

            // Create directory if it doesn't exist
            try
            {
                if (!Directory.Exists(_avatarDirectory))
                {
                    Directory.CreateDirectory(_avatarDirectory);
                    _logger.LogInformation("Successfully created avatar directory: {Path}", _avatarDirectory);
                }
                else
                {
                    _logger.LogInformation("Avatar directory already exists: {Path}", _avatarDirectory);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission denied when creating avatar directory at {Path}. Please check directory permissions.", _avatarDirectory);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create avatar directory at {Path}", _avatarDirectory);
                throw;
            }

            _avatarFileStore = new AvatarFileStore(_avatarDirectory);
        }

        /// <summary>
        /// Gets the avatar directory path.
        /// </summary>
        public string AvatarDirectory => _avatarDirectory;

        /// <summary>
        /// Gets all available avatars from configuration.
        /// </summary>
        /// <returns>List of available avatars.</returns>
        public List<AvatarInfo> GetAvailableAvatars()
        {
            using (Plugin.AcquireConfigurationLock())
            {
                if (Plugin.Instance == null)
                {
                    _logger.LogError("Plugin instance is null in GetAvailableAvatars");
                    return new List<AvatarInfo>();
                }

                var config = Plugin.Instance.Configuration;
                if (config == null)
                {
                    _logger.LogError("Plugin configuration is null");
                    return new List<AvatarInfo>();
                }

                var avatars = config.AvailableAvatars ?? new List<AvatarInfo>();

                // Clean up null entries from corrupted config
                var removed = avatars.RemoveAll(a => a == null);
                if (removed > 0)
                {
                    _logger.LogWarning("Removed {Count} null avatar entries from configuration", removed);
                    Plugin.Instance.SaveConfiguration();
                }

                if (avatars.Count == 0)
                {
                    var recoveredAvatars = RebuildAvailableAvatarsFromDisk();
                    if (recoveredAvatars.Count > 0)
                    {
                        config.AvailableAvatars = recoveredAvatars;
                        Plugin.Instance.SaveConfiguration();
                        _logger.LogWarning(
                            "Recovered {Count} avatars from disk because the configuration list was empty",
                            recoveredAvatars.Count);
                        return recoveredAvatars;
                    }
                }

                _logger.LogInformation("Returning {Count} avatars from configuration", avatars.Count);
                return avatars;
            }
        }

        private List<AvatarInfo> RebuildAvailableAvatarsFromDisk()
        {
            if (!Directory.Exists(_avatarDirectory))
            {
                return new List<AvatarInfo>();
            }

            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp",
                ".gif"
            };

            return Directory.GetFiles(_avatarDirectory)
                .Where(path => supportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                {
                    var fileName = Path.GetFileName(path);
                    var name = Path.GetFileNameWithoutExtension(path);
                    return new AvatarInfo
                    {
                        Id = name,
                        Name = name,
                        FileName = fileName,
                        DateAdded = File.GetLastWriteTimeUtc(path),
                        Category = string.Empty,
                        Origin = string.Empty
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Saves an avatar image to the avatar directory.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="imageData">The image data.</param>
        /// <param name="category">The optional category for the avatar.</param>
        /// <returns>The saved avatar info.</returns>
        public async Task<AvatarInfo> SaveAvatarAsync(string fileName, byte[] imageData, string category = "")
        {
            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null)
                {
                    _logger.LogError("Plugin instance is null, cannot save avatar to configuration");
                    throw new InvalidOperationException("Plugin not initialized");
                }

                var avatarId = Guid.NewGuid().ToString();
                var fileExtension = Path.GetExtension(fileName);
                var savedFileName = $"{avatarId}{fileExtension}";
                var avatarInfo = new AvatarInfo
                {
                    Id = avatarId,
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    FileName = savedFileName,
                    DateAdded = DateTime.UtcNow,
                    Category = category,
                    Origin = category
                };

                try
                {
                    await using (var stream = _avatarFileStore.CreateNew(savedFileName))
                    {
                        await stream.WriteAsync(imageData).ConfigureAwait(false);
                    }

                    using (Plugin.AcquireConfigurationLock())
                    {
                        var config = plugin.Configuration;
                        var previousAvatars = config.AvailableAvatars;
                        var avatars = previousAvatars ?? new List<AvatarInfo>();
                        try
                        {
                            config.AvailableAvatars = avatars;
                            avatars.Add(avatarInfo);
                            plugin.SaveConfiguration();
                        }
                        catch
                        {
                            avatars.Remove(avatarInfo);
                            config.AvailableAvatars = previousAvatars ?? new List<AvatarInfo>();
                            throw;
                        }
                    }
                }
                catch
                {
                    try
                    {
                        _avatarFileStore.TryDelete(savedFileName);
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogWarning(cleanupException, "Failed to remove incomplete avatar file: {FileName}", savedFileName);
                    }

                    throw;
                }

                _logger.LogInformation("Saved avatar: {Name} ({Id})", avatarInfo.Name, avatarInfo.Id);

                return avatarInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save avatar: {FileName}", fileName);
                throw;
            }
        }

        /// <summary>
        /// Deletes an avatar from the pool.
        /// Note: This does NOT remove the avatar from users who are currently using it.
        /// The user's profile image remains intact until they select a different avatar.
        /// </summary>
        /// <param name="avatarId">The avatar ID.</param>
        /// <returns>True if deleted successfully.</returns>
        public bool DeleteAvatar(string avatarId)
        {
            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null)
                {
                    _logger.LogError("Plugin instance is null");
                    return false;
                }

                using (Plugin.AcquireConfigurationLock())
                {
                    var config = plugin.Configuration;
                    var avatar = config.AvailableAvatars?.FirstOrDefault(a => a != null && a.Id == avatarId);

                    if (avatar == null)
                    {
                        _logger.LogWarning("Avatar not found: {Id}", avatarId);
                        return false;
                    }

                    var previousAvatars = config.AvailableAvatars;
                    var previousAvatarEntries = previousAvatars?.ToArray();
                    var previousMappings = config.UserAvatars;
                    var previousMappingEntries = previousMappings?.ToArray();
                    var previousConfigurationFile = new Dictionary<string, byte[]?>();
                    CaptureFile(previousConfigurationFile, plugin.ConfigurationFilePath);
                    var configurationPersisted = false;

                    AvatarFileDeleteResult fileDeleteResult;
                    try
                    {
                        fileDeleteResult = _avatarFileStore.TryDelete(avatar.FileName, () =>
                        {
                            var usersWithAvatar = config.UserAvatars?.Where(u => u.AvatarId == avatarId).ToList() ?? new List<UserAvatarMapping>();
                            if (usersWithAvatar.Count > 0)
                            {
                                _logger.LogWarning(
                                    "Avatar {AvatarId} is currently used by {UserCount} user(s). " +
                                    "Removing from pool but keeping user profile images intact.",
                                    avatarId,
                                    usersWithAvatar.Count);

                                config.UserAvatars?.RemoveAll(u => u.AvatarId == avatarId);
                            }

                            config.AvailableAvatars?.Remove(avatar);
                            plugin.SaveConfiguration();
                            configurationPersisted = true;
                        });
                    }
                    catch
                    {
                        if (!configurationPersisted)
                        {
                            if (previousAvatars is not null && previousAvatarEntries is not null)
                            {
                                previousAvatars.Clear();
                                previousAvatars.AddRange(previousAvatarEntries);
                            }

                            if (previousMappings is not null && previousMappingEntries is not null)
                            {
                                previousMappings.Clear();
                                previousMappings.AddRange(previousMappingEntries);
                            }

                            RestoreFiles(previousConfigurationFile);
                            throw;
                        }

                        _logger.LogWarning(
                            "Avatar {AvatarId} was removed from the persisted pool, but its file cleanup failed",
                            avatarId);
                        return true;
                    }

                    if (fileDeleteResult == AvatarFileDeleteResult.Unsafe)
                    {
                        if (configurationPersisted)
                        {
                            _logger.LogWarning("Avatar {AvatarId} was removed from the pool, but its file changed before cleanup", avatarId);
                            return true;
                        }

                        _logger.LogWarning("Refusing to delete unsafe avatar file for {AvatarId}", avatarId);
                        return false;
                    }

                    if (fileDeleteResult == AvatarFileDeleteResult.Deleted)
                    {
                        _logger.LogInformation("Deleted avatar file for {AvatarId}", avatarId);
                    }
                    else
                    {
                        _logger.LogInformation("Removing stale avatar catalog entry {AvatarId}; file is already missing", avatarId);
                    }

                    _logger.LogInformation("Deleted avatar from pool: {Name} ({Id})", avatar.Name, avatarId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete avatar: {Id}", avatarId);
                return false;
            }
        }

        /// <summary>
        /// Opens an avatar by resolving its catalog entry relative to the pinned catalog directory.
        /// </summary>
        /// <param name="avatarId">The avatar ID.</param>
        /// <returns>The opened file and name, or <see langword="null"/> if it is missing or unsafe.</returns>
        public AvatarFileRead? OpenAvatarRead(string avatarId)
        {
            if (Plugin.Instance == null)
            {
                _logger.LogError("Plugin instance is null in OpenAvatarRead");
                return null;
            }

            using (Plugin.AcquireConfigurationLock())
            {
                var avatar = Plugin.Config.AvailableAvatars?.FirstOrDefault(a => a != null && a.Id == avatarId);
                if (avatar == null)
                {
                    return null;
                }

                var stream = _avatarFileStore.TryOpenRead(avatar.FileName);
                if (stream == null)
                {
                    _logger.LogWarning(
                        "Refusing to open missing or unsafe avatar file for {AvatarId}",
                        avatarId);
                    return null;
                }

                return new AvatarFileRead(avatar.FileName, stream);
            }
        }

        /// <summary>
        /// Sets an avatar for a user using Jellyfin's internal API.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="avatarId">The avatar ID.</param>
        /// <returns>Task representing the operation.</returns>
        public async Task SetUserAvatarAsync(Guid userId, string avatarId)
        {
            await UserAvatarUpdateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SetUserAvatarCoreAsync(userId, avatarId).ConfigureAwait(false);
            }
            finally
            {
                UserAvatarUpdateLock.Release();
            }
        }

        private async Task SetUserAvatarCoreAsync(Guid userId, string avatarId)
        {
            using var profileImageUpdateLock = await _userManager.AcquireProfileImageUpdateLockAsync(userId).ConfigureAwait(false);
            _logger.LogInformation("SetUserAvatarAsync called: userId={UserId}, avatarId={AvatarId}", userId, avatarId);

            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                _logger.LogError("User not found: {UserId}", userId);
                throw new ArgumentException($"User not found: {userId}");
            }

            _logger.LogInformation("Found user: {UserName}", user.Username);

            using var avatarFile = OpenAvatarRead(avatarId);
            if (avatarFile == null)
            {
                _logger.LogError("Avatar not found: {AvatarId}", avatarId);
                throw new ArgumentException($"Avatar not found: {avatarId}");
            }

            _logger.LogInformation("Avatar file opened from catalog: {FileName}", avatarFile.FileName);

            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new InvalidOperationException("Plugin not initialized");
            }

            // Determine file extension and MIME type
            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };

            _logger.LogInformation("MIME type: {MimeType}, Extension: {Extension}", mimeType, extension);

            // Use Jellyfin's native user profile image path and save pipeline.
            // This mirrors Jellyfin.Api.Controllers.ImageController.PostUserImage.
            var userDataPath = Path.Combine(
                _serverConfigurationManager.ApplicationPaths.UserConfigurationDirectoryPath,
                user.Username);

            _logger.LogInformation("User profile image directory: {Path}", userDataPath);

            var profileImagePath = Path.Combine(userDataPath, "profile" + extension);
            _logger.LogInformation("Profile image path: {Path}", profileImagePath);

            var previousProfileImage = user.ProfileImage;
            var previousProfilePath = previousProfileImage?.Path;
            var previousFiles = new Dictionary<string, byte[]?>();
            if (FileSystemHelper.IsPathWithinRoot(previousProfilePath ?? string.Empty, userDataPath))
            {
                CaptureFile(previousFiles, previousProfilePath);
            }

            if (FileSystemHelper.IsPathWithinRoot(profileImagePath, userDataPath))
            {
                CaptureFile(previousFiles, profileImagePath);
            }

            var profileClearAttempted = false;
            var previousImageRemoved = false;
            var profileUpdateAttempted = false;

            try
            {
                Directory.CreateDirectory(userDataPath);
                if (user.ProfileImage is not null)
                {
                    profileClearAttempted = true;
                    await _userManager.ClearProfileImageAsync(user).ConfigureAwait(false);
                    previousImageRemoved = true;
                    _logger.LogInformation("Cleared existing profile image for user {UserName}", user.Username);
                }

                user.ProfileImage = new ImageInfo(profileImagePath);

                await _providerManager
                    .SaveImage(avatarFile.Stream, mimeType, profileImagePath)
                    .ConfigureAwait(false);

                profileUpdateAttempted = true;
                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

                _logger.LogInformation("Updated profile image via Jellyfin image pipeline: {Path}", profileImagePath);
                SaveUserAvatarMapping(plugin, userId, avatarId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save/update profile image via Jellyfin image pipeline");

                var restoreProfileMetadata = previousImageRemoved || profileUpdateAttempted;
                if (profileClearAttempted && !restoreProfileMetadata)
                {
                    // ClearProfileImageAsync can commit the row deletion and still throw before it
                    // updates the in-memory user. Read persisted state before deciding whether to
                    // insert a replacement ImageInfo, which avoids duplicate rows on pre-commit failures.
                    try
                    {
                        var persistedUser = _userManager.GetUserById(userId);
                        restoreProfileMetadata = persistedUser is not null && persistedUser.ProfileImage is null;
                    }
                    catch (Exception inspectionException)
                    {
                        _logger.LogWarning(
                            inspectionException,
                            "Failed to inspect profile metadata after clearing failed for user {UserId}",
                            userId);
                    }
                }

                if (restoreProfileMetadata)
                {
                    // ClearProfileImageAsync deletes the old ImageInfo row. Recreate it with Id=0
                    // so UpdateUserAsync inserts a replacement row instead of updating a deleted one.
                    user.ProfileImage = previousProfileImage == null
                        ? null
                        : new ImageInfo(previousProfileImage.Path)
                        {
                            LastModified = previousProfileImage.LastModified
                        };
                    try
                    {
                        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.LogWarning(rollbackException, "Failed to restore profile metadata for user {UserId}", userId);
                    }
                }
                else
                {
                    // The existing profile row was not removed; retain its tracked entity and avoid
                    // an unnecessary update that could create a duplicate relationship.
                    user.ProfileImage = previousProfileImage;
                }

                RestoreFiles(previousFiles);

                throw;
            }

            if (!string.IsNullOrWhiteSpace(previousProfilePath)
                && !FileSystemHelper.PathsEqual(previousProfilePath, profileImagePath))
            {
                TryDeleteObsoleteProfileImage(previousProfilePath, userDataPath, userId);
            }

            _logger.LogInformation("Successfully set avatar {AvatarId} for user {UserName} ({UserId})", avatarId, user.Username, userId);
        }

        private void TryDeleteObsoleteProfileImage(string path, string userDataPath, Guid userId)
        {
            try
            {
                var result = TryDeleteProfileImageFile(path, userDataPath);
                if (result == AvatarFileDeleteResult.Unsafe)
                {
                    _logger.LogWarning("Retaining unsafe obsolete profile image for user {UserId}", userId);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or PlatformNotSupportedException)
            {
                _logger.LogWarning(exception, "Failed to remove obsolete profile image file for user {UserId}", userId);
            }
        }

        private static AvatarFileDeleteResult TryDeleteProfileImageFile(string path, string userDataPath)
        {
            var fullRoot = Path.GetFullPath(userDataPath);
            var fullPath = Path.GetFullPath(path);
            if (!FileSystemHelper.IsPathWithinRoot(fullPath, fullRoot)
                || !FileSystemHelper.PathsEqual(Path.GetDirectoryName(fullPath) ?? string.Empty, fullRoot))
            {
                return AvatarFileDeleteResult.Unsafe;
            }

            using var fileStore = new AvatarFileStore(fullRoot);
            return fileStore.TryDelete(Path.GetFileName(fullPath));
        }

        private void SaveUserAvatarMapping(Plugin plugin, Guid userId, string avatarId)
        {
            using (Plugin.AcquireConfigurationLock())
            {
                var config = plugin.Configuration;
                var previousMappings = config.UserAvatars;
                var previousMappingEntries = previousMappings?.ToArray();
                var previousMappingValues = previousMappingEntries?
                    .Select(mapping => (Mapping: mapping, mapping.UserId, mapping.AvatarId))
                    .ToArray();
                var previousConfigurationFile = new Dictionary<string, byte[]?>();
                CaptureFile(previousConfigurationFile, plugin.ConfigurationFilePath);

                try
                {
                    config.UserAvatars ??= new List<UserAvatarMapping>();
                    var existingMapping = config.UserAvatars.FirstOrDefault(x => x.UserId == userId.ToString());
                    if (existingMapping != null)
                    {
                        existingMapping.AvatarId = avatarId;
                    }
                    else
                    {
                        config.UserAvatars.Add(new UserAvatarMapping
                        {
                            UserId = userId.ToString(),
                            AvatarId = avatarId
                        });
                    }

                    plugin.SaveConfiguration();
                }
                catch
                {
                    RestoreMappings(config, previousMappings, previousMappingEntries, previousMappingValues);
                    RestoreFiles(previousConfigurationFile);
                    throw;
                }
            }
        }

        private static void CaptureFile(IDictionary<string, byte[]?> snapshots, string? path)
        {
            if (!string.IsNullOrEmpty(path) && !snapshots.ContainsKey(path))
            {
                snapshots[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        }

        private void RestoreFiles(IDictionary<string, byte[]?> snapshots)
        {
            foreach (var (path, contents) in snapshots)
            {
                try
                {
                    if (contents == null)
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    else
                    {
                        var directory = Path.GetDirectoryName(path)!;
                        Directory.CreateDirectory(directory);
                        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.rollback");
                        try
                        {
                            File.WriteAllBytes(temporaryPath, contents);
                            File.Move(temporaryPath, path, overwrite: true);
                        }
                        finally
                        {
                            if (File.Exists(temporaryPath))
                            {
                                File.Delete(temporaryPath);
                            }
                        }
                    }
                }
                catch (Exception rollbackException)
                {
                    _logger.LogWarning(rollbackException, "Failed to restore avatar state file {Path}", path);
                }
            }
        }

        private static void RestoreMappings(
            PluginConfiguration config,
            List<UserAvatarMapping>? previousMappings,
            UserAvatarMapping[]? previousEntries,
            (UserAvatarMapping Mapping, string UserId, string AvatarId)[]? previousValues)
        {
            if (previousMappings != null && previousEntries != null && previousValues != null)
            {
                foreach (var (mapping, userId, avatarId) in previousValues)
                {
                    mapping.UserId = userId;
                    mapping.AvatarId = avatarId;
                }

                previousMappings.Clear();
                previousMappings.AddRange(previousEntries);
            }

            config.UserAvatars = previousMappings ?? new List<UserAvatarMapping>();
        }

        /// <summary>
        /// Gets all users, compatible with both Jellyfin ≤10.11.8 (Users property)
        /// and Jellyfin ≥10.11.9 (GetUsers() method).
        /// See the changelog for release 10.11.9: https://github.com/jellyfin/jellyfin/compare/v10.11.8...v10.11.9.
        /// </summary>
        private IEnumerable<User> GetAllUsers()
            => _userManager.GetUsers() ?? Enumerable.Empty<User>();

        /// <summary>
        /// Gets the avatar ID that a user has selected.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The avatar ID, or null if no avatar is set.</returns>
        public string? GetUserAvatarId(Guid userId)
        {
            if (Plugin.Instance == null)
            {
                _logger.LogError("Plugin instance is null in GetUserAvatarId");
                return null;
            }

            using (Plugin.AcquireConfigurationLock())
            {
                var config = Plugin.Config;
                if (config.UserAvatars == null)
                {
                    return null;
                }

                var mapping = config.UserAvatars.FirstOrDefault(x => x.UserId == userId.ToString());
                return mapping?.AvatarId;
            }
        }

        /// <summary>
        /// Validates all user avatars and repairs any missing profile images.
        /// This should be called at plugin startup to ensure avatars are not lost.
        /// </summary>
        /// <returns>The number of avatars that were repaired.</returns>
        public async Task<int> ValidateUserAvatarsAsync()
        {
            if (Plugin.Instance == null)
            {
                _logger.LogError("Plugin instance is null, cannot validate avatars");
                return 0;
            }

            UserAvatarMapping[] mappings;
            using (Plugin.AcquireConfigurationLock())
            {
                mappings = Plugin.Config.UserAvatars?
                    .Select(mapping => new UserAvatarMapping { UserId = mapping.UserId, AvatarId = mapping.AvatarId })
                    .ToArray() ?? Array.Empty<UserAvatarMapping>();
            }

            if (mappings.Length == 0)
            {
                _logger.LogInformation("No user avatars to validate");
                return 0;
            }

            var repairedCount = 0;
            var mappingsToRemove = new HashSet<(string UserId, string AvatarId)>();

            foreach (var mapping in mappings)
            {
                try
                {
                    if (!Guid.TryParse(mapping.UserId, out var userId))
                    {
                        _logger.LogWarning("Invalid user ID in mapping: {UserId}", mapping.UserId);
                        mappingsToRemove.Add((mapping.UserId, mapping.AvatarId));
                        continue;
                    }

                    var user = _userManager.GetUserById(userId);
                    if (user == null)
                    {
                        _logger.LogWarning("User not found for avatar mapping: {UserId}", mapping.UserId);
                        mappingsToRemove.Add((mapping.UserId, mapping.AvatarId));
                        continue;
                    }

                    var profileImageExists = user.ProfileImage != null
                        && !string.IsNullOrEmpty(user.ProfileImage.Path)
                        && File.Exists(user.ProfileImage.Path);

                    if (profileImageExists)
                    {
                        _logger.LogDebug("Avatar for user {UserName} is valid", user.Username);
                        continue;
                    }

                    _logger.LogWarning(
                        "Profile image missing for user {UserName} (expected: {Path}). Attempting to repair...",
                        user.Username,
                        user.ProfileImage?.Path ?? "null");

                    using var avatarFile = OpenAvatarRead(mapping.AvatarId);
                    if (avatarFile == null)
                    {
                        _logger.LogError(
                            "Cannot repair avatar for user {UserName}: Avatar {AvatarId} no longer exists in pool",
                            user.Username,
                            mapping.AvatarId);

                        mappingsToRemove.Add((mapping.UserId, mapping.AvatarId));

                        await ClearProfileImageForStaleAvatarMappingAsync(userId, mapping.AvatarId).ConfigureAwait(false);

                        continue;
                    }

                    await SetUserAvatarAsync(userId, mapping.AvatarId);
                    repairedCount++;
                    _logger.LogInformation("Successfully repaired avatar for user {UserName}", user.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating avatar for user {UserId}", mapping.UserId);
                }
            }

            var removedCount = 0;
            using (Plugin.AcquireConfigurationLock())
            {
                var config = Plugin.Config;
                removedCount = config.UserAvatars?.RemoveAll(mapping => mappingsToRemove.Contains((mapping.UserId, mapping.AvatarId))) ?? 0;
                if (removedCount > 0)
                {
                    Plugin.Instance.SaveConfiguration();
                }
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Removed {Count} invalid avatar mappings", removedCount);
            }

            _logger.LogInformation(
                "Avatar validation complete. Repaired: {Repaired}, Removed invalid: {Removed}",
                repairedCount,
                removedCount);

            return repairedCount;
        }

        private async Task ClearProfileImageForStaleAvatarMappingAsync(Guid userId, string avatarId)
        {
            await UserAvatarUpdateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using var profileImageUpdateLock = await _userManager.AcquireProfileImageUpdateLockAsync(userId).ConfigureAwait(false);
                using (Plugin.AcquireConfigurationLock())
                {
                    var currentMapping = Plugin.Config.UserAvatars?
                        .FirstOrDefault(mapping => mapping.UserId == userId.ToString());
                    if (currentMapping?.AvatarId != avatarId)
                    {
                        return;
                    }
                }

                var user = _userManager.GetUserById(userId);
                if (user?.ProfileImage == null)
                {
                    return;
                }

                user.ProfileImage = null;
                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                _logger.LogInformation("Cleared profile image reference for user {UserName}", user.Username);
            }
            finally
            {
                UserAvatarUpdateLock.Release();
            }
        }

        /// <summary>
        /// Cleans up orphaned profile image files in user directories.
        /// This removes old profile images that are no longer referenced by the user.
        /// </summary>
        /// <returns>The number of orphaned files deleted.</returns>
        public int CleanOrphanedProfileImages()
        {
            var deletedCount = 0;

            try
            {
                var users = GetAllUsers();
                foreach (var user in users)
                {
                    try
                    {
                        var usersRoot = Path.Combine(_appPaths.DataPath, "users");
                        var userDataPath = Path.Combine(usersRoot, user.Id.ToString("N"));
                        if (!Directory.Exists(userDataPath)
                            || !FileSystemHelper.IsPathWithinRoot(userDataPath, usersRoot))
                        {
                            continue;
                        }

                        var profileFiles = Directory.GetFiles(userDataPath, "profile_*");
                        var currentProfilePath = user.ProfileImage?.Path;

                        foreach (var file in profileFiles)
                        {
                            if (string.Equals(file, currentProfilePath, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!FileSystemHelper.IsPathWithinRoot(file, userDataPath))
                            {
                                _logger.LogWarning("Skipping profile image outside the managed user directory: {File}", file);
                                continue;
                            }

                            try
                            {
                                var result = TryDeleteProfileImageFile(file, userDataPath);
                                if (result == AvatarFileDeleteResult.Deleted)
                                {
                                    deletedCount++;
                                    _logger.LogDebug("Deleted orphaned profile image: {File}", file);
                                }
                                else if (result == AvatarFileDeleteResult.Unsafe)
                                {
                                    _logger.LogWarning("Skipping unsafe orphaned profile image: {File}", file);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not delete orphaned file: {File}", file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error cleaning profile images for user {UserId}", user.Id);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} orphaned profile images", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned profile image cleanup");
            }

            return deletedCount;
        }

        /// <summary>
        /// Gets a random avatar from the available pool.
        /// </summary>
        /// <returns>A random avatar info, or null if no avatars exist.</returns>
        public AvatarInfo? GetRandomAvatar()
        {
            using (Plugin.AcquireConfigurationLock())
            {
                var avatars = Plugin.Config.AvailableAvatars;
                if (avatars == null || avatars.Count == 0)
                {
                    return null;
                }

                var index = Random.Shared.Next(avatars.Count);
                return avatars[index];
            }
        }

        /// <summary>
        /// Assigns a random avatar to a user.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="forceAssign">When true, bypasses the auto-assign setting.</param>
        /// <returns>True if an avatar was assigned.</returns>
        public async Task<bool> AssignRandomAvatarAsync(User user, bool forceAssign = false)
        {
            if (Plugin.Instance == null)
            {
                return false;
            }

            using (Plugin.AcquireConfigurationLock())
            {
                if (!forceAssign && !Plugin.Config.EnableAutoAssign)
                {
                    return false;
                }
            }

            var hasProfileImage = user.ProfileImage != null
                && !string.IsNullOrEmpty(user.ProfileImage.Path)
                && File.Exists(user.ProfileImage.Path);

            if (hasProfileImage)
            {
                return false;
            }

            var target = GetRandomAvatar();
            if (target == null)
            {
                return false;
            }

            await SetUserAvatarAsync(user.Id, target.Id).ConfigureAwait(false);
            _logger.LogInformation("Auto-assigned random avatar {AvatarId} to user {UserName}", target.Id, user.Username);
            return true;
        }

        /// <summary>
        /// Assigns a default or random avatar to all users who have no avatar set.
        /// </summary>
        /// <returns>The number of users who received an avatar.</returns>
        public async Task<int> AssignMissingAvatarsAsync()
        {
            if (Plugin.Instance == null)
            {
                return 0;
            }

            bool hasAvatars;
            bool autoAssignEnabled;
            using (Plugin.AcquireConfigurationLock())
            {
                var config = Plugin.Config;
                hasAvatars = config.AvailableAvatars is { Count: > 0 };
                autoAssignEnabled = config.EnableAutoAssign;
            }

            if (!hasAvatars || !autoAssignEnabled)
            {
                return 0;
            }

            var assignedCount = 0;
            var users = GetAllUsers();

            foreach (var user in users)
            {
                try
                {
                    if (await AssignRandomAvatarAsync(user).ConfigureAwait(false))
                    {
                        assignedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-assign avatar to user {UserId}", user.Id);
                }
            }

            return assignedCount;
        }

        /// <summary>
        /// Removes the avatar assignment from a user without deleting the avatar from the pool.
        /// This clears the user's profile image and removes the mapping.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> RemoveUserAvatarAsync(Guid userId)
        {
            await UserAvatarUpdateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using var profileImageUpdateLock = await _userManager.AcquireProfileImageUpdateLockAsync(userId).ConfigureAwait(false);
                try
                {
                    if (Plugin.Instance == null)
                    {
                        _logger.LogError("Plugin instance is null");
                        return false;
                    }

                    var user = _userManager.GetUserById(userId);
                    if (user == null)
                    {
                        _logger.LogWarning("User not found: {UserId}", userId);
                        return false;
                    }

                    var oldProfileImagePath = user.ProfileImage?.Path;

                    user.ProfileImage = null;
                    await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(oldProfileImagePath))
                    {
                        var userDataPath = Path.Combine(
                            _serverConfigurationManager.ApplicationPaths.UserConfigurationDirectoryPath,
                            user.Username);
                        try
                        {
                            var result = TryDeleteProfileImageFile(oldProfileImagePath, userDataPath);
                            if (result == AvatarFileDeleteResult.Deleted)
                            {
                                _logger.LogInformation("Deleted profile image for user {UserId}", userId);
                            }
                            else if (result == AvatarFileDeleteResult.Unsafe)
                            {
                                _logger.LogWarning(
                                    "Retaining profile image outside or unsafe for the user's configuration directory for user {UserId}",
                                    userId);
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or PlatformNotSupportedException)
                        {
                            _logger.LogWarning(ex, "Could not delete profile image file for user {UserId}", userId);
                        }
                    }

                    using (Plugin.AcquireConfigurationLock())
                    {
                        var config = Plugin.Config;
                        var mapping = config.UserAvatars?.FirstOrDefault(x => x.UserId == userId.ToString());
                        if (mapping != null)
                        {
                            config.UserAvatars?.Remove(mapping);
                            Plugin.Instance.SaveConfiguration();
                        }
                    }

                    _logger.LogInformation("Removed avatar assignment for user {UserName}", user.Username);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove avatar for user {UserId}", userId);
                    return false;
                }
            }
            finally
            {
                UserAvatarUpdateLock.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _avatarFileStore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
