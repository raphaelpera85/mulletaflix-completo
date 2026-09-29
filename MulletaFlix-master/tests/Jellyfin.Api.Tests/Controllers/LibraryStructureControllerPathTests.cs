using System.IO;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LibraryStructureControllerPathTests
{
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
}
