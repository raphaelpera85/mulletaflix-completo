using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MulletaFlix.MediaEncoding.Keyframes.Matroska;

public class MatroskaKeyframeExtractorTests
{
    [Fact]
    public async Task GetKeyframeDataAsync_MatchesSynchronousReader()
    {
        const string path = "Matroska/Test Data/keyframes.mkv";
        var synchronous = MatroskaKeyframeExtractor.GetKeyframeData(path);
        var asynchronous = await MatroskaKeyframeExtractor.GetKeyframeDataAsync(path, CancellationToken.None);

        Assert.Equal(synchronous.TotalDuration, asynchronous.TotalDuration);
        Assert.Equal(synchronous.KeyframeTicks, asynchronous.KeyframeTicks);
        Assert.NotEmpty(asynchronous.KeyframeTicks);
    }

    [Fact]
    public async Task GetKeyframeDataAsync_CancellationDuringReadIsPropagated()
    {
        using var stream = new BlockingReadStream();
        using var cancellation = new CancellationTokenSource();
        var read = MatroskaKeyframeExtractor.GetKeyframeDataAsync(stream, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    [Fact]
    public async Task GetKeyframeDataAsync_TruncatedEbmlHeaderThrows()
    {
        using var stream = new MemoryStream(new byte[] { 0x1A });

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            MatroskaKeyframeExtractor.GetKeyframeDataAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public async Task GetKeyframeDataAsync_UsesVideoPositionAmongAllCueTracks(ulong firstTrack, ulong secondTrack)
    {
        var bytes = CreateMatroskaWithCueTracks(firstTrack, secondTrack);
        using var stream = new MemoryStream(bytes);

        var asynchronous = await MatroskaKeyframeExtractor.GetKeyframeDataAsync(stream, CancellationToken.None);

        Assert.Equal(new long[] { 50_000 }, asynchronous.KeyframeTicks);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void GetKeyframeData_UsesVideoPositionAmongAllCueTracks(ulong firstTrack, ulong secondTrack)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, CreateMatroskaWithCueTracks(firstTrack, secondTrack));

            var result = MatroskaKeyframeExtractor.GetKeyframeData(path);

            Assert.Equal(new long[] { 50_000 }, result.KeyframeTicks);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreateMatroskaWithCueTracks(ulong firstTrack, ulong secondTrack)
    {
        var info = Master("1549A966", UIntElement("2AD7B1", 1_000_000));
        var tracks = Master(
            "1654AE6B",
            Master("AE", UIntElement("D7", 1), UIntElement("83", 1)),
            Master("AE", UIntElement("D7", 2), UIntElement("83", 2)));
        var positions = Master("B7", UIntElement("F7", firstTrack))
            .Concat(Master("B7", UIntElement("F7", secondTrack))).ToArray();
        var cues = Master("1C53BB6B", Master("BB", UIntElement("B3", 5), positions));
        var seekHeadLength = Master(
            "114D9B74",
            Seek("1549A966", 0),
            Seek("1654AE6B", 0),
            Seek("1C53BB6B", 0)).Length;
        var seekHead = Master(
            "114D9B74",
            Seek("1549A966", (uint)seekHeadLength),
            Seek("1654AE6B", (uint)(seekHeadLength + info.Length)),
            Seek("1C53BB6B", (uint)(seekHeadLength + info.Length + tracks.Length)));

        return Master("18538067", seekHead, info, tracks, cues);
    }

    private static byte[] Seek(string id, uint position)
    {
        return Master("4DBB", Element("53AB", Convert.FromHexString(id)), UIntElement("53AC", position));
    }

    private static byte[] UIntElement(string id, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        var length = 1;
        while (length < bytes.Length && value >= (1UL << (length * 8)))
        {
            length++;
        }

        for (var i = 0; i < length; i++)
        {
            bytes[length - i - 1] = (byte)(value >> (i * 8));
        }

        return Element(id, bytes[..length].ToArray());
    }

    private static byte[] Master(string id, params byte[][] children)
    {
        return Element(id, children.SelectMany(static child => child).ToArray());
    }

    private static byte[] Element(string id, byte[] data)
    {
        var identifier = Convert.FromHexString(id);
        var size = EncodeSize(data.Length);
        return identifier.Concat(size).Concat(data).ToArray();
    }

    private static byte[] EncodeSize(int size)
    {
        for (var length = 1; length <= 8; length++)
        {
            var marker = 1UL << (length * 7);
            if ((ulong)size < marker - 1)
            {
                var encoded = (ulong)size + marker;
                var bytes = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    bytes[length - i - 1] = (byte)(encoded >> (i * 8));
                }

                return bytes;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(size));
    }

    private sealed class BlockingReadStream : MemoryStream
    {
        public override long Length => 1;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
