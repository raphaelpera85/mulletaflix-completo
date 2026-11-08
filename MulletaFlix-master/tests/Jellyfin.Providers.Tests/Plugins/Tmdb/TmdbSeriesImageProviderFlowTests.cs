using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Plugins.Tmdb;
using MediaBrowser.Providers.Plugins.Tmdb.TV;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.Tmdb;

[Collection("LibraryManagerTests")]
public sealed class TmdbSeriesImageProviderFlowTests : IDisposable
{
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly ILibraryManager? _previousLibraryManager;

    public TmdbSeriesImageProviderFlowTests()
    {
        _previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = _libraryManager.Object;
    }

    public void Dispose()
    {
        BaseItem.LibraryManager = _previousLibraryManager;
    }

    [Fact]
    public async Task GetImages_WithoutProviderId_ResolvesSeriesByOriginalTitleAndYear()
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var requestsTask = RespondToTmdbLocalizedTitleFallbackAsync(listener, cancellation.Token);
            var tmdbClient = new TMDbClient("fixture-key", false, $"http://127.0.0.1:{port}/3/", null, null);
            tmdbClient.SetConfig(new TMDbConfig
            {
                Images = new ConfigImageTypes
                {
                    BaseUrl = "https://images.fixture/",
                    SecureBaseUrl = "https://images.fixture/",
                    PosterSizes = ["original"]
                }
            });

            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var clientManager = new TmdbClientManager(cache, tmdbClient);
            var provider = new TmdbSeriesImageProvider(Mock.Of<IHttpClientFactory>(), clientManager);
            var series = new Series
            {
                Name = "Série Localizada",
                OriginalTitle = "Original Series",
                ProductionYear = 1995,
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };

            var result = (await provider.GetImages(series, cancellation.Token)).ToArray();
            var requests = await requestsTask;

