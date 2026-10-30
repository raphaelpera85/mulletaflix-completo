using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Updates;
using MediaBrowser.Controller;
using MediaBrowser.Model.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Exposes server update availability and in-place upgrade execution for the dashboard update center.
/// </summary>
[Route("System")]
[Authorize(Policy = Policies.RequiresElevation)]
public class UpdateInfoController : BaseMulletaFlixApiController
{
    private const string UpdateManifestUrlEnv = "MulletaFlix_UPDATE_MANIFEST_URL";
    private const string GitHubTokenEnv = "MulletaFlix_GITHUB_TOKEN";
    private const string DefaultGitHubRepo = "raphaelpera85/mulletaflix-completo";
    private const string DefaultGitHubApiUrl = $"https://api.github.com/repos/{DefaultGitHubRepo}/releases";
    private const string GitHubReleaseAssetPathPrefix = "/raphaelpera85/mulletaflix-completo/releases/download/";
    private const string EmbeddedUpdaterResource = "MulletaFlix.Api.Update.apply-update.ps1";

    private static readonly object SyncLock = new();
    private static string _installState = "Idle"; // Idle, Downloading, Extracting, ReadyToApply, Applying, Failed
    private static int _installProgress = 0;
    private static string? _installError = null;
    private static string? _extractedUpdatePath = null;
    private static string? _downloadedVersion = null;
    private static CancellationTokenSource? _activeCts = null;
    private static UpdateInfoDto? _cachedUpdateInfo;

