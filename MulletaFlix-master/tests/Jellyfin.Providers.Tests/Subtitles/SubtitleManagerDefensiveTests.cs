using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.Subtitles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Subtitles;

[Collection("LibraryManagerTests")]
public sealed class SubtitleManagerDefensiveTests : IDisposable
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<ILibraryMonitor> _monitor;
    private readonly Mock<IMediaSourceManager> _mediaSourceManager;
    private readonly Mock<ILocalizationManager> _localizationManager;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly NamingOptions _namingOptions;

    private readonly ILibraryManager _previousLibraryManager;
    private readonly IServerConfigurationManager? _previousConfigurationManager;
    private readonly IMediaSourceManager? _previousMediaSourceManager;

    public SubtitleManagerDefensiveTests()
    {
        _previousLibraryManager = BaseItem.LibraryManager;
        _previousConfigurationManager = BaseItem.ConfigurationManager;
        _previousMediaSourceManager = BaseItem.MediaSourceManager;

        Video.RecordingsManager = Mock.Of<IRecordingsManager>();
        BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();

        _fileSystem = new Mock<IFileSystem>();
        _monitor = new Mock<ILibraryMonitor>(MockBehavior.Strict);
        _mediaSourceManager = new Mock<IMediaSourceManager>();
        _localizationManager = new Mock<ILocalizationManager>();
        _libraryManager = new Mock<ILibraryManager>();
        _libraryManager.Setup(lm => lm.GetCollectionFolders(It.IsAny<BaseItem>())).Returns(new List<Folder>());

        _namingOptions = new NamingOptions();

        BaseItem.LibraryManager = _libraryManager.Object;
        var serverConfig = new Mock<IServerConfigurationManager>();
        serverConfig.Setup(c => c.ApplicationPaths).Returns(Mock.Of<IServerApplicationPaths>());
        BaseItem.ConfigurationManager = serverConfig.Object;
    }

    public void Dispose()
    {
        BaseItem.LibraryManager = _previousLibraryManager;
        BaseItem.ConfigurationManager = _previousConfigurationManager;
        BaseItem.MediaSourceManager = _previousMediaSourceManager;
        GC.SuppressFinalize(this);
    }

    private SubtitleManager CreateManager()
    {
        return new SubtitleManager(
            NullLogger<SubtitleManager>.Instance,
            _fileSystem.Object,
            _monitor.Object,
            _mediaSourceManager.Object,
            _localizationManager.Object,
            Array.Empty<ISubtitleProvider>(),
            _namingOptions);
    }

    [Fact]
    public async Task DeleteSubtitles_StreamNotFound_DoesNotThrowIndexOutOfRange()
    {
        var manager = CreateManager();
        var video = new Movie { Id = Guid.NewGuid(), Path = "C:/Media/movie.mkv" };

        _mediaSourceManager
            .Setup(m => m.GetMediaStreams(It.IsAny<MediaStreamQuery>()))
            .Returns(new List<MediaStream>());

        // Must not throw IndexOutOfRangeException or ArgumentOutOfRangeException
        await manager.DeleteSubtitles(video, 42);

        _monitor.Verify(m => m.ReportFileSystemChangeBeginning(It.IsAny<string>()), Times.Never);
        _monitor.Verify(m => m.ReportFileSystemChangeComplete(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSubtitles_EmptyStreamPath_DoesNotCallMonitorOrDelete()
    {
        var manager = CreateManager();
        var video = new Movie { Id = Guid.NewGuid(), Path = "C:/Media/movie.mkv" };

        var stream = new MediaStream
        {
            Index = 2,
            Path = string.Empty,
            Type = MediaStreamType.Subtitle
        };

        _mediaSourceManager
            .Setup(m => m.GetMediaStreams(It.IsAny<MediaStreamQuery>()))
            .Returns(new List<MediaStream> { stream });

        await manager.DeleteSubtitles(video, 2);

        _monitor.Verify(m => m.ReportFileSystemChangeBeginning(It.IsAny<string>()), Times.Never);
        _fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadSubtitle_NullLanguage_DoesNotThrowNullReferenceException()
    {
        var manager = CreateManager();
        var video = new Movie
        {
            Id = Guid.NewGuid(),
            Path = "C:/Media/Movies/MyMovie.mkv"
        };

        var libraryOptions = new LibraryOptions
        {
            SaveSubtitlesWithMedia = false
        };

        _libraryManager.Setup(lm => lm.GetLibraryOptions(video)).Returns(libraryOptions);

        using var ms = new MemoryStream("1\n00:00:01,000 --> 00:00:02,000\nTest\n"u8.ToArray());
        var response = new SubtitleResponse
        {
            Format = "srt",
            Language = null!, // Intentionally null from external provider or missing parameter
            Stream = ms
        };

        // Should not throw NullReferenceException
        var ex = await Record.ExceptionAsync(() => manager.UploadSubtitle(video, response));
        Assert.Null(ex);
    }
}
