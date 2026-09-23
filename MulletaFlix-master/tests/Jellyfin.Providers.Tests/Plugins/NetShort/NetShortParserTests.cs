using System;
using System.Linq;
using System.Text.Json.Nodes;
using MediaBrowser.Providers.Plugins.NetShort;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NetShort
{
    /// <summary>
    /// Exercises the NetShort parsers against real payloads.
    /// </summary>
    /// <remarks>
    /// Both fixtures are trimmed copies of live responses — the episode list keeps its first three
    /// entries out of thirty-six — but every field name, value and character is exactly what the
    /// platform returned. The JSON-LD fixture comes from the rendered page for the same series.
    /// </remarks>
    public class NetShortParserTests
    {
        /// <summary>
        /// The live response to /web/v4/short_play/detail_info/cascade_label for series
        /// 2082282581191360513 ("Além do 99 Perdões"), with the episode list trimmed to three.
        /// </summary>
        private const string DetailEnvelope = """
            {"code":200,"msg":"操作成功","data":{"shortPlayId":"2082282581191360513","shortPlayName":"Além do 99 Perdões","shortPlayUrl":"/pt/drama/além-do-99-perdões-2082282581191360513","shortPlayDubAddress":null,"shortPlayAddress":null,"fullEpisodeNameUrl":"/pt/full-episodes/além-do-99-perdões-2082282581191360513","shortPlayCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/1785491610380-3885058237326381-3比4jpg~tplv-vod-noop.image","shortPlayLibraryId":"2082282580876787713","shortPlayLabels":{"Reencontro de Ex":"/pt/drama/Reencontro%20de%20Ex-1983832091805343750","Crescimento Feminino":"/pt/drama/Crescimento%20Feminino-1983832092950388745","Romance Urbano":"/pt/drama/Romance%20Urbano-1983832092736479240"},"labelIds":["1983832092736479240","1983832092950388745","1983832091805343750"],"shotIntroduce":"Brooke Sterling amou por três anos o marido, o campeão de boxe Mason Masters, enquanto ele tratava o casamento como um jogo para provocar a ex. A cada decepção, ela colocava uma pérola num pote. Quando colocou a 99ª, pediu o divórcio e foi embora de vez. Só ao vê-la feliz com Sullivan Brooks, Mason entendeu o que perdeu. Ele implorou no casamento dela por outra chance, mas Brooke escolheu quem sempre a amou. Mason aprendeu tarde que o amor se perde de decepção em decepção.","totalLikeNums":"2.1K","totalChaseNums":"2.3K","videoEpisodeInfos":[{"episodeId":"2082403374911545346","episodeNo":1,"isLock":false,"episodeCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/o880UN7RekvTvphEQCQhyPTEaIImtAA54AL1OI~tplv-vod-noop.image"},{"episodeId":"2082403374898962440","episodeNo":2,"isLock":false,"episodeCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/oAI0T13QeAOXbT03ZGQIjt7gOjazsNCwaIM7wh~tplv-vod-noop.image"},{"episodeId":"2082403375100289031","episodeNo":3,"isLock":false,"episodeCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/o46l7TGNTOB0IIAw1TQ77tteQCgThOojANCIaV~tplv-vod-noop.image"}],"language":"pt_PT","viewAllReviewsUrl":"/pt/reviews/2082282581191360513","isDelisted":false,"publishTime":1785644901602}}
            """;

        /// <summary>
        /// The live response to /web/short_play/search/keyword/seo for the term "perdões", trimmed
        /// from twelve hits to the first two.
        /// </summary>
        private const string SearchEnvelope = """
            {"code":200,"msg":"操作成功","data":{"list":[{"shortPlayLibraryId":"2082282580876787713","shortPlayId":"2082282581191360513","shortPlayNameNoHL":"Além do 99 Perdões","shortPlayNameUrl":"/pt/episode/além-do-99-perdões-2082282581191360513","shortPlayCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/1785491610380-3885058237326381-3比4jpg~tplv-vod-rs:651:868.webp","shortPlayName":"Além do 99 <span style='color: #F5315E'>Perdões</span>","shotIntroduce":"Brooke Sterling amou por três anos o marido, o campeão de boxe Mason Masters, enquanto ele tratava o casamento como um jogo para provocar a ex. A cada decepção, ela colocava uma pérola num pote. Quando colocou a 99ª, pediu o divórcio e foi embora de vez. Só ao vê-la feliz com Sullivan Brooks, Mason entendeu o que perdeu. Ele implorou no casamento dela por outra chance, mas Brooke escolheu quem sempre a amou. Mason aprendeu tarde que o amor se perde de decepção em decepção.","labelNames":"Crescimento Feminino,Reencontro de Ex,Romance Urbano","likeNums":"2.8K","chaseNums":"2.3K"},{"shortPlayLibraryId":"1927679873165230081","shortPlayId":"1927680500222066690","shortPlayNameNoHL":"Perdão, Até Quando?","shortPlayNameUrl":"/pt/episode/perdão-até-quando-1927680500222066690","shortPlayCover":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/prod/1927679873165230081/1749109758350-9131657715863193-3比4.jpg~tplv-vod-rs:651:868.webp","shortPlayName":"Perdão, Até Quando?","shotIntroduce":"Leon Dantas, um advogado brilhante, abandona uma carreira promissora por amor e ajuda Lívia Gonçalves a montar um pequeno escritório.","labelNames":"Retorno do Poderoso,Arrependimento,Vida Urbana","likeNums":"4.7K","chaseNums":"3.8K"}],"totalHits":10000}}
            """;

        /// <summary>
        /// The JSON-LD block of the rendered page at
        /// https://netshort.com/pt/full-episodes/2082282581191360513, with the episode ItemList
        /// trimmed to three of the twenty pages it lists.
        /// </summary>
        private const string JsonLdPage = """
            <!DOCTYPE html><html lang="pt"><head><title>Além do 99 Perdões</title>
            <script id="json-ld" type="application/ld+json">{"@context":"https://schema.org","@graph":[{"@type":"BreadcrumbList","@id":"https://netshort.com/pt/full-episodes/2082282581191360513#breadcrumb","itemListElement":[{"@type":"ListItem","position":1,"name":"Início","item":"https://netshort.com/pt/"},{"@type":"ListItem","position":2,"name":"Dramas Épicos","item":"https://netshort.com/pt/all-episodes"},{"@type":"ListItem","position":3,"name":"Além do 99 Perdões","item":"https://netshort.com/pt/full-episodes/2082282581191360513"}]},{"@type":"TVSeries","@id":"https://netshort.com/pt/full-episodes/2082282581191360513#series","name":"Além do 99 Perdões","url":"https://netshort.com/pt/full-episodes/2082282581191360513","image":"https://awscover.netshort.com/tos-vod-mya-v-da59d5a2040f5f77/imageG/production/2082282580876787713/1785491610380-3885058237326381-3%E6%AF%944jpg~tplv-vod-noop.image","description":"Brooke Sterling amou por três anos o marido, o campeão de boxe Mason Masters, enquanto ele tratava o casamento como um jogo para provocar a ex. A cada decepção, ela colocava uma pérola num pote. Quando colocou a 99ª, pediu o divórcio e foi embora de vez. Só ao vê-la feliz com Sullivan Brooks, Mason entendeu o que perdeu. Ele implorou no casamento dela por outra chance, mas Brooke escolheu quem sempre a amou. Mason aprendeu tarde que o amor se perde de decepção em decepção.","genre":["Romance Urbano","Crescimento Feminino","Reencontro de Ex"],"numberOfEpisodes":36,"inLanguage":"pt-PT","productionCompany":{"@type":"Organization","name":"NetShort"},"potentialAction":{"@type":"WatchAction","target":"https://netshort.com/pt/episode/al%C3%A9m-do-99-perd%C3%B5es-2082282581191360513"},"hasPart":{"@type":"ItemList","numberOfItems":36,"itemListElement":[{"@type":"ListItem","position":1,"item":{"@type":"TVEpisode","name":"EP 1 - Além do 99 Perdões","url":"https://netshort.com/pt/episode/al%C3%A9m-do-99-perd%C3%B5es-2082282581191360513","episodeNumber":1,"partOfSeries":{"@id":"https://netshort.com/pt/full-episodes/2082282581191360513#series"}}},{"@type":"ListItem","position":2,"item":{"@type":"TVEpisode","name":"EP 2 - Além do 99 Perdões","url":"https://netshort.com/pt/episode/al%C3%A9m-do-99-perd%C3%B5es-2082282581191360513-ep-2","episodeNumber":2,"partOfSeries":{"@id":"https://netshort.com/pt/full-episodes/2082282581191360513#series"}}},{"@type":"ListItem","position":3,"item":{"@type":"TVEpisode","name":"EP 3 - Além do 99 Perdões","url":"https://netshort.com/pt/episode/al%C3%A9m-do-99-perd%C3%B5es-2082282581191360513-ep-3","episodeNumber":3,"partOfSeries":{"@id":"https://netshort.com/pt/full-episodes/2082282581191360513#series"}}}]}}]}</script>
            </head><body></body></html>
            """;

        [Fact]
        public void ParseResultCode_ReadsTheEnvelopeStatus()
        {
            Assert.Equal(200, NetShortParser.ParseResultCode(JsonNode.Parse(DetailEnvelope)));
            Assert.Equal("操作成功", NetShortParser.ParseResultMessage(JsonNode.Parse(DetailEnvelope)));

            // A payload that is not an envelope must not look like a success.
            Assert.Equal(-1, NetShortParser.ParseResultCode(null));
            Assert.Equal(-1, NetShortParser.ParseResultCode(JsonNode.Parse("{\"unexpected\":true}")));
        }

        [Fact]
        public void ParseSearchResults_ReadsEveryFieldTheSeriesProviderNeeds()
        {
            var results = NetShortParser.ParseSearchResults(JsonNode.Parse(SearchEnvelope));

            Assert.Equal(2, results.Count);

            var first = results[0];
            Assert.Equal("2082282581191360513", first.SeriesId);
            Assert.Equal("Além do 99 Perdões", first.Name);
            Assert.Equal("/pt/episode/além-do-99-perdões-2082282581191360513", first.PagePath);
            Assert.Contains("awscover.netshort.com", first.Cover, StringComparison.Ordinal);
            Assert.StartsWith("Brooke Sterling amou", first.Overview, StringComparison.Ordinal);
            Assert.Equal(new[] { "Crescimento Feminino", "Reencontro de Ex", "Romance Urbano" }, first.Genres);

            // A search hit carries no episodes and no release date; only the detail payload does.
            Assert.Empty(first.Episodes);
            Assert.Null(first.PremiereDate);

            Assert.Equal("1927680500222066690", results[1].SeriesId);
            Assert.Equal("Perdão, Até Quando?", results[1].Name);
            Assert.Equal(new[] { "Retorno do Poderoso", "Arrependimento", "Vida Urbana" }, results[1].Genres);
        }

        [Fact]
        public void ParseSearchResults_StripsTheHighlightMarkupFromTheTitle()
        {
            // The highlighted field carries a <span> around the matched term; the plain field is
            // preferred, and when it is absent the markup is removed rather than published as a title.
            var withoutPlainField = JsonNode.Parse(
                "{\"code\":200,\"data\":{\"list\":[{\"shortPlayId\":\"2082282581191360513\",\"shortPlayName\":\"Além do 99 <span style='color: #F5315E'>Perdões</span>\"}]}}");

            var series = Assert.Single(NetShortParser.ParseSearchResults(withoutPlainField));

            Assert.Equal("Além do 99 Perdões", series.Name);
        }

        [Fact]
        public void ParseSeriesDetail_ReadsTheSeriesAndItsEpisodes()
        {
            var series = NetShortParser.ParseSeriesDetail(JsonNode.Parse(DetailEnvelope));

            Assert.NotNull(series);
            Assert.Equal("2082282581191360513", series!.SeriesId);
            Assert.Equal("Além do 99 Perdões", series.Name);
            Assert.Equal("/pt/drama/além-do-99-perdões-2082282581191360513", series.PagePath);
            Assert.Contains("3比4jpg", series.Cover, StringComparison.Ordinal);
            Assert.Equal(3, series.Episodes.Count);
            Assert.Equal(3, series.EpisodeCount);
            Assert.True(series.HasEpisodes);

            // publishTime is the epoch in milliseconds, which is what the platform sends.
            Assert.Equal(new DateTime(2026, 8, 2, 4, 28, 21, 602, DateTimeKind.Utc), series.PremiereDate!.Value);

            // shortPlayLabels is an object on the detail endpoint; the keys are the genres.
            Assert.Equal(3, series.Genres.Count);
            Assert.Contains("Romance Urbano", series.Genres);
        }

        [Fact]
        public void ParseSeriesDetail_ReadsOneBasedEpisodeNumbersAndCovers()
        {
            var series = NetShortParser.ParseSeriesDetail(JsonNode.Parse(DetailEnvelope));

            Assert.NotNull(series);

            // NetShort numbers episodes from one, unlike DramaBox, so no adjustment is needed.
            Assert.Equal(new[] { 1, 2, 3 }, series!.Episodes.Select(episode => episode.Number));
            Assert.Equal("2082403374911545346", series.Episodes[0].EpisodeId);
            Assert.Contains("o880UN7RekvTvphEQCQhyPTEaIImtAA54AL1OI", series.Episodes[0].Cover, StringComparison.Ordinal);
            Assert.False(series.Episodes[0].Locked);
        }

        [Fact]
        public void ParseSeriesDetail_ReturnsNullWhenThePayloadIsNotASeries()
        {
            Assert.Null(NetShortParser.ParseSeriesDetail(null));
            Assert.Null(NetShortParser.ParseSeriesDetail(JsonNode.Parse("{\"code\":200,\"data\":null}")));
            Assert.Null(NetShortParser.ParseSeriesDetail(JsonNode.Parse("{\"code\":500,\"msg\":\"error\"}")));
        }

        [Fact]
        public void ParseJsonLdSeries_ReadsTheRenderedPage()
        {
            var series = NetShortParser.ParseJsonLdSeries(JsonLdPage);

            Assert.NotNull(series);
            Assert.Equal("2082282581191360513", series!.SeriesId);
            Assert.Equal("Além do 99 Perdões", series.Name);
            Assert.Equal(36, series.EpisodeCount);
            Assert.Equal(
                new[] { "Romance Urbano", "Crescimento Feminino", "Reencontro de Ex" },
                series.Genres);
            Assert.Contains("awscover.netshort.com", series.Cover, StringComparison.Ordinal);
            Assert.StartsWith("Brooke Sterling amou", series.Overview, StringComparison.Ordinal);

            // The rendered ItemList is paginated, so it carries fewer episodes than numberOfEpisodes.
            Assert.Equal(new[] { 1, 2, 3 }, series.Episodes.Select(episode => episode.Number));

            // The fallback carries no per-episode thumbnails, which is why the API is preferred.
            Assert.All(series.Episodes, episode => Assert.Empty(episode.Cover));
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html><body>no payload here</body></html>")]
        [InlineData("<script id=\"json-ld\" type=\"application/ld+json\">not json</script>")]
        [InlineData("<script id=\"json-ld\" type=\"application/ld+json\">{\"@graph\":[{\"@type\":\"BreadcrumbList\"}]}</script>")]
        public void ParseJsonLdSeries_ReturnsNullInsteadOfThrowing(string html)
        {
            Assert.Null(NetShortParser.ParseJsonLdSeries(html));
        }
    }
}
