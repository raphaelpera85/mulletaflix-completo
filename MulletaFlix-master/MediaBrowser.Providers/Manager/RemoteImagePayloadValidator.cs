using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Net;

namespace MediaBrowser.Providers.Manager;

/// <summary>
/// Stages and validates downloaded image payloads before they reach the image store.
/// </summary>
internal static class RemoteImagePayloadValidator
{
    internal const long MaximumPayloadBytes = 64L * 1024 * 1024;
    internal const long MaximumPixelCount = 16_777_216;
    internal const int MaximumCachedPayloadBytes = 8 * 1024 * 1024;
    private static readonly SemaphoreSlim _imageDecodeConcurrency = new(2, 2);

    /// <summary>
    /// Copies the payload into a bounded temporary file and fully decodes it when a
    /// compatible decoder is available. Undecodable formats still get byte-limit protection.
    /// </summary>
    /// <param name="source">Downloaded image stream. Ownership transfers to this method.</param>
    /// <param name="mimeType">Image MIME type.</param>
    /// <param name="imageEncoder">Configured image encoder.</param>
    /// <param name="temporaryDirectory">Server temporary directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A seekable stream containing the staged payload.</returns>
    internal static async Task<Stream> PrepareAsync(
        Stream source,
        string mimeType,
        IImageEncoder imageEncoder,
        string temporaryDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentNullException.ThrowIfNull(imageEncoder);

        var extension = MimeTypes.ToExtension(mimeType)?.TrimStart('.');
        var hasSafeExtension = !string.IsNullOrWhiteSpace(extension) && extension.All(char.IsAsciiLetterOrDigit);
        var canDecode = imageEncoder.SupportsImageEncoding
            && !string.Equals(extension, "svg", StringComparison.OrdinalIgnoreCase)
            && hasSafeExtension
            && imageEncoder.SupportedInputFormats.Contains(extension, StringComparer.OrdinalIgnoreCase);
        var temporaryPath = string.Empty;
        try
        {
            await using (source.ConfigureAwait(false))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
                Directory.CreateDirectory(temporaryDirectory);
                temporaryPath = Path.Combine(
                    temporaryDirectory,
                    $"remote-image-{Guid.NewGuid():N}.{(hasSafeExtension ? extension : "bin")}");

                if (source.CanSeek && source.Length > MaximumPayloadBytes)
                {
                    throw CreatePayloadTooLargeException();
                }

                await using (var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[81920];
                    long totalBytes = 0;
                    int bytesRead;
                    while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        totalBytes += bytesRead;
                        if (totalBytes > MaximumPayloadBytes)
                        {
                            throw CreatePayloadTooLargeException();
                        }

                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (canDecode)
            {
                await _imageDecodeConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (!imageEncoder.IsImageDecodable(temporaryPath, MaximumPixelCount))
                    {
                        throw new HttpRequestException(
                            "The remote image payload could not be fully decoded.",
                            null,
                            HttpStatusCode.UnprocessableEntity);
                    }
                }
                finally
                {
                    _imageDecodeConcurrency.Release();
                }
            }

            return new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Keep the original validation or I/O exception.
                }
                catch (UnauthorizedAccessException)
                {
                    // Keep the original validation or I/O exception.
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Reads HTTP image content with a hard byte limit before allocating the returned buffer.
    /// </summary>
    internal static async Task<(byte[] Buffer, int Length)> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength > MaximumPayloadBytes)
        {
            throw CreatePayloadTooLargeException();
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var contentLength = content.Headers.ContentLength;
        await using var destination = new MemoryStream(contentLength is > 0 ? (int)contentLength.Value : 0);
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaximumPayloadBytes)
            {
                throw CreatePayloadTooLargeException();
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return (destination.GetBuffer(), checked((int)destination.Length));
    }

    private static HttpRequestException CreatePayloadTooLargeException()
        => new("The remote image payload exceeds the 64 MiB limit.", null, HttpStatusCode.RequestEntityTooLarge);
}
