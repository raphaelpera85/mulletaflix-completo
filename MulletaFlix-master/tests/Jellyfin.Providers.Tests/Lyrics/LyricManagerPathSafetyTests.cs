using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Lyrics;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Lyrics;
using MediaBrowser.Providers.Lyric;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Lyrics;

public sealed partial class LyricManagerPathSafetyTests
{
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateWindowsHardLink(string fileName, string existingFileName, nint securityAttributes);

    [DllImport("libc", EntryPoint = "link", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int CreateUnixHardLink(string existingFileName, string fileName);

    [Theory]
    [InlineData("lrc")]
    [InlineData("elrc")]
    [InlineData("txt")]
    [InlineData("custom_v2")]
    [InlineData("x-lrc")]
    [InlineData("LRC")]
    public void IsSafeLyricFormat_AcceptsExtensionTokens(string format)
    {
        Assert.True(LyricManager.IsSafeLyricFormat(format));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("lrc.txt")]
    [InlineData("lrc/../../outside.txt")]
    [InlineData("lrc\\..\\..\\outside.txt")]
    [InlineData("lrc:stream")]
    [InlineData("lrc\n")]
    [InlineData("lrcé")]
    public void IsSafeLyricFormat_RejectsPathAndControlCharacters(string format)
    {
        Assert.False(LyricManager.IsSafeLyricFormat(format));
    }

    [Fact]
    public void IsPathWithinDirectory_AcceptsDescendantFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "MulletFlix-Lyrics");
        var candidate = Path.Combine(root, "track.lrc");

        Assert.True(LyricManager.IsPathWithinDirectory(root, candidate));
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsRootItself()
    {
        var root = Path.Combine(Path.GetTempPath(), "MulletFlix-Lyrics");

        Assert.False(LyricManager.IsPathWithinDirectory(root, root));
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsParentTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "MulletFlix-Lyrics");
        var candidate = Path.Combine(root, "..", "outside", "track.lrc");

        Assert.False(LyricManager.IsPathWithinDirectory(root, candidate));
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsSiblingWithSharedPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "MulletFlix-Lyrics");
        var sibling = root + "-outside";
        var candidate = Path.Combine(sibling, "track.lrc");

