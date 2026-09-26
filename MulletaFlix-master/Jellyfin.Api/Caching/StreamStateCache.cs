using System;
using System.Globalization;
using System.Text;
using MediaBrowser.Controller.Streaming;
using Microsoft.Extensions.Caching.Memory;

namespace MulletaFlix.Api.Caching;

/// <summary>
/// Caches <see cref="StreamState"/> instances built by <see cref="MulletaFlix.Api.Helpers.StreamingHelpers.GetStreamingState"/>
/// so that consecutive HLS segment requests for the same playback session do not each pay the cost of
/// re-resolving the media item, re-probing/attaching media source info and recomputing encoding parameters.
/// </summary>
/// <remarks>
/// Only cacheable when the request carries a <c>PlaySessionId</c> and <c>MediaSourceId</c> and is not a live
/// stream (live streams have side-effecting close semantics on <see cref="StreamState.Dispose"/> that make
/// sharing a single instance across requests risky). The cache key additionally folds in every request field
/// that can change the resulting encoding (audio/subtitle/video stream index, bitrate, codec, resolution, ...),
/// so switching an audio or subtitle track mid-stream — which changes those fields — naturally misses the cache
/// and rebuilds a fresh <see cref="StreamState"/> instead of serving stale state. Entries use a short sliding
/// expiration: each hit renews the window, so an actively-playing session stays warm, while an abandoned/finished
/// session (no more segment requests) is evicted and disposed automatically without requiring explicit cleanup.
/// </remarks>
public class StreamStateCache
{
    /// <summary>
    /// Sliding TTL for cached <see cref="StreamState"/> entries. Must comfortably cover the gap between two
    /// consecutive segment requests for the same session (HLS segments are typically 3-6s) while still bounding
    /// memory for sessions that stop requesting segments without an explicit stop/cleanup call.
    /// </summary>
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamStateCache"/> class.
    /// </summary>
    /// <param name="cache">Instance of the <see cref="IMemoryCache"/> interface.</param>
    public StreamStateCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Whether the given request is eligible for caching its resulting <see cref="StreamState"/>.
    /// </summary>
    /// <param name="request">The streaming request.</param>
    /// <returns>Whether the request is cacheable.</returns>
    public static bool IsCacheable(StreamingRequestDto request)
    {
        return !string.IsNullOrWhiteSpace(request.PlaySessionId)
            && !string.IsNullOrWhiteSpace(request.MediaSourceId)
            && string.IsNullOrWhiteSpace(request.LiveStreamId);
    }

    /// <summary>
    /// Builds a cache key from the parts of the request that determine the resulting encoding.
    /// Deliberately excludes per-segment fields (CurrentRuntimeTicks, ActualSegmentLengthTicks, StartTimeTicks)
    /// so the same key is reused across every segment of a playback session, but changes to it (e.g. switching
    /// the audio/subtitle track or requested bitrate) yield a different key and force a fresh <see cref="StreamState"/>.
    /// </summary>
    /// <param name="request">The streaming request.</param>
    /// <returns>A cache key string.</returns>
    public static string BuildCacheKey(StreamingRequestDto request)
    {
        var sb = new StringBuilder(128);
        sb.Append(request.PlaySessionId);
        sb.Append('|');
        sb.Append(request.MediaSourceId);
        sb.Append('|');
        sb.Append(request.DeviceId);
        sb.Append('|');
        sb.Append(request.AudioStreamIndex.GetValueOrDefault(-1).ToString(CultureInfo.InvariantCulture));
        sb.Append('|');
        sb.Append(request.SubtitleStreamIndex.GetValueOrDefault(-1).ToString(CultureInfo.InvariantCulture));
        sb.Append('|');
        sb.Append(request.VideoStreamIndex.GetValueOrDefault(-1).ToString(CultureInfo.InvariantCulture));
        sb.Append('|');
        sb.Append(request.SubtitleMethod);
        sb.Append('|');
        sb.Append(request.AudioCodec);
        sb.Append('|');
        sb.Append(request.VideoCodec);
        sb.Append('|');
        sb.Append(request.SubtitleCodec);
        sb.Append('|');
        sb.Append(request.Container);
        sb.Append('|');
        sb.Append(request.SegmentContainer);
        sb.Append('|');
        sb.Append(request.AudioBitRate.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.VideoBitRate.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.AudioChannels.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.MaxAudioChannels.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.TranscodingMaxAudioChannels.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.Width.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.Height.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.MaxWidth.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.MaxHeight.GetValueOrDefault());
        sb.Append('|');
        sb.Append(request.Static);
        sb.Append('|');
        sb.Append(request.Profile);
        sb.Append('|');
        sb.Append(request.Level);
        sb.Append('|');
        sb.Append(request.EnableMpegtsM2TsMode);
        sb.Append('|');
        sb.Append(request.RequireAvc);
        sb.Append('|');
        sb.Append(request.DeInterlace);
        sb.Append('|');
        sb.Append(request.RequireNonAnamorphic);
        sb.Append('|');
        sb.Append(request.CopyTimestamps);
        sb.Append('|');
        sb.Append(request.AlwaysBurnInSubtitleWhenTranscoding);
        sb.Append('|');
        sb.Append(request.EnableAudioVbrEncoding);

        return sb.ToString();
    }

    /// <summary>
    /// Try to get a cached <see cref="StreamState"/>.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="state">The cached state, if found.</param>
    /// <returns>Whether a cached state was found.</returns>
    public bool TryGet(string key, out StreamState? state)
    {
        return _cache.TryGetValue(key, out state);
    }

    /// <summary>
    /// Caches a <see cref="StreamState"/>, disposing whatever it replaces or expires to once evicted.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="state">The state to cache.</param>
    public void Set(string key, StreamState state)
    {
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingTtl
        };
        options.RegisterPostEvictionCallback((_, value, _, _) =>
        {
            // Fire-and-forget cleanup: mirrors the synchronous Dispose() path already used elsewhere in
            // DynamicHlsController (e.g. when ffmpeg fails to start), which itself backgrounds any live
            // stream close so it never blocks the eviction callback.
            (value as StreamState)?.Dispose();
        });

        _cache.Set(key, state, options);
    }

    /// <summary>
    /// Removes a cached entry without disposing the passed-in instance (used when the caller is about to
    /// dispose it itself, e.g. after a failed ffmpeg start).
    /// </summary>
    /// <param name="key">The cache key.</param>
    public void Invalidate(string key)
    {
        _cache.Remove(key);
    }
}
