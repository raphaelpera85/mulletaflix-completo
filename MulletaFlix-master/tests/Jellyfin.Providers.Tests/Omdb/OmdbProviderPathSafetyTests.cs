using System;
using System.IO;
using System.Net.Http;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.Omdb;
using Moq;
using Xunit;

namespace MediaBrowser.Providers.Plugins.Omdb;

public sealed class OmdbProviderPathSafetyTests
{
    [Theory]
    [InlineData("1234567", "tt1234567")]
    [InlineData("tt1234567", "tt1234567")]
    [InlineData("TT1234567", "tt1234567")]
    public void CachePaths_NormalizeValidImdbIds(string input, string normalized)
    {
        var provider = CreateProvider();

        Assert.Equal(Path.Combine("cache", "omdb", normalized + ".json"), provider.GetDataFilePath(input));
        Assert.Equal(Path.Combine("cache", "omdb", normalized + "_season_2.json"), provider.GetSeasonFilePath(input, 2));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("tt123/../../outside")]
    [InlineData("tt123\\..\\outside")]
    [InlineData("tt123abc")]
    [InlineData("tt")]
    public void CachePaths_RejectInvalidOrPathLikeImdbIds(string input)
    {
        var provider = CreateProvider();

        Assert.Throws<ArgumentException>(() => provider.GetDataFilePath(input));
        Assert.Throws<ArgumentException>(() => provider.GetSeasonFilePath(input, 2));
    }

    private static OmdbProvider CreateProvider()
    {
        var appPaths = new Mock<IServerApplicationPaths>();
        appPaths.SetupGet(paths => paths.CachePath).Returns("cache");
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.ApplicationPaths).Returns(appPaths.Object);

        return new OmdbProvider(
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IFileSystem>(),
            configuration.Object);
    }
}
