using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Plugins.AudioDb;

internal static class AudioDbCacheFile
{
    internal static async Task WriteResponseAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The cache file must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        Exception operationException = null;
        try
        {
            var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (stream.ConfigureAwait(false))
            {
                await response.Content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
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
