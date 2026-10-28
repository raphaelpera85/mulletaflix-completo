using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;

namespace MulletaFlix.Server.Implementations.Nebula;

/// <summary>Enumerates a STRM tree while retaining visibility of inaccessible subdirectories.</summary>
internal static class NebulaStrmDirectoryEnumerator
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static NebulaStrmDirectoryEnumerationResult Enumerate(
        string root,
        Func<string, IEnumerable<string>> enumerateFiles,
        Func<string, IEnumerable<string>> enumerateDirectories,
        CancellationToken cancellationToken)
    {
        var pendingDirectories = new Stack<string>();
        var visitedDirectories = new HashSet<string>(PathComparer);
        var files = new List<string>();
        var failedDirectories = new HashSet<string>(PathComparer);
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();
            if (!visitedDirectories.Add(directory))
            {
                continue;
            }

            try
            {
                foreach (var file in enumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(file);
                }
            }
            catch (Exception exception) when (IsRecoverableFileSystemException(exception))
            {
                failedDirectories.Add(directory);
            }

            try
            {
                foreach (var childDirectory in enumerateDirectories(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingDirectories.Push(childDirectory);
                }
            }
            catch (Exception exception) when (IsRecoverableFileSystemException(exception))
            {
                failedDirectories.Add(directory);
            }
        }

        return new NebulaStrmDirectoryEnumerationResult(files, failedDirectories.Count);
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
        => exception is IOException or UnauthorizedAccessException or SecurityException;
}

internal sealed record NebulaStrmDirectoryEnumerationResult(IReadOnlyList<string> Files, int FailedDirectoryCount);
