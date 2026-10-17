using System;
using System.IO;
using System.Security;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Path validation shared by Nebula components that operate inside configured roots.
/// </summary>
internal static class NebulaPathSafety
{
    /// <summary>
    /// Checks lexical containment and rejects existing reparse-point components on either path.
    /// Nonexistent descendants are allowed so callers can validate future output paths.
    /// </summary>
    internal static bool IsPathWithinRoot(string path, string root)
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
                // A later component cannot exist if its parent does not.
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
}
