using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MediaBrowser.Providers.Books.OpenPackagingFormat
{
    /// <summary>
    /// Utilities for EPUB files.
    /// </summary>
    public static class EpubUtils
    {
        internal const int MaxXmlEntryBytes = 16 * 1024 * 1024;
        internal const int MaxImageEntryBytes = 32 * 1024 * 1024;

        internal static MemoryStream ReadEntry(ZipArchiveEntry entry, int maximumBytes)
        {
            using var source = entry.Open();
            var result = new MemoryStream();
            try
            {
                CopyBounded(source, result, maximumBytes);
                result.Position = 0;
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        internal static async Task<MemoryStream> ReadEntryAsync(ZipArchiveEntry entry, int maximumBytes, CancellationToken cancellationToken)
        {
            await using var source = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
            var result = new MemoryStream();
            try
            {
                await CopyBoundedAsync(source, result, maximumBytes, cancellationToken).ConfigureAwait(false);
                result.Position = 0;
                return result;
            }
            catch
            {
                await result.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static void CopyBounded(Stream source, Stream destination, int maximumBytes)
        {
            var buffer = new byte[81920];
            var remaining = maximumBytes;
            while (true)
            {
                var read = source.Read(buffer, 0, Math.Min(buffer.Length, remaining + 1));
                if (read == 0)
                {
                    return;
                }

                if (read > remaining)
                {
                    throw new InvalidDataException($"EPUB entry exceeds the {maximumBytes}-byte read limit.");
                }

                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static async Task CopyBoundedAsync(Stream source, Stream destination, int maximumBytes, CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            var remaining = maximumBytes;
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining + 1)), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                if (read > remaining)
                {
                    throw new InvalidDataException($"EPUB entry exceeds the {maximumBytes}-byte read limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }

        /// <summary>
        /// Attempt to read content from ZIP archive.
        /// </summary>
        /// <param name="epub">The ZIP archive.</param>
        /// <returns>The content file path.</returns>
        public static string? ReadContentFilePath(ZipArchive epub)
        {
            var container = epub.GetEntry(Path.Combine("META-INF", "container.xml"));
            if (container == null)
            {
                return null;
            }

            using var containerStream = ReadEntry(container, MaxXmlEntryBytes);

            XNamespace containerNamespace = "urn:oasis:names:tc:opendocument:xmlns:container";
            var containerDocument = XDocument.Load(containerStream);
            var element = containerDocument.Descendants(containerNamespace + "rootfile").FirstOrDefault();

            return element?.Attribute("full-path")?.Value;
        }
    }
}
