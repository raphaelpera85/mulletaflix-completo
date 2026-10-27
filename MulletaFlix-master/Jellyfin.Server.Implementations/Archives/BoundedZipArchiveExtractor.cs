using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Server.Implementations.Archives;

/// <summary>
/// Extracts ZIP archives without allowing archive paths or sizes to control writes.
/// </summary>
public static class BoundedZipArchiveExtractor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly TimeSpan ManifestReadTimeout = TimeSpan.FromSeconds(45);

    public const long MaxArchiveBytes = 1024L * 1024L * 1024L;
    public const long MaxExpandedBytes = 4L * 1024L * 1024L * 1024L;
    public const long MaxEntryBytes = 1024L * 1024L * 1024L;
    public const long MaxManifestBytes = 2L * 1024L * 1024L;
    public const int MaxEntries = 50_000;

    /// <summary>
    /// Checks a downloaded package against a SHA-256 digest supplied by its release metadata.
    /// </summary>
    public static async Task<bool> VerifySha256Async(string? expectedDigest, Stream packageStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageStream);
        if (!TryParseSha256Digest(expectedDigest, out var expectedHash) || !packageStream.CanRead || !packageStream.CanSeek)
        {
            return false;
        }

        packageStream.Position = 0;
        var actualHash = await SHA256.HashDataAsync(packageStream, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    /// <summary>
    /// Returns whether a digest is a raw SHA-256 hex value or a GitHub <c>sha256:</c> digest.
    /// </summary>
    public static bool IsValidSha256Digest(string? digest) => TryParseSha256Digest(digest, out _);

    /// <summary>
    /// Accepts absolute HTTPS URLs without embedded credentials.
    /// </summary>
    public static bool IsHttpsUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>
    /// Reads UTF-8 text while limiting bytes actually received from the stream.
    /// </summary>
    public static async Task<string> ReadBoundedTextAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(source, buffer, maxBytes, cancellationToken, readTimeout: ManifestReadTimeout).ConfigureAwait(false);
        return StrictUtf8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private static bool TryParseSha256Digest(string? digest, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrWhiteSpace(digest))
        {
            return false;
        }

        var value = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : digest;
        if (value.Length != 64)
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(value);
            return hash.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a single untrusted name as a child directory of a trusted root.
    /// </summary>
    public static string GetChildPath(string allowedRoot, string childName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        if (childName is "." or ".."
            || childName.Contains('/', StringComparison.Ordinal)
            || childName.Contains('\\', StringComparison.Ordinal)
            || childName.Contains(':', StringComparison.Ordinal)
            || childName.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidDataException("The destination name must be a single safe path segment.");
        }

        var root = Path.GetFullPath(allowedRoot);
        var child = Path.GetFullPath(Path.Combine(root, childName));
        EnsurePathWithinRoot(root, child);
        return child;
    }

    /// <summary>
    /// Copies a download while stopping before it can exceed the supplied byte limit.
    /// </summary>
    public static async Task<long> CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken,
        Action<long>? progress = null,
        TimeSpan? readTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        if (readTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(readTimeout));
        }

        var buffer = new byte[81920];
        var totalBytes = 0L;
        int bytesRead;
        using var readCts = readTimeout.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        while (true)
        {
            if (readCts is not null)
            {
                readCts.CancelAfter(readTimeout!.Value);
            }

            bytesRead = await source.ReadAsync(buffer, readCts?.Token ?? cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (bytesRead > maxBytes - totalBytes)
            {
                throw new InvalidDataException($"The download exceeds the {maxBytes} byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytes += bytesRead;
            progress?.Invoke(totalBytes);
        }

        return totalBytes;
    }

    /// <summary>
    /// Extracts a seekable ZIP stream into a new child directory of <paramref name="allowedRoot"/>.
    /// </summary>
    public static async Task<string?> ExtractToDirectoryAsync(
        Stream archiveStream,
        string destinationPath,
        string allowedRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);

        if (!archiveStream.CanRead || !archiveStream.CanSeek)
        {
            throw new ArgumentException("The ZIP stream must be readable and seekable.", nameof(archiveStream));
        }

        if (archiveStream.Length > MaxArchiveBytes)
        {
            throw new InvalidDataException($"The ZIP archive exceeds the {MaxArchiveBytes} byte limit.");
        }

        var fullRoot = Path.GetFullPath(allowedRoot);
        var fullDestination = Path.GetFullPath(destinationPath);
        EnsurePathWithinRoot(fullRoot, fullDestination);
        if (Path.GetRelativePath(fullRoot, fullDestination) == ".")
        {
            throw new InvalidDataException("The extraction destination must be a child of the allowed root.");
        }

        var parent = Path.GetDirectoryName(fullDestination)!;
        Directory.CreateDirectory(parent);
        if (File.Exists(fullDestination))
        {
            throw new IOException("The extraction destination is a file.");
        }

        var stagingPath = Path.Combine(parent, ".extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);

        try
        {
            archiveStream.Position = 0;
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxEntries)
            {
                throw new InvalidDataException($"The ZIP archive exceeds the {MaxEntries} entry limit.");
            }

            var entries = new HashSet<string>(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            long declaredExpandedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ValidateEntry(entry);
                if (!entries.Add(relativePath))
                {
                    throw new InvalidDataException($"The ZIP archive contains a duplicate entry '{relativePath}'.");
                }

                if (entry.Length > MaxEntryBytes || entry.Length > MaxExpandedBytes - declaredExpandedBytes)
                {
                    throw new InvalidDataException("The ZIP archive exceeds its expanded data limit.");
                }

                declaredExpandedBytes += entry.Length;
            }

            var expandedBytes = 0L;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ValidateEntry(entry);
                var targetPath = Path.GetFullPath(Path.Combine(stagingPath, relativePath));
                EnsurePathWithinRoot(stagingPath, targetPath);

                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                var buffer = new byte[81920];
                var entryBytes = 0L;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    if (bytesRead > MaxEntryBytes - entryBytes || bytesRead > MaxExpandedBytes - expandedBytes)
                    {
                        throw new InvalidDataException("The ZIP archive exceeded its expanded data limit while extracting.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    entryBytes += bytesRead;
                    expandedBytes += bytesRead;
                }

                if (entryBytes != entry.Length)
                {
                    throw new InvalidDataException($"The ZIP entry '{relativePath}' did not match its declared length.");
                }
            }

            var backupPath = (string?)null;
            if (Directory.Exists(fullDestination))
            {
                if ((File.GetAttributes(fullDestination) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("The existing extraction destination is a link.");
                }

                backupPath = Path.Combine(parent, ".backup-" + Guid.NewGuid().ToString("N"));
                Directory.Move(fullDestination, backupPath);
            }

            try
            {
                Directory.Move(stagingPath, fullDestination);
            }
            catch
            {
                if (backupPath is not null && !Directory.Exists(fullDestination))
                {
                    Directory.Move(backupPath, fullDestination);
                }

                throw;
            }

            if (backupPath is not null)
            {
                try
                {
                    Directory.Delete(backupPath, recursive: true);
                    return null;
                }
                catch (IOException)
                {
                    return backupPath;
                }
                catch (UnauthorizedAccessException)
                {
                    return backupPath;
                }
            }

            return null;
        }
        catch
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }

            throw;
        }
    }

    private static string ValidateEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName;
        if (string.IsNullOrWhiteSpace(name)
            || name[0] == '/'
            || name.Contains('\\', StringComparison.Ordinal)
            || name.Contains(':', StringComparison.Ordinal)
            || name.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidDataException("The ZIP archive contains an invalid entry path.");
        }

        var isDirectory = name.EndsWith('/');
        var segments = (isDirectory ? name[..^1] : name).Split('/');
        if (segments.Length == 0 || segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException("The ZIP archive contains an invalid entry path.");
        }

        var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixFileType == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The ZIP archive contains a link entry.");
        }

        if (isDirectory && entry.Length != 0)
        {
            throw new InvalidDataException("The ZIP archive contains a directory with file data.");
        }

        return Path.Combine(segments);
    }

    private static void EnsurePathWithinRoot(string rootPath, string targetPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, targetPath);
        if (Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The ZIP archive entry escapes the extraction directory.");
        }
    }
}
