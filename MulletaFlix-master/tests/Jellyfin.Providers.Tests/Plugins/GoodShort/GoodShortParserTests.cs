using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using MediaBrowser.Providers.Plugins.GoodShort;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.GoodShort
{
    /// <summary>
    /// Exercises the parser against the real GoodShort payloads captured from the live site.
    /// </summary>
    /// <remarks>
    /// The fixtures under Test Data/GoodShort are the files the live site served, unedited, so a
    /// change in the shape GoodShort returns shows up here as a failing assertion rather than as an
    /// empty series in the library.
    /// </remarks>
    public class GoodShortParserTests
    {
        private static string FixturePath(string name)
        {
            return Path.Combine("Test Data", "GoodShort", name);
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(FixturePath(name));
        }

        [Fact]
        public void ParseJsonLdBlocks_FindsEveryBlockOnTheRealDetailPage()
        {
            var blocks = GoodShortParser.ParseJsonLdBlocks(Fixture("detail-page-abandonei.html"));

            // The live page ships TVSeries, BreadcrumbList, ImageObject, ItemList and SoftwareApplication.
            Assert.Equal(5, blocks.Count);
            Assert.Contains(blocks, block => string.Equals(block["@type"]?.ToString(), "TVSeries", StringComparison.Ordinal));
            Assert.Contains(blocks, block => string.Equals(block["@type"]?.ToString(), "ItemList", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html><body>no structured data here</body></html>")]
        [InlineData("<script type=\"application/ld+json\">{ not json }</script>")]
        public void ParseJsonLdBlocks_ReturnsEmptyInsteadOfThrowing(string html)
        {
            Assert.Empty(GoodShortParser.ParseJsonLdBlocks(html));
        }

        [Fact]
        public void ParseSeriesFromJsonLd_ReadsTheRealSeriesBlock()
        {
            var series = GoodShortParser.ParseSeriesFromJsonLd(Fixture("detail-page-abandonei.html"));

            Assert.NotNull(series);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", series!.Name);
            Assert.Equal("https://acf.goodshort.com/videobook/202607/cover-2WGS3kh14m.jpg", series.Cover);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", series.Overview, StringComparison.Ordinal);

            // GoodShort itself reports 37 here while the ItemList below only lists 6, which is the
            // single fact that decides whether the JSON-LD may be trusted as the episode source.
            Assert.Equal(37, series.EpisodeCount);

            // The JSON-LD block carries no id, so it is recovered from the watch target URL.
            Assert.Equal("31001543625", series.SeriesId);

            Assert.Equal("Romance", Assert.Single(series.Genres));

            // dateCreated is "2026-07-04T15:06:10+08:00": 08:00 ahead of UTC.
            Assert.Equal(new DateTime(2026, 7, 4, 7, 6, 10, DateTimeKind.Utc), series.PremiereDate!.Value);

            // JSON-LD never carries episodes on its own.
            Assert.Empty(series.Episodes);
        }

        [Fact]
        public void ParseSeriesFromJsonLd_ReadsTheRealBracketedDubbingTitle()
        {
            var series = GoodShortParser.ParseSeriesFromJsonLd(Fixture("detail-page-mafia.html"));

            Assert.NotNull(series);

            // GoodShort brackets the dubbing marker here rather than appending "(Dublado)" the way a
            // library folder name would, which is why the matcher strips the token on both sides.
            Assert.Equal("[Dublado] Abandonada pelo Don, Coroada pela Máfia", series!.Name);
            Assert.Equal("31001499548", series.SeriesId);
            Assert.Equal(32, series.EpisodeCount);
        }

        [Fact]
        public void ParseEpisodesFromJsonLd_ReadsTheTruncatedItemListThePageShips()
        {
            var episodes = GoodShortParser.ParseEpisodesFromJsonLd(Fixture("detail-page-abandonei.html"));

            // Measured on the live page: six VideoObjects, positions 1..6, for a series GoodShort
            // itself calls 37 episodes long.
            Assert.Equal(6, episodes.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, episodes.Select(episode => episode.Number));

            var first = episodes[0];
            Assert.Equal("Abandonei o Rei dos Deuses no Altar - EP 1", first.Name);
            Assert.Equal("https://acf.goodshort.com/videobook/31001543625/202607/cover-S8o8dXDQAk.jpg", first.Thumbnail);

            // The block's "duration" is ISO 8601: PT1M21S is 81 seconds.
            Assert.Equal(81, first.DurationSeconds);
            Assert.Equal(90, episodes[1].DurationSeconds);
            Assert.Equal(78, episodes[2].DurationSeconds);
        }

        [Fact]
        public void ParseSearchResultsFromHtml_ReadsTitleHrefAndId()
        {
            var results = GoodShortParser.ParseSearchResultsFromHtml(Fixture("search-page.html"));

            Assert.NotEmpty(results);

            var exiled = Assert.Single(results, result => result.SeriesId == "31001705992");
            Assert.Equal("The Exiled Princess Returns: Crowned by the Gods", exiled.Name);
            Assert.Equal("the-exiled-princess-returns-crowned-by-the-gods-31001705992", exiled.Slug);

            // Every candidate must carry the id that is later stored as the provider id.
            Assert.All(results, result => Assert.False(string.IsNullOrWhiteSpace(result.SeriesId)));
            Assert.All(results, result => Assert.False(string.IsNullOrWhiteSpace(result.Name)));

            // The rendered page contains each drama exactly once, so ids are de-duplicated.
            Assert.Equal(results.Count, results.Select(result => result.SeriesId).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void ParseSeriesFromApi_ReadsTheRealBookPayload()
        {
            var payload = JsonNode.Parse(Fixture("book-detail.json"));
            var series = GoodShortParser.ParseSeriesFromApi(payload);

            Assert.NotNull(series);
            Assert.Equal("31001543625", series!.SeriesId);
            Assert.Equal("Abandonei o Rei dos Deuses no Altar", series.Name);
            Assert.Equal("https://acf.goodshort.com/videobook/202607/cover-2WGS3kh14m.jpg", series.Cover);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", series.Overview, StringComparison.Ordinal);
            Assert.Equal(37, series.EpisodeCount);
            Assert.Equal("PORTUGUESE", series.Language);
            Assert.Equal("abandonei-o-rei-dos-deuses-no-altar-31001543625", series.Slug);
            Assert.Equal("Romance", Assert.Single(series.Genres));

            // The payload reports ratings 0, which GoodShort uses to mean "not rated".
            Assert.Null(series.Rating);

            // lastChapterTime is a plain "yyyy-MM-dd HH:mm:ss" stamp.
            Assert.Equal(new DateTime(2026, 7, 4, 15, 6, 11, DateTimeKind.Utc), series.PremiereDate!.Value);
        }

        [Fact]
        public void ParseSeriesFromApi_KeepsANonZeroRating()
        {
            var payload = JsonNode.Parse(
                """{"status":0,"data":{"book":{"bookId":"1","bookName":"X","ratings":9.3}}}""");

            var series = GoodShortParser.ParseSeriesFromApi(payload);

            Assert.NotNull(series);
            Assert.Equal(9.3, series!.Rating);
        }

        [Fact]
        public void ParseEpisodePageFromApi_ReadsTheCompleteChannelListing()
        {
            var payload = JsonNode.Parse(Fixture("chapter-page.json"));
            var page = GoodShortParser.ParseEpisodePageFromApi(payload);

            Assert.Equal(37, page.Total);
            Assert.Equal(1, page.Pages);
            Assert.Equal(37, page.Episodes.Count);

            // chapterName is the zero-padded episode number and is authoritative; "index" is the
            // zero-based ordinal GoodShort also sends.
            Assert.Equal(Enumerable.Range(1, 37), page.Episodes.Select(episode => episode.Number));

            var first = page.Episodes[0];
            Assert.Equal("56639125", first.EpisodeId);
            Assert.Equal("001", first.Name);
            Assert.Equal("https://acf.goodshort.com/videobook/31001543625/202607/cover-S8o8dXDQAk.jpg", first.Thumbnail);

            // playTime is seconds: it equals the 81 the JSON-LD calls PT1M21S.
            Assert.Equal(81, first.DurationSeconds);

            var last = page.Episodes[36];
            Assert.Equal(37, last.Number);
            Assert.Equal("56639161", last.EpisodeId);
        }

        [Fact]
        public void ParseEpisodePageFromApi_ReadsTheSecondRealSeries()
        {
            var payload = JsonNode.Parse(Fixture("chapter-page-mafia.json"));
            var page = GoodShortParser.ParseEpisodePageFromApi(payload);

            Assert.Equal(32, page.Total);
            Assert.Equal(32, page.Episodes.Count);
            Assert.Equal(1, page.Episodes[0].Number);
            Assert.Equal("53772583", page.Episodes[0].EpisodeId);
            Assert.Equal(32, page.Episodes[31].Number);
        }

        [Fact]
        public void ParseEpisodePageFromApi_FallsBackToTheZeroBasedIndex()
        {
            var payload = JsonNode.Parse(
                """{"status":0,"data":{"total":2,"pages":1,"records":[{"id":7,"chapterName":"PREVIEW","index":4},{"id":8,"chapterName":"005","index":99}]}}""");

            var page = GoodShortParser.ParseEpisodePageFromApi(payload);

            // A non numeric name falls back to index + 1, while a numeric name always wins.
            Assert.Equal(new[] { 5, 5 }, page.Episodes.Select(episode => episode.Number));
            Assert.Equal(new[] { "7", "8" }, page.Episodes.Select(episode => episode.EpisodeId));
        }

        [Fact]
        public void ParseSearchResultsFromApi_ReadsTheRealSuggestPayload()
        {
            var payload = JsonNode.Parse(Fixture("search-suggest.json"));
            var results = GoodShortParser.ParseSearchResultsFromApi(payload);

            Assert.Equal(3, results.Count);
            Assert.Contains(results, result => result.SeriesId == "31001543625");

            var dubbed = Assert.Single(results, result => result.SeriesId == "31001543628");
            Assert.Equal("[Dublado] Abandonei o Rei dos Deuses no Altar", dubbed.Name);
            Assert.Equal("dublado-abandonei-o-rei-dos-deuses-no-altar-31001543628", dubbed.Slug);
            Assert.StartsWith("No décimo ano ao lado de Aetheon", dubbed.Overview, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseApiStatus_DistinguishesSuccessFromTheRealErrorEnvelope()
        {
            Assert.Equal(0, GoodShortParser.ParseApiStatus(JsonNode.Parse(Fixture("book-detail.json"))));

            // GoodShort answers HTTP 200 for "Book not exists" and reports it only in the body.
            var failure = JsonNode.Parse(Fixture("book-not-found.json"));
            Assert.Equal(12000, GoodShortParser.ParseApiStatus(failure));

            Assert.Equal(-1, GoodShortParser.ParseApiStatus(null));
            Assert.Equal(-1, GoodShortParser.ParseApiStatus(JsonNode.Parse("""{"data":{}}""")));
        }

        [Fact]
        public void ParseSeriesFromApi_ReturnsNullForADegeneratePayload()
        {
            Assert.Null(GoodShortParser.ParseSeriesFromApi(JsonNode.Parse(Fixture("book-not-found.json"))));
            Assert.Null(GoodShortParser.ParseSeriesFromApi(null));
            Assert.Null(GoodShortParser.ParseSeriesFromApi(JsonNode.Parse("""{"data":{"book":{}}}""")));
        }

        [Theory]
        [InlineData("PT1M21S", 81)]
        [InlineData("PT1M30S", 90)]
        [InlineData("PT45S", 45)]
        [InlineData("PT1H2M3S", 3723)]
        [InlineData("P1DT1S", 86401)]
        [InlineData("PT0S", 0)]
        [InlineData("", 0)]
        [InlineData("1M21S", 0)]
        [InlineData("garbage", 0)]
        [InlineData(null, 0)]
        public void ParseIsoDurationSeconds_ReadsIso8601Durations(string? value, long expected)
        {
            Assert.Equal(expected, GoodShortParser.ParseIsoDurationSeconds(value));
        }

        [Theory]
        [InlineData("001", 1)]
        [InlineData("037", 37)]
        [InlineData(" 12 ", 12)]
        [InlineData("0", 0)]
        [InlineData("PREVIEW", 0)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseEpisodeNumber_ReadsTheZeroPaddedChapterName(string? value, int expected)
        {
            Assert.Equal(expected, GoodShortParser.ParseEpisodeNumber(value));
        }
    }
}
