using System;
using System.Collections.Generic;
using System.IO;
using MulletaFlix.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace MulletaFlix.Controller.Tests.MediaEncoding;

public class EncodingHelperTests
{
    [Fact]
    public void GetMapArgs_NoSubtitle_ExcludesAllSubs()
    {
        var state = BuildState(subtitle: null, deliveryMethod: null);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map -0:s", args, StringComparison.Ordinal);
        Assert.DoesNotContain("-map 1:", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_InternalSrt_MapsFromPrimaryInput()
    {
        var sub = new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Codec = "srt" };
        var state = BuildState(sub, SubtitleDeliveryMethod.Embed);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 0:2", args, StringComparison.Ordinal);
        Assert.DoesNotContain("-map 1:", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_InternalSubAtHigherIndex_MapsCorrectIndex()
    {
        var sub0 = new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Codec = "srt" };
        var sub1 = new MediaStream { Index = 3, Type = MediaStreamType.Subtitle, Codec = "ass" };
        var state = BuildState(sub1, SubtitleDeliveryMethod.Embed, additionalStreams: [sub0, sub1]);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 0:3", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_ExternalSrt_MapsFirstStreamFromInput1()
    {
        var sub = new MediaStream
        {
            Index = 2,
            Type = MediaStreamType.Subtitle,
            Codec = "srt",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.en.srt"
        };
        var state = BuildState(sub, SubtitleDeliveryMethod.Embed);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 1:0", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_SecondExternalSrt_StillMaps1Colon0()
    {
        // Two separate .srt files â€” selecting the second one still maps 1:0
        // because MulletaFlix feeds only the selected file as ffmpeg input 1.
        var ext1 = new MediaStream
        {
            Index = 2,
            Type = MediaStreamType.Subtitle,
            Codec = "srt",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.en.srt"
        };
        var ext2 = new MediaStream
        {
            Index = 3,
            Type = MediaStreamType.Subtitle,
            Codec = "srt",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.fr.srt"
        };
        var state = BuildState(ext2, SubtitleDeliveryMethod.Embed, additionalStreams: [ext1, ext2]);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 1:0", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_MksFirstTrack_MapsInFileIndex0()
    {
        var mks0 = new MediaStream
        {
            Index = 2,
            Type = MediaStreamType.Subtitle,
            Codec = "subrip",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.mks"
        };
        var mks1 = new MediaStream
        {
            Index = 3,
            Type = MediaStreamType.Subtitle,
            Codec = "ass",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.mks"
        };
        var state = BuildState(mks0, SubtitleDeliveryMethod.Embed, additionalStreams: [mks0, mks1]);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 1:0", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMapArgs_MksSecondTrack_MapsInFileIndex1()
    {
        var mks0 = new MediaStream
        {
            Index = 2,
            Type = MediaStreamType.Subtitle,
            Codec = "subrip",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.mks"
        };
        var mks1 = new MediaStream
        {
            Index = 3,
            Type = MediaStreamType.Subtitle,
            Codec = "ass",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.mks"
        };
        var mks2 = new MediaStream
        {
            Index = 4,
            Type = MediaStreamType.Subtitle,
            Codec = "subrip",
            IsExternal = true,
            SupportsExternalStream = true,
            Path = "/media/movie.mks"
        };
        var state = BuildState(mks1, SubtitleDeliveryMethod.Embed, additionalStreams: [mks0, mks1, mks2]);
        var args = CreateHelper().GetMapArgs(state);

        Assert.Contains("-map 1:1", args, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SubtitleDeliveryMethod.Embed, true, "movie.idx")]
    [InlineData(SubtitleDeliveryMethod.Encode, true, "movie.idx")]
    [InlineData(SubtitleDeliveryMethod.Embed, false, "movie.sub")]
    [InlineData(SubtitleDeliveryMethod.Encode, false, "movie.sub")]
    public void GetInputArgument_VobSub_UsesCorrectPath(
        SubtitleDeliveryMethod deliveryMethod,
        bool createIdxFile,
        string expectedFilename)
    {
        var tempDir = Directory.CreateTempSubdirectory("MulletaFlix-test-");
        try
        {
            var subFile = Path.Combine(tempDir.FullName, "movie.sub");
            File.WriteAllText(subFile, "dummy");

            if (createIdxFile)
            {
                File.WriteAllText(Path.Combine(tempDir.FullName, "movie.idx"), "dummy");
            }

            var sub = new MediaStream
            {
                Index = 2,
                Type = MediaStreamType.Subtitle,
                Codec = "dvdsub",
                IsExternal = true,
                SupportsExternalStream = true,
                Path = subFile
            };
            var state = BuildState(sub, deliveryMethod);
            var inputArgs = CreateHelper().GetInputArgument(state, new EncodingOptions(), null);

            Assert.Contains(expectedFilename, inputArgs, StringComparison.Ordinal);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    private static EncodingJobInfo BuildState(
        MediaStream? subtitle,
        SubtitleDeliveryMethod? deliveryMethod,
        MediaStream[]? additionalStreams = null)
    {
        var video = new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" };
        var audio = new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac" };
        var streams = new List<MediaStream> { video, audio };

        if (additionalStreams is not null)
        {
            streams.AddRange(additionalStreams);
        }
        else if (subtitle is not null)
        {
            streams.Add(subtitle);
        }

        return new EncodingJobInfo(TranscodingJobType.Progressive)
        {
            MediaSource = new MediaSourceInfo
            {
                Container = "mkv",
                MediaStreams = streams,
            },
            VideoStream = video,
            AudioStream = audio,
            SubtitleStream = subtitle,
            SubtitleDeliveryMethod = deliveryMethod ?? SubtitleDeliveryMethod.Drop,
            BaseRequest = new VideoRequestDto(),
            IsVideoRequest = true,
            IsInputVideo = true,
        };
    }

    [Fact]
    public void CanStreamCopyVideo_H264SupportedAndCopyAllowed_ReturnsTrue()
    {
        var helper = CreateHelper();
        var video = new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" };
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.SupportedVideoCodecs = ["h264", "hevc"];
        state.BaseRequest.AllowVideoStreamCopy = true;

        var result = helper.CanStreamCopyVideo(state, video);

        Assert.True(result);
    }

    [Fact]
    public void CanStreamCopyVideo_SubtitleBurnIn_ReturnsFalse()
    {
        var helper = CreateHelper();
        var video = new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "h264" };
        var sub = new MediaStream { Index = 2, Type = MediaStreamType.Subtitle, Codec = "ass" };
        var state = BuildState(sub, SubtitleDeliveryMethod.Encode);
        state.SupportedVideoCodecs = ["h264"];
        state.BaseRequest.AllowVideoStreamCopy = true;
        state.BaseRequest.SubtitleStreamIndex = 2;

        var result = helper.CanStreamCopyVideo(state, video);

        Assert.False(result);
    }

    [Fact]
    public void CanStreamCopyVideo_InterlacedAndDeinterlaceRequested_ReturnsFalse()
    {
        var helper = CreateHelper();
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.VideoStream!.IsInterlaced = true;
        state.SupportedVideoCodecs = ["h264"];
        state.BaseRequest.AllowVideoStreamCopy = true;
        state.BaseRequest.DeInterlace = true;

        var result = helper.CanStreamCopyVideo(state, state.VideoStream);

        Assert.False(result);
    }

    [Fact]
    public void CanStreamCopyVideo_CodecNotSupportedByClient_ReturnsFalse()
    {
        var helper = CreateHelper();
        var video = new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "hevc" };
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.SupportedVideoCodecs = ["h264"];
        state.BaseRequest.AllowVideoStreamCopy = true;

        var result = helper.CanStreamCopyVideo(state, video);

        Assert.False(result);
    }

    [Fact]
    public void CanStreamCopyAudio_SupportedCodec_ReturnsTrue()
    {
        var helper = CreateHelper();
        var audio = new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac", Channels = 2 };
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.BaseRequest.AllowAudioStreamCopy = true;
        state.BaseRequest.EnableAutoStreamCopy = true;

        var result = helper.CanStreamCopyAudio(state, audio, ["aac", "mp3"]);

        Assert.True(result);
    }

    [Fact]
    public void CanStreamCopyAudio_CodecNotSupported_ReturnsFalse()
    {
        var helper = CreateHelper();
        var audio = new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "flac", Channels = 2 };
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.BaseRequest.AllowAudioStreamCopy = true;
        state.BaseRequest.EnableAutoStreamCopy = true;

        var result = helper.CanStreamCopyAudio(state, audio, ["aac", "mp3"]);

        Assert.False(result);
    }

    [Fact]
    public void CanStreamCopyAudio_ChannelsExceedRequested_ReturnsFalse()
    {
        var helper = CreateHelper();
        var audio = new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac", Channels = 6 };
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.BaseRequest.AllowAudioStreamCopy = true;
        state.BaseRequest.EnableAutoStreamCopy = true;
        state.BaseRequest.MaxAudioChannels = 2;

        var result = helper.CanStreamCopyAudio(state, audio, ["aac"]);

        Assert.False(result);
    }

    [Fact]
    public void GetFastSeekCommandLineParameter_ZeroTime_ReturnsEmpty()
    {
        var helper = CreateHelper();
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.BaseRequest.StartTimeTicks = 0;

        var seekArg = helper.GetFastSeekCommandLineParameter(state, new EncodingOptions(), "ts");

        Assert.Empty(seekArg);
    }

    [Fact]
    public void GetFastSeekCommandLineParameter_ProgressiveJob_FormatsSeekArgument()
    {
        var helper = CreateHelper();
        var state = BuildState(subtitle: null, deliveryMethod: null);
        // 60 seconds = 600,000,000 ticks
        state.BaseRequest.StartTimeTicks = 600_000_000L;
        state.RunTimeTicks = 3_600_000_000L; // 6 minutes

        var seekArg = helper.GetFastSeekCommandLineParameter(state, new EncodingOptions(), "ts");

        Assert.Equal("-ss 00:01:00.000", seekArg);
    }

    [Fact]
    public void GetFastSeekCommandLineParameter_HlsRemuxing_AddsHalfSecondOffset()
    {
        var helper = CreateHelper();
        var state = BuildState(subtitle: null, deliveryMethod: null);
        state.TranscodingType = TranscodingJobType.Hls;
        state.OutputVideoCodec = "copy";
        // 60 seconds = 600,000,000 ticks. With 0.5s (5,000,000 ticks) offset: 605,000,000 ticks = 00:01:00.500
        state.BaseRequest.StartTimeTicks = 600_000_000L;
        state.RunTimeTicks = 3_600_000_000L;

        var seekArg = helper.GetFastSeekCommandLineParameter(state, new EncodingOptions(), "ts");

        Assert.Equal("-ss 00:01:00.500", seekArg);
    }

    [Fact]
    public void GetFastSeekCommandLineParameter_NearEof_ClampsToRuntimeMinusFiveSeconds()
    {
        var helper = CreateHelper();
        var state = BuildState(subtitle: null, deliveryMethod: null);
        // Runtime = 100 seconds (1,000,000,000 ticks)
        state.RunTimeTicks = 1_000_000_000L;
        // Requested seek is 98 seconds (980,000,000 ticks). Max allowed is Runtime - 5s = 95s (950,000,000 ticks = 00:01:35.000)
        state.BaseRequest.StartTimeTicks = 980_000_000L;

        var seekArg = helper.GetFastSeekCommandLineParameter(state, new EncodingOptions(), "ts");

        Assert.Equal("-ss 00:01:35.000", seekArg);
    }

    private static EncodingHelper CreateHelper()
    {
        var appPaths = Mock.Of<IApplicationPaths>();
        var mediaEncoder = new Mock<IMediaEncoder>();
        var subtitleEncoder = new Mock<ISubtitleEncoder>();
        var config = new Mock<IConfiguration>();
        var configurationManager = new Mock<IConfigurationManager>();
        var pathManager = new Mock<IPathManager>();

        mediaEncoder.Setup(m => m.GetTimeParameter(It.IsAny<long>()))
            .Returns((long ticks) => TimeSpan.FromTicks(ticks).ToString(@"hh\:mm\:ss\.fff", System.Globalization.CultureInfo.InvariantCulture));

        return new EncodingHelper(
            appPaths,
            mediaEncoder.Object,
            subtitleEncoder.Object,
            config.Object,
            configurationManager.Object,
            pathManager.Object);
    }
}

