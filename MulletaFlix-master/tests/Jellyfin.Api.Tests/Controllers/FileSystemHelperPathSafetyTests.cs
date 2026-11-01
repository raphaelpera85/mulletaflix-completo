using System;
using System.IO;
using MediaBrowser.Controller.IO;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class FileSystemHelperPathSafetyTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("C:\\outside")]
    public void TryGetChildPath_RejectsTraversalAndRootedNames(string childName)
    {
        Assert.False(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), childName, out _));
    }

    [Fact]
    public void TryGetChildPath_ReturnsDirectChildPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-child-path-" + Guid.NewGuid().ToString("N"));

        Assert.True(FileSystemHelper.TryGetChildPath(root, "valid.user", out var childPath));
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "valid.user"), childPath);
    }

    [Fact]
    public void TryGetChildPath_RejectsExistingSymlinkChild()
    {
        var root = Directory.CreateTempSubdirectory("mfx-child-link-").FullName;
        var outside = Directory.CreateTempSubdirectory("mfx-child-outside-").FullName;
        var link = Path.Combine(root, "linked-user");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
            }

            Assert.False(FileSystemHelper.TryGetChildPath(root, "linked-user", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void TryGetChildPath_RejectsTrailingPeriodOnWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.False(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), "username.", out _));
        }
        else
        {
            Assert.True(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), "username.", out _));
        }
    }

    [Fact]
    public void TryGetChildPath_RejectsTrailingSpaceOnWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.False(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), "username ", out _));
        }
        else
        {
            Assert.True(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), "username ", out _));
        }
    }

    [Theory]
    [InlineData("NUL")]
    [InlineData("NUL.txt")]
    [InlineData(" NUL.txt")]
    [InlineData("CONIN$")]
    [InlineData("COM1")]
    [InlineData("COM¹")]
    [InlineData("LPT³.log")]
    public void TryGetChildPath_RejectsWindowsDeviceNames(string childName)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.False(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), childName, out _));
        }
        else
        {
            Assert.True(FileSystemHelper.TryGetChildPath(Path.GetTempPath(), childName, out _));
        }
    }

    [Fact]
    public void IsPathWithinRoot_AcceptsRegularDescendantAndRejectsSiblingPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-path-safety-" + Guid.NewGuid().ToString("N"));
        var descendant = Path.Combine(root, "users", "avatar.png");
        var sibling = root + "-outside";

        Assert.True(FileSystemHelper.IsPathWithinRoot(descendant, root));
        Assert.False(FileSystemHelper.IsPathWithinRoot(Path.Combine(sibling, "avatar.png"), root));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsSymlinkedParentThatEscapesRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-path-safety-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "mfx-path-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "avatar.png");
        File.WriteAllBytes(outsideFile, [1, 2, 3]);

        try
        {
            var link = Path.Combine(root, "linked-user-dir");
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
            }

            Assert.False(FileSystemHelper.IsPathWithinRoot(Path.Combine(link, "avatar.png"), root));
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void PathsEqual_UsesHostPathCaseSemantics()
    {
        var path = Path.Combine(Path.GetTempPath(), "mfx-profile", "profile.png");
        var differentlyCasedPath = path.ToUpperInvariant();

        Assert.Equal(OperatingSystem.IsWindows(), FileSystemHelper.PathsEqual(path, differentlyCasedPath));
    }
}
