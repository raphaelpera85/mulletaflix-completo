using System;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MulletaFlix.Api.Caching;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Caching;

public class StreamStateCacheTests
{
    private static StreamState CreateState(string playSessionId, string mediaSourceId, int? audioStreamIndex = null)
    {
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        var transcodeManager = new Mock<ITranscodeManager>();

        return new StreamState(mediaSourceManager.Object, TranscodingJobType.Hls, transcodeManager.Object)
        {
            Request = new StreamingRequestDto
            {
                PlaySessionId = playSessionId,
                MediaSourceId = mediaSourceId,
                AudioStreamIndex = audioStreamIndex
            }
        };
    }

    [Fact]
    public void IsCacheable_MissingPlaySessionId_ReturnsFalse()
    {
        var request = new StreamingRequestDto { MediaSourceId = "abc" };

        Assert.False(StreamStateCache.IsCacheable(request));
    }

    [Fact]
    public void IsCacheable_MissingMediaSourceId_ReturnsFalse()
    {
        var request = new StreamingRequestDto { PlaySessionId = "abc" };

        Assert.False(StreamStateCache.IsCacheable(request));
    }

    [Fact]
    public void IsCacheable_LiveStream_ReturnsFalse()
    {
        var request = new StreamingRequestDto
        {
            PlaySessionId = "abc",
            MediaSourceId = "def",
            LiveStreamId = "live-1"
        };

        Assert.False(StreamStateCache.IsCacheable(request));
    }

    [Fact]
    public void IsCacheable_ValidVodRequest_ReturnsTrue()
    {
        var request = new StreamingRequestDto
        {
            PlaySessionId = "abc",
            MediaSourceId = "def"
        };

        Assert.True(StreamStateCache.IsCacheable(request));
    }

    [Fact]
    public void BuildCacheKey_SameSessionDifferentSegments_ProducesSameKey()
    {
        var requestSegment1 = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AudioStreamIndex = 1,
            CurrentRuntimeTicks = 0,
            ActualSegmentLengthTicks = 30_000_000,
            StartTimeTicks = 0
        };

        var requestSegment2 = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AudioStreamIndex = 1,
            CurrentRuntimeTicks = 30_000_000,
            ActualSegmentLengthTicks = 30_000_000,
            StartTimeTicks = 30_000_000
        };

        Assert.Equal(StreamStateCache.BuildCacheKey(requestSegment1), StreamStateCache.BuildCacheKey(requestSegment2));
    }

    [Fact]
    public void BuildCacheKey_AudioTrackSwitch_ProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AudioStreamIndex = 1
        };

        var requestAfter = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AudioStreamIndex = 2
        };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_StreamCopyOptionProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AllowVideoStreamCopy = true
        };
        var requestAfter = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            AllowVideoStreamCopy = false
        };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_AudioSampleRateProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", AudioSampleRate = 48_000 };
        var requestAfter = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", AudioSampleRate = 44_100 };
        var requestWithoutLimit = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1" };
        var requestWithZeroLimit = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", AudioSampleRate = 0 };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestWithoutLimit), StreamStateCache.BuildCacheKey(requestWithZeroLimit));
    }

    [Fact]
    public void BuildCacheKey_MaxRefFramesProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", MaxRefFrames = 2 };
        var requestAfter = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", MaxRefFrames = 4 };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_MaxVideoBitDepthProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", MaxVideoBitDepth = 8 };
        var requestAfter = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1", MaxVideoBitDepth = 10 };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_StreamOptionsProduceDifferentKeys()
    {
        var requestBefore = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            StreamOptions = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["deinterlace"] = "false"
            }
        };
        var requestAfter = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            StreamOptions = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["deinterlace"] = "true"
            }
        };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_StreamOptionOrderDoesNotChangeKey()
    {
        var requestBefore = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            StreamOptions = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["option-a"] = "a",
                ["option-b"] = "b"
            }
        };
        var requestAfter = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            StreamOptions = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["option-b"] = "b",
                ["option-a"] = "a"
            }
        };

        Assert.Equal(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_SeparatesContainerValuesContainingKeyDelimiter()
    {
        var requestA = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            Container = "mkv|mp4",
            SegmentContainer = "ts"
        };
        var requestB = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            Container = "mkv",
            SegmentContainer = "mp4|ts"
        };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestA), StreamStateCache.BuildCacheKey(requestB));
    }

    [Fact]
    public void BuildCacheKey_SubtitleTrackSwitch_ProducesDifferentKey()
    {
        var requestBefore = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            SubtitleStreamIndex = 0
        };

        var requestAfter = new StreamingRequestDto
        {
            PlaySessionId = "session-1",
            MediaSourceId = "media-1",
            SubtitleStreamIndex = 1
        };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestBefore), StreamStateCache.BuildCacheKey(requestAfter));
    }

    [Fact]
    public void BuildCacheKey_DifferentPlaySessions_ProducesDifferentKeys()
    {
        var requestA = new StreamingRequestDto { PlaySessionId = "session-a", MediaSourceId = "media-1" };
        var requestB = new StreamingRequestDto { PlaySessionId = "session-b", MediaSourceId = "media-1" };

        Assert.NotEqual(StreamStateCache.BuildCacheKey(requestA), StreamStateCache.BuildCacheKey(requestB));
    }

    [Fact]
    public void TryGetSet_RoundTrip_ReturnsCachedState()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StreamStateCache(memoryCache);

        var request = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1" };
        var key = StreamStateCache.BuildCacheKey(request);
        var state = CreateState("session-1", "media-1");

        cache.Set(key, state);

        var found = cache.TryGet(key, out var cached);

        Assert.True(found);
        Assert.Same(state, cached);
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StreamStateCache(memoryCache);

        var found = cache.TryGet("no-such-key", out var cached);

        Assert.False(found);
        Assert.Null(cached);
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StreamStateCache(memoryCache);

        var request = new StreamingRequestDto { PlaySessionId = "session-1", MediaSourceId = "media-1" };
        var key = StreamStateCache.BuildCacheKey(request);
        var state = CreateState("session-1", "media-1");

        cache.Set(key, state);
        cache.Invalidate(key);

        Assert.False(cache.TryGet(key, out _));
    }
}