    private readonly IServerApplicationHost _applicationHost;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ISystemManager _systemManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateInfoController> _logger;
    private readonly Func<ProcessStartInfo, Process?> _processLauncher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInfoController"/> class.
    /// </summary>
    /// <param name="applicationHost">The application host.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="systemManager">The system manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public UpdateInfoController(
        IServerApplicationHost applicationHost,
        IApplicationPaths applicationPaths,
        ISystemManager systemManager,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateInfoController> logger)
        : this(applicationHost, applicationPaths, systemManager, httpClientFactory, logger, null)
    {
    }

    internal UpdateInfoController(
        IServerApplicationHost applicationHost,
        IApplicationPaths applicationPaths,
        ISystemManager systemManager,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateInfoController> logger,
        Func<ProcessStartInfo, Process?>? processLauncher)
    {
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _systemManager = systemManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _processLauncher = processLauncher ?? (startInfo => Process.Start(startInfo));
    }

    /// <summary>
    /// Gets current vs. available server version, changelog, and current install state.
    /// </summary>
    /// <response code="200">Update information returned.</response>
    /// <response code="403">User does not have permission to check for updates.</response>
    /// <returns>An <see cref="UpdateInfoDto"/> with update availability.</returns>
    [HttpGet("UpdateInfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UpdateInfoDto>> GetUpdateInfo(CancellationToken cancellationToken)
    {
        var info = new UpdateInfoDto
        {
            CurrentVersion = _applicationHost.ApplicationVersionString,
            InstallState = _installState,
            InstallProgress = _installProgress,
            ErrorMessage = _installError
        };

        var manifestUrl = Environment.GetEnvironmentVariable(UpdateManifestUrlEnv);
        var targetUrl = !string.IsNullOrWhiteSpace(manifestUrl) ? manifestUrl : DefaultGitHubApiUrl;

        try
        {
            var client = CreateConfiguredClient();
            var response = await client.GetAsync(targetUrl, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var contentString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // Try parsing as standard GitHub release JSON first
                if (TryParseGitHubRelease(contentString, out var remoteVersion, out var changelog, out var archiveUrl, out var size))
                {
                    info.AvailableVersion = remoteVersion.ToString(3);
                    info.UpdateAvailable = remoteVersion > _applicationHost.ApplicationVersion;
                    info.Changelog = changelog;
                    info.ArchiveUrl = IsAllowedGitHubReleaseArchiveUrl(archiveUrl) ? archiveUrl : null;
                    info.PackageSize = size;
                }
                else if (TryParseCustomManifest(contentString, out var customVersion, out var customChangelog, out var customUrl))
                {
                    info.AvailableVersion = customVersion.ToString(3);
                    info.UpdateAvailable = customVersion > _applicationHost.ApplicationVersion;
                    info.Changelog = customChangelog;
                    info.ArchiveUrl = IsAllowedGitHubReleaseArchiveUrl(customUrl) ? customUrl : null;
                }
            }
            else
            {
                _logger.LogWarning("Update check returned HTTP status {StatusCode}.", response.StatusCode);
            }

            info.LastCheckedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch server update information.");
        }

        var updatesDir = Path.Combine(_applicationPaths.ProgramDataPath, "updates");
        var extractDir = Path.Combine(updatesDir, "extracted");
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (info.UpdateAvailable && !string.IsNullOrWhiteSpace(info.ArchiveUrl))
        {
            // 1. Check if update was already downloaded and verified on disk
            TryRestorePersistentState(updatesDir, extractDir, info.AvailableVersion);

            // 2. Se os arquivos extraídos são idênticos aos instalados, não há atualização a ser feita
            if (Directory.Exists(extractDir) && AreUpdateFilesIdentical(extractDir, installDir, _logger))
            {
                _logger.LogInformation("Arquivos da atualização são idênticos aos instalados em {InstallDir}. Ocultando atualização.", installDir);
                info.UpdateAvailable = false;
                lock (SyncLock)
                {
                    _installState = "Idle";
                    _installProgress = 0;
                    _extractedUpdatePath = null;
                }

                CleanupObsoleteUpdates(updatesDir, extractDir);
            }
            else
            {
                lock (SyncLock)
                {
                    // 3. If not yet downloaded and not currently in progress, auto-download in background!
                    if (_installState is "Idle")
                    {
                        _logger.LogInformation("New server update {Version} detected. Starting automatic background download...", info.AvailableVersion);
                        StartBackgroundDownload(info.ArchiveUrl, info.AvailableVersion);
                    }
                }
            }
        }
        else if (!info.UpdateAvailable && _applicationHost.ApplicationVersion > new Version(0, 0, 0))
        {
            if (_installState != "Applying")
            {
                CleanupObsoleteUpdates(updatesDir, extractDir);
            }
        }

        lock (SyncLock)
        {
            info.InstallState = _installState;
            info.InstallProgress = _installProgress;
            info.ErrorMessage = _installError;
            _cachedUpdateInfo = info;
        }

        return info;
    }

    /// <summary>
    /// Gets real-time update installation progress and state.
    /// </summary>
    /// <response code="200">Installation status returned.</response>
    /// <returns>The installation state and progress.</returns>
    [HttpGet("Update/Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UpdateInfoDto> GetUpdateStatus()
    {
        lock (SyncLock)
        {
            var info = _cachedUpdateInfo != null
                ? new UpdateInfoDto
                {
                    CurrentVersion = _cachedUpdateInfo.CurrentVersion,
                    AvailableVersion = _cachedUpdateInfo.AvailableVersion,
                    UpdateAvailable = _cachedUpdateInfo.UpdateAvailable,
                    Changelog = _cachedUpdateInfo.Changelog,
                    ArchiveUrl = _cachedUpdateInfo.ArchiveUrl,
                    PackageSize = _cachedUpdateInfo.PackageSize,
                    LastCheckedAt = _cachedUpdateInfo.LastCheckedAt,
                    InstallState = _installState,
                    InstallProgress = _installProgress,
                    ErrorMessage = _installError
                }
                : new UpdateInfoDto
                {
                    CurrentVersion = _applicationHost.ApplicationVersionString,
                    InstallState = _installState,
                    InstallProgress = _installProgress,
                    ErrorMessage = _installError
                };

            return info;
        }
    }

    /// <summary>
    /// Downloads and extracts the update package in the background.
    /// </summary>
    /// <response code="202">Update process initiated.</response>
    /// <response code="400">No update archive URL available.</response>
    /// <returns>Update status.</returns>
    [HttpPost("Update/Install")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> InstallUpdate([FromQuery] string? archiveUrl, [FromQuery] bool force = false)
    {
        if (!string.IsNullOrWhiteSpace(archiveUrl) && !IsAllowedGitHubReleaseArchiveUrl(archiveUrl))
        {
            return BadRequest(new { Message = "The update archive URL is not an approved MulletaFlix release asset." });
        }

        lock (SyncLock)
        {
            if (_installState is "Downloading" or "Extracting" or "Applying")
            {
                return Accepted(new { Message = "Update is already in progress.", State = _installState, Progress = _installProgress });
            }

            if (!force && _installState == "ReadyToApply" && !string.IsNullOrWhiteSpace(_extractedUpdatePath) && Directory.Exists(_extractedUpdatePath))
            {
                return Accepted(new { Message = "Update package is already downloaded and ready to apply upon user approval.", State = _installState, Progress = _installProgress });
            }
        }

        string? availableVersion = _cachedUpdateInfo?.AvailableVersion;
        if (string.IsNullOrWhiteSpace(archiveUrl))
        {
            // Resolve from latest release if not passed
            var updateInfo = (await GetUpdateInfo(CancellationToken.None).ConfigureAwait(false)).Value;
            archiveUrl = updateInfo?.ArchiveUrl;
            availableVersion = updateInfo?.AvailableVersion;
            if (string.IsNullOrWhiteSpace(archiveUrl))
            {
                return BadRequest(new { Message = "No update archive URL found for the latest release." });
            }
        }

        if (!IsAllowedGitHubReleaseArchiveUrl(archiveUrl))
        {
            return BadRequest(new { Message = "No approved MulletaFlix release archive URL is available." });
        }

        StartBackgroundDownload(archiveUrl!, availableVersion);
        return Accepted(new { Message = "Update download started in background.", State = _installState, Progress = _installProgress });
    }

    /// <summary>
    /// Applies the downloaded update in-place and restarts MulletaFlix.
    /// </summary>
    /// <response code="200">Update execution initiated.</response>
    /// <response code="400">Update is not ready to be applied.</response>
    /// <returns>Result message.</returns>
    [HttpPost("Update/Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult ApplyUpdate()
    {
        string? updateSource;
        lock (SyncLock)
        {
            if (_installState != "ReadyToApply" || string.IsNullOrWhiteSpace(_extractedUpdatePath) || !Directory.Exists(_extractedUpdatePath))
            {
                return BadRequest(new { Message = "Update package is not downloaded or ready to apply." });
            }

            _installState = "Applying";
            _installProgress = 100;
            updateSource = _extractedUpdatePath;
        }

        try
        {
            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dataDir = _applicationPaths.ProgramDataPath;
            var pid = Environment.ProcessId;

            // Always execute updater script from updatesDir (outside installDir) to prevent file locks on installDir
            var updatesDir = Path.Combine(dataDir, "updates");
            Directory.CreateDirectory(updatesDir);
            var scriptPath = Path.Combine(updatesDir, "apply-update.ps1");
            WriteEmbeddedUpdaterScript(scriptPath);

            _logger.LogInformation("Launching in-place updater: {ScriptPath} with source {Source} target {Target} PID {Pid}", scriptPath, updateSource, installDir, pid);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\" -InstallDirectory \"{installDir}\" -UpdateSourceDirectory \"{updateSource}\" -ProcessId {pid} -DataDirectory \"{dataDir}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            if (_processLauncher(startInfo) is null)
            {
                throw new InvalidOperationException("The operating system did not start the updater process.");
            }
        }
        catch (Exception ex)
        {
            const string message = "The updater could not be started. The downloaded package is still available; try applying it again.";
            lock (SyncLock)
            {
                _installState = "ReadyToApply";
                _installProgress = 100;
                _installError = message;
            }

            _logger.LogError(ex, "Could not launch the in-place updater; restoring the update state to ReadyToApply.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = message, State = _installState, Progress = _installProgress });
        }

        // Schedule server shutdown to release DLL and file locks
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500).ConfigureAwait(false);
            _logger.LogInformation("Shutting down MulletaFlix to allow in-place file replacement.");
            _systemManager.Shutdown();
        });

        return new OkObjectResult(new { Message = "Update application started. Server is shutting down to apply updates." });
    }

    internal HttpClient CreateConfiguredClient(bool includeGitHubToken = true)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MulletaFlix", _applicationHost.ApplicationVersionString));

        var token = Environment.GetEnvironmentVariable(GitHubTokenEnv);
        if (includeGitHubToken && !string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = null;
        }

        return client;
    }

    private void StartBackgroundDownload(string archiveUrl, string? availableVersion)
    {
        lock (SyncLock)
        {
            if (_installState is "Downloading" or "Extracting" or "Applying")
            {
                return;
            }

            _activeCts?.Cancel();
            _activeCts = new CancellationTokenSource();
            _installState = "Downloading";
            _installProgress = 0;
            _installError = null;
            _downloadedVersion = availableVersion;
        }

        var token = _activeCts.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await DownloadAndPrepareUpdateAsync(archiveUrl, availableVersion, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (SyncLock)
                    {
                        _installState = "Failed";
                        _installError = ex.Message;
                    }

                    _logger.LogError(ex, "Failed to automatically download and extract update package.");
                }
            },
            token);
    }

    private void TryRestorePersistentState(string updatesDir, string extractDir, string? availableVersion)
    {
        var stateFile = Path.Combine(updatesDir, "update_state.json");
        try
        {
            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (System.IO.File.Exists(stateFile))
            {
                var json = System.IO.File.ReadAllText(stateFile);
                var state = JsonSerializer.Deserialize<UpdatePersistentState>(json);
                if (state != null
                    && !string.IsNullOrWhiteSpace(state.DownloadedVersion)
                    && state.InstallState == "ReadyToApply"
                    && (string.IsNullOrWhiteSpace(availableVersion) || string.Equals(state.DownloadedVersion, availableVersion, StringComparison.OrdinalIgnoreCase))
                    && Directory.Exists(state.ExtractedPath ?? extractDir)
                    && Directory.EnumerateFileSystemEntries(state.ExtractedPath ?? extractDir).Any())
                {
                    var targetExtract = state.ExtractedPath ?? extractDir;
                    if (AreUpdateFilesIdentical(targetExtract, installDir, _logger))
                    {
                        _logger.LogInformation("Arquivos extraídos em {Dir} são idênticos aos instalados. Descartando atualização redundante.", targetExtract);
                        CleanupObsoleteUpdates(updatesDir, targetExtract);
                        return;
                    }

                    lock (SyncLock)
                    {
                        _installState = "ReadyToApply";
                        _installProgress = 100;
                        _installError = null;
                        _extractedUpdatePath = targetExtract;
                        _downloadedVersion = state.DownloadedVersion;
                    }

                    return;
                }
            }

            // Fallback check: If extractDir has files and package.zip exists with non-zero length
            var packageZip = Path.Combine(updatesDir, "package.zip");
            if (Directory.Exists(extractDir)
                && Directory.EnumerateFileSystemEntries(extractDir).Any()
                && System.IO.File.Exists(packageZip)
                && new FileInfo(packageZip).Length > 1024 * 1024
                && !string.IsNullOrWhiteSpace(availableVersion))
            {
                if (AreUpdateFilesIdentical(extractDir, installDir, _logger))
                {
                    _logger.LogInformation("Arquivos em {Dir} são idênticos aos instalados. Descartando atualização redundante.", extractDir);
                    CleanupObsoleteUpdates(updatesDir, extractDir);
                    return;
                }

                lock (SyncLock)
                {
                    _installState = "ReadyToApply";
                    _installProgress = 100;
                    _installError = null;
                    _extractedUpdatePath = extractDir;
                    _downloadedVersion = availableVersion;
                }

                SavePersistentState(updatesDir, extractDir, availableVersion, "ReadyToApply");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not restore update state from {StateFile}", stateFile);
        }
    }

    private static void SavePersistentState(string updatesDir, string extractDir, string? version, string state)
    {
        try
        {
            Directory.CreateDirectory(updatesDir);
            var stateFile = Path.Combine(updatesDir, "update_state.json");
            var stateObj = new UpdatePersistentState
            {
                DownloadedVersion = version,
                InstallState = state,
                ExtractedPath = extractDir,
                CompletedAt = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(stateObj);
            System.IO.File.WriteAllText(stateFile, json);
        }
        catch
        {
            // Best effort
        }
    }

    private void CleanupObsoleteUpdates(string updatesDir, string extractDir)
    {
        try
        {
            var stateFile = Path.Combine(updatesDir, "update_state.json");
            if (System.IO.File.Exists(stateFile))
            {
                System.IO.File.Delete(stateFile);
            }

            var packageZip = Path.Combine(updatesDir, "package.zip");
            if (System.IO.File.Exists(packageZip))
            {
                System.IO.File.Delete(packageZip);
            }

            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean up obsolete updates.");
        }

        lock (SyncLock)
        {
            _installState = "Idle";
            _installProgress = 0;
            _extractedUpdatePath = null;
            _downloadedVersion = null;
        }
    }

    private async Task DownloadAndPrepareUpdateAsync(string archiveUrl, string? availableVersion, CancellationToken cancellationToken)
    {
        if (!IsAllowedGitHubReleaseArchiveUrl(archiveUrl))
        {
            throw new InvalidOperationException("The update archive URL is not an approved MulletaFlix release asset.");
        }

        var updatesDir = Path.Combine(_applicationPaths.ProgramDataPath, "updates");
        Directory.CreateDirectory(updatesDir);

        var packageZip = Path.Combine(updatesDir, "package.zip");
        var extractDir = Path.Combine(updatesDir, "extracted");

        if (System.IO.File.Exists(packageZip))
        {
            System.IO.File.Delete(packageZip);
        }

        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }

        // 1. Download
        _logger.LogInformation("Downloading update package to {Dest}.", packageZip);
        // GitHub credentials are only needed for the release API. Never attach them to asset
        // downloads, which can redirect to a separate GitHub-owned host.
        var client = CreateConfiguredClient(includeGitHubToken: false);
        using (var response = await client.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            if (!IsAllowedGitHubArchiveResponseUri(response.RequestMessage?.RequestUri))
            {
                throw new InvalidOperationException("The update archive redirected to an unapproved host.");
            }

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var readBytes = 0L;

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var fileStream = new FileStream(packageZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[64 * 1024];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    readBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((readBytes * 80) / totalBytes);
                        lock (SyncLock)
                        {
                            _installProgress = Math.Clamp(progress, 0, 80);
                        }
                    }
                }
            }
        }

        // 2. Extract
        lock (SyncLock)
        {
            _installState = "Extracting";
            _installProgress = 85;
        }

        _logger.LogInformation("Extracting update package to {ExtractDir}", extractDir);
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(packageZip, extractDir, true);

        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (AreUpdateFilesIdentical(extractDir, installDir, _logger))
        {
            _logger.LogInformation("Arquivos extraídos da atualização são idênticos aos instalados. Atualização dispensada.");
            lock (SyncLock)
            {
                _installState = "Idle";
                _installProgress = 0;
                _extractedUpdatePath = null;
            }

            CleanupObsoleteUpdates(updatesDir, extractDir);
            return;
        }

        lock (SyncLock)
        {
            _extractedUpdatePath = extractDir;
            _installState = "ReadyToApply";
            _installProgress = 100;
            _installError = null;
            _downloadedVersion = availableVersion;
        }

        SavePersistentState(updatesDir, extractDir, availableVersion, "ReadyToApply");
        _logger.LogInformation("Update package is ready to be applied from {ExtractDir} upon user approval.", extractDir);
    }

    internal static bool IsAllowedGitHubReleaseArchiveUrl(string? archiveUrl)
    {
        if (!Uri.TryCreate(archiveUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port != 443
            || !string.Equals(uri.IdnHost, "github.com", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var decodedPath = Uri.UnescapeDataString(uri.AbsolutePath);
        if (!decodedPath.StartsWith(GitHubReleaseAssetPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var assetPath = decodedPath[GitHubReleaseAssetPathPrefix.Length..];
        var segments = assetPath.Split('/');
        return segments.Length >= 2
            && segments.All(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..")
            && !assetPath.Contains("\\", StringComparison.Ordinal);
    }

    internal static bool IsAllowedGitHubArchiveResponseUri(Uri? uri)
    {
        if (uri is null
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port != 443)
        {
            return false;
        }

        return IsAllowedGitHubReleaseArchiveUrl(uri.AbsoluteUri)
            || string.Equals(uri.IdnHost, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.IdnHost, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryParseGitHubRelease(string json, out Version version, out string? changelog, out string? archiveUrl, out long? size)
    {
        version = new Version(0, 0, 0);
        changelog = null;
        archiveUrl = null;
        size = null;

        try
        {
            var trimmed = json.TrimStart();
            var candidates = new List<GitHubReleasePayload>();

            if (trimmed.StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<GitHubReleasePayload>>(json);
                if (list != null)
                {
                    candidates.AddRange(list);
                }
            }
            else if (trimmed.StartsWith('{'))
            {
                var single = JsonSerializer.Deserialize<GitHubReleasePayload>(json);
                if (single != null)
                {
                    candidates.Add(single);
                }
            }

            GitHubReleasePayload? bestRelease = null;
            GitHubReleaseAsset? bestArchive = null;
            var bestVersion = new Version(0, 0, 0);

            foreach (var release in candidates)
            {
                if (release.Prerelease || string.IsNullOrWhiteSpace(release.TagName))
                {
                    continue;
                }

                // Filter out mobile app releases
                if (release.TagName.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var cleanTag = release.TagName.Trim().TrimStart('v', 'V');
                if (!Version.TryParse(cleanTag, out var parsed))
                {
                    continue;
                }

                var zipAsset = ServerUpdateArchiveSelector.Select(release.Assets, asset => asset.Name);
                if (zipAsset == null)
                {
                    continue;
                }

                if (parsed > bestVersion)
                {
                    bestVersion = parsed;
                    bestRelease = release;
                    bestArchive = zipAsset;
                }
            }

            if (bestRelease != null && bestArchive != null && bestVersion > new Version(0, 0, 0))
            {
                version = bestVersion;
                changelog = bestRelease.Body;
                archiveUrl = bestArchive.BrowserDownloadUrl;
                size = bestArchive.Size;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseCustomManifest(string json, out Version version, out string? changelog, out string? archiveUrl)
    {
        version = new Version(0, 0, 0);
        changelog = null;
        archiveUrl = null;

        try
        {
            var manifest = System.Text.Json.JsonSerializer.Deserialize<ServerUpdateManifest>(json);
            if (manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.Version)
                && Version.TryParse(manifest.Version, out var parsed))
            {
                version = parsed;
                changelog = manifest.Changelog;
                archiveUrl = manifest.ArchiveUrl;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static void WriteEmbeddedUpdaterScript(string destinationPath)
    {
        using var updaterScript = typeof(UpdateInfoController).Assembly.GetManifestResourceStream(EmbeddedUpdaterResource)
            ?? throw new InvalidOperationException($"Embedded updater resource '{EmbeddedUpdaterResource}' is missing.");
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        updaterScript.CopyTo(destination);
    }

    /// <summary>
    /// Compara se todos os arquivos do pacote de atualização já estão presentes e são idênticos aos arquivos instalados.
    /// Se forem idênticos, nenhuma atualização é necessária.
    /// </summary>
    internal static bool AreUpdateFilesIdentical(string updateDir, string installDir, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(updateDir) || string.IsNullOrWhiteSpace(installDir)
            || !Directory.Exists(updateDir) || !Directory.Exists(installDir))
        {
            return false;
        }

        try
        {
            var updateFiles = Directory.EnumerateFiles(updateDir, "*", SearchOption.AllDirectories).ToList();
            if (updateFiles.Count == 0)
            {
                return false;
            }

            foreach (var updateFile in updateFiles)
            {
                var relPath = Path.GetRelativePath(updateDir, updateFile);

                // Ignora scripts de atualização e arquivos temporários
                if (string.Equals(relPath, "apply-update.ps1", StringComparison.OrdinalIgnoreCase)
                    || relPath.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || relPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                    || relPath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var installFile = Path.Combine(installDir, relPath);
                if (!System.IO.File.Exists(installFile))
                {
                    logger?.LogDebug("Arquivo da atualização não encontrado na instalação: {File}", relPath);
                    return false;
                }

                var updateInfo = new FileInfo(updateFile);
                var installInfo = new FileInfo(installFile);

                if (updateInfo.Length != installInfo.Length)
                {
                    logger?.LogDebug("Tamanho diferente para {File}: update={UpdateLen}, instalado={InstallLen}", relPath, updateInfo.Length, installInfo.Length);
                    return false;
                }

                if (!AreFileContentsIdentical(updateFile, installFile))
                {
                    logger?.LogDebug("Conteúdo divergente em {File}", relPath);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Erro ao comparar arquivos da atualização com arquivos instalados.");
            return false;
        }
    }

    private static bool AreFileContentsIdentical(string fileA, string fileB)
    {
        const int BufferSize = 64 * 1024;
        var bufferA = new byte[BufferSize];
        var bufferB = new byte[BufferSize];

        using var streamA = new FileStream(fileA, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var streamB = new FileStream(fileB, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (streamA.Length != streamB.Length)
        {
            return false;
        }

        int bytesReadA;
        while ((bytesReadA = streamA.Read(bufferA, 0, bufferA.Length)) > 0)
        {
            var bytesReadB = streamB.Read(bufferB, 0, bytesReadA);
            if (bytesReadA != bytesReadB)
            {
                return false;
            }

            if (!bufferA.AsSpan(0, bytesReadA).SequenceEqual(bufferB.AsSpan(0, bytesReadB)))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class GitHubReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    private sealed class ServerUpdateManifest
    {
        public string Version { get; set; } = string.Empty;

        public string ArchiveUrl { get; set; } = string.Empty;

        public string? Checksum { get; set; }

        public string? Changelog { get; set; }
    }

    private sealed class UpdatePersistentState
    {
        public string? DownloadedVersion { get; set; }

        public string? InstallState { get; set; }

        public string? ExtractedPath { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}

