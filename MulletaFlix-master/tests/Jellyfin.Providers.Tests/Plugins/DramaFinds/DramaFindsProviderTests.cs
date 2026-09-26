using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Plugins.DramaFinds;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaFinds
{
    /// <summary>
    /// Exercises the provider layer against the captured DramaFinds payloads, with the HTTP calls
    /// served by a fake handler.
    /// </summary>
    public sealed class DramaFindsProviderTests
    {
        private const string SearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":12,"total":2,"pages":1,"size":2,"list":[
              {"dramaId":47218,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","thumbnailUrl":"https://static.flareflow.tv/covers/47218.jpg?auth_key=1790122207-0-0-aa","rating":9.3,"episodes":52,"releaseDate":"2025-12-24"},
              {"dramaId":47011,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","thumbnailUrl":"https://static.flareflow.tv/covers/47011.jpg?auth_key=1790122205-0-0-bb","rating":8.0,"episodes":52,"releaseDate":"2025-12-22"}
            ]},"error_code":"00000"}
            """;

        private const string DetailJson47011 = """
            {"msg":"success","code":0,"data":{"dramaId":"47011","title":"99 Amuletos, 99 Desilusões","totalCount":52,"episodes":null,"thumbnailUrl":"https://static.flareflow.tv/covers/47011.jpg?auth_key=1790122205-0-0-bb","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","rating":8.0,"viewsCount":2132,"likesCount":17954,"freeCount":8,"releaseDate":"2025-12-22","tags":null},"error_code":"00000"}
            """;

        private const string DetailJson47218 = """
            {"msg":"success","code":0,"data":{"dramaId":"47218","title":"99 Amuletos, 99 Desilusões","totalCount":52,"episodes":null,"thumbnailUrl":"https://static.flareflow.tv/covers/47218.jpg?auth_key=1790122207-0-0-aa","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","rating":9.3,"viewsCount":12505,"likesCount":9982,"freeCount":8,"releaseDate":"2025-12-24","tags":null},"error_code":"00000"}
            """;

        private const string EmptySearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":12,"total":0,"pages":0,"size":0,"list":[]},"error_code":"00000"}
            """;

        [Fact]
        public async Task SeriesProvider_FillsMetadataFromTheCapturedPayload()
        {
            var provider = CreateSeriesProvider(SearchJson);
            var info = new SeriesInfo { Name = "99 Amuletos, 99 Desilusões" };
            info.ProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.NotNull(result.Item);
            Assert.Equal("99 Amuletos, 99 Desilusões", result.Item.Name);
            Assert.Equal("Lilah perdeu tudo — o amor, a carreira e a própria voz.", result.Item.Overview);
            Assert.Equal(8.0f, result.Item.CommunityRating!.Value);

            // releaseDate is an ISO calendar day, so the year comes straight from it.
            Assert.Equal(2025, result.Item.ProductionYear);
            Assert.Equal(new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate!.Value);
            Assert.Equal("47011", result.Item.GetProviderId(DramaFindsSeriesProvider.ProviderKey));
        }

        [Fact]
        public async Task SeriesProvider_ReadsTheEpisodeCountFromTheDetailPayload()
        {
            var provider = CreateSeriesProvider(SearchJson);
            var info = new SeriesInfo { Name = "99 Amuletos, 99 Desilusões" };
            info.ProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);

            // The episode provider is what consumes this, so the count has to survive the detail
            // parse: the API reports it in totalCount and reports "episodes" as null.
            var episodes = DramaFindsParser.BuildEpisodes(
                DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(DetailJson47011))!);
            Assert.Equal(52, episodes.Count);
        }

        /// <summary>
        /// The platform keeps two ids for this drama. Both are offered so the user picks the
        /// edition, instead of the provider silently discarding one.
        /// </summary>
        [Fact]
        public async Task SeriesProvider_OffersEveryDuplicateEdition()
        {
            var provider = CreateSeriesProvider(SearchJson);

            var results = (await provider.GetSearchResults(new SeriesInfo { Name = "99 Amuletos, 99 Desilusões" }, CancellationToken.None)).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal(new[] { "47218", "47011" }, results.Select(result => result.GetProviderId(DramaFindsSeriesProvider.ProviderKey)).ToArray());
            Assert.All(results, result => Assert.Equal("99 Amuletos, 99 Desilusões", result.Name));
            Assert.All(results, result => Assert.Equal(DramaFindsSeriesProvider.ProviderKey, result.SearchProviderName));
        }

        /// <summary>
        /// When nothing else identified the series, the provider decides alone. The choice has to be
        /// deterministic and it is the most recent edition of the same title.
        /// </summary>
        [Fact]
        public async Task SeriesProvider_PrefersTheMostRecentEditionWhenDecidingAlone()
        {
            var provider = CreateSeriesProvider(SearchJson);

            var result = await provider.GetMetadata(new SeriesInfo { Name = "99 Amuletos, 99 Desilusões" }, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("47218", result.Item.GetProviderId(DramaFindsSeriesProvider.ProviderKey));
            Assert.Equal(9.3f, result.Item.CommunityRating!.Value);
            Assert.Equal(new DateTime(2025, 12, 24, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate!.Value);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataWhenThePlatformHasNoMatch()
        {
            var provider = CreateSeriesProvider(EmptySearchJson);

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Abandonei o Rei dos Deuses no Altar" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        /// <summary>
        /// A pasted public episode URL resolves exactly, without paying for a search.
        /// </summary>
        [Fact]
        public async Task SeriesProvider_ResolvesAPastedUrlWithoutSearching()
        {
            var provider = CreateSeriesProvider(SearchJson);

            var results = (await provider.GetSearchResults(
                new SeriesInfo { Name = "https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilusões/episode-2" },
                CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("99 Amuletos, 99 Desilusões", only.Name);
            Assert.Equal("47011", only.GetProviderId(DramaFindsSeriesProvider.ProviderKey));
        }

        /// <summary>
        /// The cover URL arrives signed and the signature expires within hours, so a persisted one
        /// would stop resolving. Every path that exposes a cover has to expose it unsigned.
        /// </summary>
        [Fact]
        public async Task EveryCoverExposedToTheLibraryHasNoExpiringSignature()
        {
            var provider = CreateSeriesProvider(SearchJson);

            var searchResults = (await provider.GetSearchResults(new SeriesInfo { Name = "99 Amuletos, 99 Desilusões" }, CancellationToken.None)).ToList();
            Assert.All(searchResults, result => Assert.DoesNotContain("auth_key", result.ImageUrl!, StringComparison.Ordinal));

            var series = new Series();
            series.SetProviderId(DramaFindsSeriesProvider.ProviderKey, "47011");
            var images = (await CreateImageProvider(SearchJson).GetImages(series, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal(ImageType.Primary, only.Type);
            Assert.Equal("https://static.flareflow.tv/covers/47011.jpg", only.Url);
        }

        [Fact]
        public async Task EpisodeProvider_DerivesTheLocalizedNameAndPublicUrl()
        {
            var provider = CreateEpisodeProvider(SearchJson);
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\99 Amuletos, 99 Desilusões\\Season 01\\e01.strm",
                IndexNumber = 1
            };
            info.SeriesProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.Equal("Episódio 1", result.Item.Name);

            // The public watch URL is the only per-episode identifier the platform has.
            Assert.Equal(
                "https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilus%C3%B5es/episode-1",
                result.Item.GetProviderId(DramaFindsSeriesProvider.ProviderKey));
        }

        [Fact]
        public async Task EpisodeProvider_ResolvesTheLastEpisodeOfTheCount()
        {
            var provider = CreateEpisodeProvider(SearchJson);
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\99 Amuletos, 99 Desilusões\\Season 01\\e52.strm",
                IndexNumber = 52
            };
            info.SeriesProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 52", result.Item.Name);
            Assert.EndsWith("/episode-52", result.Item.GetProviderId(DramaFindsSeriesProvider.ProviderKey), StringComparison.Ordinal);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSeriesHasNoDramaFindsId()
        {
            var provider = CreateEpisodeProvider(SearchJson);
            var info = new EpisodeInfo { Path = "N:\\Series\\Outra\\Season 01\\e01.strm", IndexNumber = 1 };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingBeyondTheEpisodeCount()
        {
            var provider = CreateEpisodeProvider(SearchJson);
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\99 Amuletos, 99 Desilusões\\Season 01\\e99.strm",
                IndexNumber = 99
            };
            info.SeriesProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWithoutAnEpisodeNumber()
        {
            var provider = CreateEpisodeProvider(SearchJson);
            var info = new EpisodeInfo { Path = "N:\\Series\\99 Amuletos, 99 Desilusões\\Season 01\\e01.strm" };
            info.SeriesProviderIds[DramaFindsSeriesProvider.ProviderKey] = "47011";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeImageProvider_UsesTheParentSeriesCover()
        {
            var seriesId = Guid.NewGuid();
            var series = new Series { Id = seriesId };
            series.SetProviderId(DramaFindsSeriesProvider.ProviderKey, "47011");

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemById(seriesId)).Returns(series);

            var episode = new Episode { SeriesId = seriesId, IndexNumber = 1 };
            var provider = CreateImageProvider(SearchJson, libraryManager.Object);

            var images = (await provider.GetImages(episode, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal("https://static.flareflow.tv/covers/47011.jpg", only.Url);
        }

        [Fact]
        public void ImageProvider_SupportsSeriesAndEpisodesOnly()
        {
            var provider = CreateImageProvider(SearchJson);

            Assert.True(provider.Supports(new Series()));
            Assert.True(provider.Supports(new Episode()));
            Assert.False(provider.Supports(new Movie()));
            Assert.Equal(new[] { ImageType.Primary }, provider.GetSupportedImages(new Series()));
        }

        [Fact]
        public void ExternalId_DeclaresTheDramaFindsKeyForSeries()
        {
            var externalId = new DramaFindsExternalId();

            Assert.Equal("DramaFinds", externalId.ProviderName);
            Assert.Equal("DramaFinds", externalId.Key);
            Assert.Null(externalId.Type);
            Assert.True(externalId.Supports(new Series()));
            Assert.False(externalId.Supports(new Movie()));
        }

        private static DramaFindsSeriesProvider CreateSeriesProvider(string searchResponse)
        {
            return new DramaFindsSeriesProvider(
                CreateClient(searchResponse),
                CreateHttpClientFactory(searchResponse),
                new Mock<ILogger<DramaFindsSeriesProvider>>().Object);
        }

        private static DramaFindsEpisodeProvider CreateEpisodeProvider(string searchResponse)
        {
            return new DramaFindsEpisodeProvider(
                CreateClient(searchResponse),
                CreateHttpClientFactory(searchResponse),
                new Mock<ILogger<DramaFindsEpisodeProvider>>().Object);
        }

        private static DramaFindsImageProvider CreateImageProvider(string searchResponse, ILibraryManager? libraryManager = null)
        {
            return new DramaFindsImageProvider(
                CreateClient(searchResponse),
                libraryManager ?? new Mock<ILibraryManager>().Object,
                CreateHttpClientFactory(searchResponse),
                new Mock<ILogger<DramaFindsImageProvider>>().Object);
        }

        /// <summary>
        /// Builds a client whose handler answers each endpoint with its captured payload. A pasted
        /// id resolves through the detail endpoint, so both have to be routed.
        /// </summary>
        private static DramaFindsClient CreateClient(string searchResponse)
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    if (!request.RequestUri!.AbsolutePath.EndsWith("/episodes", StringComparison.Ordinal))
                    {
                        return Json(searchResponse);
                    }

                    var body = request.Content!.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
                    return Json(body.Contains("\"47218\"", StringComparison.Ordinal) ? DetailJson47218 : DetailJson47011);
                });

            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler.Object));

            return new DramaFindsClient(factory.Object, new Mock<ILogger<DramaFindsClient>>().Object);
        }

        /// <summary>
        /// The image providers only pass a URL through to the HTTP client, so the factory answers
        /// the same payload for any request.
        /// </summary>
        private static IHttpClientFactory CreateHttpClientFactory(string searchResponse)
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => Json(searchResponse));

            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler.Object));

            return factory.Object;
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
