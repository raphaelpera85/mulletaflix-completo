using System;
using MediaBrowser.MediaEncoding.Encoder;
using MediaBrowser.Model.MediaInfo;
using Xunit;

namespace Jellyfin.MediaEncoding.Tests.Encoder
{
    public class EncodingUtilsTests
    {
        [Fact]
        public void GetInputArgument_WithWindowsPath_DoesNotIncludeFilePrefix()
        {
            var windowsPath = @"N:\Filmes\#Alive (2020)\#Alive (2020).mkv";
            var result = EncodingUtils.GetInputArgument("file", windowsPath, MediaProtocol.File);

            Assert.Equal($"\"{windowsPath}\"", result);
            Assert.DoesNotContain("file:", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetInputArgument_WithHttpUrl_PreservesUrlInQuotes()
        {
            var url = "http://example.com/stream.m3u8";
            var result = EncodingUtils.GetInputArgument("file", url, MediaProtocol.Http);

            Assert.Equal($"\"{url}\"", result);
        }
    }
}
