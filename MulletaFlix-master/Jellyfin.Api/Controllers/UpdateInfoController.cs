using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
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
    private const string DefaultGitHubApiUrl = $"https://api.github.com/repos/{DefaultGitHubRepo}/releases/latest";

    private static readonly object SyncLock = new();
    private static string _installState = "Idle"; // Idle, Downloading, Extracting, ReadyToApply, Applying, Failed
    private static int _installProgress = 0;
    private static string? _installError = null;
    private static string? _extractedUpdatePath = null;
    private static CancellationTokenSource? _activeCts = null;

    private readonly IServerApplicationHost _applicationHost;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ISystemManager _systemManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateInfoController> _logger;

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
    {
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _systemManager = systemManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
                    info.ArchiveUrl = archiveUrl;
                    info.PackageSize = size;
                }
                else if (TryParseCustomManifest(contentString, out var customVersion, out var customChangelog, out var customUrl))
                {
                    info.AvailableVersion = customVersion.ToString(3);
                    info.UpdateAvailable = customVersion > _applicationHost.ApplicationVersion;
                    info.Changelog = customChangelog;
                    info.ArchiveUrl = customUrl;
                }
            }
            else
            {
                _logger.LogWarning("Update check returned HTTP status {StatusCode} from {Url}", response.StatusCode, targetUrl);
            }

            info.LastCheckedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch server update info from {Url}.", targetUrl);
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
    public ActionResult<object> GetUpdateStatus()
    {
        lock (SyncLock)
        {
            return Ok(new
            {
                State = _installState,
                Progress = _installProgress,
                ErrorMessage = _installError,
                ReadyToApply = string.Equals(_installState, "ReadyToApply", StringComparison.OrdinalIgnoreCase)
            });
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
        lock (SyncLock)
        {
            if (_installState is "Downloading" or "Extracting" or "Applying")
            {
                return Accepted(new { Message = "Update is already in progress.", State = _installState, Progress = _installProgress });
            }
        }

        if (string.IsNullOrWhiteSpace(archiveUrl))
        {
            // Resolve from latest release if not passed
            var updateInfo = (await GetUpdateInfo(CancellationToken.None).ConfigureAwait(false)).Value;
            archiveUrl = updateInfo?.ArchiveUrl;
            if (string.IsNullOrWhiteSpace(archiveUrl))
            {
                return BadRequest(new { Message = "No update archive URL found for the latest release." });
            }
        }

        lock (SyncLock)
        {
            _activeCts?.Cancel();
            _activeCts = new CancellationTokenSource();
            _installState = "Downloading";
            _installProgress = 0;
            _installError = null;
        }

        var token = _activeCts.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await DownloadAndPrepareUpdateAsync(archiveUrl, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (SyncLock)
                    {
                        _installState = "Failed";
                        _installError = ex.Message;
                    }

                    _logger.LogError(ex, "Failed to download and extract update package.");
                }
            },
            token);

        return Accepted(new { Message = "Update download started.", State = _installState, Progress = _installProgress });
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
    public ActionResult<object> ApplyUpdate()
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

        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dataDir = _applicationPaths.ProgramDataPath;
        var pid = Environment.ProcessId;

        // Ensure apply-update.ps1 script exists in the update directory or install directory
        var scriptPath = Path.Combine(installDir, "apply-update.ps1");
        if (!System.IO.File.Exists(scriptPath))
        {
            scriptPath = Path.Combine(dataDir, "updates", "apply-update.ps1");
            System.IO.File.WriteAllText(scriptPath, EmbeddedUpdaterScript);
        }

        _logger.LogInformation("Launching in-place updater: {ScriptPath} with source {Source} target {Target} PID {Pid}", scriptPath, updateSource, installDir, pid);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\" -InstallDirectory \"{installDir}\" -UpdateSourceDirectory \"{updateSource}\" -ProcessId {pid} -DataDirectory \"{dataDir}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        Process.Start(startInfo);

        // Schedule server shutdown to release DLL and file locks
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500).ConfigureAwait(false);
            _logger.LogInformation("Shutting down MulletaFlix to allow in-place file replacement.");
            _systemManager.Shutdown();
        });

        return Ok(new { Message = "Update application started. Server is shutting down to apply updates." });
    }

    private HttpClient CreateConfiguredClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MulletaFlix", _applicationHost.ApplicationVersionString));

        var token = Environment.GetEnvironmentVariable(GitHubTokenEnv);
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private async Task DownloadAndPrepareUpdateAsync(string archiveUrl, CancellationToken cancellationToken)
    {
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
        _logger.LogInformation("Downloading update package from {Url} to {Dest}", archiveUrl, packageZip);
        var client = CreateConfiguredClient();
        using (var response = await client.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
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

        lock (SyncLock)
        {
            _extractedUpdatePath = extractDir;
            _installState = "ReadyToApply";
            _installProgress = 100;
        }

        _logger.LogInformation("Update package is ready to be applied from {ExtractDir}", extractDir);
    }

    private static bool TryParseGitHubRelease(string json, out Version version, out string? changelog, out string? archiveUrl, out long? size)
    {
        version = new Version(0, 0, 0);
        changelog = null;
        archiveUrl = null;
        size = null;

        try
        {
            var release = System.Text.Json.JsonSerializer.Deserialize<GitHubReleasePayload>(json);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return false;
            }

            var cleanTag = release.TagName.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(cleanTag, out var parsed))
            {
                return false;
            }

            version = parsed;
            changelog = release.Body;

            var zipAsset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zipAsset is not null)
            {
                archiveUrl = zipAsset.BrowserDownloadUrl;
                size = zipAsset.Size;
            }

            return true;
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

    private const string EmbeddedUpdaterScript = @"param([string]$InstallDirectory, [string]$UpdateSourceDirectory, [int]$ProcessId, [string]$DataDirectory)
$ErrorActionPreference = 'Continue'
$waited = 0
while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ($waited -lt 30)) { Start-Sleep -Seconds 1; $waited++ }
if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue }
Get-Process | Where-Object { $_.ProcessName -like '*MulletaFlix.Windows.Tray*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
try { Copy-Item -Path ""$UpdateSourceDirectory\*"" -Destination $InstallDirectory -Recurse -Force -ErrorAction Stop } catch {}
try { Remove-Item -LiteralPath $UpdateSourceDirectory -Recurse -Force -ErrorAction SilentlyContinue } catch {}
$service = Get-Service -Name 'MulletaFlixServer' -ErrorAction SilentlyContinue
if ($service) { Start-Service -Name 'MulletaFlixServer' -ErrorAction SilentlyContinue }
else {
    $exePath = Join-Path $InstallDirectory 'MulletaFlix.exe'
    if (Test-Path -LiteralPath $exePath) {
        $args = if ($DataDirectory) { ""--datadir `""$DataDirectory`"""" } else { """" }
        Start-Process -FilePath $exePath -ArgumentList $args -WindowStyle Hidden
    }
}
$trayPath = Join-Path $InstallDirectory 'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'
if (Test-Path -LiteralPath $trayPath) { Start-Process -FilePath $trayPath }
";

    private sealed class GitHubReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }
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
}