            var image = Assert.Single(result);
            Assert.Equal("https://images.fixture/original/poster-pt.jpg", image.Url);
            Assert.Contains(requests, request => request.Contains("Série Localizada", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(requests, request => request.Contains("Original Series", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(requests, request => request.Contains("/tv/42/images", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    [Fact]
    public async Task GetSeasonImages_WithoutSeriesProviderId_ResolvesParentSeriesByOriginalTitle()
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            var series = CreateSeries();
            var season = new Season
            {
                Id = Guid.NewGuid(),
                SeriesId = series.Id,
                IndexNumber = 1,
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            _libraryManager.Setup(manager => manager.GetItemById(series.Id)).Returns(series);

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var requestsTask = RespondToTmdbLocalizedTitleFallbackAsync(listener, cancellation.Token);
            using var client = new TMDbClient("fixture-key", false, $"http://127.0.0.1:{port}/3/", null, null);
            client.SetConfig(CreateImageConfig());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var manager = new TmdbClientManager(cache, client);
            var provider = new TmdbSeasonImageProvider(Mock.Of<IHttpClientFactory>(), manager);

            var result = (await provider.GetImages(season, cancellation.Token)).ToArray();
            var requests = await requestsTask;

            var image = Assert.Single(result);
            Assert.Equal("https://images.fixture/original/poster-season.jpg", image.Url);
            Assert.Contains(requests, request => request.Contains("/tv/42/season/1/images", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    [Fact]
    public async Task GetEpisodeImages_WithoutSeriesProviderId_ResolvesParentSeriesByOriginalTitle()
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            var series = CreateSeries();
            var episode = new Episode
            {
                Id = Guid.NewGuid(),
                SeriesId = series.Id,
                ParentIndexNumber = 1,
                IndexNumber = 1,
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            _libraryManager.Setup(manager => manager.GetItemById(series.Id)).Returns(series);

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var requestsTask = RespondToTmdbLocalizedTitleFallbackAsync(listener, cancellation.Token);
            using var client = new TMDbClient("fixture-key", false, $"http://127.0.0.1:{port}/3/", null, null);
            client.SetConfig(CreateImageConfig());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var manager = new TmdbClientManager(cache, client);
            var provider = new TmdbEpisodeImageProvider(Mock.Of<IHttpClientFactory>(), manager);

            var result = (await provider.GetImages(episode, cancellation.Token)).ToArray();
            var requests = await requestsTask;

            var image = Assert.Single(result);
            Assert.Equal("https://images.fixture/original/still-pt.jpg", image.Url);
            Assert.Contains(requests, request => request.Contains("/tv/42/season/1/episode/1", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    private static Series CreateSeries() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Série Localizada",
        OriginalTitle = "Original Series",
        ProductionYear = 1995,
        PreferredMetadataLanguage = "pt-BR",
        PreferredMetadataCountryCode = "BR"
    };

    private static TMDbConfig CreateImageConfig() => new()
    {
        Images = new ConfigImageTypes
        {
            BaseUrl = "https://images.fixture/",
            SecureBaseUrl = "https://images.fixture/",
            PosterSizes = ["original"]
        }
    };

    private static Plugin? EnsureTmdbPluginInitialized()
    {
        var previousPlugin = Plugin.Instance;
        var paths = new Mock<IApplicationPaths>();
        paths.Setup(value => value.PluginsPath).Returns(Path.GetTempPath());
        paths.Setup(value => value.PluginConfigurationsPath).Returns(Path.GetTempPath());
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
            .Returns(new PluginConfiguration());
        var plugin = new Plugin(paths.Object, serializer.Object);
        plugin.Configuration.RateLimitDelayMs = 0;
        plugin.Configuration.PosterSize = "original";
        return previousPlugin;
    }

    private static void RestoreTmdbPlugin(Plugin? previousPlugin)
    {
        var property = typeof(Plugin).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!;
        property.GetSetMethod(nonPublic: true)!.Invoke(null, [previousPlugin]);
    }

    private static async Task<IReadOnlyList<string>> RespondToTmdbLocalizedTitleFallbackAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var requests = new List<string>();
        while (requests.Count < 8)
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            requests.Add(Uri.UnescapeDataString(requestLine).Replace('+', ' '));
            var decodedRequest = requests[^1];
            var body = decodedRequest.Contains("/tv/42/season/1/episode/1", StringComparison.OrdinalIgnoreCase)
                ? "{\"id\":1,\"images\":{\"stills\":[{\"aspect_ratio\":1.78,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/still-pt.jpg\",\"vote_average\":7.0,\"vote_count\":1}]}}"
                : decodedRequest.Contains("/tv/42/season/1/images", StringComparison.OrdinalIgnoreCase)
                    ? "{\"id\":1,\"posters\":[{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/poster-season.jpg\",\"vote_average\":7.0,\"vote_count\":1}]}"
                    : decodedRequest.Contains("/tv/42/images", StringComparison.OrdinalIgnoreCase)
                        ? "{\"id\":42,\"posters\":[{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/poster-pt.jpg\",\"vote_average\":7.0,\"vote_count\":1}],\"backdrops\":[],\"logos\":[]}"
                        : decodedRequest.Contains("Original Series", StringComparison.OrdinalIgnoreCase)
                            ? "{\"page\":1,\"results\":[{\"id\":42,\"name\":\"Original Series\",\"original_name\":\"Original Series\",\"first_air_date\":\"1995-11-20\",\"poster_path\":\"/poster-pt.jpg\"}],\"total_pages\":1,\"total_results\":1}"
                            : "{\"page\":1,\"results\":[],\"total_pages\":1,\"total_results\":0}";
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(response, cancellationToken);

            if (decodedRequest.Contains("/tv/42/images", StringComparison.OrdinalIgnoreCase)
                || decodedRequest.Contains("/tv/42/season/1/episode/1", StringComparison.OrdinalIgnoreCase)
                || (decodedRequest.Contains("/tv/42/season/1/images", StringComparison.OrdinalIgnoreCase)
                    && !decodedRequest.Contains("/episode/", StringComparison.OrdinalIgnoreCase)))
            {
                return requests;
            }
        }

        throw new InvalidOperationException(string.Join(" | ", requests));
    }
}
