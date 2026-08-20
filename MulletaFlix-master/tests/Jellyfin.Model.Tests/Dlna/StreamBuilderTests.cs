using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions.Json;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Model.Tests
{
    public partial class StreamBuilderTests
    {

        [Theory]
        // EnableSubtitleExtraction = false, internal subtitles
        [InlineData("srt", "srt", false, false, PlayMethod.Transcode, SubtitleDeliveryMethod.Encode)]
        [InlineData("srt", "srt", false, false, PlayMethod.DirectPlay, SubtitleDeliveryMethod.External)]
        [InlineData("pgssub", "pgssub", false, false, PlayMethod.Transcode, SubtitleDeliveryMethod.Encode)]
        [InlineData("pgssub", "pgssub", false, false, PlayMethod.DirectPlay, SubtitleDeliveryMethod.External)]
        [InlineData("pgssub", "srt", false, false, PlayMethod.Transcode, SubtitleDeliveryMethod.Encode)]
        // EnableSubtitleExtraction = false, external subtitles
        [InlineData("srt", "srt", false, true, PlayMethod.Transcode, SubtitleDeliveryMethod.External)]
        // EnableSubtitleExtraction = true, internal subtitles
        [InlineData("srt", "srt", true, false, PlayMethod.Transcode, SubtitleDeliveryMethod.External)]
        [InlineData("pgssub", "pgssub", true, false, PlayMethod.Transcode, SubtitleDeliveryMethod.External)]
        [InlineData("pgssub", "pgssub", true, false, PlayMethod.DirectPlay, SubtitleDeliveryMethod.External)]
        [InlineData("pgssub", "srt", true, false, PlayMethod.Transcode, SubtitleDeliveryMethod.Encode)]
        // EnableSubtitleExtraction = true, external subtitles
        [InlineData("srt", "srt", true, true, PlayMethod.Transcode, SubtitleDeliveryMethod.External)]
        public void GetSubtitleProfile_RespectsExtractionSetting(
            string codec,
            string profileFormat,
            bool enableSubtitleExtraction,
            bool isExternal,
            PlayMethod playMethod,
            SubtitleDeliveryMethod expectedMethod)
        {
            var mediaSource = new MediaSourceInfo();
            var subtitleStream = new MediaStream
            {
                Type = MediaStreamType.Subtitle,
                Index = 0,
                IsExternal = isExternal,
                Path = isExternal ? "/media/sub." + codec : null,
                Codec = codec,
                SupportsExternalStream = MediaStream.IsTextFormat(codec)
            };

            var subtitleProfiles = new[]
            {
                new SubtitleProfile { Format = profileFormat, Method = SubtitleDeliveryMethod.External }
            };

            var transcoderSupport = new Mock<ITranscoderSupport>();
            transcoderSupport.Setup(t => t.CanExtractSubtitles(It.IsAny<string>())).Returns(enableSubtitleExtraction);

            var result = StreamBuilder.GetSubtitleProfile(
                mediaSource,
                subtitleStream,
                subtitleProfiles,
                playMethod,
                transcoderSupport.Object,
                null,
                null);

            Assert.Equal(expectedMethod, result.Method);
        }

        [Theory]
        // External text subs embedded into MKV when transcoding (#16403)
        [InlineData("srt", true, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        [InlineData("ass", true, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        // External graphical subs embedded into MKV when transcoding
        [InlineData("pgssub", true, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        [InlineData("dvdsub", true, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        // External subs remain external when transcoding to non-MKV containers
        [InlineData("srt", true, PlayMethod.Transcode, "mp4", MediaStreamProtocol.hls, SubtitleDeliveryMethod.External)]
        [InlineData("srt", true, PlayMethod.Transcode, "ts", MediaStreamProtocol.hls, SubtitleDeliveryMethod.External)]
        // External subs remain external during DirectPlay even with MKV
        [InlineData("srt", true, PlayMethod.DirectPlay, "mkv", null, SubtitleDeliveryMethod.External)]
        // Internal subs still embedded into MKV when transcoding (existing behavior)
        [InlineData("srt", false, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        [InlineData("pgssub", false, PlayMethod.Transcode, "mkv", MediaStreamProtocol.http, SubtitleDeliveryMethod.Embed)]
        public void GetSubtitleProfile_ReturnsExpectedDeliveryMethod(
            string codec,
            bool isExternal,
            PlayMethod playMethod,
            string outputContainer,
            MediaStreamProtocol? transcodingSubProtocol,
            SubtitleDeliveryMethod expectedMethod)
        {
            var mediaSource = new MediaSourceInfo();
            var subtitleStream = new MediaStream
            {
                Codec = codec,
                Language = "eng",
                IsExternal = isExternal,
                Type = MediaStreamType.Subtitle,
                SupportsExternalStream = true
            };

            var subtitleProfiles = new[]
            {
                new SubtitleProfile { Format = codec, Method = SubtitleDeliveryMethod.Embed },
                new SubtitleProfile { Format = codec, Method = SubtitleDeliveryMethod.External }
            };

            var transcoderSupport = new Mock<ITranscoderSupport>();
            transcoderSupport.Setup(x => x.CanExtractSubtitles(It.IsAny<string>())).Returns(true);

            var result = StreamBuilder.GetSubtitleProfile(
                mediaSource,
                subtitleStream,
                subtitleProfiles,
                playMethod,
                transcoderSupport.Object,
                outputContainer,
                transcodingSubProtocol);

            Assert.Equal(expectedMethod, result.Method);
        }
    }
}
