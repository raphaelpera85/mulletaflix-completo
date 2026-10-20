using System;
using System.IO;
using MediaBrowser.Controller.IO;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class FileSystemHelperPathSafetyTests
{
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
