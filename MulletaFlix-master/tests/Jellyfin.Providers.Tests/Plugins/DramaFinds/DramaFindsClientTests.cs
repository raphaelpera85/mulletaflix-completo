using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.DramaFinds;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaFinds
{
    /// <summary>
    /// Exercises the client against the captured DramaFinds payloads, with the HTTP call served by
    /// a fake handler. This is the closest thing to an end-to-end run that does not require the
    /// network or a live server.
    /// </summary>
    public sealed class DramaFindsClientTests
    {
        private const string SearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":10,"total":2,"pages":1,"size":2,"list":[
              {"dramaId":47218,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo.","thumbnailUrl":"https://static.flareflow.tv/covers/47218.jpg?auth_key=1790122207-0-0-aa","rating":9.3,"episodes":52,"releaseDate":"2025-12-24"},
              {"dramaId":47011,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo.","thumbnailUrl":"https://static.flareflow.tv/covers/47011.jpg?auth_key=1790122205-0-0-bb","rating":8.0,"episodes":52,"releaseDate":"2025-12-22"}
            ]},"error_code":"00000"}
            """;

        private const string DetailJson47011 = """
            {"msg":"success","code":0,"data":{"dramaId":"47011","title":"99 Amuletos, 99 Desilusões","totalCount":52,"episodes":null,"thumbnailUrl":"https://static.flareflow.tv/covers/47011.jpg?auth_key=1790122205-0-0-bb","description":"Lilah perdeu tudo.","rating":8.0,"releaseDate":"2025-12-22","tags":null},"error_code":"00000"}
            """;

        private const string DetailJson47218 = """
            {"msg":"success","code":0,"data":{"dramaId":"47218","title":"99 Amuletos, 99 Desilusões","totalCount":52,"episodes":null,"thumbnailUrl":"https://static.flareflow.tv/covers/47218.jpg?auth_key=1790122207-0-0-aa","description":"Lilah perdeu tudo.","rating":9.3,"releaseDate":"2025-12-24","tags":null},"error_code":"00000"}
            """;

        private const string EmptySearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":10,"total":0,"pages":0,"size":0,"list":[]},"error_code":"00000"}
            """;

        private const string GuestIdMissingJson = """
            {"code":10,"errorCode":"00000","msg":"GuestId Missing"}
            """;

        /// <summary>
        /// The guest identity is part of the wire contract: without the header the API answers
        /// {"code":10,"msg":"GuestId Missing"} and every lookup silently returns nothing.
        /// </summary>
        [Fact]
        public void GuestIdentity_IsEightCharactersShapedLikeTheSiteGeneratesIt()
        {
            Assert.Equal(8, DramaFindsClient.GuestIdentity.Length);
            Assert.Matches("^[0-9]{4}[a-z0-9]{4}$", DramaFindsClient.GuestIdentity);
        }

        [Fact]
        public async Task SearchAsync_SendsTheHeadersTheApiRequires()
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(SearchJson, captured);

            await client.SearchAsync("99 Amuletos", 10, CancellationToken.None);

            var single = Assert.Single(captured);
            var request = single.Request;

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.dramafinds.com/api/v1/short-dramas/search", request.RequestUri!.ToString());
            Assert.Equal(DramaFindsClient.GuestIdentity, Header(request, "Guest-Id"));
            Assert.Equal("pt", Header(request, "X-Language"));
            Assert.Equal("https://dramafinds.com", Header(request, "Origin"));
            Assert.Equal("https://dramafinds.com/pt/", Header(request, "Referer"));
            Assert.Contains("Mozilla", Header(request, "User-Agent"), StringComparison.Ordinal);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);

            Assert.Contains("\"keyword\":\"99 Amuletos\"", single.Body, StringComparison.Ordinal);
            Assert.Contains("\"pageNum\":1", single.Body, StringComparison.Ordinal);
            Assert.Contains("\"pageSize\":10", single.Body, StringComparison.Ordinal);
        }

        /// <summary>
        /// The API lists 47011 first even though 47218 is the newer edition of the same title. Both
        /// score 1.0 against the exact query, so the deterministic tie-break has to decide rather
        /// than inheriting the row order.
        /// </summary>
        [Fact]
        public async Task SearchAsync_RanksBySimilarityInsteadOfTrustingTheApiRowOrder()
        {
            using var client = CreateClient(SearchJson, new List<CapturedRequest>());

            var matches = await client.SearchAsync("99 Amuletos, 99 Desilusões", 10, CancellationToken.None);

            Assert.Equal(2, matches.Count);
            Assert.All(matches, match => Assert.Equal(1.0, match.Score));
            Assert.Equal(new[] { "47218", "47011" }, matches.Select(match => match.Drama.DramaId).ToArray());
        }

        /// <summary>
        /// The endpoint is fuzzy by substring, so a query can return rows that are not the title at
        /// all. Those are discarded rather than offered or auto-identified.
        /// </summary>
        [Fact]
        public async Task SearchAsync_DiscardsFuzzyRowsThatAreNotTheQueriedTitle()
        {
            const string FuzzyJson = """
                {"msg":"success","code":0,"data":{"list":[
                  {"dramaId":67991,"title":"Traído e Caçado pela Própria Agência","episodes":60,"releaseDate":"2025-11-01"},
                  {"dramaId":177195,"title":"Cazado por mi propia agencia","episodes":40,"releaseDate":"2025-10-01"}
                ]},"error_code":"00000"}
                """;

            using var client = CreateClient(FuzzyJson, new List<CapturedRequest>());

            var matches = await client.SearchAsync("A Agência", 10, CancellationToken.None);

            Assert.Empty(matches);
        }

        /// <summary>
        /// A search that matched nothing is an ordinary answer on this platform, not a failure, so
        /// it must not arm the cooldown.
        /// </summary>
        [Fact]
        public async Task SearchAsync_ReturnsEmptyWithoutArmingTheCooldownWhenNothingMatched()
        {
            using var client = CreateClient(EmptySearchJson, new List<CapturedRequest>());

            var matches = await client.SearchAsync("Abandonei o Rei dos Deuses no Altar", 10, CancellationToken.None);

            Assert.Empty(matches);
            Assert.False(client.IsInFailureCooldown);
        }

        /// <summary>
        /// The API reports its own refusals with HTTP 200 and a non-zero envelope code, so the
        /// cooldown has to be armed from the body rather than from the transport status. Once armed
        /// a library-wide run must stop costing round trips.
        /// </summary>
        [Fact]
        public async Task SearchAsync_ArmsTheCooldownAfterTheApiRefusesAndStopsCalling()
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(GuestIdMissingJson, captured);

            var first = await client.SearchAsync("99 Amuletos", 10, CancellationToken.None);

            Assert.Empty(first);
            Assert.True(client.IsInFailureCooldown);
            Assert.Single(captured);

            var second = await client.SearchAsync("Outro Título", 10, CancellationToken.None);

            Assert.Empty(second);
            Assert.Single(captured);
        }

        [Fact]
        public async Task SearchAsync_ArmsTheCooldownOnATransportFailure()
        {
            using var client = CreateClient(handler: (_, _) => throw new HttpRequestException("connection refused"));

            var matches = await client.SearchAsync("99 Amuletos", 10, CancellationToken.None);

            Assert.Empty(matches);
            Assert.True(client.IsInFailureCooldown);
        }

        [Fact]
        public async Task GetDramaAsync_SynthesizesEpisodesAndStripsTheSignedCover()
        {
            using var client = CreateClient(DetailJson47011, new List<CapturedRequest>());

            var drama = await client.GetDramaAsync("47011", CancellationToken.None);

            Assert.NotNull(drama);
            Assert.Equal("47011", drama!.DramaId);
            Assert.Equal(52, drama.Episodes.Count);
            Assert.Equal("Episódio 1", drama.Episodes[0].Name);
            Assert.Equal("https://static.flareflow.tv/covers/47011.jpg", drama.Cover);
        }

        [Fact]
        public async Task GetDramaAsync_CachesSoAnEpisodeRunDoesNotRefetchTheDrama()
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(DetailJson47011, captured);

            await client.GetDramaAsync("47011", CancellationToken.None);
            await client.GetDramaAsync("47011", CancellationToken.None);

            Assert.Single(captured);
        }

        /// <summary>
        /// A pasted public URL or a bare id resolves exactly, so it never pays for a fuzzy search
        /// and never risks being answered with a different drama.
        /// </summary>
        [Theory]
        [InlineData("47011")]
        [InlineData("https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilusões/episode-2")]
        public async Task SearchAsync_ResolvesAnExactIdentifierWithoutSearching(string query)
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(DetailJson47011, captured);

            var matches = await client.SearchAsync(query, 10, CancellationToken.None);

            var only = Assert.Single(matches);
            Assert.Equal("47011", only.Drama.DramaId);
            Assert.Equal(1.0, only.Score);

            var single = Assert.Single(captured);
            Assert.EndsWith("/v1/short-dramas/episodes", single.Request.RequestUri!.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// The detail endpoint rejects an encrypted payload, so the id has to leave as the plain
        /// string the site itself sends.
        /// </summary>
        [Fact]
        public async Task GetDramaAsync_SendsTheRawUnencryptedBody()
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(DetailJson47011, captured);

            await client.GetDramaAsync("47011", CancellationToken.None);

            var single = Assert.Single(captured);
            Assert.Equal("https://api.dramafinds.com/api/v1/short-dramas/episodes", single.Request.RequestUri!.ToString());
            Assert.Contains("\"dramaId\":\"47011\"", single.Body, StringComparison.Ordinal);
            Assert.Contains("\"indexE\":0", single.Body, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task GetDramaAsync_ReturnsNullForAnEmptyId(string? dramaId)
        {
            var captured = new List<CapturedRequest>();
            using var client = CreateClient(DetailJson47011, captured);

            Assert.Null(await client.GetDramaAsync(dramaId, CancellationToken.None));
            Assert.Empty(captured);
        }

        [Fact]
        public void ResolveDramaId_DelegatesToTheMatcher()
        {
            Assert.Equal("47011", DramaFindsClient.ResolveDramaId("47011"));
            Assert.Null(DramaFindsClient.ResolveDramaId("99 Amuletos, 99 Desilusões"));
        }

        /// <summary>
        /// Reads a header the client sent. User-Agent is a structured header, so its single raw
        /// value comes back split into one entry per product token.
        /// </summary>
        private static string Header(HttpRequestMessage request, string name)
        {
            Assert.True(request.Headers.TryGetValues(name, out var values), $"Missing header {name}.");
            return string.Join(' ', values);
        }

        /// <summary>
        /// Routes the two endpoints to their captured payloads, so a test that resolves an id
        /// exercises the detail call and a test that searches exercises the search call. The body is
        /// read while the request is still alive: the client disposes its request once the call
        /// returns.
        /// </summary>
        private static DramaFindsClient CreateClient(string searchResponse, List<CapturedRequest> captured)
        {
            return CreateClient(
                handler: (request, _) =>
                {
                    var body = request.Content is null
                        ? string.Empty
                        : request.Content.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();

                    captured.Add(new CapturedRequest(request, body));

                    if (!request.RequestUri!.AbsolutePath.EndsWith("/episodes", StringComparison.Ordinal))
                    {
                        return Json(searchResponse);
                    }

                    return Json(body.Contains("\"47218\"", StringComparison.Ordinal) ? DetailJson47218 : DetailJson47011);
                });
        }

        private static DramaFindsClient CreateClient(
            Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            var messageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            messageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(handler);

            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(messageHandler.Object));

            return new DramaFindsClient(factory.Object, new Mock<ILogger<DramaFindsClient>>().Object);
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
        }

        /// <summary>
        /// What the client actually put on the wire, captured before it disposes the request.
        /// </summary>
        private sealed class CapturedRequest
        {
            public CapturedRequest(HttpRequestMessage request, string body)
            {
                Request = request;
                Body = body;
            }

            public HttpRequestMessage Request { get; }

            public string Body { get; }
        }
    }
}
