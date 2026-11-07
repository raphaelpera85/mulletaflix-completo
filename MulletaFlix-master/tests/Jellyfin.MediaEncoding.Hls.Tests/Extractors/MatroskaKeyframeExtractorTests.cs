using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.MediaEncoding.Hls.Extractors;

public class MatroskaKeyframeExtractorTests
{
    [Fact]
    public async Task TryExtractKeyframesAsync_UsesAsyncMatroskaReader()
    {
        var extractor = new MatroskaKeyframeExtractor(NullLogger<MatroskaKeyframeExtractor>.Instance);

        var result = await extractor.TryExtractKeyframesAsync(
            System.Guid.Empty,
            Path.Combine("Matroska", "Test Data", "keyframes.mkv"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.KeyframeTicks);
    }
}
