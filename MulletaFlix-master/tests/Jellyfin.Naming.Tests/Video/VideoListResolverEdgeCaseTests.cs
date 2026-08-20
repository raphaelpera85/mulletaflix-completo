using System;
using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MediaBrowser.Model.Entities;
using Xunit;

namespace MulletaFlix.Naming.Tests.Video
{
    public partial class VideoListResolverTests
    {

        [Fact]
        public void TestSeparateFiles()
        {
            // These should be considered separate, unrelated videos
            var files = new[]
            {
                "My video 1.mkv",
                "My video 2.mkv",
                "My video 3.mkv",
                "My video 4.mkv",
                "My video 5.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void TestMultiDisc()
        {
            var files = new[]
            {
                "M:/Movies (DVD)/Movies (Musical)/Sound of Music (1965)/Sound of Music Disc 1",
                "M:/Movies (DVD)/Movies (Musical)/Sound of Music (1965)/Sound of Music Disc 2"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, true, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TestPoundSign()
        {
            // These should be considered separate, unrelated videos
            var files = new[]
            {
                "My movie #1.mp4",
                "My movie #2.mp4"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, true, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestStackedWithTrailer()
        {
            var files = new[]
            {
                "No (2012) part1.mp4",
                "No (2012) part2.mp4",
                "No (2012) part1-trailer.mp4",
                "No (2012)-trailer.mp4"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(3, result.Count);
            Assert.False(result[0].ExtraType.HasValue);
            Assert.Equal(ExtraType.Trailer, result[1].ExtraType);
            Assert.Equal(ExtraType.Trailer, result[2].ExtraType);
        }

        [Fact]
        public void TestExtrasByFolderName()
        {
            var files = new[]
            {
                "/Movies/Top Gun (1984)/movie.mp4",
                "/Movies/Top Gun (1984)/Top Gun (1984)-trailer.mp4",
                "/Movies/Top Gun (1984)/Top Gun (1984)-trailer2.mp4",
                "/Movies/trailer.mp4"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(4, result.Count);
            Assert.False(result[0].ExtraType.HasValue);
            Assert.Equal(ExtraType.Trailer, result[1].ExtraType);
            Assert.Equal(ExtraType.Trailer, result[2].ExtraType);
            Assert.Equal(ExtraType.Trailer, result[3].ExtraType);
        }

        [Fact]
        public void TestDoubleTags()
        {
            var files = new[]
            {
                "/MCFAMILY-PC/Private3$/Heterosexual/Breast In Class 2 Counterfeit Racks (2011)/Breast In Class 2 Counterfeit Racks (2011) Disc 1 cd1.avi",
                "/MCFAMILY-PC/Private3$/Heterosexual/Breast In Class 2 Counterfeit Racks (2011)/Breast In Class 2 Counterfeit Racks (2011) Disc 1 cd2.avi",
                "/MCFAMILY-PC/Private3$/Heterosexual/Breast In Class 2 Counterfeit Racks (2011)/Breast In Class 2 Disc 2 cd1.avi",
                "/MCFAMILY-PC/Private3$/Heterosexual/Breast In Class 2 Counterfeit Racks (2011)/Breast In Class 2 Disc 2 cd2.avi"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestArgumentOutOfRangeException()
        {
            var files = new[]
            {
                "/nas-markrobbo78/Videos/INDEX HTPC/Movies/Watched/3 - ACTION/Argo (2012)/movie.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TestColony()
        {
            var files = new[]
            {
                "The Colony.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TestFourSisters()
        {
            var files = new[]
            {
                "Four Sisters and a Wedding - A.avi",
                "Four Sisters and a Wedding - B.avi"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            // The result should contain two individual movies
            // Version grouping should not work here, because the files are not in a directory with the name 'Four Sisters and a Wedding'
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestFourRooms()
        {
            var files = new[]
            {
                "Four Rooms - A.avi",
                "Four Rooms - A.mp4"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestMovieTrailer()
        {
            var files = new[]
            {
                "/Server/Despicable Me/Despicable Me (2010).mkv",
                "/Server/Despicable Me/trailer.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
            Assert.False(result[0].ExtraType.HasValue);
            Assert.Equal(ExtraType.Trailer, result[1].ExtraType);
        }

        [Fact]
        public void Resolve_TrailerInTrailersFolder_ReturnsCorrectExtraType()
        {
            var files = new[]
            {
                "/Server/Despicable Me/Despicable Me (2010).mkv",
                "/Server/Despicable Me/trailers/some title.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
            Assert.False(result[0].ExtraType.HasValue);
            Assert.Equal(ExtraType.Trailer, result[1].ExtraType);
        }

        [Fact]
        public void TestSubfolders()
        {
            var files = new[]
            {
                "/Movies/Despicable Me/Despicable Me.mkv",
                "/Movies/Despicable Me/trailers/trailer.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Equal(2, result.Count);
            Assert.False(result[0].ExtraType.HasValue);
            Assert.Equal(ExtraType.Trailer, result[1].ExtraType);
        }

        [Fact]
        public void TestDirectoryStack()
        {
            var stack = new FileStack(string.Empty, false, Array.Empty<string>());
            Assert.False(stack.ContainsFile("XX", true));
        }
    }
}
