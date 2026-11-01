using System;
using System.IO;
using System.Security;
using MediaBrowser.Controller.IO;
using Xunit;

namespace Jellyfin.Controller.Tests.IO;

public sealed class RootedFileSystemTests
{
    [Fact]
    public void TryDeleteFile_DeletesDescendant()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Safe rooted deletion is enabled only on Windows.");

        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(root, "item", "season")).FullName;
        var file = Path.Combine(nested, "poster.jpg");
        File.WriteAllText(file, "poster");

        Assert.True(RootedFileSystem.TryDeleteFile(root, file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void TryDeleteFile_RejectsSiblingWithRootPrefix()
    {
        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata-backup")).FullName;
        var file = Path.Combine(sibling, "poster.jpg");
        File.WriteAllText(file, "preserve");

        Assert.False(RootedFileSystem.TryDeleteFile(root, file));
        Assert.Equal("preserve", File.ReadAllText(file));
    }

    [Fact]
    public void TryDeleteFile_RejectsTraversal()
    {
        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var outside = Path.Combine(temporary.Path, "poster.jpg");
        File.WriteAllText(outside, "preserve");
        var traversal = Path.Combine(root, "..", "poster.jpg");

        Assert.False(RootedFileSystem.TryDeleteFile(root, traversal));
        Assert.Equal("preserve", File.ReadAllText(outside));
    }

    [Fact]
    public void TryDeleteFile_RejectsWindowsDeviceNameComponents()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows device names are reserved only on Windows.");

        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var candidate = Path.Combine(root, " NUL.txt", "poster.jpg");

        Assert.False(RootedFileSystem.TryDeleteFile(root, candidate));
    }

    [Fact]
    public void TryDeleteFile_FollowsConfiguredRootLinkButNotDescendantLink()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Safe rooted deletion is enabled only on Windows.");

        using var temporary = new TemporaryDirectory();
        var targetRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "target")).FullName;
        var linkedRoot = Path.Combine(temporary.Path, "configured-root");
        CreateDirectoryLink(linkedRoot, targetRoot);
        var rootFile = Path.Combine(targetRoot, "root-image.jpg");
        File.WriteAllText(rootFile, "delete");

        Assert.True(RootedFileSystem.TryDeleteFile(linkedRoot, Path.Combine(linkedRoot, "root-image.jpg")));
        Assert.False(File.Exists(rootFile));

        var outsideRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "outside")).FullName;
        var outsideFile = Path.Combine(outsideRoot, "outside.jpg");
        File.WriteAllText(outsideFile, "preserve");
        var fileLink = Path.Combine(targetRoot, "linked-file.jpg");
        CreateFileLink(fileLink, outsideFile);

        Assert.True(RootedFileSystem.TryDeleteFile(linkedRoot, Path.Combine(linkedRoot, "linked-file.jpg")));
        Assert.False(File.Exists(fileLink));
        Assert.Equal("preserve", File.ReadAllText(outsideFile));

        var descendantLink = Path.Combine(targetRoot, "linked-folder");
        CreateDirectoryLink(descendantLink, outsideRoot);

        Assert.False(RootedFileSystem.TryDeleteFile(linkedRoot, Path.Combine(linkedRoot, "linked-folder", "outside.jpg")));
        Assert.Equal("preserve", File.ReadAllText(outsideFile));
    }

    [Fact]
    public void TryDeleteFile_OnNonWindowsFailsClosed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "This test covers non-Windows fail-closed behavior.");

        using var temporary = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var file = Path.Combine(root, "poster.jpg");
        File.WriteAllText(file, "preserve");

        Assert.Throws<PlatformNotSupportedException>(() => RootedFileSystem.TryDeleteFile(root, file));
        Assert.Equal("preserve", File.ReadAllText(file));
    }

    private static void CreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException or SecurityException)
        {
            Assert.Skip($"Directory symbolic links are unavailable in this environment: {exception.GetType().Name}.");
        }
    }

    private static void CreateFileLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException or SecurityException)
        {
            Assert.Skip($"File symbolic links are unavailable in this environment: {exception.GetType().Name}.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory().FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
