using System;
using System.Collections.Generic;
using System.IO;
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
using MediaBrowser.Providers.Plugins.NetShort;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NetShort
{
    /// <summary>
    /// Exercises the provider layer against real payloads, with the HTTP calls served by a fake
    /// handler. This is the closest thing to an end-to-end run that needs neither the network nor a
    /// live server.
    /// </summary>
    /// <remarks>
    /// The visitor login response is the one captured from the live site and is still encrypted with
    /// the server's per-response key, so these tests also prove the client unwraps a genuine
    /// response. Everything else is served as plain JSON, which is a shape the API really uses — the
    /// client only decrypts when the response carries an <c>encrypt-key</c> header.
    /// </remarks>
    public class NetShortProviderTests
    {
        private const string SeriesId = "2082282581191360513";

        /// <summary>
        /// The piece of the captured login response this provider has to be able to read.
        /// </summary>
        private const string CapturedToken =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJsb2dpblR5cGUiOiJsb2dpbiIsImxvZ2luSWQiOjIxMDI1MjUxODE3Mjk3NzU2MTcsInJuU3RyIjoiZVRFTmJ5bXdaT0lxUE9kOFNqUTJSUDRlQURLZW43OGYifQ.SiaUdYrCJna7BEoYD4dhTozmxTKdOX3FMbzQppKtpqE";

        private const string LoginResponseKeyHeader =
            "iTAfpg0EZM5ubRKfNEhSRSJhQAPqDqDTjkGWg1S/xtqqU026QPgPmlxZkYHZVIMtOI3Xo1l1zP2uhQ+z0QOVHfhU5uLhWYtk760X7OBDRCvtQCPq5avKKdUC0VRJYCTYqQpLGesaPvGtYEv09iIBCMvC2CD+B99bJpn/XUgYo05XbGfAbgRBVilPjT9/QKADZ0vamQgpZCNAb5TfLhsS9X2A/GBUgNCvSetsk472y1TWQUBrIBeToUedcSkEuznfEyt3YaW2R5ITnR6LA5uPHImq78zHpltn9jv5GqggsKleyP98MioDXnjAfydfOBoRJ3Ej3QW3u3e4MDXk4QtOkw==";

        private const string LoginResponseBody =
            "ArmqUy/Mz2tYMy0DlPEqkKV85pCo/oRxjU0T09XBCxmQrUFHzwC086XKsfNKsp2gfufB/96kWQkBRXr+JzTD54PDamvYdesEhiusLIrNBD4cLyxIMJxE3YHIi/DNRnYHClyCDQrJsxqQX26T9z+jTMt+F8NgupIbmcEek8Onq9wfPXXub3efThE5XQss91ZvJfOJ+yFsuIIWlHQZodazCR9B5Q0DSDmAOK/GRLJ8hv0msLOPVLuWNYjSFPNPZ3pNpDWLtavABwfaeuou6VzAmT1qBWP5zuZzTql9Zq/yVmW9IXPnDJ4n1f4IUIg0rIgvzvktT0awsLBAXFuVctAvp5DXfOmL6xj4Oxp4gPGO42oLZNILIYjTiLFbSYxvFjer3piwnQ1x7NnSyox8/CWh4MU9DoGS4MeirmEURjlFpvKw7LhTE0/4XvqRRXRLNq3XVpcrrGUhdnScxXeNSSOHCiSOAnyHB54s1E/WUaKggk7vlxTSPOPgjC01lLYjkYY3+qR/5zD+Ljo2S54UY1FyOAuXJ/k68FeIwwTrE9Qd+/aloydhZ/VneQs6eYHuNs8GeKTReMRF9dbjNu4xI1I6navkvtXFU6+I/QtCvtiC51rs/mqVkRIsE+ADo4e9UuwHmderpLNEPz1Hv32+GvNrkMmj0h+kLYHkLHiPq1ojTCU=";

        private const string DetailEnvelope = """
            {"code":200,"msg":"操作成功","data":{"shortPlayId":"2082282581191360513","shortPlayName":"Além do 99 Perdões","shortPlayUrl":"/pt/drama/além-do-99-perdões-2082282581191360513","shortPlayCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/1785491610380-3885058237326381-3比4jpg~tplv-vod-noop.image","shortPlayLibraryId":"2082282580876787713","shortPlayLabels":{"Reencontro de Ex":"/pt/drama/Reencontro%20de%20Ex-1983832091805343750","Crescimento Feminino":"/pt/drama/Crescimento%20Feminino-1983832092950388745","Romance Urbano":"/pt/drama/Romance%20Urbano-1983832092736479240"},"labelIds":["1983832092736479240","1983832092950388745","1983832091805343750"],"shotIntroduce":"Brooke Sterling amou por três anos o marido, o campeão de boxe Mason Masters, enquanto ele tratava o casamento como um jogo para provocar a ex.","totalLikeNums":"2.1K","totalChaseNums":"2.3K","videoEpisodeInfos":[{"episodeId":"2082403374911545346","episodeNo":1,"isLock":false,"episodeCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/o880UN7RekvTvphEQCQhyPTEaIImtAA54AL1OI~tplv-vod-noop.image"},{"episodeId":"2082403374898962440","episodeNo":2,"isLock":true,"episodeCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/oAI0T13QeAOXbT03ZGQIjt7gOjazsNCwaIM7wh~tplv-vod-noop.image"}],"language":"pt_PT","isDelisted":false,"publishTime":1785644901602}}
            """;

        private const string SearchEnvelope = """
            {"code":200,"msg":"操作成功","data":{"list":[{"shortPlayId":"2082282581191360513","shortPlayNameNoHL":"Além do 99 Perdões","shortPlayNameUrl":"/pt/episode/além-do-99-perdões-2082282581191360513","shortPlayCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/1785491610380-3885058237326381-3比4jpg~tplv-vod-rs:651:868.webp","shortPlayName":"Além do 99 <span style='color: #F5315E'>Perdões</span>","shotIntroduce":"Brooke Sterling amou por três anos o marido.","labelNames":"Crescimento Feminino,Reencontro de Ex,Romance Urbano","likeNums":"2.8K","chaseNums":"2.3K"}],"totalHits":10000}}
            """;

        private const string JsonLdPage = """
            <!DOCTYPE html><html lang="pt"><head>
            <script id="json-ld" type="application/ld+json">{"@context":"https://schema.org","@graph":[{"@type":"TVSeries","@id":"https://netshort.com/pt/full-episodes/2082282581191360513#series","name":"Além do 99 Perdões","url":"https://netshort.com/pt/full-episodes/2082282581191360513","image":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/poster.jpg","description":"Brooke Sterling amou por três anos o marido.","genre":["Romance Urbano","Crescimento Feminino"],"numberOfEpisodes":36,"hasPart":{"@type":"ItemList","numberOfItems":36,"itemListElement":[{"@type":"ListItem","position":1,"item":{"@type":"TVEpisode","name":"EP 1 - Além do 99 Perdões","url":"https://netshort.com/pt/episode/x-2082282581191360513","episodeNumber":1}}]}}]}</script>
            </head><body></body></html>
            """;

        [Fact]
        public async Task SeriesProvider_FillsMetadataFromTheLiveDetailPayload()
        {
            var provider = CreateSeriesProvider();
            var info = new SeriesInfo { Name = "Além do 99 Perdoes" };
            info.ProviderIds[NetShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.NotNull(result.Item);
            Assert.Equal("Além do 99 Perdões", result.Item.Name);
            Assert.StartsWith("Brooke Sterling amou", result.Item.Overview, StringComparison.Ordinal);
            Assert.Equal(SeriesId, result.Item.GetProviderId(NetShortSeriesProvider.ProviderKey));

            // shortPlayLabels keys become genres, and publishTime the premiere date.
            Assert.Equal(3, result.Item.Genres.Length);
            Assert.Contains("Romance Urbano", result.Item.Genres);
            Assert.Equal(new DateTime(2026, 8, 2, 4, 28, 21, 602, DateTimeKind.Utc), result.Item.PremiereDate!.Value);
            Assert.Equal(2026, result.Item.ProductionYear);
        }

        [Fact]
        public async Task SeriesProvider_SearchesByTitleWhenNoIdIsKnown()
        {
            var provider = CreateSeriesProvider();

            var results = (await provider.GetSearchResults(new SeriesInfo { Name = "perdões" }, CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("Além do 99 Perdões", only.Name);
            Assert.Equal(SeriesId, only.GetProviderId(NetShortSeriesProvider.ProviderKey));
            Assert.Contains("awscover.netshort.com", only.ImageUrl, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SeriesProvider_ResolvesAPastedUrlWithoutSearching()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);
            var provider = CreateSeriesProvider(client);

            var info = new SeriesInfo { Name = "https://netshort.com/pt/hotseries/além-do-99-perdões-" + SeriesId };
            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("Além do 99 Perdões", only.Name);
            Assert.Equal(SeriesId, only.GetProviderId(NetShortSeriesProvider.ProviderKey));

            // A pasted id short-circuits the keyword search entirely.
            Assert.DoesNotContain(handler.RequestedUrls, url => url.Contains("/search/", StringComparison.Ordinal));
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataWhenTheSearchIsEmpty()
        {
            using var handler = new FakeNetShortHandler(request =>
                request.RequestUri!.AbsolutePath.Contains("/search/", StringComparison.Ordinal)
                    ? Json("{\"code\":200,\"msg\":\"操作成功\",\"data\":{\"list\":[],\"totalHits\":0}}")
                    : DefaultResponder(request));
            using var client = CreateClient(handler);
            var provider = CreateSeriesProvider(client);

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Chuva Negra" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_DerivesTheLocalizedTitleFromTheEpisodeNumber()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Além do 99 Perdões\\Season 01\\e01.strm",
                IndexNumber = 1
            };
            info.SeriesProviderIds[NetShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 1", result.Item.Name);
        }

        [Fact]
        public async Task EpisodeProvider_LeavesTheRunTimeAloneBecauseNetShortDoesNotReportOne()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo { Path = "N:\\Series\\x\\Season 01\\e02.strm", IndexNumber = 2 };
            info.SeriesProviderIds[NetShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            // An invented duration would be worse than none, and the payload carries no duration.
            Assert.True(result.HasMetadata);
            Assert.Equal("Episódio 2", result.Item.Name);
            Assert.Null(result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSeriesHasNoNetShortId()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo { Path = "N:\\Series\\Outra\\Season 01\\e01.strm", IndexNumber = 1 };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingForAnEpisodeBeyondTheList()
        {
            var provider = CreateEpisodeProvider();
            var info = new EpisodeInfo { Path = "N:\\Series\\x\\Season 01\\e99.strm", IndexNumber = 99 };
            info.SeriesProviderIds[NetShortSeriesProvider.ProviderKey] = SeriesId;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task SeriesImageProvider_OffersTheCoverTheApiReturned()
        {
            var series = new Series();
            series.SetProviderId(NetShortSeriesProvider.ProviderKey, SeriesId);
            var provider = CreateImageProvider();

            var images = (await provider.GetImages(series, CancellationToken.None)).ToList();

            // The CDN only serves the transform variants the site itself requested, so the URL is
            // passed through untouched rather than rewritten to a size that answers 404.
            var only = Assert.Single(images);
            Assert.Equal(ImageType.Primary, only.Type);
            Assert.EndsWith("3比4jpg~tplv-vod-noop.image", only.Url, StringComparison.Ordinal);
        }

        [Fact]
        public async Task EpisodeImageProvider_UsesTheEpisodeThumbnailOfTheParentSeries()
        {
            var seriesId = Guid.NewGuid();
            var series = new Series { Id = seriesId };
            series.SetProviderId(NetShortSeriesProvider.ProviderKey, SeriesId);

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemById(seriesId)).Returns(series);

            var episode = new Episode { SeriesId = seriesId, IndexNumber = 1 };
            var provider = CreateImageProvider(libraryManager.Object);

            var images = (await provider.GetImages(episode, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Contains("o880UN7RekvTvphEQCQhyPTEaIImtAA54AL1OI", only.Url, StringComparison.Ordinal);
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

        [Fact]
        public async Task Client_EncryptsTheRequestBodyWithTheCapturedKey()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);

            // The same query and page size the captured session used.
            await client.SearchAsync("perdões", 10, CancellationToken.None);

            // The search body is the one from the capture, so encrypting it has to reproduce the
            // ciphertext the site itself sent. Nothing short of the exact key, mode, padding and
            // JSON encoding produces this value.
            Assert.Contains(
                "K7nWS3lANYn5aY4vRG3IudeQSHX6ZO5zjdDeOBrlRjhSh+8DVWqQTr62TboJWfA9UC0mdr2iKkwAIeg92qLe7oMhicEc/S0YwhytSlyyOPs=",
                handler.RequestBodies);
        }

        [Fact]
        public async Task Client_SendsThePortugueseContentLanguage()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);

            await client.SearchAsync("perdões", 10, CancellationToken.None);

            // Regression guard: the live API answers an English catalogue to a Portuguese query when
            // this header is missing, and it is easy to lose because HttpRequestHeaders refuses it.
            Assert.NotEmpty(handler.ContentLanguageHeaders);
            Assert.All(handler.ContentLanguageHeaders, language => Assert.Equal("pt_PT", language));
        }

        [Fact]
        public async Task Client_ReadsTheEncryptedVisitorLoginResponse()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);

            await client.SearchAsync("perdões", 10, CancellationToken.None);

            // The search only succeeds because the client unwrapped the captured login response and
            // replayed its token. The login call sends no Authorization header, so the single
            // recorded header is the one the search used.
            var authorization = Assert.Single(handler.AuthorizationHeaders);
            Assert.Equal("Bearer " + CapturedToken, authorization);
        }

        [Fact]
        public async Task Client_FallsBackToTheRenderedPageWhenTheApiRefuses()
        {
            using var handler = new FakeNetShortHandler(request =>
                (request.Method == HttpMethod.Post)
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : Html(JsonLdPage));
            using var client = CreateClient(handler);

            var series = await client.GetSeriesAsync(SeriesId, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal("Além do 99 Perdões", series!.Name);
            Assert.Equal(36, series.EpisodeCount);
            Assert.Contains("awscover.netshort.com", series.Cover, StringComparison.Ordinal);

            // The page lists only the first slice of episodes and carries no thumbnails.
            var only = Assert.Single(series.Episodes);
            Assert.Equal(1, only.Number);
            Assert.Empty(only.Cover);
        }

        [Fact]
        public async Task Client_StopsCallingNetShortOnceItIsRefusing()
        {
            using var handler = new FakeNetShortHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            using var client = CreateClient(handler);

            // The first series pays for the failure and arms the quiet period.
            Assert.Null(await client.GetSeriesAsync(SeriesId, CancellationToken.None));
            var afterFirst = handler.RequestedUrls.Count;
            Assert.True(afterFirst > 0);

            // Every later series must cost nothing at all, which is the whole point of the cooldown:
            // without it, a refused site writes one warning line per item.
            Assert.Null(await client.GetSeriesAsync("2082282581271052289", CancellationToken.None));
            Assert.Equal(afterFirst, handler.RequestedUrls.Count);
        }

        [Fact]
        public async Task Client_DoesNotArmTheCooldownForASeriesThatSimplyDoesNotExist()
        {
            // A 404 page and a business-level API code mean "not found", not "the site is down".
            using var handler = new FakeNetShortHandler(request =>
                (request.Method == HttpMethod.Post)
                    ? Json("{\"code\":500,\"msg\":\"not found\",\"data\":null}")
                    : new HttpResponseMessage(HttpStatusCode.NotFound));
            using var client = CreateClient(handler);

            Assert.Null(await client.GetSeriesAsync(SeriesId, CancellationToken.None));
            var afterFirst = handler.RequestedUrls.Count;

            Assert.Null(await client.GetSeriesAsync("2082282581271052289", CancellationToken.None));
            Assert.True(handler.RequestedUrls.Count > afterFirst, "A missing series must not silence the source.");
        }

        [Fact]
        public async Task Client_CachesASeriesSoTheSameIdIsFetchedOnce()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);

            var first = await client.GetSeriesAsync(SeriesId, CancellationToken.None);
            var second = await client.GetSeriesAsync(SeriesId, CancellationToken.None);

            Assert.NotNull(first);
            Assert.Same(first, second);

            var detailCalls = handler.RequestedUrls.Count(url => url.Contains("detail_info", StringComparison.Ordinal));
            Assert.Equal(1, detailCalls);
        }

        [Fact]
        public async Task Client_ResolvesATitleThroughTheSearchAndScoresIt()
        {
            using var handler = new FakeNetShortHandler(DefaultResponder);
            using var client = CreateClient(handler);

            var exact = await client.MatchByTitleAsync("Além do 99 Perdões", 0.92, CancellationToken.None);
            Assert.NotNull(exact);
            Assert.Equal(SeriesId, exact!.Series.SeriesId);
            Assert.Equal(1.0, exact.Score);

            // A title the catalogue does not carry must not be forced onto the top hit.
            Assert.Null(await client.MatchByTitleAsync("Chuva Negra", 0.92, CancellationToken.None));
        }

        private static HttpResponseMessage DefaultResponder(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("visitor_login", StringComparison.Ordinal))
            {
                // The real captured response: encrypted body plus the encrypt-key header that makes
                // it readable.
                var response = Json(LoginResponseBody);
                response.Headers.TryAddWithoutValidation("encrypt-key", LoginResponseKeyHeader);
                return response;
            }

            if (path.Contains("/search/", StringComparison.Ordinal))
            {
                return Json(SearchEnvelope);
            }

            if (path.Contains("detail_info", StringComparison.Ordinal))
            {
                return Json(DetailEnvelope);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
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

        private static NetShortSeriesProvider CreateSeriesProvider(NetShortClient? client = null)
        {
            return new NetShortSeriesProvider(
                client ?? CreateClient(new FakeNetShortHandler(DefaultResponder)),
                CreateHttpClientFactory(new FakeNetShortHandler(DefaultResponder)),
                new Mock<ILogger<NetShortSeriesProvider>>().Object);
        }

        private static NetShortEpisodeProvider CreateEpisodeProvider()
        {
            return new NetShortEpisodeProvider(
                CreateClient(new FakeNetShortHandler(DefaultResponder)),
                CreateHttpClientFactory(new FakeNetShortHandler(DefaultResponder)),
                new Mock<ILogger<NetShortEpisodeProvider>>().Object);
        }

        private static NetShortImageProvider CreateImageProvider(ILibraryManager? libraryManager = null)
        {
            return new NetShortImageProvider(
                CreateClient(new FakeNetShortHandler(DefaultResponder)),
                libraryManager ?? new Mock<ILibraryManager>().Object,
                CreateHttpClientFactory(new FakeNetShortHandler(DefaultResponder)),
                new Mock<ILogger<NetShortImageProvider>>().Object);
        }

        private static NetShortClient CreateClient(FakeNetShortHandler handler)
        {
            return new NetShortClient(
                CreateHttpClientFactory(handler),
                new Mock<ILogger<NetShortClient>>().Object);
        }

        private static IHttpClientFactory CreateHttpClientFactory(FakeNetShortHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(clientFactory => clientFactory.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler, disposeHandler: false));

            return factory.Object;
        }

        /// <summary>
        /// Routes every call through one delegate and records what was sent, so a test can assert on
        /// the wire rather than only on the result.
        /// </summary>
        private sealed class FakeNetShortHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public FakeNetShortHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            public List<string> RequestedUrls { get; } = new List<string>();

            public List<string> RequestBodies { get; } = new List<string>();

            public List<string> AuthorizationHeaders { get; } = new List<string>();

            public List<string> ContentLanguageHeaders { get; } = new List<string>();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestedUrls.Add(request.RequestUri!.ToString());

                if (request.Headers.Authorization is not null)
                {
                    AuthorizationHeaders.Add(request.Headers.Authorization.ToString());
                }

                // Content-Language is a content header, which is exactly why it is recorded from the
                // content here: if it ever moves back to request.Headers, .NET drops it silently and
                // the API searches the wrong locale, with no error anywhere.
                if (request.Content is not null
                    && request.Content.Headers.TryGetValues("Content-Language", out var languages))
                {
                    ContentLanguageHeaders.AddRange(languages);
                }

                if (request.Content is not null)
                {
                    RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                }

                return _responder(request);
            }
        }
    }
}

