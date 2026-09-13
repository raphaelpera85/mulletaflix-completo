using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.AudioDb;
using MediaBrowser.Providers.Plugins.MusicBrainz;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.Music;

public sealed class MusicImageResponseTests
{
    [Theory]
    [InlineData("AudioDbArtist")]
    [InlineData("AudioDbAlbum")]
    [InlineData("MusicBrainzArtist")]
    [InlineData("MusicBrainzAlbum")]
    public async Task GetImageResponse_UsesDefaultHttpClient(string provider)
    {
        using var client = new HttpClient(new StubHandler());
        var factory = new StubHttpClientFactory(client);
        var response = provider switch
        {
            "AudioDbArtist" => await new AudioDbArtistProvider(
                Mock.Of<IServerConfigurationManager>(),
                Mock.Of<IFileSystem>(),
                factory).GetImageResponse("https://example.test/image.jpg", CancellationToken.None),
            "AudioDbAlbum" => await new AudioDbAlbumProvider(
                Mock.Of<IServerConfigurationManager>(),
                Mock.Of<IFileSystem>(),
                factory).GetImageResponse("https://example.test/image.jpg", CancellationToken.None),
            "MusicBrainzArtist" => await new MusicBrainzArtistProvider(factory)
                .GetImageResponse("https://example.test/image.jpg", CancellationToken.None),
            "MusicBrainzAlbum" => await new MusicBrainzAlbumProvider(factory)
                .GetImageResponse("https://example.test/image.jpg", CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };

        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
