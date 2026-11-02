using System.IO;
using System.Text.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MulletaFlix.MediaEncoding.Keyframes.FfProbe
{
    public class FfProbeKeyframeExtractorTests
    {
        [Theory]
        [InlineData("keyframes.txt", "keyframes_result.json")]
        [InlineData("keyframes_streamduration.txt", "keyframes_streamduration_result.json")]
        public void ParseStream_Valid_Success(string testDataFileName, string resultFileName)
        {
            var testDataPath = Path.Combine("FfProbe/Test Data", testDataFileName);
            var resultPath = Path.Combine("FfProbe/Test Data", resultFileName);
            using var resultFileStream = File.OpenRead(resultPath);
            var expectedResult = JsonSerializer.Deserialize<KeyframeData>(resultFileStream)!;

            using var fileStream = File.OpenRead(testDataPath);
            using var streamReader = new StreamReader(fileStream);

            var result = FfProbeKeyframeExtractor.ParseStream(streamReader);

            Assert.Equal(expectedResult.TotalDuration, result.TotalDuration);
            Assert.Equal(expectedResult.KeyframeTicks, result.KeyframeTicks);
        }

        [Fact]
        public void ParseStream_MalformedLines_IgnoresInvalidRecords()
        {
            using var streamReader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("unexpected output\npacket,1.5\npacket,2.5,K_\nformat,3.0\n")));

            var result = FfProbeKeyframeExtractor.ParseStream(streamReader);

            Assert.Equal(TimeSpan.FromSeconds(3).Ticks, result.TotalDuration);
            Assert.Equal(new[] { TimeSpan.FromSeconds(2.5).Ticks }, result.KeyframeTicks);
        }

        [Theory]
        [InlineData("keyframes.txt", "keyframes_result.json")]
        [InlineData("keyframes_streamduration.txt", "keyframes_streamduration_result.json")]
        public async Task ParseStreamAsync_Valid_Success(string testDataFileName, string resultFileName)
        {
            var testDataPath = Path.Combine("FfProbe/Test Data", testDataFileName);
            var resultPath = Path.Combine("FfProbe/Test Data", resultFileName);
            using var resultFileStream = File.OpenRead(resultPath);
            var expectedResult = JsonSerializer.Deserialize<KeyframeData>(resultFileStream)!;
            using var fileStream = File.OpenRead(testDataPath);
            using var streamReader = new StreamReader(fileStream);

            var result = await FfProbeKeyframeExtractor.ParseStreamAsync(streamReader, CancellationToken.None);

            Assert.Equal(expectedResult.TotalDuration, result.TotalDuration);
            Assert.Equal(expectedResult.KeyframeTicks, result.KeyframeTicks);
        }

        [Fact]
        public async Task ParseStreamAsync_Canceled_ThrowsOperationCanceledException()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            using var streamReader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("packet,1.5,K_\n")));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                FfProbeKeyframeExtractor.ParseStreamAsync(streamReader, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task GetKeyframeDataAsync_CanceledBeforeStart_DoesNotLaunchProcess()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                FfProbeKeyframeExtractor.GetKeyframeDataAsync(string.Empty, string.Empty, cancellationTokenSource.Token));
        }
    }
}
