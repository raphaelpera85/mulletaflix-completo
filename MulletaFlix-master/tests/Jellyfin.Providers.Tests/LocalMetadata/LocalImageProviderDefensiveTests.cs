using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.LocalMetadata.Images;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.LocalMetadata
{
    public class LocalImageProviderDefensiveTests
    {
        [Fact]
        public void EpisodeLocalImageProvider_NullItemPath_ReturnsEmptyWithoutException()
        {
            var provider = new EpisodeLocalImageProvider();
            var episode = new Episode
            {
                Path = null!
            };

            var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);

            var images = provider.GetImages(episode, directoryService.Object);

            Assert.Empty(images);
        }

        [Fact]
        public void EpisodeLocalImageProvider_EmptyItemPath_ReturnsEmptyWithoutException()
        {
            var provider = new EpisodeLocalImageProvider();
            var episode = new Episode
            {
                Path = string.Empty
            };

            var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);

            var images = provider.GetImages(episode, directoryService.Object);

            Assert.Empty(images);
        }

        [Fact]
        public void LocalImageProvider_SeasonWithNullSeries_DoesNotThrowNullReferenceException()
        {
            var fileSystem = new Mock<IFileSystem>();
            var provider = new LocalImageProvider(fileSystem.Object);

            var season = new Season
            {
                IndexNumber = 1,
                Name = "Season 1",
                Path = "D:/TV/MySeries/Season 01"
            };

            var directoryService = new Mock<IDirectoryService>();
            directoryService.Setup(ds => ds.GetFileSystemEntries(It.IsAny<string>()))
                .Returns([]);

            // season.Series is null because LibraryManager is not set.
            // Before fix, this throws NullReferenceException on series.IsFileProtocol.
            var exception = Record.Exception(() => provider.GetImages(season, directoryService.Object));

            Assert.Null(exception);
        }

        [Fact]
        public void LocalImageProvider_SeasonWithNullName_DoesNotThrowNullReferenceException()
        {
            var fileSystem = new Mock<IFileSystem>();
            var provider = new LocalImageProvider(fileSystem.Object);

            var season = new Season
            {
                IndexNumber = 1,
                Name = null!,
                Path = "D:/TV/MySeries/Season 01"
            };

            var directoryService = new Mock<IDirectoryService>();
            directoryService.Setup(ds => ds.GetFileSystemEntries(It.IsAny<string>()))
                .Returns([]);

            // Before fix, season.Name.Replace throws NullReferenceException.
            var exception = Record.Exception(() => provider.GetImages(season, directoryService.Object));

            Assert.Null(exception);
        }
    }
}
