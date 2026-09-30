using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaMediaSuggestionTests
{
    [Fact]
    public void SearchMediaSuggestions_IndexesStrmFilesFromConfiguredLibraryLocations()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-media-suggestions-" + Guid.NewGuid().ToString("N"));
        var episodeDirectory = Path.Combine(root, "Série Exemplo", "Season 01");
        var uppercaseEpisodeDirectory = Path.Combine(root, "Outra Série", "Season 01");
        Directory.CreateDirectory(episodeDirectory);
        Directory.CreateDirectory(uppercaseEpisodeDirectory);
        File.WriteAllText(Path.Combine(episodeDirectory, "S01E01.strm"), "https://example.invalid/video");
        File.WriteAllText(Path.Combine(uppercaseEpisodeDirectory, "S01E01.STRM"), "https://example.invalid/video");

        try
        {
            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager
                .Setup(manager => manager.GetConfiguration("nebulaftp"))
                .Returns(new NebulaFtpConfiguration { MonitorPaths = Array.Empty<string>() });

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager
                .Setup(manager => manager.GetVirtualFolders())
                .Returns([new VirtualFolderInfo { Locations = [root] }]);

            using var manager = new NebulaFtpManager(
                configurationManager.Object,
                NullLogger<NebulaFtpManager>.Instance,
                NullLoggerFactory.Instance,
                libraryManager.Object);

            var suggestions = manager.SearchMediaSuggestions("Série Ex", limit: 10);

            var suggestion = Assert.Single(suggestions);
            Assert.Equal("Série Exemplo", suggestion.Title);
            Assert.Equal("Series", suggestion.MediaType);
            Assert.Contains(manager.GetMediaSuggestionCatalog(), item => item.Title == "Outra Série");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(2000)]
    public void SearchMediaSuggestions_IndexesSmallAndLargeStrmLibraries(int titleCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-strm-scenario-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (var index = 0; index < titleCount; index++)
            {
                var seriesDirectory = Path.Combine(root, $"Série {index:D4}", "Season 01");
                Directory.CreateDirectory(seriesDirectory);
                File.WriteAllText(Path.Combine(seriesDirectory, "S01E01.strm"), "https://example.invalid/video");
            }

            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager
                .Setup(manager => manager.GetConfiguration("nebulaftp"))
                .Returns(new NebulaFtpConfiguration { MonitorPaths = Array.Empty<string>() });
            var libraryManager = new Mock<ILibraryManager>();
            libraryManager
                .Setup(manager => manager.GetVirtualFolders())
                .Returns([new VirtualFolderInfo { Locations = [root] }]);

            using var manager = new NebulaFtpManager(
                configurationManager.Object,
                NullLogger<NebulaFtpManager>.Instance,
                NullLoggerFactory.Instance,
                libraryManager.Object);

            var catalog = manager.GetMediaSuggestionCatalog();

            Assert.Equal(titleCount, catalog.Count);
            Assert.DoesNotContain(catalog, item => item.Title == "S01E01");
            Assert.Contains(catalog, item => item.Title == $"Série {titleCount - 1:D4}");
            Assert.Single(manager.SearchMediaSuggestions($"Série {titleCount - 1:D4}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SearchMediaSuggestions_PreservesHomonymsAcrossCategoriesAndIgnoresEpisodeNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-homonyms-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var category in new[] { "Animações", "Series" })
            {
                var path = Path.Combine(root, category, "Atomic (2025)", "Season 01");
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "Atomic S01E01.strm"), "https://example.invalid/episode");
            }

            var animalPath = Path.Combine(root, "Series", "Animal Kingdom (2016)", "Season 01");
            Directory.CreateDirectory(animalPath);
            File.WriteAllText(Path.Combine(animalPath, "S01E01.strm"), "https://example.invalid/episode");
            var configManager = new Mock<IServerConfigurationManager>();
            configManager.Setup(manager => manager.GetConfiguration("nebulaftp"))
                .Returns(new NebulaFtpConfiguration { MonitorPaths = [root] });
            using var manager = new NebulaFtpManager(configManager.Object, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

            var atomic = manager.SearchMediaSuggestions("Atomic");

            Assert.Equal(2, atomic.Count);
            Assert.Equal(new[] { "Animation", "Series" }, atomic.Select(item => item.MediaType).Order().ToArray());
            Assert.All(atomic, item => Assert.Equal("Atomic (2025)", item.Title));
            Assert.Equal("Series", Assert.Single(manager.SearchMediaSuggestions("Animal Kingdom")).MediaType);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("2001 A Space Odyssey (1968)", 1968)]
    [InlineData("Blade Runner 2049 (2017)", 2017)]
    [InlineData("2001 A Space Odyssey", null)]
    [InlineData("Blade Runner 2049", null)]
    public void SearchMediaSuggestions_ExtractsOnlyTrailingReleaseYear(string title, int? expectedYear)
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-suggestion-year-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, title + ".strm"), "https://example.invalid/movie");
            var configManager = new Mock<IServerConfigurationManager>();
            configManager.Setup(manager => manager.GetConfiguration("nebulaftp"))
                .Returns(new NebulaFtpConfiguration { MonitorPaths = [root] });
            using var manager = new NebulaFtpManager(configManager.Object, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

            Assert.Equal(expectedYear, Assert.Single(manager.SearchMediaSuggestions(title)).Year);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrioritizeMedia_PersistsRequestWhenNebulaWorkersAreStopped()
    {
        var config = new NebulaFtpConfiguration { Enabled = false };
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("nebulaftp")).Returns(config);

        using var manager = new NebulaFtpManager(
            configurationManager.Object,
            NullLogger<NebulaFtpManager>.Instance,
            NullLoggerFactory.Instance);

        manager.PrioritizeMedia(string.Empty, seriesName: "  The Requested Show  ");

        Assert.Equal(new[] { "The Requested Show" }, config.RequestedMediaPriorities);
        configurationManager.Verify(manager => manager.SaveConfiguration("nebulaftp", config), Times.Once);
    }

    [Fact]
    public void IsMediaRequestPrioritized_UsesPersistedTitlesWithoutCaseSensitivity()
    {
        var config = new NebulaFtpConfiguration { RequestedMediaPriorities = ["The Requested Show"] };
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("nebulaftp")).Returns(config);
        using var manager = new NebulaFtpManager(
            configurationManager.Object,
            NullLogger<NebulaFtpManager>.Instance,
            NullLoggerFactory.Instance);

        Assert.True(manager.IsMediaRequestPrioritized("  the requested show "));
        Assert.False(manager.IsMediaRequestPrioritized("Another Show"));
        Assert.False(manager.IsMediaRequestPrioritized("  "));
    }
}
