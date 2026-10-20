using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Controller.IO;

/// <summary>
/// Helper methods for file system management.
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// Determines whether two paths identify the same path using the host operating system's path semantics.
    /// </summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><c>true</c> when both normalized paths compare equal; otherwise, <c>false</c>.</returns>
    public static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks lexical containment and rejects existing reparse-point components on either path.
    /// Nonexistent descendants are allowed so callers can validate future output paths.
    /// </summary>
    /// <param name="path">The candidate path.</param>
    /// <param name="root">The allowed root directory.</param>
    /// <returns><c>true</c> if the candidate is beneath the root and has no reparse-point components.</returns>
    public static bool IsPathWithinRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root);
            var relativePath = Path.GetRelativePath(fullRoot, fullPath);

            if (Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, ".", StringComparison.Ordinal)
                || string.Equals(relativePath, "..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return false;
            }

            return HasNoReparsePointComponents(fullRoot) && HasNoReparsePointComponents(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return false;
        }
    }

    private static bool HasNoReparsePointComponents(string fullPath)
    {
        var volumeRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(volumeRoot))
        {
            return false;
        }

        var current = volumeRoot;
        if (IsReparsePoint(current))
        {
            return false;
        }

        var relative = fullPath[volumeRoot.Length..];
        foreach (var component in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            if (IsReparsePoint(current))
            {
                return false;
            }

            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }
        }

        return true;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes the file.
    /// </summary>
    /// <param name="fileSystem">The fileSystem.</param>
    /// <param name="path">The path.</param>
    /// <param name="logger">The logger.</param>
    public static void DeleteFile(IFileSystem fileSystem, string path, ILogger logger)
    {
        try
        {
            fileSystem.DeleteFile(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Error deleting file {Path}", path);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Error deleting file {Path}", path);
        }
    }

    /// <summary>
    /// Recursively delete empty folders.
    /// </summary>
    /// <param name="fileSystem">The fileSystem.</param>
    /// <param name="path">The path.</param>
    /// <param name="logger">The logger.</param>
    public static void DeleteEmptyFolders(IFileSystem fileSystem, string path, ILogger logger)
    {
        foreach (var directory in fileSystem.GetDirectoryPaths(path))
        {
            DeleteEmptyFolders(fileSystem, directory, logger);
            if (!fileSystem.GetFileSystemEntryPaths(directory).Any())
            {
                try
                {
                    Directory.Delete(directory, false);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogError(ex, "Error deleting directory {Path}", directory);
                }
                catch (IOException ex)
                {
                    logger.LogError(ex, "Error deleting directory {Path}", directory);
                }
            }
        }
    }

    /// <summary>
    /// Resolves a single link hop for the specified path.
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> if the path is not a symbolic link or the filesystem does not support link resolution (e.g., exFAT).
    /// </remarks>
    /// <param name="path">The file path to resolve.</param>
    /// <returns>
    /// A <see cref="FileInfo"/> representing the next link target if the path is a link; otherwise, <c>null</c>.
    /// </returns>
    private static FileInfo? Resolve(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false) as FileInfo;
        }
        catch (IOException)
        {
            // Filesystem doesn't support links (e.g., exFAT).
            return null;
        }
    }

    /// <summary>
    /// Gets the target of the specified file link.
    /// </summary>
    /// <remarks>
    /// This helper exists because of this upstream runtime issue; https://github.com/dotnet/runtime/issues/92128.
    /// </remarks>
    /// <param name="linkPath">The path of the file link.</param>
    /// <param name="returnFinalTarget">true to follow links to the final target; false to return the immediate next link.</param>
    /// <returns>
    /// A <see cref="FileInfo"/> if the <paramref name="linkPath"/> is a link, regardless of if the target exists; otherwise, <c>null</c>.
    /// </returns>
    public static FileInfo? ResolveLinkTarget(string linkPath, bool returnFinalTarget = false)
    {
        // Check if the file exists so the native resolve handler won't throw at us.
        if (!File.Exists(linkPath))
        {
            return null;
        }

        if (!returnFinalTarget)
        {
            return Resolve(linkPath);
        }

        var targetInfo = Resolve(linkPath);
        if (targetInfo is null || !targetInfo.Exists)
        {
            return targetInfo;
        }

        var currentPath = targetInfo.FullName;
        var visited = new HashSet<string>(StringComparer.Ordinal) { linkPath, currentPath };

        while (true)
        {
            var linkInfo = Resolve(currentPath);
            if (linkInfo is null)
            {
                break;
            }

            var targetPath = linkInfo.FullName;

            // If an infinite loop is detected, return the file info for the
            // first link in the loop we encountered.
            if (!visited.Add(targetPath))
            {
                return new FileInfo(targetPath);
            }

            targetInfo = linkInfo;
            currentPath = targetPath;

            // Exit if the target doesn't exist, so the native resolve handler won't throw at us.
            if (!targetInfo.Exists)
            {
                break;
            }
        }

        return targetInfo;
    }

    /// <summary>
    /// Gets the target of the specified file link.
    /// </summary>
    /// <remarks>
    /// This helper exists because of this upstream runtime issue; https://github.com/dotnet/runtime/issues/92128.
    /// </remarks>
    /// <param name="fileInfo">The file info of the file link.</param>
    /// <param name="returnFinalTarget">true to follow links to the final target; false to return the immediate next link.</param>
    /// <returns>
    /// A <see cref="FileInfo"/> if the <paramref name="fileInfo"/> is a link, regardless of if the target exists; otherwise, <c>null</c>.
    /// </returns>
    public static FileInfo? ResolveLinkTarget(FileInfo fileInfo, bool returnFinalTarget = false)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        return ResolveLinkTarget(fileInfo.FullName, returnFinalTarget);
    }
}
