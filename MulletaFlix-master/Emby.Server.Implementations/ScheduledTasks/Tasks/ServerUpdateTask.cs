using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Archives;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

public sealed class ServerUpdateTask : IScheduledTask
{
    private static readonly TimeSpan ArchiveReadIdleTimeout = TimeSpan.FromSeconds(45);
    private const string UpdateManifestUrlEnv = "MulletaFlix_UPDATE_MANIFEST_URL";
    private const string UpdateServiceNameEnv = "MulletaFlix_UPDATE_SERVICE_NAME";
    private const string UpdateInstallRootEnv = "MulletaFlix_UPDATE_INSTALL_ROOT";
    private const string UpdateCheckHoursEnv = "MulletaFlix_UPDATE_CHECK_HOURS";

    private readonly ILogger<ServerUpdateTask> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IApplicationPaths _applicationPaths;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public ServerUpdateTask(
        ILogger<ServerUpdateTask> logger,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost applicationHost,
        IApplicationPaths applicationPaths,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    public string Name => "Check Server Updates";

    public string Description => "Checks for MulletaFlix server updates and pre-downloads them automatically in the background, awaiting user approval to apply.";

    public string Category => "System";

    public string Key => "ServerUpdates";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.StartupTrigger
        };

        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(GetCheckIntervalHours()).Ticks
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Server update checks are currently implemented for Windows installs only.");
            progress.Report(100);
            return;
        }

        try
        {
            progress.Report(5);

            var manifestUrl = Environment.GetEnvironmentVariable(UpdateManifestUrlEnv);
            var targetUrl = !string.IsNullOrWhiteSpace(manifestUrl) ? manifestUrl : "https://api.github.com/repos/raphaelpera85/mulletaflix-completo/releases/latest";
            if (!BoundedZipArchiveExtractor.IsHttpsUrl(targetUrl))
            {
                _logger.LogWarning("Server update manifest URL must use HTTPS without embedded credentials.");
                progress.Report(100);
                return;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MulletaFlix", _applicationHost.ApplicationVersionString));

            var token = Environment.GetEnvironmentVariable("MulletaFlix_GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Server update check returned HTTP {StatusCode}.", response.StatusCode);
                progress.Report(100);
                return;
            }

            if (!BoundedZipArchiveExtractor.IsHttpsUrl(response.RequestMessage?.RequestUri?.ToString()))
            {
                _logger.LogWarning("Server update manifest redirect did not remain on HTTPS.");
                progress.Report(100);
                return;
            }

            await using var manifestStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var contentString = await BoundedZipArchiveExtractor.ReadBoundedTextAsync(
                manifestStream,
                BoundedZipArchiveExtractor.MaxManifestBytes,
                cancellationToken).ConfigureAwait(false);
            Version? remoteVersion = null;
            string? archiveUrl = null;
            string? checksum = null;

            if (TryParseGitHubRelease(contentString, out var gitHubVersion, out var gitHubUrl, out var gitHubChecksum))
            {
                remoteVersion = gitHubVersion;
                archiveUrl = gitHubUrl;
                checksum = gitHubChecksum;
            }
            else if (TryParseCustomManifest(contentString, out var customVersion, out var customUrl, out var customChecksum))
            {
                remoteVersion = customVersion;
                archiveUrl = customUrl;
                checksum = customChecksum;
            }

            if (remoteVersion is null || !BoundedZipArchiveExtractor.IsHttpsUrl(archiveUrl) || !BoundedZipArchiveExtractor.IsValidSha256Digest(checksum))
            {
                _logger.LogWarning("Could not resolve the update version, archive URL, or valid SHA-256 package digest.");
                progress.Report(100);
                return;
            }

            var currentVersion = _applicationHost.ApplicationVersion;
            if (remoteVersion <= currentVersion)
            {
                _logger.LogDebug(
                    "Server update not required. Current version {CurrentVersion} is already at or above remote version {RemoteVersion}.",
                    currentVersion,
                    remoteVersion);
                progress.Report(100);
                return;
            }

            progress.Report(20);

            var updatesDir = Path.Combine(_applicationPaths.ProgramDataPath, "updates");
            Directory.CreateDirectory(updatesDir);
            var extractDir = Path.Combine(updatesDir, "extracted");
            var stateFile = Path.Combine(updatesDir, "update_state.json");

            // Check if already extracted and matches remote version
            if (File.Exists(stateFile) && Directory.Exists(extractDir) && Directory.EnumerateFileSystemEntries(extractDir).Any())
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(stateFile, cancellationToken).ConfigureAwait(false);
                    var existingState = JsonSerializer.Deserialize<UpdatePersistentState>(existingJson);
                    if (existingState != null
                        && string.Equals(existingState.DownloadedVersion, remoteVersion.ToString(3), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existingState.VerifiedChecksum, checksum, StringComparison.OrdinalIgnoreCase)
                        && existingState.InstallState == "ReadyToApply")
                    {
                        _logger.LogInformation("Update {RemoteVersion} is already downloaded and ready to apply upon user approval in Dashboard.", remoteVersion.ToString(3));
                        progress.Report(100);
                        return;
                    }
                }
                catch
                {
                    // proceed to download
                }
            }

            var packageZip = Path.Combine(updatesDir, "package.zip");
            if (File.Exists(packageZip))
            {
                File.Delete(packageZip);
            }

            _logger.LogInformation("Auto-downloading server update {RemoteVersion} in background...", remoteVersion.ToString(3));
            await DownloadArchiveAsync(client, archiveUrl, packageZip, cancellationToken).ConfigureAwait(false);

            progress.Report(70);

            _logger.LogInformation("Extracting update package to {ExtractDir}...", extractDir);
            await using (var archiveStream = new FileStream(packageZip, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                if (!await BoundedZipArchiveExtractor.VerifySha256Async(checksum, archiveStream, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidDataException("The server update package SHA-256 digest does not match the release metadata.");
                }

                var retainedBackupPath = await BoundedZipArchiveExtractor.ExtractToDirectoryAsync(
                    archiveStream,
                    extractDir,
                    updatesDir,
                    cancellationToken).ConfigureAwait(false);
                if (retainedBackupPath is not null)
                {
                    _logger.LogWarning("The previous extracted server update could not be removed and remains at {BackupPath}.", retainedBackupPath);
                }
            }

            var state = new UpdatePersistentState
            {
                DownloadedVersion = remoteVersion.ToString(3),
                VerifiedChecksum = checksum,
                InstallState = "ReadyToApply",
                ExtractedPath = extractDir,
                CompletedAt = DateTime.UtcNow
            };
            await File.WriteAllTextAsync(stateFile, JsonSerializer.Serialize(state), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Server update {RemoteVersion} downloaded and extracted automatically in background. Awaiting user approval to apply in Dashboard.",
                remoteVersion.ToString(3));

            progress.Report(100);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server update task failed.");
            progress.Report(100);
        }
    }

    private static int GetCheckIntervalHours()
    {
        var raw = Environment.GetEnvironmentVariable(UpdateCheckHoursEnv);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) && hours > 0
            ? hours
            : 12;
    }

    private static async Task DownloadArchiveAsync(HttpClient client, string archiveUrl, string archivePath, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (!BoundedZipArchiveExtractor.IsHttpsUrl(response.RequestMessage?.RequestUri?.ToString()))
        {
            throw new InvalidDataException("The server update package redirect did not remain on HTTPS.");
        }

        if (response.Content.Headers.ContentLength > BoundedZipArchiveExtractor.MaxArchiveBytes)
        {
            throw new InvalidDataException("The server update package exceeds the download size limit.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[128 * 1024];
        int read;
        readCts.CancelAfter(ArchiveReadIdleTimeout);
        while ((read = await responseStream.ReadAsync(buffer.AsMemory(), readCts.Token).ConfigureAwait(false)) > 0)
        {
            if (read > BoundedZipArchiveExtractor.MaxArchiveBytes - fileStream.Length)
            {
                throw new InvalidDataException("The server update package exceeds the download size limit.");
            }

            readCts.CancelAfter(ArchiveReadIdleTimeout);
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static bool TryParseGitHubRelease(string json, out Version? version, out string? archiveUrl, out string? checksum)
    {
        version = null;
        archiveUrl = null;
        checksum = null;

        try
        {
            var release = JsonSerializer.Deserialize<GitHubReleasePayload>(json);
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
            var zipAsset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zipAsset is not null)
            {
                archiveUrl = zipAsset.BrowserDownloadUrl;
                checksum = zipAsset.Digest;
            }

            return BoundedZipArchiveExtractor.IsHttpsUrl(archiveUrl) && BoundedZipArchiveExtractor.IsValidSha256Digest(checksum);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryParseCustomManifest(string json, out Version? version, out string? archiveUrl, out string? checksum)
    {
        version = null;
        archiveUrl = null;
        checksum = null;

        try
        {
            var manifest = JsonSerializer.Deserialize<ServerUpdateManifest>(json);
            if (manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.Version)
                && Version.TryParse(manifest.Version, out var parsed))
            {
                version = parsed;
                archiveUrl = manifest.ArchiveUrl;
                checksum = manifest.Checksum;
                return BoundedZipArchiveExtractor.IsHttpsUrl(archiveUrl) && BoundedZipArchiveExtractor.IsValidSha256Digest(checksum);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private sealed class GitHubReleasePayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }

    private sealed class UpdatePersistentState
    {
        public string? DownloadedVersion { get; set; }

        public string? VerifiedChecksum { get; set; }

        public string? InstallState { get; set; }

        public string? ExtractedPath { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    private sealed class ServerUpdateManifest
    {
        public string Version { get; set; } = string.Empty;

        public string ArchiveUrl { get; set; } = string.Empty;

        public string? Checksum { get; set; }
    }
}
