using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Archives;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Archives;

public sealed class BoundedZipArchiveExtractorTests
{
    [Theory]
    [InlineData("https://updates.example.test/manifest.json", true)]
    [InlineData("HTTPs://updates.example.test/archive.zip", true)]
    [InlineData("http://updates.example.test/manifest.json", false)]
    [InlineData("https://user:secret@updates.example.test/manifest.json", false)]
    [InlineData("/relative/manifest.json", false)]
    [InlineData("not a URL", false)]
    public void IsHttpsUrl_RequiresAbsoluteHttpsWithoutCredentials(string url, bool expected)
        => Assert.Equal(expected, BoundedZipArchiveExtractor.IsHttpsUrl(url));

    [Fact]
    public void GetChildPath_ResolvesPackageNameWithinRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix_plugin_test_" + Guid.NewGuid().ToString("N"));

        Assert.Equal(Path.Combine(root, "Plugin_1.2.3"), BoundedZipArchiveExtractor.GetChildPath(root, "Plugin_1.2.3"));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("nested/plugin")]
    [InlineData("nested\\plugin")]
    [InlineData("C:outside")]
    public void GetChildPath_RejectsNonSegmentPackageNames(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix_plugin_test_" + Guid.NewGuid().ToString("N"));

        Assert.Throws<InvalidDataException>(() => BoundedZipArchiveExtractor.GetChildPath(root, name));
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_ExtractsFilesIntoNewChildDirectory()
    {
        using var archive = CreateArchive((zip) => WriteEntry(zip, "nested/file.txt", "safe"));
        var (root, destination) = CreatePaths();

        try
        {
            await BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken);

            Assert.Equal("safe", await File.ReadAllTextAsync(Path.Combine(destination, "nested", "file.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("nested/../../outside.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("nested\\outside.txt")]
    public async Task ExtractToDirectoryAsync_RejectsUnsafePathsBeforeCreatingDestination(string entryName)
    {
        using var archive = CreateArchive(zip => WriteEntry(zip, entryName, "unsafe"));
        var (root, destination) = CreatePaths();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken));

            Assert.False(Directory.Exists(destination));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_RejectsSymbolicLinkEntry()
    {
        using var archive = CreateArchive(zip =>
        {
            var entry = zip.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)0xA0000000);
        });
        var (root, destination) = CreatePaths();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken));

            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_ReplacesExistingDestinationAfterArchiveIsValid()
    {
        using var archive = CreateArchive(zip => WriteEntry(zip, "file.txt", "new"));
        var (root, destination) = CreatePaths();
        Directory.CreateDirectory(destination);
        var existingFile = Path.Combine(destination, "existing.txt");
        await File.WriteAllTextAsync(existingFile, "keep", TestContext.Current.CancellationToken);

        try
        {
            await BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken);

            Assert.False(File.Exists(existingFile));
            Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(destination, "file.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_KeepsExistingDestinationWhenArchiveIsRejected()
    {
        using var archive = CreateArchive(zip => WriteEntry(zip, "../outside.txt", "unsafe"));
        var (root, destination) = CreatePaths();
        Directory.CreateDirectory(destination);
        var existingFile = Path.Combine(destination, "existing.txt");
        await File.WriteAllTextAsync(existingFile, "keep", TestContext.Current.CancellationToken);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken));

            Assert.Equal("keep", await File.ReadAllTextAsync(existingFile, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CopyBoundedAsync_StopsBeforeWritingBytesAboveLimit()
    {
        using var source = new MemoryStream([1, 2, 3, 4]);
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => BoundedZipArchiveExtractor.CopyBoundedAsync(source, destination, 3, TestContext.Current.CancellationToken));

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task ReadBoundedTextAsync_AcceptsTextAtLimit()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("safe"));

        Assert.Equal("safe", await BoundedZipArchiveExtractor.ReadBoundedTextAsync(source, 4, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadBoundedTextAsync_RejectsTextAboveLimit()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("oversized"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => BoundedZipArchiveExtractor.ReadBoundedTextAsync(source, 8, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadBoundedTextAsync_RejectsInvalidUtf8()
    {
        using var source = new MemoryStream([0xC3, 0x28]);

        await Assert.ThrowsAsync<DecoderFallbackException>(
            () => BoundedZipArchiveExtractor.ReadBoundedTextAsync(source, 2, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyBoundedAsync_CancelsWhenReadExceedsTimeout()
    {
        using var source = new StalledReadStream();
        using var destination = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BoundedZipArchiveExtractor.CopyBoundedAsync(
                source,
                destination,
                10,
                TestContext.Current.CancellationToken,
                readTimeout: TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public async Task VerifySha256Async_AcceptsMatchingGitHubDigest()
    {
        var bytes = Encoding.UTF8.GetBytes("release package");
        using var package = new MemoryStream(bytes);
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        Assert.True(await BoundedZipArchiveExtractor.VerifySha256Async(digest, package, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifySha256Async_AllowsExtractionFromSameVerifiedStream()
    {
        using var archive = CreateArchive(zip => WriteEntry(zip, "verified.txt", "trusted"));
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(archive.ToArray())).ToLowerInvariant();
        var (root, destination) = CreatePaths();

        try
        {
            Assert.True(await BoundedZipArchiveExtractor.VerifySha256Async(digest, archive, TestContext.Current.CancellationToken));
            await BoundedZipArchiveExtractor.ExtractToDirectoryAsync(archive, destination, root, TestContext.Current.CancellationToken);

            Assert.Equal("trusted", await File.ReadAllTextAsync(Path.Combine(destination, "verified.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task VerifySha256Async_RejectsTamperedPackageAndMalformedDigest()
    {
        using var package = new MemoryStream(Encoding.UTF8.GetBytes("changed package"));

        Assert.False(await BoundedZipArchiveExtractor.VerifySha256Async(new string('0', 64), package, TestContext.Current.CancellationToken));
        Assert.False(await BoundedZipArchiveExtractor.VerifySha256Async("sha256:invalid", package, TestContext.Current.CancellationToken));
        Assert.False(await BoundedZipArchiveExtractor.VerifySha256Async(null, package, TestContext.Current.CancellationToken));
    }

    private static MemoryStream CreateArchive(Action<ZipArchive> writeEntries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            writeEntries(archive);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    private static (string Root, string Destination) CreatePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix_zip_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return (root, Path.Combine(root, "extracted"));
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StalledReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
