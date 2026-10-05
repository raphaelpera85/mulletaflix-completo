using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;

namespace MulletaFlix.LiveTv.Recordings;

internal static class ActiveRecordingStreamPathResolver
{
    public static string? Resolve(IEnumerable<ActiveRecordingInfo> activeRecordings, string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId))
        {
            return null;
        }

        var recording = activeRecordings.FirstOrDefault(candidate =>
            string.Equals(candidate.StreamId, streamId, StringComparison.Ordinal)
            && candidate.Timer?.Status == RecordingStatus.InProgress
            && candidate.CancellationTokenSource?.IsCancellationRequested != true);

        return recording?.Path;
    }
}
