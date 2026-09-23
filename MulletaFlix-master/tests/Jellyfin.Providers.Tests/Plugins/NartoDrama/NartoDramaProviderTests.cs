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
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Plugins.NartoDrama;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NartoDrama
{
    /// <summary>
    /// Exercises the provider layer against the real NartoDrama pages, with the HTTP calls served by
    /// a fake handler that routes by path. This is the closest thing to an end-to-end run that needs
    /// neither the network nor a live server.
    /// </summary>
    public sealed class NartoDramaProviderTests : IDisposable
    {
        private const string DetailPath = "/detail/watch/abandonei-o-rei-dos-deuses-no-altar";

        private const string Slug = "abandonei-o-rei-dos-deuses-no-altar";

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

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [Fact]
        public async Task SeriesProvider_FillsMetadataFromTheCapturedPage()
        {
            var provider = CreateSeriesProvider(DetailRoutes());
            var info = new SeriesInfo { Name = "Abandonei o Rei dos Deuses no Altar" };
            info.ProviderIds[NartoDramaSeriesProvider.ProviderKey] = Slug;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.NotNull(result.Item);

            // The published title carries " - Streaming grátis"; the library gets the clean one.
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", result.Item.Name);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", result.Item.Overview, StringComparison.Ordinal);
            Assert.DoesNotContain("Streaming grátis", result.Item.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Narto Drama -", result.Item.Overview, StringComparison.Ordinal);

            Assert.Equal(new[] { "Distúrbio", "Lamento", "Casamento", "Nobreza" }, result.Item.Tags);
            Assert.Equal(Slug, result.Item.GetProviderId(NartoDramaSeriesProvider.ProviderKey));
        }

        [Fact]
        public async Task SeriesProvider_LeavesTheReleaseDateToOtherProviders()
        {
            // The detail page carries no date anywhere: no datePublished in its JSON-LD, no time
            // element and no year in the metadata block. A date invented here would be a fabrication.
            var provider = CreateSeriesProvider(DetailRoutes());
            var info = new SeriesInfo { Name = "Abandonei o Rei dos Deuses no Altar" };
            info.ProviderIds[NartoDramaSeriesProvider.ProviderKey] = Slug;

            var item = (await provider.GetMetadata(info, CancellationToken.None)).Item;

            Assert.Null(item.PremiereDate);
            Assert.Null(item.ProductionYear);
            Assert.Null(item.CommunityRating);
        }

        [Fact]
        public async Task SeriesProvider_ResolvesAPastedUrlWithoutSearching()
        {
            var handler = new RoutingHandler(DetailRoutes());
            var provider = CreateSeriesProvider(handler);
            var info = new SeriesInfo
            {
                Name = "https://narto-drama.com/detail/watch/" + Slug + "/37?lang=pt-PT"
            };

            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            var only = Assert.Single(results);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", only.Name);
            Assert.Equal(Slug, only.GetProviderId(NartoDramaSeriesProvider.ProviderKey));
            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", only.ImageUrl);

            // The episode URL was rewritten to the series page rather than followed.
            Assert.Equal(new[] { DetailPath }, handler.Paths);
        }

        [Fact]
        public async Task SeriesProvider_RanksTheSiteCandidatesWithTheLocalMatcher()
        {
            var provider = CreateSeriesProvider(DetailRoutes());
            var info = new SeriesInfo { Name = "Abandonei Minha Família e Virei Milionária" };

            var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

            Assert.Equal(3, results.Count);
            Assert.Equal("abandonei-minha-familia-e-virei-milionaria", results[0].GetProviderId(NartoDramaSeriesProvider.ProviderKey));
        }

        [Fact]
        public async Task SeriesProvider_AutoIdentifiesATitleItIsConfidentAbout()
        {
            var provider = CreateSeriesProvider(DetailRoutes());
            var info = new SeriesInfo { Name = "Abandonei o Rei dos Deuses no Altar" };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", result.Item.Name);
            Assert.Equal(Slug, result.Item.GetProviderId(NartoDramaSeriesProvider.ProviderKey));
        }

        /// <summary>
        /// Measured live: /search answers with fallback hits for a term the catalogue does not
        /// contain, so the first hit is not evidence. Without a threshold this library series would be
        /// renamed to whatever the site listed first.
        /// </summary>
        [Fact]
        public async Task SeriesProvider_RefusesACandidateThatIsNotSimilarEnough()
        {
            var handler = new RoutingHandler(DetailRoutes());
            var provider = CreateSeriesProvider(handler);

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Chuva Negra" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
            Assert.Equal(new[] { "/search" }, handler.Paths);
        }

        [Fact]
        public async Task SeriesProvider_ReturnsNoMetadataWhenTheSearchPageHasNoResults()
        {
            var routes = DetailRoutes();
            routes["/search"] = _ => Html("<html><body><div class=\"search-results-summary\">Sem resultados</div></body></html>");
            var provider = CreateSeriesProvider(new RoutingHandler(routes));

            var result = await provider.GetMetadata(new SeriesInfo { Name = "Chuva Negra" }, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_DerivesTheLocalizedTitleFromTheEpisodeNumber()
        {
            var provider = CreateEpisodeProvider(DetailRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e02.strm",
                IndexNumber = 2
            };
            info.SeriesProviderIds[NartoDramaSeriesProvider.ProviderKey] = Slug;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.True(result.QueriedById);
            Assert.Equal("Episódio 2", result.Item.Name);

            // The page publishes no duration for an episode, so the runtime is left to the media file.
            Assert.Null(result.Item.RunTimeTicks);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingBeyondTheEpisodeList()
        {
            var provider = CreateEpisodeProvider(DetailRoutes());
            var info = new EpisodeInfo
            {
                Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e99.strm",
                IndexNumber = 99
            };
            info.SeriesProviderIds[NartoDramaSeriesProvider.ProviderKey] = Slug;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWhenTheSeriesHasNoNartoDramaId()
        {
            var provider = CreateEpisodeProvider(DetailRoutes());
            var info = new EpisodeInfo { Path = "N:\\Series\\Outra\\Season 01\\e01.strm", IndexNumber = 1 };

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_ReturnsNothingWithoutAnEpisodeNumber()
        {
            var provider = CreateEpisodeProvider(DetailRoutes());
            var info = new EpisodeInfo { Path = "N:\\Series\\Abandonei o Rei dos Deuses no Altar\\Season 01\\e.strm" };
            info.SeriesProviderIds[NartoDramaSeriesProvider.ProviderKey] = Slug;

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
        }

        [Fact]
        public async Task EpisodeProvider_OffersNoSearchResultsOfItsOwn()
        {
            var provider = CreateEpisodeProvider(DetailRoutes());

            var results = await provider.GetSearchResults(
                new EpisodeInfo { Path = "N:\\Series\\Abandonei\\Season 01\\e01.strm" },
                CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task ImageProvider_OffersThePosterThePagePublished()
        {
            var provider = CreateImageProvider(DetailRoutes());
            var series = new Series();
            series.SetProviderId(NartoDramaSeriesProvider.ProviderKey, Slug);

            var images = (await provider.GetImages(series, CancellationToken.None)).ToList();

            var only = Assert.Single(images);
            Assert.Equal(ImageType.Primary, only.Type);
            Assert.Equal("https://img.nartodrama-api.online/poster/91437.jpg", only.Url);
            Assert.Equal(NartoDramaSeriesProvider.ProviderKey, only.ProviderName);
        }

        [Fact]
        public async Task ImageProvider_OffersNothingWithoutASlug()
        {
            var provider = CreateImageProvider(DetailRoutes());

            Assert.Empty(await provider.GetImages(new Series(), CancellationToken.None));
        }

        [Fact]
        public async Task ImageProvider_OffersNothingForAnItemThatIsNotASeries()
        {
            var provider = CreateImageProvider(DetailRoutes());

            Assert.Empty(await provider.GetImages(new Movie(), CancellationToken.None));
        }

        [Fact]
        public void ImageProvider_SupportsSeriesOnly()
        {
            var provider = CreateImageProvider(DetailRoutes());

            Assert.True(provider.Supports(new Series()));
            Assert.False(provider.Supports(new Episode()));
            Assert.False(provider.Supports(new Movie()));
            Assert.Equal(new[] { ImageType.Primary }, provider.GetSupportedImages(new Series()));
        }

        [Fact]
        public void ExternalId_DeclaresTheSeriesProviderKey()
        {
            var externalId = new NartoDramaExternalId();

            Assert.Equal("NartoDrama", externalId.ProviderName);
            Assert.Equal("NartoDrama", externalId.Key);
            Assert.Null(externalId.Type);
            Assert.True(externalId.Supports(new Series()));
            Assert.False(externalId.Supports(new Episode()));
        }

        [Fact]
        public async Task Client_CachesASeriesBetweenProviders()
        {
            var handler = new RoutingHandler(DetailRoutes());
            var client = CreateClient(handler);

            var first = await client.GetSeriesAsync(Slug, CancellationToken.None);
            var second = await client.GetSeriesAsync(Slug, CancellationToken.None);

            Assert.NotNull(first);
            Assert.Same(first, second);

            // The detail page is the only request either provider needs, and it is paid once.
            Assert.Equal(new[] { DetailPath }, handler.Paths);
        }

        [Fact]
        public async Task Client_ReadsAllThirtySevenEpisodesFromTheSinglePage()
        {
            var client = CreateClient(DetailRoutes());

            var series = await client.GetSeriesAsync(Slug, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(37, series!.EpisodeCount);
            Assert.Equal(37, series.Episodes.Count);
            Assert.True(series.EpisodesAreComplete);
            Assert.Equal(37, series.Episodes[36].Number);
            Assert.Equal("037", series.Episodes[36].Label);
        }

        [Fact]
        public async Task Client_RequestsThePinnedLocale()
        {
            var handler = new RoutingHandler(DetailRoutes());
            var client = CreateClient(handler);

            await client.GetSeriesAsync(Slug, CancellationToken.None);
            await client.SearchAsync("abandonei", 3, CancellationToken.None);

            Assert.All(handler.Uris, uri => Assert.Contains("lang=pt-PT", uri, StringComparison.Ordinal));
            Assert.Contains(DetailPath, handler.Paths);
            Assert.Contains("/search", handler.Paths);
        }

        /// <summary>
        /// Verified live: an unknown slug answers 404 while a real one answers 200, so a stale
        /// provider id must not take the whole provider offline for the next half hour.
        /// </summary>
        [Fact]
        public async Task Client_TreatsAMissingSeriesAsAnAnswerRatherThanAnOutage()
        {
            var routes = new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.Ordinal)
            {
                [DetailPath] = _ => Status(HttpStatusCode.NotFound)
            };

            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            Assert.Null(await client.GetSeriesAsync(Slug, CancellationToken.None));

            Assert.False(client.IsInFailureCooldown);
            Assert.Equal(new[] { DetailPath }, handler.Paths);
        }

        [Fact]
        public async Task Client_GoesQuietForTheCooldownAfterAnUnreadablePage()
        {
            var routes = DetailRoutes();
            routes[DetailPath] = _ => Status(HttpStatusCode.ServiceUnavailable);
            routes["/search"] = _ => Status(HttpStatusCode.ServiceUnavailable);

            var handler = new RoutingHandler(routes);
            var client = CreateClient(handler);

            Assert.False(client.IsInFailureCooldown);

            Assert.Empty(await client.SearchAsync("abandonei", 5, CancellationToken.None));
            Assert.True(client.IsInFailureCooldown);

            var requestsAfterFirstFailure = handler.Paths.Count;

            // Everything after that must be answered from the cooldown without touching the wire.
            Assert.Null(await client.GetSeriesAsync(Slug, CancellationToken.None));
            Assert.Empty(await client.SearchAsync("outra coisa", 5, CancellationToken.None));

            Assert.Equal(requestsAfterFirstFailure, handler.Paths.Count);
        }

        [Fact]
        public async Task Client_StaysReadyWhenTheSearchSimplyHasNoHits()
        {
            var routes = DetailRoutes();
            routes["/search"] = _ => Html("<html><body><section id=\"search-results-grid\"></section></body></html>");

            var client = CreateClient(routes);

            Assert.Empty(await client.SearchAsync("zzzz nao existe zzzz", 5, CancellationToken.None));

            // An empty result set is a real answer, not a failure.
            Assert.False(client.IsInFailureCooldown);
        }

        [Fact]
        public async Task Client_NeverLetsAFailedRefreshThrow()
        {
            var routes = new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.Ordinal)
            {
                [DetailPath] = _ => Status(HttpStatusCode.InternalServerError),
                ["/search"] = _ => Status(HttpStatusCode.Forbidden)
            };

            var client = CreateClient(routes);

            Assert.Null(await client.GetSeriesAsync(Slug, CancellationToken.None));
            Assert.Empty(await client.SearchAsync("nada", 5, CancellationToken.None));
        }

        [Fact]
        public void Client_ExposesThePinnedLocaleAndResolvesSlugs()
        {
            Assert.Equal("pt-PT", NartoDramaClient.Locale);
            Assert.Equal(TimeSpan.FromMinutes(30), NartoDramaClient.FailureCooldown);
            Assert.Equal(Slug, NartoDramaClient.ResolveSlug("https://narto-drama.com/detail/watch/" + Slug + "?lang=pt-PT"));
            Assert.Null(NartoDramaClient.ResolveSlug("Coração à Conquista"));
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

        private static Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> DetailRoutes()
        {
            return new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.Ordinal)
            {
                [DetailPath] = _ => Html(SeriesHtml),
                ["/search"] = _ => Html(SearchHtml)
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

        private NartoDramaSeriesProvider CreateSeriesProvider(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            return CreateSeriesProvider(new RoutingHandler(routes));
        }

        private NartoDramaSeriesProvider CreateSeriesProvider(RoutingHandler handler)
        {
            return new NartoDramaSeriesProvider(
                CreateClient(handler),
                CreateHttpClientFactory(handler),
                new Mock<ILogger<NartoDramaSeriesProvider>>().Object);
        }

        private NartoDramaEpisodeProvider CreateEpisodeProvider(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            var handler = new RoutingHandler(routes);

            return new NartoDramaEpisodeProvider(
                CreateClient(handler),
                CreateHttpClientFactory(handler),
                new Mock<ILogger<NartoDramaEpisodeProvider>>().Object);
        }

        private NartoDramaImageProvider CreateImageProvider(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            var handler = new RoutingHandler(routes);

            return new NartoDramaImageProvider(
                CreateClient(handler),
                CreateHttpClientFactory(handler),
                new Mock<ILogger<NartoDramaImageProvider>>().Object);
        }

        private NartoDramaClient CreateClient(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
        {
            return CreateClient(new RoutingHandler(routes));
        }

        private NartoDramaClient CreateClient(RoutingHandler handler)
        {
            var client = new NartoDramaClient(
                CreateHttpClientFactory(handler),
                new Mock<ILogger<NartoDramaClient>>().Object);

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
        /// page fail while the other keeps working. That is what tells the detail page apart from the
        /// search page.
        /// </summary>
        private sealed class RoutingHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes;

            public RoutingHandler(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
            {
                _routes = routes;
            }

            public List<string> Paths { get; } = new List<string>();

            public List<string> Uris { get; } = new List<string>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                Paths.Add(path);
                Uris.Add(request.RequestUri?.ToString() ?? string.Empty);

                if (_routes.TryGetValue(path, out var factory))
                {
                    return Task.FromResult(factory(request));
                }

                return Task.FromResult(Status(HttpStatusCode.NotFound));
            }
        }
    }
}