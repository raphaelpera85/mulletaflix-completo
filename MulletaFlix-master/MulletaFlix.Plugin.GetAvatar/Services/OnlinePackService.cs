using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MulletaFlix.Plugin.GetAvatar.Controllers;

namespace MulletaFlix.Plugin.GetAvatar.Services
{
    /// <summary>
    /// Service for discovering and importing online avatar packs from GitHub releases.
    /// </summary>
    public class OnlinePackService
    {
        private const string GitHubReleaseApiUrl = "https://api.github.com/repos/cedev-1/jellyfin-avatars/releases/latest";
        internal const string PackDownloadHttpClientName = "GetAvatar.PackDownload";
        private const long MaxZipSizeBytes = 1024L * 1024L * 1024L;
        private const long MaxImageSizeBytes = 5L * 1024L * 1024L;
        private const long MaxPackImageBytes = 2L * 1024L * 1024L * 1024L;
        private const int MaxPackEntries = 10000;
        private const int MaxDownloadRedirects = 5;
        private static readonly TimeSpan PackCacheDuration = TimeSpan.FromMinutes(15);

        private static readonly Dictionary<string, string> PackDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "dp", "Disney Plus" },
            { "nf", "Netflix" },
            { "one", "Xbox One" },
            { "playstation", "PlayStation" },
            { "pp", "Paramount Plus" },
            { "pv", "Prime Video" },
            { "sonny", "Sonny Angels" },
            { "steam", "Steam" }
        };

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AvatarService _avatarService;
        private readonly ILogger<OnlinePackService> _logger;
        private readonly object _cacheLock = new object();
        private List<OnlinePackInfo>? _cachedPacks;
        private DateTime _cachedAtUtc;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnlinePackService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="avatarService">The avatar service.</param>
        /// <param name="logger">The logger instance.</param>
        public OnlinePackService(
            IHttpClientFactory httpClientFactory,
            AvatarService avatarService,
            ILogger<OnlinePackService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _avatarService = avatarService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the available avatar packs from the latest GitHub release.
        /// </summary>
        /// <param name="forceRefresh">Whether to bypass the in-memory cache.</param>
        /// <returns>List of available packs.</returns>
        public Task<List<OnlinePackInfo>> GetAvailablePacksAsync(bool forceRefresh = false)
        {
            lock (_cacheLock)
            {
                if (!forceRefresh && _cachedPacks is not null && DateTime.UtcNow - _cachedAtUtc < PackCacheDuration)
                {
                    return Task.FromResult(_cachedPacks.ToList());
                }
            }

            return GetAvailablePacksCoreAsync();
        }

        private async Task<List<OnlinePackInfo>> GetAvailablePacksCoreAsync()
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Jellyfin-Plugin-GetAvatar");
            client.Timeout = TimeSpan.FromSeconds(30);

            _logger.LogInformation("Fetching online avatar packs from {Url}", GitHubReleaseApiUrl);

            var response = await client.GetAsync(GitHubReleaseApiUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var packs = new List<OnlinePackInfo>();

            if (!root.TryGetProperty("assets", out var assets))
            {
                _logger.LogWarning("No assets found in GitHub release response");
                return packs;
            }

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(name) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = Path.GetFileNameWithoutExtension(name);
                var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                var size = asset.TryGetProperty("size", out var sizeProperty) ? sizeProperty.GetInt64() : 0;

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    continue;
                }

                packs.Add(new OnlinePackInfo
                {
                    Id = id,
                    Name = GetDisplayName(id),
                    FileName = name,
                    DownloadUrl = downloadUrl,
                    Size = size
                });
            }

            _logger.LogInformation("Found {Count} online avatar packs", packs.Count);

            lock (_cacheLock)
            {
                _cachedPacks = packs.ToList();
                _cachedAtUtc = DateTime.UtcNow;
            }

            return packs;
        }

        /// <summary>
        /// Imports the specified online avatar packs.
        /// </summary>
        /// <param name="packs">The packs to import.</param>
        /// <returns>The import result.</returns>
        public async Task<ImportOnlinePacksResult> ImportPacksAsync(IEnumerable<OnlinePackInfo> packs)
        {
            var selectedPacks = packs?.ToList() ?? new List<OnlinePackInfo>();
            if (selectedPacks.Count == 0)
            {
                return new ImportOnlinePacksResult();
            }

            var result = new ImportOnlinePacksResult();

            foreach (var pack in selectedPacks)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(pack.DownloadUrl) || string.IsNullOrWhiteSpace(pack.FileName))
                    {
                        throw new InvalidOperationException($"Pack data is incomplete for {pack.Name ?? pack.Id}.");
                    }

                    _logger.LogInformation("Importing online avatar pack: {PackName} ({FileName})", pack.Name, pack.FileName);
                    var details = await ImportPackAsync(pack).ConfigureAwait(false);
                    result.ImportedCount += details.ImportedCount;
                    result.TotalImages += details.TotalImages;
                    result.DuplicateCount += details.DuplicateCount;
                    result.PackResults.Add(new PackImportResult
                    {
                        PackId = pack.Id,
                        PackName = pack.Name,
                        ImportedCount = details.ImportedCount,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import online avatar pack: {PackName}", pack.Name);
                    result.PackResults.Add(new PackImportResult
                    {
                        PackId = pack.Id,
                        PackName = pack.Name,
                        ImportedCount = 0,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }

        private async Task<PackImportDetails> ImportPackAsync(OnlinePackInfo pack)
        {
            if (!IsTrustedDownloadUrl(pack.DownloadUrl))
            {
                throw new InvalidOperationException("Avatar pack downloads must come from the configured GitHub releases repository.");
            }

            var existingHashes = GetExistingAvatarHashes();
            var tempDirectory = Path.Combine(Path.GetTempPath(), "GetAvatar", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var zipPath = Path.Combine(tempDirectory, "pack.zip");
                await DownloadFileAsync(pack.DownloadUrl, zipPath, MaxZipSizeBytes).ConfigureAwait(false);

                using var archive = ZipFile.OpenRead(zipPath);
                if (archive.Entries.Count > MaxPackEntries)
                {
                    throw new InvalidDataException($"Avatar pack contains more than {MaxPackEntries} entries.");
                }

                var imageEntries = archive.Entries
                    .Where(entry => AllowedExtensions.Contains(Path.GetExtension(entry.FullName.Replace('\\', '/')).ToLowerInvariant()))
                    .ToList();

                var totalImageBytes = imageEntries
                    .Where(entry => entry.Length <= MaxImageSizeBytes)
                    .Aggregate(0L, (total, entry) => checked(total + entry.Length));
                if (totalImageBytes > MaxPackImageBytes)
                {
                    throw new InvalidDataException($"Avatar pack images exceed the {MaxPackImageBytes / (1024 * 1024)} MB expanded size limit.");
                }

                _logger.LogInformation("Found {Count} image files in pack {PackName}", imageEntries.Count, pack.Name);

                var details = new PackImportDetails
                {
                    TotalImages = imageEntries.Count
                };

                var importedImageBytes = 0L;
                foreach (var entry in imageEntries)
                {
                    try
                    {
                        if (entry.Length > MaxImageSizeBytes)
                        {
                            _logger.LogWarning("Skipping image {FileName}: exceeds {MaxSize} MB", entry.FullName, MaxImageSizeBytes / (1024 * 1024));
                            continue;
                        }

                        var fileName = Path.GetFileName(entry.FullName.Replace('\\', '/'));
                        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
                        {
                            _logger.LogWarning("Skipping avatar pack entry with an invalid image name: {EntryName}", entry.FullName);
                            continue;
                        }

                        await using var entryStream = entry.Open();
                        using var imageBuffer = new MemoryStream((int)entry.Length);
                        var buffer = new byte[81920];
                        int bytesRead;
                        while ((bytesRead = await entryStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
                        {
                            if (imageBuffer.Length + bytesRead > MaxImageSizeBytes
                                || importedImageBytes + imageBuffer.Length + bytesRead > MaxPackImageBytes)
                            {
                                throw new InvalidDataException("Avatar pack image data exceeds its expanded size limit.");
                            }

                            await imageBuffer.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        }

                        var imageData = imageBuffer.ToArray();
                        importedImageBytes += imageData.Length;
                        var hash = ComputeHash(imageData);

                        if (existingHashes.Contains(hash))
                        {
                            details.DuplicateCount++;
                            _logger.LogDebug("Skipping duplicate image: {FileName}", fileName);
                            continue;
                        }

                        await _avatarService.SaveAvatarAsync(fileName, imageData, pack.Name).ConfigureAwait(false);
                        existingHashes.Add(hash);
                        details.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import image {FileName} from pack {PackName}", entry.FullName, pack.Name);
                    }
                }

                _logger.LogInformation("Imported {ImportedCount} avatars from pack {PackName} ({DuplicateCount} duplicates skipped)", details.ImportedCount, pack.Name, details.DuplicateCount);
                return details;
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary directory: {Path}", tempDirectory);
                }
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath, long maxSizeBytes)
        {
            using var client = _httpClientFactory.CreateClient(PackDownloadHttpClientName);
            client.Timeout = TimeSpan.FromMinutes(10);

            var currentUri = new Uri(url);
            HttpResponseMessage response;
            for (var redirectCount = 0; ; redirectCount++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!IsRedirect(response.StatusCode))
                {
                    break;
                }

                using (response)
                {
                    if (redirectCount >= MaxDownloadRedirects || response.Headers.Location is null)
                    {
                        throw new InvalidDataException("Avatar pack download returned too many or invalid redirects.");
                    }

                    var nextUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    if (!IsTrustedRedirectUrl(nextUri))
                    {
                        throw new InvalidDataException("Avatar pack download redirected outside the trusted GitHub hosts.");
                    }

                    currentUri = nextUri;
                }
            }

            using (response)
            {
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > maxSizeBytes)
            {
                throw new InvalidOperationException($"File size ({contentLength.Value} bytes) exceeds maximum allowed size ({maxSizeBytes} bytes).");
            }

            var totalBytesRead = 0L;
            using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                totalBytesRead += bytesRead;

                if (totalBytesRead > maxSizeBytes)
                {
                    throw new InvalidOperationException($"Downloaded file exceeded maximum allowed size ({maxSizeBytes} bytes).");
                }
            }
            }
        }

        private static bool IsRedirect(HttpStatusCode statusCode)
            => statusCode is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                || (int)statusCode == 308;

        private static bool IsTrustedRedirectUrl(Uri uri)
            => uri.Scheme == Uri.UriSchemeHttps
                && uri.IsDefaultPort
                && uri.UserInfo.Length == 0
                && uri.Fragment.Length == 0
                && (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

        private static bool IsTrustedDownloadUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort
                || uri.UserInfo.Length != 0
                || uri.Query.Length != 0
                || uri.Fragment.Length != 0)
            {
                return false;
            }

            var segments = uri.AbsolutePath.Split('/');
            return segments.Length >= 7
                && segments[1] == "cedev-1"
                && segments[2] == "jellyfin-avatars"
                && segments[3] == "releases"
                && segments[4] == "download"
                && segments.Skip(5).All(segment =>
                {
                    var decoded = Uri.UnescapeDataString(segment);
                    return decoded.Length > 0
                        && decoded is not "." and not ".."
                        && !decoded.Contains('/', StringComparison.Ordinal)
                        && !decoded.Contains('\\', StringComparison.Ordinal);
                });
        }

        private HashSet<string> GetExistingAvatarHashes()
        {
            var hashes = new HashSet<string>();
            var avatarDirectory = _avatarService.AvatarDirectory;

            if (!Directory.Exists(avatarDirectory))
            {
                return hashes;
            }

            foreach (var file in Directory.GetFiles(avatarDirectory))
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    hashes.Add(ComputeHash(data));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute hash for existing avatar: {File}", file);
                }
            }

            return hashes;
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToHexString(hash);
        }

        private static string GetDisplayName(string packId)
        {
            return PackDisplayNames.TryGetValue(packId, out var displayName)
                ? displayName
                : char.ToUpperInvariant(packId[0]) + packId.Substring(1);
        }

        /// <summary>
        /// Result of importing online avatar packs.
        /// </summary>
        public class ImportOnlinePacksResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ImportOnlinePacksResult"/> class.
            /// </summary>
            public ImportOnlinePacksResult()
            {
                PackResults = new List<PackImportResult>();
            }

            /// <summary>
            /// Gets or sets the total number of avatars imported.
            /// </summary>
            public int ImportedCount { get; set; }

            /// <summary>
            /// Gets or sets the total number of image files found in selected packs.
            /// </summary>
            public int TotalImages { get; set; }

            /// <summary>
            /// Gets or sets the total number of duplicate images skipped.
            /// </summary>
            public int DuplicateCount { get; set; }

            /// <summary>
            /// Gets or sets the detailed results for each pack.
            /// </summary>
            public List<PackImportResult> PackResults { get; set; }
        }

        private class PackImportDetails
        {
            public int ImportedCount { get; set; }

            public int TotalImages { get; set; }

            public int DuplicateCount { get; set; }
        }

        /// <summary>
        /// Result of importing a single online avatar pack.
        /// </summary>
        public class PackImportResult
        {
            /// <summary>
            /// Gets or sets the pack identifier.
            /// </summary>
            public string PackId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the pack display name.
            /// </summary>
            public string PackName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the number of avatars imported from this pack.
            /// </summary>
            public int ImportedCount { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the import succeeded.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// Gets or sets the error message if the import failed.
            /// </summary>
            public string ErrorMessage { get; set; } = string.Empty;
        }
    }
}