        Assert.False(LyricManager.IsPathWithinDirectory(root, candidate));
    }

    [Fact]
    public void GetSafeLyricSavePaths_ReturnsEmptyWhenNoSafeRootExists()
    {
        Assert.Empty(LyricManager.GetSafeLyricSavePaths(string.Empty, string.Empty, "track.lrc", true));
    }

    [Fact]
    public void GetSafeLyricSavePaths_UsesMetadataRootWhenMediaRootIsUnavailable()
    {
        using var temp = new TemporaryDirectory();

        var savePaths = LyricManager.GetSafeLyricSavePaths(string.Empty, temp.Path, "track.lrc", true);

        Assert.Equal([Path.Combine(temp.Path, "track.lrc")], savePaths);
    }

    [Fact]
    public async Task TrySaveLyric_ReturnsFalseAndDisposesStreamWhenNoSafeDestinationExists()
    {
        var manager = new LyricManager(
            NullLogger<LyricManager>.Instance,
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryMonitor>(),
            Mock.Of<IMediaSourceManager>(),
            [],
            []);
        var audio = new TestAudio { Path = string.Empty };
        var lyrics = new MemoryStream(Encoding.UTF8.GetBytes("[00:01.00]Hello"));

        var saved = await manager.TrySaveLyric(audio, new LibraryOptions { SaveLyricsWithMedia = true }, "lrc", lyrics);

        Assert.False(saved);
        Assert.False(lyrics.CanRead);
    }

    [Fact]
    public async Task DownloadLyrics_ThrowsAndReportsFailureWhenNoSafeDestinationExists()
    {
        const string providerName = "test-provider";
        var provider = new Mock<ILyricProvider>();
        provider.SetupGet(p => p.Name).Returns(providerName);
        provider.Setup(p => p.GetLyricsAsync("remote-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LyricResponse
            {
                Format = "lrc",
                Stream = new MemoryStream(Encoding.UTF8.GetBytes("[00:01.00]Hello"))
            });
        var manager = new LyricManager(
            NullLogger<LyricManager>.Instance,
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryMonitor>(),
            Mock.Of<IMediaSourceManager>(),
            [provider.Object],
            [new LrcLyricParser()]);
        var failures = new List<LyricDownloadFailureEventArgs>();
        manager.LyricDownloadFailure += (_, args) => failures.Add(args);
        var audio = new TestAudio { Path = string.Empty };
        var lyricId = $"{providerName.ToLowerInvariant().GetMD5():N}_remote-id";

        var exception = await Assert.ThrowsAsync<IOException>(() => manager.DownloadLyricsAsync(
            audio,
            new LibraryOptions { SaveLyricsWithMedia = true },
            lyricId,
            CancellationToken.None));

        Assert.Contains("safe destination", exception.Message, StringComparison.Ordinal);
        var failure = Assert.Single(failures);
        Assert.Same(audio, failure.Item);
        Assert.Same(exception, failure.Exception);
        Assert.Equal(providerName, failure.Provider);
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsSymbolicLinkToOutsideFile()
    {
        using var temp = new TemporaryDirectory();
        var root = Path.Combine(temp.Path, "library");
        Directory.CreateDirectory(root);
        var outsideFile = Path.Combine(temp.Path, "outside.lrc");
        File.WriteAllText(outsideFile, "outside");
        var linkPath = Path.Combine(root, "track.lrc");

        try
        {
            File.CreateSymbolicLink(linkPath, outsideFile);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
        }

        Assert.False(LyricManager.IsPathWithinDirectory(root, linkPath));
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsDanglingSymbolicLink()
    {
        using var temp = new TemporaryDirectory();
        var root = Path.Combine(temp.Path, "library");
        Directory.CreateDirectory(root);
        var linkPath = Path.Combine(root, "track.lrc");

        try
        {
            File.CreateSymbolicLink(linkPath, Path.Combine(temp.Path, "not-created.lrc"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
        }

        Assert.False(LyricManager.IsPathWithinDirectory(root, linkPath));
    }

    [Fact]
    public void IsPathWithinDirectory_RejectsSymbolicLinkAsConfiguredRoot()
    {
        using var temp = new TemporaryDirectory();
        var target = Path.Combine(temp.Path, "target");
        var root = Path.Combine(temp.Path, "library-link");
        Directory.CreateDirectory(target);

        try
        {
            Directory.CreateSymbolicLink(root, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
        }

        Assert.False(LyricManager.IsPathWithinDirectory(root, Path.Combine(root, "track.lrc")));
    }

    [Fact]
    public async Task TrySaveToFiles_ReplacesHardLinkWithoutTruncatingItsOtherTarget()
    {
        using var temp = new TemporaryDirectory();
        var originalPath = Path.Combine(temp.Path, "original.txt");
        var lyricPath = Path.Combine(temp.Path, "track.lrc");
        await File.WriteAllTextAsync(originalPath, "preserve-original-data", TestContext.Current.CancellationToken);
        var hardLinkCreated = OperatingSystem.IsWindows()
            ? CreateWindowsHardLink(lyricPath, originalPath, 0)
            : CreateUnixHardLink(originalPath, lyricPath) == 0;
        if (!hardLinkCreated)
        {
            Assert.Skip($"Hard links are unavailable in this environment. Native error: {Marshal.GetLastPInvokeError()}.");
        }

        var libraryMonitor = new Mock<ILibraryMonitor>();
        var manager = new LyricManager(
            NullLogger<LyricManager>.Instance,
            Mock.Of<IFileSystem>(),
            libraryMonitor.Object,
            Mock.Of<IMediaSourceManager>(),
            [],
            []);

        await using var lyrics = new MemoryStream(Encoding.UTF8.GetBytes("new lyrics"));
        await manager.TrySaveToFiles(lyrics, [lyricPath]);

        Assert.Equal("preserve-original-data", await File.ReadAllTextAsync(originalPath, TestContext.Current.CancellationToken));
        Assert.Equal("new lyrics", await File.ReadAllTextAsync(lyricPath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(temp.Path, ".track.lrc.*.tmp"));
    }

    [Fact]
    public async Task DeleteLyricsAsync_EmptyPathOrMissingFile_DoesNotDeleteOrThrow()
    {
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        var fileSystem = new Mock<IFileSystem>();
        var libraryMonitor = new Mock<ILibraryMonitor>();

        var testStreams = new MediaStream[]
        {
            new MediaStream { Path = string.Empty, Type = MediaStreamType.Lyric },
            new MediaStream { Path = "   ", Type = MediaStreamType.Lyric },
            new MediaStream { Path = "/non/existent/path.lrc", Type = MediaStreamType.Lyric },
        };

        mediaSourceManager
            .Setup(m => m.GetMediaStreams(It.IsAny<MediaStreamQuery>()))
            .Returns(testStreams);

        fileSystem
            .Setup(f => f.FileExists("/non/existent/path.lrc"))
            .Returns(false);

        var manager = new LyricManager(
            NullLogger<LyricManager>.Instance,
            fileSystem.Object,
            libraryMonitor.Object,
            mediaSourceManager.Object,
            [],
            []);

        var audio = new TestAudio();
        await manager.DeleteLyricsAsync(audio);

        fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("mulletaflix-lyrics-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class TestAudio : MediaBrowser.Controller.Entities.Audio.Audio
    {
        public override string GetInternalMetadataPath() => string.Empty;
    }
}
