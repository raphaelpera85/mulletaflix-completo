using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MulletaFlix.Data.Enums;
using MediaBrowser.Model.Entities;
using Xunit;

namespace MulletaFlix.Naming.Tests.Video
{
    public partial class MultiVersionTests
    {

        [Fact]
        public void TestMovieStackingWithPartNaming()
        {
            // Movie stacking with "part1"/"part2" naming
            var files = new[]
            {
                "/movies/Movie/Movie part1.mkv",
                "/movies/Movie/Movie part2.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        [Fact]
        public void TestMovieStackingWithDashPartNaming()
        {
            // Movie stacking with "- part1" / "- part2" dash separator
            var files = new[]
            {
                "/movies/Movie/Movie - part1.mkv",
                "/movies/Movie/Movie - part2.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        [Fact]
        public void TestMovieStackingWithPtNaming()
        {
            // Movie stacking with "pt1"/"pt2" short form
            var files = new[]
            {
                "/movies/Movie/Movie.pt1.mkv",
                "/movies/Movie/Movie.pt2.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        [Fact]
        public void TestMovieStackingWithHyphenNoSpaces()
        {
            // Movie stacking with hyphen directly adjacent to "part" (no spaces)
            var files = new[]
            {
                "/movies/Movie/Movie-part1.mkv",
                "/movies/Movie/Movie-part2.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        [Fact]
        public void TestMovieStackingWithHyphenNoSpacesAndVersion()
        {
            // Movie stacking with hyphen-no-space separators plus a version alternate
            var files = new[]
            {
                "/movies/Movie/Movie-1080p-part1.mkv",
                "/movies/Movie/Movie-1080p-part2.mkv",
                "/movies/Movie/Movie-720p.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            // Stacked 1080p (2 files) should be primary, 720p is alternate
            Assert.Equal(2, result[0].Files.Count);
            Assert.Single(result[0].AlternateVersions);
        }

        [Fact]
        public void TestMovieMultiVersionWithStackedAlternate()
        {
            // Movie folder where the folder-named file is the primary (single file via primaryOverride)
            // and an alternate version is itself a stack. The stacked alternate must keep all its files.
            var files = new[]
            {
                "/movies/Inception (2010)/Inception (2010).mkv",
                "/movies/Inception (2010)/Inception (2010) - 4k part1.mkv",
                "/movies/Inception (2010)/Inception (2010) - 4k part2.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList()).ToList();

            Assert.Single(result);
            Assert.Single(result[0].Files);
            Assert.Equal("/movies/Inception (2010)/Inception (2010).mkv", result[0].Files[0].Path);

            Assert.Single(result[0].AlternateVersions);
            var stackedAlternate = result[0].AlternateVersions[0];
            Assert.Equal(2, stackedAlternate.Files.Count);
            Assert.All(stackedAlternate.Files, f => Assert.Contains("4k part", f.Path, StringComparison.Ordinal));
        }

        [Fact]
        public void TestEpisodeStackingWithHyphenNoSpaces()
        {
            // Episode stacking with hyphen-no-space separators plus version alternate
            var files = new[]
            {
                "/TV/Show/Season 1/Show - S01E01-1080p-cd1.mkv",
                "/TV/Show/Season 1/Show - S01E01-1080p-cd2.mkv",
                "/TV/Show/Season 1/Show - S01E01-720p.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList(),
                collectionType: CollectionType.tvshows).ToList();

            Assert.Single(result);
            // Stacked 1080p (2 files) should be primary, 720p is alternate
            Assert.Equal(2, result[0].Files.Count);
            Assert.Single(result[0].AlternateVersions);
        }

        [Fact]
        public void TestEpisodeStackingWithHyphenNoSpacesAndTitle()
        {
            // Episode stacking with title and hyphen-no-space separators
            var files = new[]
            {
                "/TV/Show/Season 1/Show - S01E01 - Pilot-1080p-part1.mkv",
                "/TV/Show/Season 1/Show - S01E01 - Pilot-1080p-part2.mkv",
                "/TV/Show/Season 1/Show - S01E01 - Pilot-720p.mkv"
            };

            var result = _videoListResolver.Resolve(
                files.Select(i => VideoResolver.Resolve(i, false, _namingOptions)).OfType<VideoFileInfo>().ToList(),
                collectionType: CollectionType.tvshows).ToList();

            Assert.Single(result);
            // Stacked 1080p (2 files) should be primary, 720p is alternate
            Assert.Equal(2, result[0].Files.Count);
            Assert.Single(result[0].AlternateVersions);
        }
    }
}
