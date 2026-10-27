using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Books.OpenPackagingFormat;
using Xunit;

namespace Jellyfin.Providers.Tests.Books.OpenPackagingFormat;

public sealed class EpubUtilsTests
{
    [Fact]
    public void ReadEntry_RejectsExpandedDataPastLimit()
    {
        using var archive = CreateArchive(33);
        var entry = archive.GetEntry("entry")!;

        Assert.Throws<InvalidDataException>(() => EpubUtils.ReadEntry(entry, 32));
    }

    [Fact]
    public async Task ReadEntryAsync_RejectsExpandedDataPastLimit()
    {
        using var archive = CreateArchive(33);
        var entry = archive.GetEntry("entry")!;

        await Assert.ThrowsAsync<InvalidDataException>(() => EpubUtils.ReadEntryAsync(entry, 32, CancellationToken.None));
    }

    [Fact]
    public void ReadEntry_AllowsEntryAtLimit()
    {
        using var archive = CreateArchive(32);
        var entry = archive.GetEntry("entry")!;

        using var result = EpubUtils.ReadEntry(entry, 32);

        Assert.Equal(32, result.Length);
    }

    private static ZipArchive CreateArchive(int contentLength)
    {
        var backingStream = new MemoryStream();
        using (var writer = new ZipArchive(backingStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var output = writer.CreateEntry("entry");
            using var outputStream = output.Open();
            outputStream.Write(new byte[contentLength]);
        }

        backingStream.Position = 0;
        return new ZipArchive(backingStream, ZipArchiveMode.Read, leaveOpen: false);
    }
}
