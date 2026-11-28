// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Configuration;
using IntroSkipper.Db;
using IntroSkipper.FFmpeg;
using IntroSkipper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class EntrypointLibraryOptionsTests
{
    [Fact]
    public async Task ItemUpdated_OnlyQueuesItemsFromLibrariesWithIntroSkipperEnabled()
    {
        var previousPlugin = Plugin.Instance;
        var temp = Directory.CreateTempSubdirectory("mfx-introskipper-entrypoint-");
        AnalysisScheduler? scheduler = null;
        Entrypoint? entrypoint = null;
        try
        {
            var configuration = new PluginConfiguration { AutoDetectIntros = true };
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.PluginsPath).Returns(temp.FullName);
            paths.Setup(value => value.PluginConfigurationsPath).Returns(temp.FullName);

            var serializer = new Mock<IXmlSerializer>();
            serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(configuration);

            var serverConfiguration = new Mock<IServerConfigurationManager>();
            serverConfiguration.Setup(value => value.Configuration).Returns(new ServerConfiguration());

            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(value => value.Plugins).Returns(Array.Empty<LocalPlugin>());
            _ = new Plugin(
                paths.Object,
                serializer.Object,
                serverConfiguration.Object,
                Mock.Of<ILibraryManager>(),
                Mock.Of<IChapterManager>(),
                pluginManager.Object);

            var libraryManager = new Mock<ILibraryManager>();
            var disabledOptions = new LibraryOptions { DisabledMediaSegmentProviders = [Plugin.ProviderName] };
            var enabledOptions = new LibraryOptions();
            libraryManager.Setup(value => value.GetLibraryOptions(It.IsAny<MediaBrowser.Controller.Entities.BaseItem>()))
                .Returns((MediaBrowser.Controller.Entities.BaseItem item) => item.Name == "Disabled" ? disabledOptions : enabledOptions);

            var ffmpeg = new Mock<IFFmpegService>();
            ffmpeg.Setup(value => value.CheckFFmpegVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

            scheduler = new AnalysisScheduler(null!, null!, null!, TimeProvider.System, NullLogger<AnalysisScheduler>.Instance);
            entrypoint = new Entrypoint(
                libraryManager.Object,
                Mock.Of<IDetectionCacheDatabase>(),
                ffmpeg.Object,
                NullLogger<Entrypoint>.Instance,
                scheduler);
            await entrypoint.StartAsync(CancellationToken.None);

            libraryManager.Raise(value => value.ItemUpdated += null, libraryManager.Object, new ItemChangeEventArgs
            {
                Item = CreateMovie("Disabled"),
                UpdateReason = ItemUpdateType.MetadataDownload
            });
            Assert.Equal(0, scheduler.Status.ChangedItems);

            libraryManager.Raise(value => value.ItemUpdated += null, libraryManager.Object, new ItemChangeEventArgs
            {
                Item = CreateMovie("Enabled"),
                UpdateReason = ItemUpdateType.MetadataDownload
            });
            Assert.Equal(1, scheduler.Status.ChangedItems);
        }
        finally
        {
            if (entrypoint is not null)
            {
                await entrypoint.StopAsync(CancellationToken.None);
            }

            if (scheduler is not null)
            {
                await scheduler.StopAsync(CancellationToken.None);
                scheduler.Dispose();
            }

            typeof(Plugin).GetProperty(nameof(Plugin.Instance))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(null, [previousPlugin]);
            Directory.Delete(temp.FullName, recursive: true);
        }
    }

    private static Movie CreateMovie(string name) => new TestMovie
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = $"C:\\media\\{name}.mkv"
    };

    private sealed class TestMovie : Movie
    {
        public override MediaBrowser.Model.Entities.LocationType LocationType
            => MediaBrowser.Model.Entities.LocationType.FileSystem;
    }
}
