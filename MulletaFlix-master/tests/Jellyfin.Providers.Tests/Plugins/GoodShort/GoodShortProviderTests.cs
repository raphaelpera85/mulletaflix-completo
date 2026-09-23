using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Plugins.GoodShort;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.GoodShort
{
    /// <summary>
    /// Exercises the provider layer against the real GoodShort payloads, with the HTTP calls served
    /// by a fake handler that routes on the request path and body. This is the closest thing to an
    /// end-to-end run that does not require the network or a live server.
    /// </summary>
    public sealed class GoodShortProviderTests : IDisposable
    {
        private const string DetailPath = "/hwycreels/book/detail";
        private const string ChapterPath = "/hwycreels/chapter/page";
        private const string SuggestPath = "/hwycreels/book/search/suggest";

        private const string SeriesId = "31001543625";
        private const string SeriesSlug = "abandonei-o-rei-dos-deuses-no-altar-31001543625";
        private const string DramaPath = "/drama/" + SeriesSlug;

        private const string SecondSeriesId = "31001499548";
        private const string SecondSeriesUrl =
            "https://www.goodshort.com/drama/dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548";

        /// <summary>
        /// The rendered search page, which the search endpoint falls back to and which the site
        /// serves at /search with the term in the query string.
        /// </summary>
        private const string SearchPagePath = "/search";

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [Fact]
        public async Task SeriesProvider_FillsMetadataFromTheLiveApiPayloads()
        {
            var provider = CreateSeriesProvider(ApiRoutes());
            var info = new SeriesInfo { Name = "Abandonei o Rei dos Deuses no Altar" };
            info.ProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.NotNull(result.Item);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", result.Item.Name);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", result.Item.Overview, StringComparison.Ordinal);
            Assert.Equal(new[] { "Romance" }, result.Item.Genres);
            Assert.Equal(2026, result.Item.ProductionYear);
            Assert.Equal(new DateTime(2026, 7, 4, 15, 6, 11, DateTimeKind.Utc), result.Item.PremiereDate!.Value);
            Assert.Equal(SeriesId, result.Item.GetProviderId(GoodShortSeriesProvider.ProviderKey));

            // ratings is 0 on the live payload, which means unrated, so no score may be published.
            Assert.Null(result.Item.CommunityRating);
        }

        [Fact]
        public async Task SeriesProvider_FillsTheSecondReportedSeriesByItsBareId()
        {
            var provider = CreateSeriesProvider(ApiRoutes());
            var info = new SeriesInfo { Name = "[Dublado] Abandonada pelo Don, Coroada pela Máfia" };
            info.ProviderIds[GoodShortSeriesProvider.ProviderKey] = SecondSeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("[Dublado] Abandonada pelo Don, Coroada pela Máfia", result.Item!.Name);
            Assert.Equal(SecondSeriesId, result.Item.GetProviderId(GoodShortSeriesProvider.ProviderKey));
        }

        [Fact]
        public async Task SeriesProvider_ResolvesAPastedUrlWithoutSearching()
        {
            var handler = new RoutingHandler(ApiRoutes());
            var provider = CreateSeriesProvider(handler);

            var info = new SeriesInfo { Name = SecondSeriesUrl };

            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("[Dublado] Abandonada pelo Don, Coroada pela Máfia", only.Name);
            Assert.Equal(SecondSeriesId, only.GetProviderId(GoodShortSeriesProvider.ProviderKey));
            Assert.Contains("cover-vmZTLAz96K.jpg", only.ImageUrl!, StringComparison.Ordinal);

            // A pasted URL must be resolved by id, never by the search endpoint.
            Assert.DoesNotContain(SuggestPath, handler.Requests);
        }

        [Fact]
        public async Task SeriesProvider_SearchesByTitleWhenNoIdIsKnown()
        {
            var handler = new RoutingHandler(ApiRoutes());
            var provider = CreateSeriesProvider(handler);
            var info = new SeriesInfo { Name = "rei dos deuses" };

            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            Assert.Equal(3, results.Count);
            Assert.Equal("31001543628", results[0].GetProviderId(GoodShortSeriesProvider.ProviderKey));
            Assert.Contains(SuggestPath, handler.Requests);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataForAnUnrelatedTitle()
        {
            var routes = ApiRoutes();
            routes[SuggestPath] = _ => Json("""{"status":0,"data":{"suggest":[],"majors":[]}}""");

            var provider = CreateSeriesProvider(new RoutingHandler(routes));

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Chuva Negra" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataForAnIdThatDoesNotExist()
        {
            // Verified live: the detail endpoint answers HTTP 200, status 0 and no book for an
            // unknown id, so a transport status check alone would report an empty series.
            var routes = ApiRoutes();
            routes[DetailPath] = _ => Json(Fixture("book-detail-unknown.json"));

            var provider = CreateSeriesProvider(new RoutingHandler(routes));
            var info = new SeriesInfo { Name = "Nada" };
            info.ProviderIds[GoodShortSeriesProvider.ProviderKey] = "99999999999999";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataForTheBookMissingEnvelope()
        {
            // The channel endpoint signals a missing book with a non-zero envelope status instead.
            var routes = ApiRoutes();
            routes[DetailPath] = _ => Json(Fixture("book-not-found.json"));

            var provider = CreateSeriesProvider(new RoutingHandler(routes));
            var info = new SeriesInfo { Name = "Nada" };
            info.ProviderIds[GoodShortSeriesProvider.ProviderKey] = "99999999999999";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_DerivesLocalizedTitleAndRuntimeFromSeconds()
        {
            var provider = CreateEpisodeProvider(ApiRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e01.strm",
                IndexNumber = 1
            };
            info.SeriesProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 1", result.Item.Name);

            // GoodShort reports playTime in seconds; MulletaFlix stores ticks.
            Assert.Equal(81L * 10_000_000L, result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ReachesTheLastEpisodeOfTheCompleteList()
        {
            // The page and the JSON-LD both stop at episode 6, so reaching 37 is the proof that the
            // episode list really came from the channel endpoint.
            var provider = CreateEpisodeProvider(ApiRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e37.strm",
                IndexNumber = 37
            };
            info.SeriesProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 37", result.Item.Name);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSeriesHasNoGoodShortId()
        {
            var provider = CreateEpisodeProvider(ApiRoutes());
            var info = new EpisodeInfo { Path = "N:\\Series\\Outra\\Season 01\\e01.strm", IndexNumber = 1 };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingForAnEpisodeBeyondTheCompleteList()
        {
            var provider = CreateEpisodeProvider(ApiRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e99.strm",
                IndexNumber = 99
            };
            info.SeriesProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ServesEpisodesFromThePageFallbackOnceTheSlugIsKnown()
        {
            // The degraded flow: the endpoints are down, but the user pasted a drama URL, so the
            // series provider learned the slug and cached the six episodes the page carries. The
            // episode provider then runs against the same client, as it does in a refresh pipeline.
            var routes = BrokenApiRoutes();
            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            var series = await client.GetSeriesAsync(SeriesId, SeriesSlug, CancellationToken.None);
            Assert.NotNull(series);
            Assert.Equal(6, series!.Episodes.Count);

            var provider = CreateEpisodeProvider(client, handler);
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e03.strm",
                IndexNumber = 3
            };
            info.SeriesProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 3", result.Item.Name);
            Assert.Equal(78L * 10_000_000L, result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSlugWasNeverLearned()
        {
            // Without a slug the page fallback cannot run at all, because a series page is addressed
            // by slug and the endpoints that would reveal one are unreachable.
            var provider = CreateEpisodeProvider(BrokenApiRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e03.strm",
                IndexNumber = 3
            };
            info.SeriesProviderIds[GoodShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task SeriesImageProvider_OffersTheUnsignedCover()
        {
            var series = new Series();
            series.SetProviderId(GoodShortSeriesProvider.ProviderKey, SeriesId);
            var provider = CreateImageProvider(ApiRoutes());

            var images = (await provider.GetImages(series, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal(ImageType.Primary, only.Type);
            Assert.Equal("https://acf.goodshort.com/videobook/202607/cover-2WGS3kh14m.jpg", only.Url);
        }

        [Fact]
        public async Task SeriesImageProvider_OffersNothingWithoutAGoodShortId()
        {
            var provider = CreateImageProvider(ApiRoutes());

            var images = await provider.GetImages(new Series(), CancellationToken.None);

            Assert.Empty(images);
        }

        [Fact]
        public async Task EpisodeImageProvider_UsesTheThumbnailOfTheParentSeries()
        {
            var seriesId = Guid.NewGuid();
            var series = new Series { Id = seriesId };
            series.SetProviderId(GoodShortSeriesProvider.ProviderKey, SeriesId);

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemById(seriesId)).Returns(series);

            var episode = new Episode { SeriesId = seriesId, IndexNumber = 1 };
            var provider = CreateImageProvider(ApiRoutes(), libraryManager.Object);

            var images = (await provider.GetImages(episode, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal(
                "https://acf.goodshort.com/videobook/31001543625/202607/cover-S8o8dXDQAk.jpg",
                only.Url);
        }

        [Fact]
        public async Task EpisodeImageProvider_OffersNothingWithoutAGoodShortIdOnTheSeries()
        {
            var seriesId = Guid.NewGuid();
            var series = new Series { Id = seriesId };

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemById(seriesId)).Returns(series);

            var episode = new Episode { SeriesId = seriesId, IndexNumber = 1 };
            var provider = CreateImageProvider(ApiRoutes(), libraryManager.Object);

            Assert.Empty(await provider.GetImages(episode, CancellationToken.None));
        }

        [Fact]
        public void ImageProvider_SupportsSeriesAndEpisodesOnly()
        {
            var provider = CreateImageProvider(ApiRoutes());

            Assert.True(provider.Supports(new Series()));
            Assert.True(provider.Supports(new Episode()));
            Assert.False(provider.Supports(new Movie()));
            Assert.Equal(new[] { ImageType.Primary }, provider.GetSupportedImages(new Series()));
        }

        [Fact]
        public void ExternalId_DeclaresTheSeriesProviderKey()
        {
            var externalId = new GoodShortExternalId();

            Assert.Equal("GoodShort", externalId.ProviderName);
            Assert.Equal("GoodShort", externalId.Key);
            Assert.True(externalId.Supports(new Series()));
            Assert.False(externalId.Supports(new Episode()));
        }

        [Fact]
        public async Task Client_ReportsTheCompleteEpisodeCountFromTheApi()
        {
            var client = CreateClient(ApiRoutes());

            var series = await client.GetSeriesAsync(SeriesId, null, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(37, series!.EpisodeCount);

            // The whole point of the API path: all 37 episodes, not the 6 the page carries.
            Assert.Equal(37, series.Episodes.Count);
            Assert.True(series.EpisodesAreComplete);
            Assert.Equal(37, series.Episodes[36].Number);
        }

        [Fact]
        public async Task Client_MarksThePageFallbackAsIncomplete()
        {
            var client = CreateClient(BrokenApiRoutes());

            var series = await client.GetSeriesAsync(SeriesId, SeriesSlug, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(37, series!.EpisodeCount);
            Assert.Equal(6, series.Episodes.Count);
            Assert.False(series.EpisodesAreComplete);
        }

        [Fact]
        public async Task Client_FallsBackToTheRenderedSearchPageWhenTheSearchApiFails()
        {
            var routes = ApiRoutes();
            routes[SuggestPath] = _ => Status(HttpStatusCode.InternalServerError);
            routes[SearchPagePath] = _ => Html(Fixture("search-page.html"));

            var client = CreateClient(routes);

            var matches = await client.SearchAsync("Crowned by the Gods", 5, CancellationToken.None);

            Assert.NotEmpty(matches);
            Assert.Contains(matches, match => match.Series.SeriesId == "31001705992");

            // The page fallback carries no synopsis, so the ranked list is title-only.
            Assert.All(matches, match => Assert.False(string.IsNullOrWhiteSpace(match.Series.Name)));
            Assert.True(matches.Count <= 5);
        }

        [Fact]
        public async Task Client_ReturnsNothingInsteadOfGoingQuietWhenTheSearchSimplyHasNoHits()
        {
            var routes = ApiRoutes();
            routes[SuggestPath] = _ => Json("""{"status":0,"data":{"suggest":[],"majors":[]}}""");

            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            var matches = await client.SearchAsync("zzzz nao existe zzzz", 5, CancellationToken.None);

            // An empty candidate list is a real answer, not a failure, so the page fallback is never
            // consulted and the client stays ready for the next item.
            Assert.Empty(matches);
            Assert.False(client.IsInFailureCooldown);
            Assert.DoesNotContain(SearchPagePath, handler.Requests);
        }

        [Fact]
        public async Task Client_TreatsAMissingBookAsAnAnswerRatherThanAnOutage()
        {
            var routes = ApiRoutes();
            routes[DetailPath] = _ => Json(Fixture("book-detail-unknown.json"));

            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            Assert.Null(await client.GetSeriesAsync("99999999999999", null, CancellationToken.None));

            // A stale provider id must not take the whole provider offline for the next half hour.
            Assert.False(client.IsInFailureCooldown);

            // Only the detail endpoint was consulted: an absent book needs neither the page nor the
            // channel listing.
            Assert.Equal(new[] { DetailPath }, handler.Requests);
        }

        [Fact]
        public async Task Client_GoesQuietForTheCooldownAfterBothSourcesFail()
        {
            var routes = BrokenApiRoutes();
            routes[SearchPagePath] = _ => Status(HttpStatusCode.ServiceUnavailable);

            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            Assert.False(client.IsInFailureCooldown);

            // First failure: the search endpoint and then the rendered page are both tried.
            var first = await client.SearchAsync("qualquer coisa", 5, CancellationToken.None);
            Assert.Empty(first);
            Assert.True(client.IsInFailureCooldown);

            var requestsAfterFirstFailure = handler.Requests.Count;

            // Everything after that must be answered from the cooldown without touching the wire.
            Assert.Null(await client.GetSeriesAsync(SeriesId, SeriesSlug, CancellationToken.None));
            Assert.Empty(await client.SearchAsync("outra coisa", 5, CancellationToken.None));

            Assert.Equal(requestsAfterFirstFailure, handler.Requests.Count);
            Assert.Equal(2, requestsAfterFirstFailure);
        }

        [Fact]
        public async Task Client_NeverLetsAFailedRefreshThrow()
        {
            var routes = BrokenApiRoutes();
            routes[SuggestPath] = _ => Json("not json at all");
            routes[DramaPath] = _ => Html("<html><body>no structured data</body></html>");
            routes[SearchPagePath] = _ => Html("<html><body>nothing here</body></html>");

            var client = CreateClient(routes);

            Assert.Null(await client.GetSeriesAsync(SeriesId, SeriesSlug, CancellationToken.None));
            Assert.Empty(await client.SearchAsync("nada", 5, CancellationToken.None));
        }

        [Fact]
        public async Task Client_CachesASeriesBetweenProviders()
        {
            var handler = new RoutingHandler(ApiRoutes());
            var client = CreateClient(handler);

            var first = await client.GetSeriesAsync(SeriesId, null, CancellationToken.None);
            var second = await client.GetSeriesAsync(SeriesId, null, CancellationToken.None);

            Assert.NotNull(first);
            Assert.Same(first, second);

            // Two requests served the first read (book detail plus the channel page) and nothing more.
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Client_SendsAJsonContentTypeWithoutACharsetParameter()
        {
            // Verified live: the endpoint answers 415 unless the media type is exactly
            // application/json, which is why the content type is set explicitly.
            var recorder = new RoutingHandler(ApiRoutes());
            var client = CreateClient(recorder);

            await client.GetSeriesAsync(SeriesId, null, CancellationToken.None);

            Assert.NotEmpty(recorder.ContentTypes);
            Assert.All(recorder.ContentTypes, contentType => Assert.Equal("application/json", contentType));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine("Test Data", "GoodShort", name));
        }

        /// <summary>
        /// The routes the live site serves for the two series the fixtures were captured from. The
        /// detail and channel routes read the requested book id out of the body, because both
        /// endpoints share one path and are told apart only by that id.
        /// </summary>
        /// <returns>The route table.</returns>
        private static Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> ApiRoutes()
        {
            return new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.Ordinal)
            {
                [DetailPath] = request => Json(IsSecondSeries(request)
                    ? Fixture("book-detail-mafia.json")
                    : Fixture("book-detail.json")),
                [ChapterPath] = request => Json(IsSecondSeries(request)
                    ? Fixture("chapter-page-mafia.json")
                    : Fixture("chapter-page.json")),
                [SuggestPath] = _ => Json(Fixture("search-suggest.json")),
                [DramaPath] = _ => Html(Fixture("detail-page-abandonei.html"))
            };
        }

        /// <summary>
        /// The same routes with every JSON endpoint refusing, leaving only the rendered pages.
        /// </summary>
        /// <returns>The route table.</returns>
        private static Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> BrokenApiRoutes()
        {
            return new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.Ordinal)
            {
                [DetailPath] = _ => Status(HttpStatusCode.ServiceUnavailable),
                [ChapterPath] = _ => Status(HttpStatusCode.ServiceUnavailable),
                [SuggestPath] = _ => Status(HttpStatusCode.ServiceUnavailable),
                [DramaPath] = _ => Html(Fixture("detail-page-abandonei.html"))
            };
        }

        private static bool IsSecondSeries(HttpRequestMessage request)
        {
            return request.Content is not null
                && request.Content.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult()
                    .Contains(SecondSeriesId, StringComparison.Ordinal);
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage Html(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/html")
            };
        }

        private static HttpResponseMessage Status(HttpStatusCode status)
        {
            return new HttpResponseMessage(status);
        }

        private GoodShortSeriesProvider CreateSeriesProvider(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            return CreateSeriesProvider(new RoutingHandler(routes));
        }

        private GoodShortSeriesProvider CreateSeriesProvider(RoutingHandler handler)
        {
            return new GoodShortSeriesProvider(
                CreateClient(handler),
                CreateHttpClientFactory(handler),
                new Mock<ILogger<GoodShortSeriesProvider>>().Object);
        }

        private GoodShortEpisodeProvider CreateEpisodeProvider(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            var handler = new RoutingHandler(routes);
            return CreateEpisodeProvider(CreateClient(handler), handler);
        }

        private GoodShortEpisodeProvider CreateEpisodeProvider(GoodShortClient client, RoutingHandler handler)
        {
            return new GoodShortEpisodeProvider(
                client,
                CreateHttpClientFactory(handler),
                new Mock<ILogger<GoodShortEpisodeProvider>>().Object);
        }

        private GoodShortImageProvider CreateImageProvider(
            Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes,
            ILibraryManager? libraryManager = null)
        {
            var handler = new RoutingHandler(routes);

            return new GoodShortImageProvider(
                CreateClient(handler),
                libraryManager ?? new Mock<ILibraryManager>().Object,
                CreateHttpClientFactory(handler),
                new Mock<ILogger<GoodShortImageProvider>>().Object);
        }

        private GoodShortClient CreateClient(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            return CreateClient(new RoutingHandler(routes));
        }

        private GoodShortClient CreateClient(RoutingHandler handler)
        {
            var client = new GoodShortClient(
                CreateHttpClientFactory(handler),
                new Mock<ILogger<GoodShortClient>>().Object);

            _disposables.Add(client);
            return client;
        }

        private static IHttpClientFactory CreateHttpClientFactory(RoutingHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() =>
                {
                    var httpClient = new HttpClient(handler, disposeHandler: false);
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    return httpClient;
                });

            return factory.Object;
        }

        /// <summary>
        /// Answers each request from a route table keyed by absolute path, so a test can make one
        /// endpoint fail while the others keep working. That is what tells apart the API path, the
        /// HTML fallback and the search fallback.
        /// </summary>
        private sealed class RoutingHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes;

            public RoutingHandler(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
            {
                _routes = routes;
            }

            public List<string> Requests { get; } = new List<string>();

            public List<string?> ContentTypes { get; } = new List<string?>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                Requests.Add(path);
                ContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);

                if (_routes.TryGetValue(path, out var factory))
                {
                    return Task.FromResult(factory(request));
                }

                return Task.FromResult(Status(HttpStatusCode.NotFound));
            }
        }
    }
}
