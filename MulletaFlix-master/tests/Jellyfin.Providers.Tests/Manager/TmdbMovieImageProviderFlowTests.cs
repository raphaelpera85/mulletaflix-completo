using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Manager;
using MediaBrowser.Providers.Plugins.Tmdb;
using MediaBrowser.Providers.Plugins.Tmdb.Movies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public partial class ItemImageProviderTests
{
    private static readonly PropertyInfo _tmdbPluginInstanceProperty =
        typeof(Plugin).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo _tmdbPluginInstanceSetter =
        _tmdbPluginInstanceProperty.GetSetMethod(nonPublic: true)!;

    [Fact]
    public async Task RefreshImages_TmdbPortugueseFixture_PreservesSelectedImageLanguageAndProvider()
    {
        var previousPlugin = Plugin.Instance;
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.PluginsPath).Returns(System.IO.Path.GetTempPath());
            paths.Setup(value => value.PluginConfigurationsPath).Returns(System.IO.Path.GetTempPath());
            var serializer = new Mock<IXmlSerializer>();
            serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            var plugin = new Plugin(paths.Object, serializer.Object);
            plugin.Configuration.RateLimitDelayMs = 0;
            plugin.Configuration.PosterSize = "original";

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var apiRequest = RespondToTmdbRequestAsync(listener, cancellation.Token);
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

            var cache = new MemoryCache(new MemoryCacheOptions());
            using var clientManager = new TmdbClientManager(cache, tmdbClient);
            using var imageHttpClient = new HttpClient(new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MediaTypeNames.Image.Jpeg) }
                }
            }));
            var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            httpClientFactory.Setup(factory => factory.CreateClient(NamedClient.Default)).Returns(imageHttpClient);
            var tmdbImageProvider = new TmdbMovieImageProvider(httpClientFactory.Object, clientManager);

            var item = new Movie
            {
                Path = "fixture.mkv",
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };
            item.SetProviderId(MetadataProvider.Tmdb, "42");
            BaseItem.FileSystem = Mock.Of<IFileSystem>();
            BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
            var libraryOptions = GetLibraryOptions(item, ImageType.Primary, 1);
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                    item,
                    It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                    It.IsAny<CancellationToken>()))
                .Returns((BaseItem _, RemoteImageQuery _, CancellationToken token) => tmdbImageProvider.GetImages(item, token));
            providerManager.Setup(manager => manager.SaveImage(
                    item,
                    It.IsAny<System.IO.Stream>(),
                    MediaTypeNames.Image.Jpeg,
                    ImageType.Primary,
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<BaseItem, System.IO.Stream, string, ImageType, int?, CancellationToken>((savedItem, _, _, type, _, _) =>
                    savedItem.SetImage(new ItemImageInfo { Path = "poster.jpg", Type = type }, 0))
                .Returns(Task.CompletedTask);

            var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
                item,
                libraryOptions,
                [tmdbImageProvider],
                new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
                cancellation.Token);
            var requestedImages = await apiRequest;

            var savedImage = item.GetImageInfo(ImageType.Primary, 0)!;
            Assert.True(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
            Assert.Contains("include_image_language=pt,null,en", Uri.UnescapeDataString(requestedImages), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("poster.jpg", savedImage.Path);
            Assert.Equal("pt-BR", savedImage.Language);
            Assert.Equal(TmdbUtils.ProviderName, savedImage.ProviderName);
        }
        finally
        {
            _tmdbPluginInstanceSetter.Invoke(null, [previousPlugin]);
        }
    }

    [Fact]
    public async Task GetImages_InvalidTmdbProviderId_ReturnsEmptyWhenImdbFallbackIsUnavailable()
    {
        var provider = new TmdbMovieImageProvider(Mock.Of<IHttpClientFactory>(), null!);
        var item = new Movie
        {
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        item.SetProviderId(MetadataProvider.Tmdb, "not-a-number");

        var result = await provider.GetImages(item, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetImages_WithoutProviderId_ResolvesMovieByTitleAndYear()
    {
        var previousPlugin = Plugin.Instance;
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.PluginsPath).Returns(System.IO.Path.GetTempPath());
            paths.Setup(value => value.PluginConfigurationsPath).Returns(System.IO.Path.GetTempPath());
            var serializer = new Mock<IXmlSerializer>();
            serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            var plugin = new Plugin(paths.Object, serializer.Object);
            plugin.Configuration.RateLimitDelayMs = 0;
            plugin.Configuration.PosterSize = "original";

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var requestsTask = RespondToTmdbSearchAndImagesAsync(listener, cancellation.Token);
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
            var provider = new TmdbMovieImageProvider(Mock.Of<IHttpClientFactory>(), clientManager);
            var item = new Movie
            {
                Name = "As Namoradas do Papai",
                OriginalTitle = "It Takes Two",
                ProductionYear = 1995,
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };

            var result = (await provider.GetImages(item, cancellation.Token)).ToArray();
            var requests = await requestsTask;

            var image = Assert.Single(result);
            Assert.Equal("https://images.fixture/original/poster-pt.jpg", image.Url);
            Assert.Equal("pt-BR", image.Language);
            Assert.Contains(requests, request => request.Contains("/search/movie", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(requests, request => request.Contains("/movie/42/images", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _tmdbPluginInstanceSetter.Invoke(null, [previousPlugin]);
        }
    }

    [Fact]
    public async Task GetImages_WithoutProviderId_FallsBackToOriginalTitleWhenLocalizedTitleMisses()
    {
        var previousPlugin = Plugin.Instance;
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.PluginsPath).Returns(System.IO.Path.GetTempPath());
            paths.Setup(value => value.PluginConfigurationsPath).Returns(System.IO.Path.GetTempPath());
            var serializer = new Mock<IXmlSerializer>();
            serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            var plugin = new Plugin(paths.Object, serializer.Object);
            plugin.Configuration.RateLimitDelayMs = 0;
            plugin.Configuration.PosterSize = "original";

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
            var provider = new TmdbMovieImageProvider(Mock.Of<IHttpClientFactory>(), clientManager);
            var item = new Movie
            {
                Name = "As Namoradas do Papai",
                OriginalTitle = "It Takes Two",
                ProductionYear = 1995,
                PreferredMetadataLanguage = "pt-BR",
                PreferredMetadataCountryCode = "BR"
            };

            var result = (await provider.GetImages(item, cancellation.Token)).ToArray();
            var requests = await requestsTask;

            var image = Assert.Single(result);
            Assert.Equal("https://images.fixture/original/poster-pt.jpg", image.Url);
            Assert.Contains(requests, request => request.Contains("as%20namoradas", StringComparison.OrdinalIgnoreCase) || request.Contains("as+namoradas", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(requests, request => request.Contains("it%20takes%20two", StringComparison.OrdinalIgnoreCase) || request.Contains("it+takes+two", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(requests, request => request.Contains("/movie/42/images", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _tmdbPluginInstanceSetter.Invoke(null, [previousPlugin]);
        }
    }

    [Fact]
    public async Task RefreshImages_DoesNotDownloadEnglishFallbackForPortugueseLibrary()
    {
        var item = new Movie
        {
            Id = Guid.NewGuid(),
            Path = "fixture.mkv",
            PreferredMetadataLanguage = "pt-BR",
            PreferredMetadataCountryCode = "BR"
        };
        BaseItem.FileSystem = Mock.Of<IFileSystem>();
        BaseItem.MediaSourceManager = Mock.Of<IMediaSourceManager>();
        var provider = new Mock<IRemoteImageProvider>(MockBehavior.Strict);
        provider.Setup(value => value.Name).Returns(TmdbUtils.ProviderName);
        provider.Setup(value => value.GetSupportedImages(item)).Returns([ImageType.Primary]);
        provider.Setup(value => value.GetImageResponse("https://images.fixture/poster-en.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MediaTypeNames.Image.Jpeg) }
                }
            });

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.Is<RemoteImageQuery>(query => query.IncludeAllLanguages),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteImageInfo
            {
                ProviderName = TmdbUtils.ProviderName,
                Url = "https://images.fixture/poster-en.jpg",
                Type = ImageType.Primary,
                Language = "en",
                Width = 1000
            }]);
        providerManager.Setup(manager => manager.SaveImage(
                item,
                It.IsAny<System.IO.Stream>(),
                MediaTypeNames.Image.Jpeg,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, System.IO.Stream, string, ImageType, int?, CancellationToken>((savedItem, _, _, type, _, _) =>
                savedItem.SetImage(new ItemImageInfo { Path = "poster.jpg", Type = type }, 0))
            .Returns(Task.CompletedTask);

        var result = await GetItemImageProvider(providerManager.Object, null).RefreshImages(
            item,
            GetLibraryOptions(item, ImageType.Primary, 1),
            [provider.Object],
            new ImageRefreshOptions(Mock.Of<IDirectoryService>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.UpdateType.HasFlag(ItemUpdateType.ImageUpdate));
        Assert.Null(item.GetImageInfo(ImageType.Primary, 0));
    }

    private static async Task<string> RespondToTmdbRequestAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new System.IO.StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        const string body = "{\"id\":42,\"posters\":[{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/poster-pt.jpg\",\"vote_average\":7.0,\"vote_count\":1},{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"en\",\"file_path\":\"/poster-en.jpg\",\"vote_average\":6.0,\"vote_count\":1}],\"backdrops\":[],\"logos\":[]}";
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
        await stream.WriteAsync(response, cancellationToken);
        return requestLine ?? string.Empty;
    }

    private static async Task<IReadOnlyList<string>> RespondToTmdbSearchAndImagesAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var requests = new List<string>();
        var payloads = new[]
        {
            "{\"page\":1,\"results\":[{\"id\":42,\"title\":\"As Namoradas do Papai\",\"original_title\":\"It Takes Two\",\"release_date\":\"1995-11-20\",\"poster_path\":\"/poster-pt.jpg\"}],\"total_pages\":1,\"total_results\":1}",
            "{\"id\":42,\"posters\":[{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/poster-pt.jpg\",\"vote_average\":7.0,\"vote_count\":1}],\"backdrops\":[],\"logos\":[]}"
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

    private static async Task<IReadOnlyList<string>> RespondToTmdbLocalizedTitleFallbackAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var requests = new List<string>();
        while (requests.Count < 6)
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            using var reader = new System.IO.StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            requests.Add(requestLine);
            var body = requestLine.Contains("/movie/42/images", StringComparison.OrdinalIgnoreCase)
                ? "{\"id\":42,\"posters\":[{\"aspect_ratio\":0.7,\"height\":1000,\"iso_639_1\":\"pt\",\"file_path\":\"/poster-pt.jpg\",\"vote_average\":7.0,\"vote_count\":1}],\"backdrops\":[],\"logos\":[]}"
                : requestLine.Contains("it%20takes%20two", StringComparison.OrdinalIgnoreCase) || requestLine.Contains("it+takes+two", StringComparison.OrdinalIgnoreCase)
                    ? "{\"page\":1,\"results\":[{\"id\":42,\"title\":\"It Takes Two\",\"original_title\":\"It Takes Two\",\"release_date\":\"1995-11-20\",\"poster_path\":\"/poster-pt.jpg\"}],\"total_pages\":1,\"total_results\":1}"
                    : "{\"page\":1,\"results\":[],\"total_pages\":1,\"total_results\":0}";
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(response, cancellationToken);

            if (requestLine.Contains("/movie/42/images", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return requests;
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_respond(request));
        }
    }
}
