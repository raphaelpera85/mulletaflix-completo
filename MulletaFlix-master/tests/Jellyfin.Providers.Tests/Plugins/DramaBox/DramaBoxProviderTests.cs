using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Plugins.DramaBox;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MulletaFlix.Data.Enums;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaBox
{
    /// <summary>
    /// Exercises the provider layer against the captured DramaBox payload, with the HTTP call
    /// served by a fake handler. This is the closest thing to an end-to-end run that does not
    /// require the network or a live server.
    /// </summary>
    public sealed class DramaBoxProviderTests : IDisposable
    {
        private readonly string _cachePath = Path.Combine(Path.GetTempPath(), "dramabox-tests-" + Guid.NewGuid().ToString("N"));

        private const string DetailJson = """
            <!DOCTYPE html><html><body>
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{
              "bookInfo":{
                "bookId":"42000002641",
                "bookName":"3.2.1, Adeus e Ponto Final",
                "bookNameEn":"3-2-1-Farewell-Forever",
                "cover":"https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000004501/42000004501.jpg@w=360&h=640",
                "introduction":"A decisão de Júlia está tomada.",
                "chapterCount":56,
                "tags":["Strong Female Lead","Love Triangle"],
                "typeTwoNames":["Liderança Feminina"],
                "language":"PORTUGUESE",
                "simpleLanguage":"pt",
                "ratings":9.3,
                "shelfTime":"2026-01-29 10:00:26",
                "performerList":[
                  {"performerName":"Cayman Cardiff","performerFormatName":"Cayman-Cardiff","performerAvatar":"https://example.invalid/a.jpg"}
                ]
              },
              "chapterList":[
                {"id":"700157514","name":"第一集","index":0,"unlock":true,"m3u8Flag":false,"cover":"https://thwztvideo.dramaboxdb.com/x/700327408.mp4.jpg@w=100&h=135","duration":134489},
                {"id":"700157515","name":"第二集","index":1,"unlock":true,"m3u8Flag":false,"cover":"https://thwztvideo.dramaboxdb.com/y/700327409.mp4.jpg@w=100&h=135","duration":133050}
              ]
            }}}</script>
            </body></html>
            """;

        [Fact]
        public async Task SeriesProvider_FillsMetadataFromTheCapturedPayload()
        {
            var provider = CreateSeriesProvider();
            var info = new SeriesInfo { Name = "3.2 1, Adeus e Ponto Final" };
            info.ProviderIds[DramaBoxSeriesProvider.ProviderKey] = "42000002641";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.NotNull(result.Item);
            Assert.Equal("3.2.1, Adeus e Ponto Final", result.Item.Name);
            Assert.Equal("A decisão de Júlia está tomada.", result.Item.Overview);
            Assert.Equal(new[] { "Liderança Feminina" }, result.Item.Genres);
            Assert.Equal(new[] { "Strong Female Lead", "Love Triangle" }, result.Item.Tags);
            Assert.Equal(9.3f, result.Item.CommunityRating!.Value);
            Assert.Equal(2026, result.Item.ProductionYear);
            Assert.Equal(new DateTime(2026, 1, 29, 10, 0, 26, DateTimeKind.Utc), result.Item.PremiereDate!.Value);
            Assert.Equal("42000002641", result.Item.GetProviderId(DramaBoxSeriesProvider.ProviderKey));

            var person = Assert.Single(result.People);
            Assert.Equal("Cayman Cardiff", person.Name);
            Assert.Equal(PersonKind.Actor, person.Type);
        }

        [Fact]
        public async Task SeriesProvider_ResolvesAPastedUrlWithoutTheIndex()
        {
            var provider = CreateSeriesProvider();
            var info = new SeriesInfo
            {
                Name = "https://www.dramabox.com/pt/drama/42000002641/321-Adeus-e-Ponto-Final"
            };

            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("3.2.1, Adeus e Ponto Final", only.Name);
            Assert.Equal("42000002641", only.GetProviderId(DramaBoxSeriesProvider.ProviderKey));
            Assert.Contains("42000004501.jpg", only.ImageUrl, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataForAnUnrelatedTitle()
        {
            // No index file exists, so a title that is not a DramaBox URL cannot be resolved.
            var provider = CreateSeriesProvider();

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Chuva Negra" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_DerivesLocalizedTitleAndRuntimeFromTheChapter()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\3.2.1, Adeus e Ponto Final\\Season 01\\e01.strm",
                IndexNumber = 1
            };
            info.SeriesProviderIds[DramaBoxSeriesProvider.ProviderKey] = "42000002641";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 1", result.Item.Name);

            // DramaBox reports milliseconds; MulletaFlix stores ticks.
            Assert.Equal(134_489L * 10_000L, result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ResolvesTheSecondChapterForEpisodeTwo()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\3.2.1, Adeus e Ponto Final\\Season 01\\e02.strm",
                IndexNumber = 2
            };
            info.SeriesProviderIds[DramaBoxSeriesProvider.ProviderKey] = "42000002641";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.Equal("Episódio 2", result.Item.Name);
            Assert.Equal(133_050L * 10_000L, result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSeriesHasNoDramaBoxId()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo { Path = "N:\\Series\\Outra\\Season 01\\e01.strm", IndexNumber = 1 };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingForAnEpisodeBeyondTheChapterList()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\3.2.1, Adeus e Ponto Final\\Season 01\\e99.strm",
                IndexNumber = 99
            };
            info.SeriesProviderIds[DramaBoxSeriesProvider.ProviderKey] = "42000002641";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task SeriesImageProvider_OffersAnUpscaledCover()
        {
            var series = new Series();
            series.SetProviderId(DramaBoxSeriesProvider.ProviderKey, "42000002641");
            var provider = CreateImageProvider();

            var images = (await provider.GetImages(series, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal(ImageType.Primary, only.Type);
            Assert.EndsWith("42000004501.jpg@w=720&h=1280", only.Url, StringComparison.Ordinal);
        }

        [Fact]
        public async Task EpisodeImageProvider_UsesTheChapterThumbnailOfTheParentSeries()
        {
            var seriesId = Guid.NewGuid();
            var series = new Series { Id = seriesId };
            series.SetProviderId(DramaBoxSeriesProvider.ProviderKey, "42000002641");

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemById(seriesId)).Returns(series);

            var episode = new Episode { SeriesId = seriesId, IndexNumber = 1 };
            var provider = CreateImageProvider(libraryManager.Object);

            var images = (await provider.GetImages(episode, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.EndsWith("700327408.mp4.jpg@w=480&h=640", only.Url, StringComparison.Ordinal);
        }

        [Fact]
        public void ImageProvider_SupportsSeriesAndEpisodesOnly()
        {
            var provider = CreateImageProvider();

            Assert.True(provider.Supports(new Series()));
            Assert.True(provider.Supports(new Episode()));
            Assert.False(provider.Supports(new Movie()));
            Assert.Equal(new[] { ImageType.Primary }, provider.GetSupportedImages(new Series()));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_cachePath))
                {
                    Directory.Delete(_cachePath, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }

            GC.SuppressFinalize(this);
        }

        private DramaBoxSeriesProvider CreateSeriesProvider()
        {
            return new DramaBoxSeriesProvider(CreateClient(), CreateHttpClientFactory(), new Mock<ILogger<DramaBoxSeriesProvider>>().Object);
        }

        private DramaBoxEpisodeProvider CreateEpisodeProvider()
        {
            return new DramaBoxEpisodeProvider(CreateClient(), CreateHttpClientFactory(), new Mock<ILogger<DramaBoxEpisodeProvider>>().Object);
        }

        private DramaBoxImageProvider CreateImageProvider(ILibraryManager? libraryManager = null)
        {
            return new DramaBoxImageProvider(
                CreateClient(),
                libraryManager ?? new Mock<ILibraryManager>().Object,
                CreateHttpClientFactory(),
                new Mock<ILogger<DramaBoxImageProvider>>().Object);
        }

        private DramaBoxClient CreateClient()
        {
            var paths = new Mock<IServerApplicationPaths>();
            paths.SetupGet(path => path.CachePath).Returns(_cachePath);

            return new DramaBoxClient(CreateHttpClientFactory(), paths.Object, new Mock<ILogger<DramaBoxClient>>().Object);
        }

        /// <summary>
        /// Builds an HTTP client factory whose handler always answers with the captured payload, so
        /// the client, the parser and the providers are exercised together without any network use.
        /// </summary>
        /// <returns>The factory.</returns>
        private static IHttpClientFactory CreateHttpClientFactory()
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(DetailJson, System.Text.Encoding.UTF8, "text/html")
                });

            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler.Object));

            return factory.Object;
        }
    }
}
