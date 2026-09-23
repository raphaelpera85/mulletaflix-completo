using System.Linq;
using MediaBrowser.Providers.Plugins.DramaFinds;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaFinds
{
    public class DramaFindsParserTests
    {
        /// <summary>
        /// Faithful copy of the live search payload for "99 Amuletos, 99 Desilusões", captured from
        /// POST /v1/short-dramas/search. Note the two rows: the platform keeps two ids (47011 and
        /// 47218) for the same title, with different release dates and ratings, and both have to
        /// survive parsing so the caller can offer them.
        /// </summary>
        private const string SearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":10,"total":2,"pages":1,"size":2,"list":[
              {"md5Id":"e0ecebf7-5655-42f4-8fa0-3dc28da97eb0","dramaId":47218,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","thumbnailUrl":"https://static.flareflow.tv/images/cover/2026/08/27/3d29141b01e74cc7abe85e8a65c2068e.jpg?auth_key=1790122207-0-0-67db105417544c7b7ec112a37f1f8b1c","rating":9.3,"likesCount":9982,"viewsCount":12505,"sourceId":5,"episodes":52,"releaseDate":"2025-12-24"},
              {"md5Id":"cddc0874-ae3d-4627-b574-1dbab1ad67b5","dramaId":47011,"country":"pt","title":"99 Amuletos, 99 Desilusões","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz.","thumbnailUrl":"https://static.flareflow.tv/images/cover/2026/08/19/257a729035714b5ea33e4c6c963f6ba8.jpg?auth_key=1790122205-0-0-3baa2feceb8ceb65e263121cb57003a4","rating":8.0,"likesCount":17954,"viewsCount":2132,"sourceId":5,"episodes":52,"releaseDate":"2025-12-22"}
            ]},"error_code":"00000","error_message":null,"error_trace":null,"user_tip":null}
            """;

        /// <summary>
        /// Faithful copy of the live detail payload for drama 47011, captured from
        /// POST /v1/short-dramas/episodes with {"dramaId":"47011","indexE":0}. The fields that shape
        /// the code are all here: totalCount carries the episode count while "episodes" is null,
        /// tags is null, thumbnailUrl is signed, and mayLikeList is unrelated noise.
        /// </summary>
        private const string DetailJson47011 = """
            {"msg":"success","code":0,"data":{"dramaId":"47011","title":"99 Amuletos, 99 Desilusões","totalCount":52,"episodes":null,"thumbnailUrl":"https://static.flareflow.tv/images/cover/2026/08/19/257a729035714b5ea33e4c6c963f6ba8.jpg?auth_key=1790122205-0-0-3baa2feceb8ceb65e263121cb57003a4","description":"Lilah perdeu tudo — o amor, a carreira e a própria voz. Mas quando um produtor famoso entra em cena, ela descobre que fingir pode ser o primeiro passo.","rating":8.0,"viewsCount":2132,"likesCount":17954,"freeCount":8,"sourceId":5,"cooperate":1,"country":"pt","md5Id":"cddc0874-ae3d-4627-b574-1dbab1ad67b5","isTrailer":0,"hasPlayUrl":0,"isDub":0,"exclusiveType":0,"isCooperate":1,"isAuthorized":1,"isFreePlayable":1,"isApiPlay":0,"isSelfStored":1,"seriesId":null,"releaseDate":"2025-12-22","tags":null,"mayLikeList":[{"md5Id":"c07c5f07-cadd-4f53-acb3-a5bc642b5244","dramaId":48964,"country":"pt","title":"De Herdeira Perdida a Rainha do Crime","rating":null,"totalCount":null}],"seoTitle":"99 Amuletos, 99 Desilusões | Assistir Todos os Episódios Grátis"},"error_code":"00000","error_message":null,"error_trace":null,"user_tip":null}
            """;

        /// <summary>
        /// The live answer for a search that genuinely matched nothing. "Abandonei o Rei dos Deuses
        /// no Altar" only exists on other platforms, and DramaFinds answers code 0 with an empty
        /// list, which is a normal outcome rather than a failure.
        /// </summary>
        private const string EmptySearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":10,"total":0,"pages":0,"size":0,"list":[]},"error_code":"00000","error_message":null,"error_trace":null,"user_tip":null}
            """;

        /// <summary>
        /// The live answer when the Guest-Id header is missing. Note the transport status is 200.
        /// </summary>
        private const string GuestIdMissingJson = """
            {"code":10,"errorCode":"00000","msg":"GuestId Missing"}
            """;

        /// <summary>
        /// The live rows returned for the query "A Agência", which are not that title at all. The
        /// search endpoint is fuzzy by substring, so these are what a provider that trusted the
        /// first row would identify the wrong series with.
        /// </summary>
        private const string FuzzySearchJson = """
            {"msg":"success","code":0,"data":{"pageNum":1,"pageSize":10,"total":2,"pages":1,"size":2,"list":[
              {"dramaId":67991,"country":"pt","title":"Traído e Caçado pela Própria Agência","description":"a","thumbnailUrl":"https://static.flareflow.tv/x.jpg?auth_key=1-0-0-a","rating":7.0,"episodes":60,"releaseDate":"2025-11-01"},
              {"dramaId":177195,"country":"pt","title":"Cazado por mi propia agencia","description":"b","thumbnailUrl":"https://static.flareflow.tv/y.jpg?auth_key=2-0-0-b","rating":6.0,"episodes":40,"releaseDate":"2025-10-01"}
            ]},"error_code":"00000"}
            """;

        [Fact]
        public void ParseEnvelope_ReturnsNullInsteadOfThrowing()
        {
            Assert.Null(DramaFindsParser.ParseEnvelope(null));
            Assert.Null(DramaFindsParser.ParseEnvelope(string.Empty));
            Assert.Null(DramaFindsParser.ParseEnvelope("<html>not json</html>"));
        }

        [Fact]
        public void ReadCode_ReportsTheApiFailureThatArrivesWithHttp200()
        {
            var envelope = DramaFindsParser.ParseEnvelope(GuestIdMissingJson);

            Assert.NotNull(envelope);
            Assert.Equal(10, DramaFindsParser.ReadCode(envelope));
            Assert.Equal("GuestId Missing", DramaFindsParser.ReadMessage(envelope));
            Assert.False(DramaFindsParser.IsSuccess(envelope));
        }

        [Fact]
        public void ParseSearchResults_ReadsEveryFieldTheProvidersNeed()
        {
            var results = DramaFindsParser.ParseSearchResults(DramaFindsParser.ParseEnvelope(SearchJson));

            Assert.Equal(2, results.Count);

            var drama = results[1];
            Assert.Equal("47011", drama.DramaId);
            Assert.Equal("cddc0874-ae3d-4627-b574-1dbab1ad67b5", drama.Md5Id);
            Assert.Equal("99 Amuletos, 99 Desilusões", drama.Title);
            Assert.Equal("pt", drama.Country);
            Assert.Equal(8.0, drama.Rating);
            Assert.Equal(52, drama.EpisodeCount);
            Assert.Equal("2025-12-22", drama.ReleaseDate);
            Assert.Equal(17954, drama.LikesCount);
            Assert.Equal(2132, drama.ViewsCount);
            Assert.StartsWith("Lilah perdeu tudo", drama.Overview, System.StringComparison.Ordinal);

            // A search row carries no episode list: the API reports the count only.
            Assert.Empty(drama.Episodes);
        }

        [Fact]
        public void ParseSearchResults_KeepsEveryDuplicateIdForTheSameTitle()
        {
            var results = DramaFindsParser.ParseSearchResults(DramaFindsParser.ParseEnvelope(SearchJson));

            // Both rows share the title and the episode count but not the id, the rating or the
            // release date. Dropping either one would silently pick an edition for the user.
            Assert.Equal(2, results.Count);
            Assert.All(results, drama => Assert.Equal("99 Amuletos, 99 Desilusões", drama.Title));
            Assert.Equal(new[] { "47218", "47011" }, results.Select(drama => drama.DramaId).ToArray());
            Assert.Equal(new[] { 9.3, 8.0 }, results.Select(drama => drama.Rating ?? 0).ToArray());
        }

        [Fact]
        public void ParseSearchResults_ReturnsEmptyForASearchThatMatchedNothing()
        {
            var envelope = DramaFindsParser.ParseEnvelope(EmptySearchJson);

            Assert.True(DramaFindsParser.IsSuccess(envelope));
            Assert.Empty(DramaFindsParser.ParseSearchResults(envelope));
        }

        [Fact]
        public void ParseDramaDetail_ReadsTheDramaAndItsEpisodeCount()
        {
            var drama = DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(DetailJson47011));

            Assert.NotNull(drama);
            Assert.Equal("47011", drama!.DramaId);
            Assert.Equal("99 Amuletos, 99 Desilusões", drama.Title);
            Assert.Equal(52, drama.EpisodeCount);
            Assert.Equal("2025-12-22", drama.ReleaseDate);
            Assert.Equal(8.0, drama.Rating);
            Assert.Equal(8, drama.FreeCount);
            Assert.Equal(52, drama.Episodes.Count);

            // The platform reports tags as null on every payload seen, so nothing is invented.
            Assert.Empty(drama.Tags);
        }

        [Fact]
        public void ParseDramaDetail_StripsTheExpiringAuthKeyFromTheCover()
        {
            var drama = DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(DetailJson47011));

            Assert.NotNull(drama);
            Assert.DoesNotContain("auth_key", drama!.Cover, System.StringComparison.Ordinal);
            Assert.DoesNotContain("?", drama.Cover, System.StringComparison.Ordinal);
            Assert.Equal(
                "https://static.flareflow.tv/images/cover/2026/08/19/257a729035714b5ea33e4c6c963f6ba8.jpg",
                drama.Cover);
        }

        /// <summary>
        /// The API never returns the episode list, so every episode has to be synthesized. The name
        /// is what the platform's own episode pages publish as og:title, and the URL is the only
        /// per-episode identifier it has.
        /// </summary>
        [Fact]
        public void ParseDramaDetail_SynthesizesEveryEpisodeFromOneToTheCount()
        {
            var drama = DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(DetailJson47011));

            Assert.NotNull(drama);
            var episodes = drama!.Episodes;

            Assert.Equal(52, episodes.Count);
            Assert.Equal(Enumerable.Range(1, 52).ToArray(), episodes.Select(episode => episode.Number).ToArray());

            Assert.Equal("Episódio 1", episodes[0].Name);
            Assert.Equal("Episódio 2", episodes[1].Name);
            Assert.Equal("Episódio 52", episodes[51].Name);

            Assert.Equal(
                "https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilus%C3%B5es/episode-1",
                episodes[0].Url);
            Assert.Equal(
                "https://dramafinds.com/pt/video-play/47011/99-amuletos-99-desilus%C3%B5es/episode-52",
                episodes[51].Url);
        }

        [Fact]
        public void ParseDramaDetail_ReturnsNullWhenThePayloadHasNoData()
        {
            Assert.Null(DramaFindsParser.ParseDramaDetail(null));
            Assert.Null(DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(GuestIdMissingJson)));
            Assert.Null(DramaFindsParser.ParseDramaDetail(DramaFindsParser.ParseEnvelope(EmptySearchJson)));
        }

        [Fact]
        public void BuildEpisodes_ProducesNothingWhenThePayloadReportsNoEpisodes()
        {
            var drama = new DramaFindsDrama { DramaId = "1", Title = "Sem Episódios", EpisodeCount = 0 };

            Assert.Empty(DramaFindsParser.BuildEpisodes(drama));
        }

        /// <summary>
        /// Guards the fuzzy-search contract the providers depend on: the rows the endpoint returns
        /// for a shared word have to score below the candidate threshold, so a caller that ranks and
        /// filters them never identifies the wrong series.
        /// </summary>
        [Fact]
        public void ParseSearchResults_FuzzyRowsScoreBelowTheCandidateThreshold()
        {
            var results = DramaFindsParser.ParseSearchResults(DramaFindsParser.ParseEnvelope(FuzzySearchJson));

            Assert.Equal(2, results.Count);
            Assert.All(
                results,
                drama => Assert.True(
                    DramaFindsTitleMatcher.Similarity(drama.Title, "A Agência") < DramaFindsTitleMatcher.MinimumCandidateScore));
        }
    }
}
