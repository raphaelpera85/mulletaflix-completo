using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Providers.Plugins.NartoDrama;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NartoDrama
{
    /// <summary>
    /// Exercises the parser against the real NartoDrama markup.
    /// </summary>
    /// <remarks>
    /// The two fixtures are trimmed copies of the pages the live site served for
    /// <c>abandonei-o-rei-dos-deuses-no-altar</c> and for <c>/search?q=abandonei</c>: the JSON-LD
    /// blocks, the metadata block, the episode anchors and the search cards are byte for byte what
    /// came off the wire, and only the unrelated navigation, ads and scripts were dropped. A change
    /// in the shape NartoDrama serves therefore shows up here as a failing assertion rather than as
    /// a silently empty synopsis in the library.
    /// </remarks>
    public class NartoDramaParserTests
    {
        private const string SeriesHtml = """
            <!DOCTYPE html><html lang="pt-PT"><head>
                <meta charset="UTF-8">
                <title>Abandonei o Rei dos Deuses no Altar - Streaming grátis</title>
                <meta name="nd-current-movie-id" content="91437">
                <meta property="og:image" content="https://img.nartodrama-api.online/poster/91437.jpg">
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"TVSeries","name":"Abandonei o Rei dos Deuses no Altar - Streaming grátis","description":"Abandonei o Rei dos Deuses no Altar. No décimo ano ao lado de Aetheon, subi ao altar que ele havia erguido para mim no cume do Monte Olimpo. O casamento era digno do R. Narto Drama - Assistir Short Dramas, Filmes e Anime grátis de provedores BiliTV, CubeTV, Shortical, DotDrama, DotDrama II, Dramabite, Dramabox, DramaWave, e mais","url":"https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT","image":"https://img.nartodrama-api.online/poster/91437.jpg","inLanguage":"pt_PT"}</script>
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Início","item":"https://narto-drama.com?lang=pt-PT"},{"@type":"ListItem","position":2,"name":"Abandonei o Rei dos Deuses no Altar","item":"https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT"}]}</script>
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Organization","name":"Narto Drama","alternateName":"NartoDrama","url":"https://narto-drama.com","logo":"https://narto-drama.com/narto-drama-logo-compressed.png","image":"https://narto-drama.com/narto-drama-logo-compressed.png","description":"Narto Drama é uma plataforma de streaming grátis de dramas curtos, drama reels, dramas chineses (C-drama), dramas coreanos (K-drama), dramas japoneses (J-drama), filmes, séries e anime, com legendas e dublagem.","sameAs":["https://www.facebook.com/people/NartoDrama/61588554265983/","https://www.youtube.com/@NartoDrama","https://www.tiktok.com/@narto.drama","https://t.me/nartodrama"]}</script>
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"ItemList","name":"Narto Drama Navigation","itemListElement":[{"@type":"SiteNavigationElement","position":1,"name":"Início","url":"https://narto-drama.com?lang=pt-PT"},{"@type":"SiteNavigationElement","position":2,"name":"Filmes e Séries","url":"https://narto-drama.com?lang=pt-PT&type=movie-series"},{"@type":"SiteNavigationElement","position":3,"name":"Anime","url":"https://narto-drama.com?lang=pt-PT&type=anime"},{"@type":"SiteNavigationElement","position":4,"name":"Destaque","url":"https://narto-drama.com/featured?lang=pt-PT"}]}</script>
                <style>
                    .episode-item {
                        border: 1px solid #e2e6ef;
                        border-radius: 999px;
                        padding: 8px 12px;
                        text-decoration: none;
                        color: #202533;
                        background: #fff;
                        font-size: 13px;
                    }
                    .episode-item:hover {
                        border-color: #ff4d6d;
                        background: #ffe8ee;
                    }
                    @media (max-width: 980px) {
                        .episode-item {
                            width: 100%;
                            min-width: 0;
                            padding: 8px 6px;
                            text-align: center;
                            justify-content: center;
                        }
                    }
                </style>
            </head><body>
            <main class="container">
                <p class="breadcrumb">Início / Detalhe / Abandonei o Rei dos Deuses no Altar</p>
                <section class="layout">
                    <article class="surface movie-info">
                        <div class="movie-meta">
                            <img class="poster" src="https://img.nartodrama-api.online/poster/91437.jpg" alt="Abandonei o Rei dos Deuses no Altar" width="300" height="400" loading="eager">
                            <div>
                                <h1 class="movie-title">Abandonei o Rei dos Deuses no Altar</h1>
                                <p class="movie-sub">
                                                                <!-- Playable: 37 | Total DB: 37 -->
                                         37 Episódios
                                                        </p>
                                <div class="movie-tags">
                                    <a class="movie-tag-pill" href="https://narto-drama.com/tag/disturbio?lang=pt-PT">#Distúrbio</a>
                                    <a class="movie-tag-pill" href="https://narto-drama.com/tag/lamento?lang=pt-PT">#Lamento</a>
                                    <a class="movie-tag-pill" href="https://narto-drama.com/tag/casamento?lang=pt-PT">#Casamento</a>
                                    <a class="movie-tag-pill" href="https://narto-drama.com/tag/nobreza?lang=pt-PT">#Nobreza</a>
                                </div>
                                <div class="movie-desc">No décimo ano ao lado de Aetheon, subi ao altar que ele havia erguido para mim no cume do Monte Olimpo. O casamento era digno do Rei dos Deuses... grandioso, perfeito, sufocante. A luz dourava o Olimpo, os cálices brilhavam, e todos esperavam ver o momento em que eu enfim me tornaria sua rainha.
            Então Aetheon ergueu a taça, entediado, e confessou que havia me traído pouco antes da cerimônia. Com uma mortal.
            — Continue o rito ou acabe com tudo agora. Você decide.
            O mundo gelou ao meu redor. Perguntei se ele a amava tanto assim. Aetheon apenas franziu o cenho, como se a minha dor fosse uma falta de elegância. Para ele, ela era só uma mortal frágil, uma distração passageira. Eu, impecável por dez anos, havia me tornado perfeita demais para ainda ser desejada.
            Girando o Anel do Trovão no dedo, ele me ofereceu a coroa como se ela pudesse apagar a humilhação.
            — Se escolher seguir com a cerimônia,, ainda será minha rainha. Se quiser fazer uma cena, faça. Não vou impedir.
            Fiquei imóvel diante do altar, esmagada pelo esplendor daquele casamento sem amor. Esperei dez anos para vestir uma coroa. Mas, naquele instante, entendi que havia coroas feitas para reinar. E outras, para aprisionar.</div>
                            </div>
                        </div>
                    </article>
                    <aside class="episode-panel">
                        <h2>Episódios (37)</h2>
                        <p class="episode-note">Assistir episódio 1</p>
                        <div class="episode-list">
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/1?lang=pt-PT" title="001"> EP 1 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/2?lang=pt-PT" title="002"> EP 2 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/3?lang=pt-PT" title="003"> EP 3 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/4?lang=pt-PT" title="004"> EP 4 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/5?lang=pt-PT" title="005"> EP 5 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/6?lang=pt-PT" title="006"> EP 6 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/7?lang=pt-PT" title="007"> EP 7 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/8?lang=pt-PT" title="008"> EP 8 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/9?lang=pt-PT" title="009"> EP 9 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/10?lang=pt-PT" title="010"> EP 10 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/11?lang=pt-PT" title="011"> EP 11 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/12?lang=pt-PT" title="012"> EP 12 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/13?lang=pt-PT" title="013"> EP 13 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/14?lang=pt-PT" title="014"> EP 14 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/15?lang=pt-PT" title="015"> EP 15 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/16?lang=pt-PT" title="016"> EP 16 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/17?lang=pt-PT" title="017"> EP 17 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/18?lang=pt-PT" title="018"> EP 18 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/19?lang=pt-PT" title="019"> EP 19 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/20?lang=pt-PT" title="020"> EP 20 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/21?lang=pt-PT" title="021"> EP 21 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/22?lang=pt-PT" title="022"> EP 22 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/23?lang=pt-PT" title="023"> EP 23 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/24?lang=pt-PT" title="024"> EP 24 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/25?lang=pt-PT" title="025"> EP 25 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/26?lang=pt-PT" title="026"> EP 26 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/27?lang=pt-PT" title="027"> EP 27 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/28?lang=pt-PT" title="028"> EP 28 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/29?lang=pt-PT" title="029"> EP 29 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/30?lang=pt-PT" title="030"> EP 30 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/31?lang=pt-PT" title="031"> EP 31 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/32?lang=pt-PT" title="032"> EP 32 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/33?lang=pt-PT" title="033"> EP 33 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/34?lang=pt-PT" title="034"> EP 34 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/35?lang=pt-PT" title="035"> EP 35 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/36?lang=pt-PT" title="036"> EP 36 </a>
                            <a class="episode-item" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/37?lang=pt-PT" title="037"> EP 37 </a>
                        </div>
                        <a class="watch-first" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar/1?lang=pt-PT">Assistir episódio 1</a>
                    </aside>
                </section>
            </main>
            <script>
                var currentEpCount = 37;
                var _epGateKey = 'nd_epcheck_v1_' + String(91437);
            </script>
            </body></html>

            """;

        private const string SearchHtml = """
            <!DOCTYPE html><html lang="pt-PT"><head><title>Resultados da pesquisa | Narto Drama</title></head><body>
            <main class="container">
                <section class="watch-history-rail" id="watch-history-rail" hidden aria-label="Histórico">
                    <div class="watch-history-slick" id="watch-history-slick"></div>
                </section>
                <div class="search-results-summary" id="search-results-summary">Resultados da pesquisa <strong>"abandonei"</strong></div>
                <div class="search-provider-loading" id="search-provider-loading"><span class="loading-spinner" aria-hidden="true"></span><span class="loading-text">Carregando...</span></div>
                <section class="grid provider-results" id="search-results-grid">
                    <article class="card provider-search-card" tabindex="0" role="link" data-watch-url="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT&amp;from=home" data-search-score="0" data-search-title="Abandonei o Rei dos Deuses no Altar" data-search-description="No décimo ano ao lado de Aetheon, subi ao altar que ele havia erguido para mim no cume do Monte Olimpo. O casamento era digno do Rei dos Deuses... grandioso, perfeito, su...">
                    <a class="card-link-overlay" href="https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT&amp;from=home" target="_blank" rel="noopener noreferrer">Abandonei o Rei dos Deuses no Altar</a>
                    <article class="card provider-search-card" tabindex="0" role="link" data-watch-url="https://narto-drama.com/detail/watch/abandonei-minha-familia-e-virei-milionaria?lang=pt-PT&amp;from=home" data-search-score="0" data-search-title="Abandonei Minha Família e Virei Milionária" data-search-description="Na vida passada, Luana Dias sacrificou tudo pelo pai e pelos três irmãos. Quando a família finalmente alcançou o sucesso, a irmã mais velha voltou e tomou todo o amor da...">
                    <a class="card-link-overlay" href="https://narto-drama.com/detail/watch/abandonei-minha-familia-e-virei-milionaria?lang=pt-PT&amp;from=home" target="_blank" rel="noopener noreferrer">Abandonei Minha Família e Virei Milionária</a>
                    <article class="card provider-search-card" tabindex="0" role="link" data-watch-url="https://narto-drama.com/detail/watch/abandonei-minha-persona-de-esposa-boazinha?lang=pt-PT&amp;from=home" data-search-score="0" data-search-title="Abandonei Minha Persona de Esposa Boazinha" data-search-description="">
                    <a class="card-link-overlay" href="https://narto-drama.com/detail/watch/abandonei-minha-persona-de-esposa-boazinha?lang=pt-PT&amp;from=home" target="_blank" rel="noopener noreferrer">Abandonei Minha Persona de Esposa Boazinha</a>
                </section>
            </main>
            <script>
                var html = '<article class="card provider-search-card' + (isAdult ? ' adult-restricted' : '') + '" tabindex="0" role="link" data-watch-url="' + escapeHtml(watchUrl) + '">';
            </script>
            </body></html>

            """;

        [Fact]
        public void ParseJsonLdBlocks_ReadsEveryBlockOfTheDetailPage()
        {
            var blocks = NartoDramaParser.ParseJsonLdBlocks(SeriesHtml);

            // The page ships four: TVSeries, BreadcrumbList, Organization and a navigation ItemList.
            Assert.Equal(4, blocks.Count);
            Assert.Equal("TVSeries", blocks[0]!["@type"]!.ToString());
            Assert.Equal("BreadcrumbList", blocks[1]!["@type"]!.ToString());
            Assert.Equal("Organization", blocks[2]!["@type"]!.ToString());
            Assert.Equal("ItemList", blocks[3]!["@type"]!.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html><body>no payload here</body></html>")]
        [InlineData("<script type=\"application/ld+json\">not json</script>")]
        public void ParseJsonLdBlocks_ReturnsNothingInsteadOfThrowing(string html)
        {
            Assert.Empty(NartoDramaParser.ParseJsonLdBlocks(html));
        }

        /// <summary>
        /// The regression this parser exists for: the site publishes the library title with a
        /// localized marketing marker glued to it, and the clean title is what belongs in the library.
        /// </summary>
        [Fact]
        public void ParseSeriesFromJsonLd_StripsTheStreamingSuffixFromTheTitle()
        {
            var series = NartoDramaParser.ParseSeriesFromJsonLd(SeriesHtml);

            Assert.NotNull(series);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", series!.Name);
        }

        [Fact]
        public void ParseSeriesFromJsonLd_ReadsTheCoverAndTheCanonicalSlug()
        {
            var series = NartoDramaParser.ParseSeriesFromJsonLd(SeriesHtml);

            Assert.NotNull(series);
            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", series!.Cover);
            Assert.Equal("abandonei-o-rei-dos-deuses-no-altar", series.Slug);
        }

        /// <summary>
        /// The JSON-LD description is the only synopsis on part of the catalogue, and the site wraps
        /// it in the title in front and its own platform pitch behind.
        /// </summary>
        [Fact]
        public void ParseSeriesFromJsonLd_CleansTheSynopsisOfTheTitlePrefixAndThePlatformPitch()
        {
            var series = NartoDramaParser.ParseSeriesFromJsonLd(SeriesHtml);

            Assert.NotNull(series);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", series!.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("Abandonei o Rei dos Deuses no Altar.", series.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("Narto Drama", series.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("BiliTV", series.Overview, StringComparison.Ordinal);
        }

        /// <summary>
        /// The homepage boilerplate the site pastes in when it has no synopsis must not be published
        /// as one; it describes the platform, not the series.
        /// </summary>
        [Fact]
        public void CleanDescription_DropsThePlatformPlaceholder()
        {
            const string Placeholder = "Plataforma gratuita para dramas curtos e mini séries com legendas em português no Narto Drama.";
            var description = "(Sulih suara) Dunia Game Ini, Aku yang Berkuasa. " + Placeholder
                + " Narto Drama - Assistir Short Dramas, Filmes e Anime grátis de provedores BiliTV, CubeTV.";

            var result = NartoDramaParser.CleanDescription(description, "(Sulih suara) Dunia Game Ini, Aku yang Berkuasa");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void CleanDescription_KeepsARealSynopsisAndDropsTheTrailingPitch()
        {
            var result = NartoDramaParser.CleanDescription(
                "Coração à Conquista. Para se vingar do marido, ela tentou ficar com um rapaz. Narto Drama - Assistir Short Dramas gratuitamente.",
                "Coração à Conquista");

            Assert.Equal("Para se vingar do marido, ela tentou ficar com um rapaz.", result);
        }

        /// <summary>
        /// A 404 is a definitive answer for a slug, and the client keys its failure cooldown off the
        /// fetch outcome, so it must not be reported as a failure. Verified live: an unknown slug
        /// answers 404 while a real one answers 200.
        /// </summary>
        [Fact]
        public void ParseSeries_ReturnsNullForAPageWithNothingRecognizable()
        {
            Assert.Null(NartoDramaParser.ParseSeries("<html><body>nothing here</body></html>", "qualquer-coisa"));
            Assert.Null(NartoDramaParser.ParseSeries(string.Empty, "qualquer-coisa"));
            Assert.Null(NartoDramaParser.ParseSeries(null, "qualquer-coisa"));
        }

        [Fact]
        public void ParseSeries_PrefersTheCleanSynopsisBlockOverTheJsonLdDescription()
        {
            var series = NartoDramaParser.ParseSeries(SeriesHtml, "abandonei-o-rei-dos-deuses-no-altar");

            Assert.NotNull(series);

            // div.movie-desc carries the full synopsis with neither the title prefix nor the pitch.
            Assert.StartsWith("No décimo ano ao lado de Aetheon", series!.Overview, StringComparison.Ordinal);
            Assert.Contains("outras, para aprisionar.", series.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("Narto Drama -", series.Overview, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseSeries_ReadsTheTitleSlugSynopsisAndTags()
        {
            var series = NartoDramaParser.ParseSeries(SeriesHtml, "abandonei-o-rei-dos-deuses-no-altar");

            Assert.NotNull(series);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", series!.Name);
            Assert.Equal("abandonei-o-rei-dos-deuses-no-altar", series.Slug);
            Assert.Equal(new[] { "Distúrbio", "Lamento", "Casamento", "Nobreza" }, series.Tags);
        }

        [Fact]
        public void ParseTags_ReadsThePillsWithoutTheirHash()
        {
            Assert.Equal(new[] { "Distúrbio", "Lamento", "Casamento", "Nobreza" }, NartoDramaParser.ParseTags(SeriesHtml));
            Assert.Empty(NartoDramaParser.ParseTags("<html></html>"));
        }

        [Fact]
        public void ParseContentId_ReadsTheMetaTag()
        {
            Assert.Equal("91437", NartoDramaParser.ParseContentId(SeriesHtml));
        }

        [Fact]
        public void ParseContentId_FallsBackToTheEpisodeCheckScript()
        {
            const string Html = "<html><body><script>var _epGateKey = 'nd_epcheck_v1_' + String(91437);</script></body></html>";

            Assert.Equal("91437", NartoDramaParser.ParseContentId(Html));
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html><body>no id anywhere</body></html>")]
        public void ParseContentId_ReturnsEmptyWhenThePageExposesNone(string html)
        {
            Assert.Equal(string.Empty, NartoDramaParser.ParseContentId(html));
        }

        /// <summary>
        /// The episode list is the whole reason the detail page is the only request the provider
        /// needs: it carries all 37 episodes, and the page states the same total for itself.
        /// </summary>
        [Fact]
        public void ParseEpisodes_ReadsEveryOneOfTheThirtySevenAnchors()
        {
            var episodes = NartoDramaParser.ParseEpisodes(SeriesHtml);

            Assert.Equal(37, episodes.Count);
            Assert.Equal(1, episodes[0].Number);
            Assert.Equal("001", episodes[0].Label);
            Assert.Equal(37, episodes[36].Number);
            Assert.Equal("037", episodes[36].Label);
            Assert.Equal(episodes.Select(episode => episode.Number).Distinct().Count(), episodes.Count);

            // Ordered by number, which is how the provider indexes into it.
            Assert.Equal(Enumerable.Range(1, 37), episodes.Select(episode => episode.Number));
        }

        [Fact]
        public void ParseEpisodes_IgnoresTheCssRulesAndTheWatchFirstButton()
        {
            // The class name appears three more times in the page's stylesheet, and the page also
            // links episode 1 from a "watch first" button that is not part of the list.
            Assert.Equal(40, CountOccurrences(SeriesHtml, "episode-item"));
            Assert.Equal(37, NartoDramaParser.ParseEpisodes(SeriesHtml).Count);
        }

        [Fact]
        public void ParseEpisodeCount_ReadsTheScriptVariable()
        {
            Assert.Equal(37, NartoDramaParser.ParseEpisodeCount(SeriesHtml));
            Assert.Equal(0, NartoDramaParser.ParseEpisodeCount("<html></html>"));
        }

        [Fact]
        public void ParseSeries_FallsBackToTheAnchorCountWhenThePageStatesNoTotal()
        {
            var html = """
                <html><head><script type="application/ld+json">{"@type":"TVSeries","name":"Outra Série - Streaming grátis"}</script></head>
                <body><a class="episode-item" href="https://narto-drama.com/detail/watch/outra-serie/1?lang=pt-PT" title="001">EP 1</a>
                <a class="episode-item" href="https://narto-drama.com/detail/watch/outra-serie/2?lang=pt-PT" title="002">EP 2</a></body></html>
                """;

            var series = NartoDramaParser.ParseSeries(html, "outra-serie");

            Assert.NotNull(series);
            Assert.Equal(2, series!.EpisodeCount);
            Assert.Equal(2, series.Episodes.Count);
            Assert.True(series.EpisodesAreComplete);
            Assert.Equal("Outra Série", series.Name);
        }

        [Fact]
        public void ParseSeries_FallsBackToTheHeadingWhenTheJsonLdIsAbsent()
        {
            var html = """
                <html><head><meta name="nd-current-movie-id" content="777">
                <meta property="og:image" content="https://img.nartodrama-api.online/poster/777.webp"></head>
                <body><h1 class="movie-title">Título Limpo</h1><div class="movie-desc">Sinopse limpa.</div></body></html>
                """;

            var series = NartoDramaParser.ParseSeries(html, "titulo-limpo");

            Assert.NotNull(series);
            Assert.Equal("Título Limpo", series!.Name);
            Assert.Equal("Sinopse limpa.", series.Overview);
            Assert.Equal("https://img.nartodrama-api.online/poster/777.webp", series.Cover);
            Assert.Equal("777", series.ContentId);
        }

        [Fact]
        public void ParseSeries_FallsBackToThePosterUrlBuiltFromTheContentId()
        {
            var html = """
                <html><head><meta name="nd-current-movie-id" content="91437"></head>
                <body><h1 class="movie-title">Sem Imagem</h1></body></html>
                """;

            var series = NartoDramaParser.ParseSeries(html, "sem-imagem");

            Assert.NotNull(series);
            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", series!.Cover);
        }

        [Theory]
        [InlineData("91437", "https://img.nartodrama-api.online/poster/91437.jpg")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("abc", "")]
        [InlineData(null, "")]
        public void BuildPosterUrl_BuildsTheCdnUrlOnlyForANumericId(string? contentId, string expected)
        {
            Assert.Equal(expected, NartoDramaParser.BuildPosterUrl(contentId));
        }

        [Fact]
        public void ParseSearchResults_ReadsTheRenderedCardsInTheOrderTheSiteRankedThem()
        {
            var results = NartoDramaParser.ParseSearchResults(SearchHtml);

            Assert.Equal(3, results.Count);
            Assert.Equal("abandonei-o-rei-dos-deuses-no-altar", results[0].Slug);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", results[0].Name);
            Assert.Contains("subi ao altar", results[0].Overview, StringComparison.Ordinal);

            // The accented title survives, and it is the displayed text rather than the slug.
            Assert.Equal("abandonei-minha-familia-e-virei-milionaria", results[1].Slug);
            Assert.Equal("Abandonei Minha Família e Virei Milionária", results[1].Name);
            Assert.Equal("abandonei-minha-persona-de-esposa-boazinha", results[2].Slug);
        }

        /// <summary>
        /// The page's own JavaScript builds a search card from a string template. It mentions the
        /// same class and attribute names, so a parser that reads the markup naively would return a
        /// result whose slug is JavaScript source.
        /// </summary>
        [Fact]
        public void ParseSearchResults_IgnoresTheCardTemplateInTheScriptBlock()
        {
            var results = NartoDramaParser.ParseSearchResults(SearchHtml);

            Assert.All(results, result => Assert.DoesNotContain("escapeHtml", result.Slug, StringComparison.Ordinal));
            Assert.All(results, result => Assert.DoesNotContain("'", result.Slug, StringComparison.Ordinal));
            Assert.All(results, result => Assert.DoesNotContain("(", result.Slug, StringComparison.Ordinal));
        }

        [Fact]
        public void ParseSearchResultsFromJsonLd_ReadsTheCollectionPageList()
        {
            const string Html = """
                <html><head><script type="application/ld+json">
                {"@type":"CollectionPage","name":"Resultados para \"abandonei\"","mainEntity":{"@type":"ItemList","itemListElement":[
                {"@type":"ListItem","position":1,"url":"https://narto-drama.com/detail/watch/abandonei-o-rei-dos-deuses-no-altar?lang=pt-PT&from=home","name":"Abandonei o Rei dos Deuses no Altar","image":"https://img.nartodrama-api.online/poster/91437.jpg"}]}}
                </script></head><body></body></html>
                """;

            var results = NartoDramaParser.ParseSearchResultsFromJsonLd(Html);

            var only = Assert.Single(results);
            Assert.Equal("abandonei-o-rei-dos-deuses-no-altar", only.Slug);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", only.Name);
            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", only.Cover);
        }

        /// <summary>
        /// The site also ships an ItemList for its site navigation. Every entry there points at a
        /// menu URL rather than a detail page, which is what keeps it out of the results.
        /// </summary>
        [Fact]
        public void ParseSearchResultsFromJsonLd_IgnoresTheNavigationItemList()
        {
            const string Html = """
                <html><head><script type="application/ld+json">
                {"@type":"ItemList","name":"Narto Drama Navigation","itemListElement":[
                {"@type":"SiteNavigationElement","position":1,"name":"Início","url":"https://narto-drama.com?lang=pt-PT"},
                {"@type":"SiteNavigationElement","position":2,"name":"Anime","url":"https://narto-drama.com?lang=pt-PT&type=anime"}]}
                </script></head><body></body></html>
                """;

            Assert.Empty(NartoDramaParser.ParseSearchResultsFromJsonLd(Html));
        }

        [Fact]
        public void ParseSearchResults_ReturnsNothingForAPageWithNoResults()
        {
            const string Html = "<html><body><div class=\"search-results-summary\">Nenhum resultado</div></body></html>";

            Assert.Empty(NartoDramaParser.ParseSearchResults(Html));
            Assert.Empty(NartoDramaParser.ParseSearchResults(null));
            Assert.Empty(NartoDramaParser.ParseSearchResultsFromJsonLd(null));
        }

        private static int CountOccurrences(string value, string needle)
        {
            var count = 0;
            var index = 0;

            while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}