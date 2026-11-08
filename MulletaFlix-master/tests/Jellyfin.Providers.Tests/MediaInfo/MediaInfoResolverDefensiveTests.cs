using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Providers.MediaInfo;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.MediaInfo;

[Collection("LibraryManagerTests")]
public class MediaInfoResolverDefensiveTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly AudioResolver _audioResolver;
    private readonly SubtitleResolver _subtitleResolver;

    public MediaInfoResolverDefensiveTests()
    {
        Video.RecordingsManager = Mock.Of<IRecordingsManager>();
        BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();

        _fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        var localizationManager = Mock.Of<ILocalizationManager>();
        var mediaEncoder = Mock.Of<IMediaEncoder>();

        _audioResolver = new AudioResolver(
            Mock.Of<ILogger<AudioResolver>>(),
            localizationManager,
            mediaEncoder,
            _fileSystem.Object,
            new NamingOptions());

        _subtitleResolver = new SubtitleResolver(
            Mock.Of<ILogger<SubtitleResolver>>(),
            localizationManager,
            mediaEncoder,
            _fileSystem.Object,
            new NamingOptions());
    }

    [Fact]
    public void GetExternalFiles_Audio_NonExistentFolder_ReturnsEmptyAndDoesNotCallDirectoryService()
    {
        var audio = new Audio
        {
            Path = "C:/Media/Music/Album/track.mp3"
        };

        var containingFolder = audio.ContainingFolderPath;
        _fileSystem.Setup(fs => fs.DirectoryExists(containingFolder)).Returns(false);

        var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);

        var result = _audioResolver.GetExternalFiles(audio, directoryService.Object, false);

        Assert.Empty(result);
        directoryService.Verify(ds => ds.GetFilePaths(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetExternalFiles_Audio_EmptyFolder_ReturnsEmptyWithoutThrowing()
    {
        var audio = new Audio
        {
            Path = "track.mp3" // containing folder is empty/null
        };

        _fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
        var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);

        var result = _audioResolver.GetExternalFiles(audio, directoryService.Object, false);

        Assert.Empty(result);
        directoryService.Verify(ds => ds.GetFilePaths(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetExternalFiles_Video_EmptyFolder_ReturnsEmptyWithoutThrowing()
    {
        var video = new Movie
        {
            Path = "movie.mkv"
        };

        _fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
        var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);

        var result = _subtitleResolver.GetExternalFiles(video, directoryService.Object, false);

        Assert.Empty(result);
        directoryService.Verify(ds => ds.GetFilePaths(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetExternalFiles_Video_EmptyFileNameWithoutExtension_ReturnsEmptyWithoutMatchingFiles()
    {
        var video = new Movie
        {
            Path = "C:/Media/Movies/.mkv" // Empty FileNameWithoutExtension
        };

        var folder = video.ContainingFolderPath;
        _fileSystem.Setup(fs => fs.DirectoryExists(folder)).Returns(true);
        _fileSystem.Setup(fs => fs.DirectoryExists(It.IsRegex("library.*"))).Returns(false);

        var directoryService = new Mock<IDirectoryService>();
        directoryService.Setup(ds => ds.GetFilePaths(folder, false))
            .Returns(new List<string> { "C:/Media/Movies/-flag.srt", "C:/Media/Movies/other.srt" });

        var result = _subtitleResolver.GetExternalFiles(video, directoryService.Object, false);

        Assert.Empty(result);
    }

    [Fact]
    public void GetExternalFiles_Audio_EmptyFileNameWithoutExtension_ReturnsEmptyWithoutMatchingFiles()
    {
        var audio = new Audio
        {
            Path = "C:/Media/Music/.mp3" // Empty FileNameWithoutExtension
        };

        var folder = audio.ContainingFolderPath;
        _fileSystem.Setup(fs => fs.DirectoryExists(folder)).Returns(true);
        _fileSystem.Setup(fs => fs.DirectoryExists(It.IsRegex("library.*"))).Returns(false);

        var directoryService = new Mock<IDirectoryService>();
        directoryService.Setup(ds => ds.GetFilePaths(folder, false))
            .Returns(new List<string> { "C:/Media/Music/-flag.lrc", "C:/Media/Music/other.lrc" });

        var result = _audioResolver.GetExternalFiles(audio, directoryService.Object, false);

        Assert.Empty(result);
    }

    [Fact]
    public void GetInternalMetadataPath_NullConfigurationManager_DoesNotThrow()
    {
        BaseItem.ConfigurationManager = null;

        var audio = new Audio();
        var path = audio.GetInternalMetadataPath();

        Assert.NotNull(path);
        Assert.Contains("library", path, StringComparison.Ordinal);
    }
}
