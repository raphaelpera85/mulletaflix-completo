using System;
using System.IO;
using Jellyfin.Server.Implementations.Nebula;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaPathSafetyTests
{
    [Fact]
    public void IsPathWithinRoot_AllowsContainedAndNotYetCreatedDestinations()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Assert.True(NebulaPathSafety.IsPathWithinRoot(root, root));
            Assert.True(NebulaPathSafety.IsPathWithinRoot(Path.Combine(root, "nested", "poster.jpg"), root));
            Assert.False(NebulaPathSafety.IsPathWithinRoot(Path.Combine(root, "..", "outside.txt"), root));
            Assert.False(NebulaPathSafety.IsPathWithinRoot(root + "-backup", root));
            Assert.False(NebulaPathSafety.IsPathWithinRoot("bad\0path", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinRoot_RejectsExistingSymlinkInCandidatePath()
    {
        var parent = CreateTemporaryDirectory();
        var root = Path.Combine(parent, "root");
        var outside = Path.Combine(parent, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            var link = Path.Combine(root, "linked");
            Directory.CreateSymbolicLink(link, outside);
            var escaped = Path.Combine(link, "metadata.nfo");

            Assert.False(NebulaPathSafety.IsPathWithinRoot(escaped, root));
            Assert.False(NebulaDownloaderEngine.IsPathWithinRoot(escaped, root));
            Assert.False(NebulaMongoContext.IsPathWithinRoot(escaped, root));
            Assert.False(NebulaMetadataExportService.IsPathWithinRoot(escaped, root));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinRoot_RejectsSymlinkUsedAsConfiguredRoot()
    {
        var parent = CreateTemporaryDirectory();
        var actualRoot = Path.Combine(parent, "actual");
        var linkedRoot = Path.Combine(parent, "linked-root");
        Directory.CreateDirectory(actualRoot);

        try
        {
            Directory.CreateSymbolicLink(linkedRoot, actualRoot);

            Assert.False(NebulaPathSafety.IsPathWithinRoot(Path.Combine(linkedRoot, "file.mkv"), linkedRoot));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinRoot_RejectsDanglingSymlink()
    {
        var parent = CreateTemporaryDirectory();
        var root = Path.Combine(parent, "root");
        Directory.CreateDirectory(root);

        try
        {
            var danglingLink = Path.Combine(root, "dangling");
            Directory.CreateSymbolicLink(danglingLink, Path.Combine(parent, "missing-target"));

            Assert.False(NebulaPathSafety.IsPathWithinRoot(Path.Combine(danglingLink, "file.mkv"), root));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nebula-path-safety-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
