using System;
using System.IO;

namespace MediaBrowser.Providers.Plugins.MidiaStorageOnline;

internal static class MidiaStorageOnlineAtomicFile
{
    internal static void WriteAllText(string path, string contents)
    {
        WriteAtomically(path, stream =>
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(contents);
        });
    }

    internal static void WriteAtomically(string path, Action<Stream> writeContents)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The destination must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        Exception operationException = null;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writeContents(stream);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch (Exception exception)
        {
            operationException = exception;
            throw;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupException) when (operationException is not null)
            {
                operationException.Data["TemporaryFileCleanupException"] = cleanupException;
            }
        }
    }
}
