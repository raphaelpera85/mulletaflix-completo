using MulletaFlix.Api.Middleware;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class RequestPathLogRedactorTests
{
    [Theory]
    [InlineData("/LiveTv/LiveRecordings/capability-secret/stream", "/LiveTv/LiveRecordings/[REDACTED]/stream")]
    [InlineData("/livetv/liverecordings/capability-secret/STREAM", "/livetv/liverecordings/[REDACTED]/STREAM")]
    [InlineData("/prefix/LiveTv/LiveRecordings/capability-secret/stream", "/prefix/LiveTv/LiveRecordings/[REDACTED]/stream")]
    [InlineData("/LiveTv/LiveRecordings/capability-secret/status", "/LiveTv/LiveRecordings/capability-secret/status")]
    [InlineData("/Videos/capability-secret/stream", "/Videos/capability-secret/stream")]
    public void RedactSensitiveSegments_OnlyRedactsLiveRecordingCapability(string path, string expected)
    {
        Assert.Equal(expected, RequestPathLogRedactor.RedactSensitiveSegments(path));
    }
}
