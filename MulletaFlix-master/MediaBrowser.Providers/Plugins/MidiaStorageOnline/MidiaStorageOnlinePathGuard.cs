using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Plugins.MidiaStorageOnline;

/// <summary>
/// Validates paths derived from playlist entries and the sync manifest.
/// </summary>
internal static class MidiaStorageOnlinePathGuard
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Resolves a relative path below the configured output root.
    /// </summary>
    /// <param name="rootPath">Configured output root.</param>
    /// <param name="relativePath">Untrusted path from a playlist or manifest.</param>
    /// <returns>The canonical destination path.</returns>
    /// <exception cref="InvalidDataException">The path is rooted, malformed, or escapes the root.</exception>
    public static string ResolveUnderRoot(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Output root must not be empty.", nameof(rootPath));
        }

        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath[0] is '/' or '\\'
            || relativePath.Contains(":", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Media sync path must be relative to the configured output root.");
        }

        var segments = relativePath.Split(PathSeparators, StringSplitOptions.None);
        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment)
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal)
                || segment.Contains("\0", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Media sync path contains an invalid path segment.");
        }

        var fullRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(segments)));
        if (!IsPathWithinRoot(fullRoot, fullPath))
        {
            throw new InvalidDataException("Media sync path escapes the configured output root.");
        }

        return fullPath;
    }

    /// <summary>
    /// Rejects existing reparse-point components below the configured output root.
    /// </summary>
    /// <param name="rootPath">Configured output root.</param>
    /// <param name="fullPath">Canonical destination path.</param>
    public static void EnsureNoReparsePoints(string rootPath, string fullPath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var fullDestination = Path.GetFullPath(fullPath);
        if (!IsPathWithinRoot(fullRoot, fullDestination))
        {
            throw new InvalidDataException("Media sync path escapes the configured output root.");
        }

        EnsurePathHasNoReparsePoints(fullRoot);

        var relativePath = Path.GetRelativePath(fullRoot, fullDestination);
        var currentPath = fullRoot;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            try
            {
                if (new FileInfo(currentPath).LinkTarget is not null
                    || (File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Media sync cannot read, write, or delete through a reparse point.");
                }
            }
            catch (FileNotFoundException)
            {
                break;
            }
            catch (DirectoryNotFoundException)
            {
                break;
            }
        }
    }

    private static void EnsurePathHasNoReparsePoints(string fullPath)
    {
        var pathRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(pathRoot))
        {
            throw new InvalidDataException("Media sync output root is not an absolute filesystem path.");
        }

        var currentPath = pathRoot;
        foreach (var segment in Path.GetRelativePath(pathRoot, fullPath).Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            EnsurePathComponentIsNotReparsePoint(currentPath);
        }
    }

    private static void EnsurePathComponentIsNotReparsePoint(string path)
    {
        try
        {
            if (new FileInfo(path).LinkTarget is not null
                || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Media sync cannot read, write, or delete through a reparse point.");
            }
        }
        catch (FileNotFoundException)
        {
            // The remaining path does not exist yet; its existing ancestors were checked.
        }
        catch (DirectoryNotFoundException)
        {
            // The remaining path does not exist yet; its existing ancestors were checked.
        }
    }

    /// <summary>
    /// Checks whether a path is the root or one of its descendants.
    /// </summary>
    /// <param name="rootPath">Configured output root.</param>
    /// <param name="fullPath">Canonical path to check.</param>
    /// <returns><see langword="true"/> when the path is contained by the root.</returns>
    public static bool IsPathWithinRoot(string rootPath, string fullPath)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(rootPath), Path.GetFullPath(fullPath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", comparison)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, comparison)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Writes text to a new sibling file and atomically replaces the destination, without following existing hard links.
    /// </summary>
    public static async Task WriteTextAtomicallyAsync(string rootPath, string filePath, string content, CancellationToken cancellationToken)
    {
        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            EnsureNoReparsePoints(rootPath, temporaryPath);
            var bytes = Encoding.UTF8.GetBytes(content);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            EnsureNoReparsePoints(rootPath, filePath);
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            EnsureNoReparsePoints(rootPath, temporaryPath);
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
