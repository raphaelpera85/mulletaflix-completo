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
    public void GetUserConfigurationDirectoryPath_RejectsUnsafeUsername(string username)
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-users-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => FileSystemHelper.GetUserConfigurationDirectoryPath(root, username));
    }

    [Fact]
    public void GetUserConfigurationDirectoryPath_ResolvesSafeUsernameInsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-users-" + Guid.NewGuid().ToString("N"));

        var path = FileSystemHelper.GetUserConfigurationDirectoryPath(root, "raphael.pera");

        Assert.True(FileSystemHelper.IsPathWithinRoot(path, root));
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "raphael.pera")), path);
    }

    [Fact]
    public void GetUserConfigurationDirectoryPath_RejectsWindowsTrailingDotAliases()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "mfx-users-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => FileSystemHelper.GetUserConfigurationDirectoryPath(root, ".. ."));
        Assert.Throws<ArgumentException>(() => FileSystemHelper.GetUserConfigurationDirectoryPath(root, "profile."));
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
            Assert.Throws<ArgumentException>(() => FileSystemHelper.GetUserConfigurationDirectoryPath(root, "linked-user-dir"));
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
