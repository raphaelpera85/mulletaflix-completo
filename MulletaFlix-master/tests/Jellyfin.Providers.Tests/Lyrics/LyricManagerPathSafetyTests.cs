using System;
using System.IO;
using MediaBrowser.Providers.Lyric;
using Xunit;

namespace MulletaFlix.Providers.Tests.Lyrics;

public sealed class LyricManagerPathSafetyTests
{
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
}
