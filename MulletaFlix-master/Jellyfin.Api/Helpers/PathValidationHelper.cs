using System;
using System.IO;

namespace MulletaFlix.Api.Helpers;

/// <summary>
/// Helper for validating file system paths to prevent traversal attacks.
/// </summary>
public static class PathValidationHelper
{
    /// <summary>
    /// Validates that a given path is within an allowed base directory.
    /// Prevents path traversal attacks (e.g., ../../../etc/passwd).
    /// </summary>
    /// <param name="userProvidedPath">The path provided by the user (untrusted).</param>
    /// <param name="baseDirectory">The allowed base directory.</param>
    /// <returns>The canonicalized full path if valid; throws if outside base directory.</returns>
    /// <exception cref="ArgumentException">If path is invalid or outside base directory.</exception>
    public static string ValidatePathWithinBase(string? userProvidedPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(userProvidedPath))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(userProvidedPath));
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));
        }

        // Canonicalize both paths
        string fullPath = Path.GetFullPath(userProvidedPath);
        string normalizedBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));

        // Determine string comparison (case-sensitive on Linux, case-insensitive on Windows)
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // Verify the full path is within the base directory
        if (!fullPath.StartsWith(normalizedBase, comparison))
        {
            throw new ArgumentException(
                $"Path '{userProvidedPath}' resolves outside of allowed base directory '{baseDirectory}'.",
                nameof(userProvidedPath));
        }

        // For extra safety, also check that after the base path, the next character is a separator or end
        if (fullPath.Length > normalizedBase.Length)
        {
            char nextChar = fullPath[normalizedBase.Length];
            if (nextChar != Path.DirectorySeparatorChar && nextChar != Path.AltDirectorySeparatorChar)
            {
                throw new ArgumentException(
                    $"Path '{userProvidedPath}' is not properly within the base directory.",
                    nameof(userProvidedPath));
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Attempts to validate a path within a base directory.
    /// </summary>
    /// <param name="userProvidedPath">The path provided by the user (untrusted).</param>
    /// <param name="baseDirectory">The allowed base directory.</param>
    /// <param name="validatedPath">The canonicalized full path if valid; null otherwise.</param>
    /// <returns>True if the path is valid and within base directory; false otherwise.</returns>
    public static bool TryValidatePathWithinBase(string? userProvidedPath, string baseDirectory, out string? validatedPath)
    {
        validatedPath = null;

        try
        {
            validatedPath = ValidatePathWithinBase(userProvidedPath, baseDirectory);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Canonicalizes a path with safe error handling.
    /// </summary>
    /// <param name="userProvidedPath">The path to canonicalize.</param>
    /// <param name="canonicalPath">The canonicalized path if successful; null otherwise.</param>
    /// <returns>True if canonicalization succeeded; false otherwise.</returns>
    public static bool TryCanonicalizePath(string? userProvidedPath, out string? canonicalPath)
    {
        canonicalPath = null;

        if (string.IsNullOrWhiteSpace(userProvidedPath))
        {
            return false;
        }

        try
        {
            canonicalPath = Path.GetFullPath(userProvidedPath);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
