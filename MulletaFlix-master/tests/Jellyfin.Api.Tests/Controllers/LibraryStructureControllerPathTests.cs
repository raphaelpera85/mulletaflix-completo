using System;
using System.IO;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LibraryStructureControllerPathTests
{
    [Theory]
    [InlineData("Movies")]
    [InlineData("Drama & Comedy")]
    public void IsSinglePathSegment_AllowsDirectoryNames(string name)
    {
        Assert.True(LibraryStructureController.IsSinglePathSegment(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/Movies")]
    [InlineData("nested\\Movies")]
    [InlineData("C:\\outside")]
    [InlineData("invalid\0path")]
    public void IsSinglePathSegment_RejectsInvalidNames(string name)
    {
        Assert.False(LibraryStructureController.IsSinglePathSegment(name));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsParentTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-library-root");
        var candidate = Path.Combine(root, "..", "outside");

        Assert.False(LibraryStructureController.IsPathWithinRoot(root, candidate));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsRootItself()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-library-root");

        Assert.False(LibraryStructureController.IsPathWithinRoot(root, root));
    }

    [Fact]
    public void IsPathWithinRoot_AllowsDescendant()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-library-root");
        var candidate = Path.Combine(root, "Movies");

        Assert.True(LibraryStructureController.IsPathWithinRoot(root, candidate));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsSiblingWithSharedPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-library-root");
        var candidate = root + "-outside";

        Assert.False(LibraryStructureController.IsPathWithinRoot(root, candidate));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsInvalidPathInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-library-root");

        Assert.False(LibraryStructureController.IsPathWithinRoot(root, "invalid\0path"));
    }

    [Fact]
    public void IsPathWithinRoot_RejectsDescendantReachedThroughSymbolicLink()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            var link = Path.Combine(root, "linked");
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
            }

            var candidate = Path.Combine(link, "media");
            Assert.StartsWith(
                Path.GetFullPath(root),
                Path.GetFullPath(candidate),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            Assert.False(LibraryStructureController.IsPathWithinRoot(root, candidate));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinRoot_RejectsRootThatIsASymbolicLink()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var rootLink = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            try
            {
                Directory.CreateSymbolicLink(rootLink, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
            }

            Assert.False(LibraryStructureController.IsPathWithinRoot(rootLink, Path.Combine(rootLink, "media")));
        }
        finally
        {
            if (Directory.Exists(rootLink))
            {
                Directory.Delete(rootLink);
            }

            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void IsPathWithinRoot_RejectsRootBelowSymbolicLinkAncestor()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var link = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
            }

            var root = Path.Combine(link, "library");
            Assert.False(LibraryStructureController.IsPathWithinRoot(root, Path.Combine(root, "media")));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(outside, recursive: true);
        }
    }
}
