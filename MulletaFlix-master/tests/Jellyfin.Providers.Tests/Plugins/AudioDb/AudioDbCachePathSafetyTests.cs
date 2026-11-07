using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.AudioDb;
using Moq;
using Xunit;

namespace MediaBrowser.Providers.Plugins.AudioDb;

public sealed class AudioDbCachePathSafetyTests
{
    private const string ValidMusicBrainzId = "0383dadf-2a4e-4d10-a46a-e9e041da8eb3";

    [Fact]
    public void CachePaths_UseCanonicalMusicBrainzUuid()
    {
        var appPaths = new Mock<IApplicationPaths>();
        appPaths.SetupGet(paths => paths.CachePath).Returns("cache");

        Assert.Equal(
            Path.Combine("cache", "audiodb-album", ValidMusicBrainzId, "album.json"),
            AudioDbAlbumProvider.GetAlbumInfoPath(appPaths.Object, ValidMusicBrainzId.ToUpperInvariant()));
        Assert.Equal(
            Path.Combine("cache", "audiodb-artist", ValidMusicBrainzId, "artist.json"),
            AudioDbArtistProvider.GetArtistInfoPath(appPaths.Object, ValidMusicBrainzId.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("../../outside")]
    [InlineData("..\\..\\outside")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("not-a-mbid")]
    public void CachePaths_RejectNonCanonicalOrPathLikeIds(string musicBrainzId)
    {
        var appPaths = Mock.Of<IApplicationPaths>();

        Assert.Throws<ArgumentException>(() => AudioDbAlbumProvider.GetAlbumInfoPath(appPaths, musicBrainzId));
        Assert.Throws<ArgumentException>(() => AudioDbArtistProvider.GetArtistInfoPath(appPaths, musicBrainzId));
    }

    [Fact]
    public async Task DownloadArtistInfo_RejectsInvalidIdBeforeHttpRequest()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var provider = new AudioDbArtistProvider(
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IFileSystem>(),
            httpClientFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.DownloadArtistInfo("../../outside", CancellationToken.None));

        httpClientFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DownloadAlbumInfo_RejectsInvalidIdBeforeHttpRequest()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var provider = new AudioDbAlbumProvider(
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IFileSystem>(),
            httpClientFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.DownloadInfo("../../outside", CancellationToken.None));

        httpClientFactory.VerifyNoOtherCalls();
    }
}
