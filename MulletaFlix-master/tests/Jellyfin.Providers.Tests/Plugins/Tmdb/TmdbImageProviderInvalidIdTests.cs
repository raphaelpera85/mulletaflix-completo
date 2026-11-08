using System;
using System.Collections.Generic;
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
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Plugins.Tmdb;
using MediaBrowser.Providers.Plugins.Tmdb.BoxSets;
using MediaBrowser.Providers.Plugins.Tmdb.Movies;
using MediaBrowser.Providers.Plugins.Tmdb.People;
using MediaBrowser.Providers.Plugins.Tmdb.TV;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.Tmdb;

[Collection("LibraryManagerTests")]
public sealed class TmdbImageProviderInvalidIdTests : IDisposable
{
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly ILibraryManager? _previousLibraryManager;

    public TmdbImageProviderInvalidIdTests()
    {
        _previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = _libraryManager.Object;
    }

    public void Dispose()
    {
        BaseItem.LibraryManager = _previousLibraryManager;
    }

    [Fact]
    public async Task SeriesWithInvalidTmdbIdReturnsEmpty()
    {
        var series = new Series
        {
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        series.SetProviderId(MetadataProvider.Tmdb, "not-a-number");

        var provider = new TmdbSeriesImageProvider(Mock.Of<IHttpClientFactory>(), null!);

        var result = await provider.GetImages(series, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SeasonWithInvalidSeriesTmdbIdReturnsEmpty()
    {
        var series = new Series
        {
            Id = Guid.NewGuid(),
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        series.SetProviderId(MetadataProvider.Tmdb, "not-a-number");
        var season = new Season
        {
            IndexNumber = 1,
            SeriesId = series.Id,
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        _libraryManager.Setup(manager => manager.GetItemById(series.Id)).Returns(series);

        var provider = new TmdbSeasonImageProvider(Mock.Of<IHttpClientFactory>(), null!);

        var result = await provider.GetImages(season, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task EpisodeWithInvalidSeriesTmdbIdReturnsEmpty()
    {
        var series = new Series
        {
            Id = Guid.NewGuid(),
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        series.SetProviderId(MetadataProvider.Tmdb, "not-a-number");
        var episode = new Episode
        {
            IndexNumber = 1,
            SeriesId = series.Id,
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        _libraryManager.Setup(manager => manager.GetItemById(series.Id)).Returns(series);

        var provider = new TmdbEpisodeImageProvider(Mock.Of<IHttpClientFactory>(), null!);

        var result = await provider.GetImages(episode, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BoxSetWithInvalidTmdbIdReturnsEmpty()
    {
        var boxSet = new BoxSet();
        boxSet.SetProviderId(MetadataProvider.Tmdb, "not-a-number");

        var provider = new TmdbBoxSetImageProvider(Mock.Of<IHttpClientFactory>(), null!);

        var result = await provider.GetImages(boxSet, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task PersonWithInvalidTmdbIdReturnsEmpty(string invalidId)
    {
        var person = new MediaBrowser.Controller.Entities.Person();
        person.SetProviderId(MetadataProvider.Tmdb, invalidId);

        var provider = new TmdbPersonImageProvider(Mock.Of<IHttpClientFactory>(), null!);

        var result = await provider.GetImages(person, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    public async Task SeasonMetadataWithInvalidSeriesTmdbIdReturnsNoMetadata(string invalidId)
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            var info = new SeasonInfo
            {
                IndexNumber = 1,
                SeriesProviderIds = new Dictionary<string, string>
                {
                    [MetadataProvider.Tmdb.ToString()] = invalidId
                }
            };

            var provider = new TmdbSeasonProvider(Mock.Of<IHttpClientFactory>(), null!);
            var result = await provider.GetMetadata(info, TestContext.Current.CancellationToken);

            Assert.False(result.HasMetadata);
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    public async Task EpisodeMetadataWithInvalidSeriesTmdbIdReturnsNoMetadata(string invalidId)
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            var info = new EpisodeInfo
            {
                IndexNumber = 1,
                ParentIndexNumber = 1,
                SeriesProviderIds = new Dictionary<string, string>
                {
                    [MetadataProvider.Tmdb.ToString()] = invalidId
                }
            };

            var provider = new TmdbEpisodeProvider(Mock.Of<IHttpClientFactory>(), null!);
            var result = await provider.GetMetadata(info, TestContext.Current.CancellationToken);

            Assert.False(result.HasMetadata);
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    [Fact]
    public async Task MovieGetSearchResults_ImdbLookupUsesMetadataLanguageAndFallsBackWhenFindResultsEmpty()
    {
        var previousPlugin = EnsureTmdbPluginInitialized();
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var requestsTask = RespondToFindAndSearchRequestsAsync(listener, cancellation.Token);

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
            _libraryManager
                .Setup(manager => manager.ParseName(It.IsAny<string>()))
                .Returns((string name) => new ItemLookupInfo { Name = name });

            var provider = new TmdbMovieProvider(_libraryManager.Object, clientManager, Mock.Of<IHttpClientFactory>());
            var searchInfo = new MovieInfo
            {
                Name = "Cidade de Deus",
                MetadataLanguage = "pt-BR",
                MetadataCountryCode = "BR"
            };
            searchInfo.SetProviderId(MetadataProvider.Tmdb, "not-a-number");
            searchInfo.SetProviderId(MetadataProvider.Imdb, "tt0317248");

            var results = await provider.GetSearchResults(searchInfo, cancellation.Token);
            var requests = await requestsTask;

            var match = Assert.Single(results);
            Assert.Equal("Cidade de Deus", match.Name);
            Assert.Equal("598", match.GetProviderId(MetadataProvider.Tmdb));
            Assert.Equal(2, requests.Count);
            Assert.Contains("language=pt-BR", Uri.UnescapeDataString(requests[0]), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("null", Uri.UnescapeDataString(requests[0]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            RestoreTmdbPlugin(previousPlugin);
        }
    }

    private static Plugin? EnsureTmdbPluginInitialized()
    {
        var previousPlugin = Plugin.Instance;
        var paths = new Mock<IApplicationPaths>();
        paths.Setup(value => value.PluginsPath).Returns(System.IO.Path.GetTempPath());
        paths.Setup(value => value.PluginConfigurationsPath).Returns(System.IO.Path.GetTempPath());
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

    private static async Task<IReadOnlyList<string>> RespondToFindAndSearchRequestsAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var requests = new List<string>();
        var payloads = new[]
        {
            "{\"movie_results\":[],\"person_results\":[],\"tv_results\":[],\"tv_episode_results\":[],\"tv_season_results\":[]}",
            "{\"page\":1,\"results\":[{\"id\":598,\"title\":\"Cidade de Deus\",\"original_title\":\"Cidade de Deus\",\"poster_path\":\"/poster.jpg\"}],\"total_pages\":1,\"total_results\":1}"
        };

        foreach (var body in payloads)
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            using var reader = new System.IO.StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            requests.Add(requestLine);
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(response, cancellationToken);
        }

        return requests;
    }
}
