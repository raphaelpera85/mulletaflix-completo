using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MulletaFlix.LiveTv.Recordings;
using Xunit;

namespace MulletaFlix.LiveTv.Tests.Recordings;

public sealed class ActiveRecordingStreamPathResolverTests
{
    [Fact]
    public void Resolve_ReturnsPathForActiveCapability()
    {
        var recording = CreateRecording("timer-id", "stream-capability", RecordingStatus.InProgress);

        var path = ActiveRecordingStreamPathResolver.Resolve([recording], "stream-capability");

        Assert.Equal(recording.Path, path);
    }

    [Fact]
    public void Resolve_DoesNotAcceptTimerId()
    {
        var recording = CreateRecording("timer-id", "different-stream-capability", RecordingStatus.InProgress);

        var path = ActiveRecordingStreamPathResolver.Resolve([recording], "timer-id");

        Assert.Null(path);
    }

    [Theory]
    [InlineData(RecordingStatus.Completed)]
    [InlineData(RecordingStatus.Error)]
    public void Resolve_RejectsNonActiveRecording(RecordingStatus status)
    {
        var recording = CreateRecording("timer-id", "stream-capability", status);

        var path = ActiveRecordingStreamPathResolver.Resolve([recording], "stream-capability");

        Assert.Null(path);
    }

    [Fact]
    public void Resolve_RejectsCancelledRecording()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var recording = CreateRecording("timer-id", "stream-capability", RecordingStatus.InProgress);
        recording.CancellationTokenSource = cancellation;

        var path = ActiveRecordingStreamPathResolver.Resolve([recording], "stream-capability");

        Assert.Null(path);
    }

    [Fact]
    public void ActiveRecordingInfo_CreatesDistinctOpaqueCapabilities()
    {
        var first = new ActiveRecordingInfo();
        var second = new ActiveRecordingInfo();

        Assert.Matches("^[0-9a-f]{32}$", first.StreamId);
        Assert.NotEqual(first.StreamId, second.StreamId);
    }

    private static ActiveRecordingInfo CreateRecording(string timerId, string streamId, RecordingStatus status)
        => new()
        {
            Id = timerId,
            StreamId = streamId,
            Path = "recording.ts",
            Timer = new TimerInfo { Status = status },
            CancellationTokenSource = new CancellationTokenSource()
        };
}
