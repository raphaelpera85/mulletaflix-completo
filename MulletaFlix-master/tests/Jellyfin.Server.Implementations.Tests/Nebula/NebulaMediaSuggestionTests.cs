using System;
using System.IO;
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
        Directory.CreateDirectory(episodeDirectory);
        File.WriteAllText(Path.Combine(episodeDirectory, "S01E01.strm"), "https://example.invalid/video");

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
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
