using System;
using System.IO;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline.Api;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public class MidiaStorageOnlineManifestPathTests
{
    [Theory]
    [InlineData("Filmes/Example (2024)/Example.strm")]
    [InlineData("Series\\Example\\Season 01\\Episode.strm")]
    [InlineData("Series/..Season/Episode.strm")]
    public void TryGetConfinedManifestPath_AcceptsNestedRelativePaths(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-strm-root");

        var accepted = MidiaStorageOnlineController.TryGetConfinedManifestPath(root, relativePath, out var fullPath);

        Assert.True(accepted);
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.strm")]
    [InlineData("Filmes/../../outside.strm")]
    [InlineData("..\\..\\outside.strm")]
    [InlineData("Filmes\\..\\..\\outside.strm")]
    [InlineData("/tmp/outside.strm")]
    [InlineData("C:\\outside.strm")]
    [InlineData("C:outside.strm")]
    [InlineData("\\\\server\\share\\outside.strm")]
    [InlineData("Filmes/./Example.strm")]
    public void TryGetConfinedManifestPath_RejectsRootedAndTraversalPaths(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-strm-root");

        var accepted = MidiaStorageOnlineController.TryGetConfinedManifestPath(root, relativePath, out var fullPath);

        Assert.False(accepted);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void TryGetConfinedManifestPath_RejectsEmptyInputs()
    {
        Assert.False(MidiaStorageOnlineController.TryGetConfinedManifestPath(string.Empty, "Film.strm", out _));
        Assert.False(MidiaStorageOnlineController.TryGetConfinedManifestPath("root", " ", out _));
    }
}
