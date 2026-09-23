using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Parses the three shapes GoodShort exposes: the JSON-LD blocks and the rendered markup of the
    /// server side pages, and the JSON of the site's own backend endpoints.
    /// </summary>
    /// <remarks>
    /// Two sources exist and they disagree about how much of a series they carry, which is the whole
    /// reason this class has two detail parsers:
    /// the detail page's JSON-LD ItemList listed only 6 VideoObjects for a series GoodShort itself
    /// reports as 37 episodes, and an "EP 7"-onwards thumbnail is simply absent from it;
    /// the page's own channel endpoint reports all 37 on one page. The JSON-LD is therefore the
    /// fallback and the endpoint is the primary source, and <see cref="GoodShortSeries.EpisodesAreComplete"/>
    /// records which one answered. Every value is read through a defensive accessor because GoodShort
    /// mixes strings and numbers for the same field between endpoints, for example "bookId" is a
    /// string on the book payload and a number on the channel payload.
    /// </remarks>
    public static class GoodShortParser
    {
        /// <summary>
        /// Matches one JSON-LD script block. The attribute order varies between pages, so the type
        /// attribute is matched wherever it appears in the tag rather than at a fixed offset.
        /// </summary>
        private static readonly Regex JsonLdRegex = new Regex(
            "<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches one rendered search result: an anchor into a drama page that carries the title in
        /// an item-title div. The rendered page also contains anchors into the same drama pages for
        /// the "trending" rail, so callers get every anchor and rank by title instead of trusting order.
        /// </summary>
        private static readonly Regex DramaAnchorRegex = new Regex(
            "<a[^>]*href=[\"'](?<href>/drama/[^\"']+)[\"'][^>]*>(?<body>.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the visible title of a search result.
        /// </summary>
        private static readonly Regex ItemTitleRegex = new Regex(
            "<div[^>]*class=[\"'][^\"']*item-title[^\"']*[\"'][^>]*>(?<title>[^<]*)</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the alt text a search result image carries, used when the item-title div is absent.
        /// </summary>
        private static readonly Regex ImageAltRegex = new Regex(
            "<img[^>]*alt=[\"'](?<alt>[^\"']*)[\"']",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses every JSON-LD block of a page, skipping the ones that are not valid JSON.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The parsed blocks, in document order.</returns>
        public static IReadOnlyList<JsonNode> ParseJsonLdBlocks(string? html)
        {
            var blocks = new List<JsonNode>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return blocks;
            }

            foreach (Match match in JsonLdRegex.Matches(html))
            {
                var payload = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                try
                {
                    var node = JsonNode.Parse(payload);
                    if (node is not null)
                    {
                        blocks.Add(node);
                    }
                }
                catch (JsonException)
                {
                    // A malformed block is not fatal: GoodShort ships several per page and the
                    // providers only need the TVSeries and ItemList ones.
                }
            }

            return blocks;
        }

        /// <summary>
        /// Reads the series metadata out of the JSON-LD of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML of a /drama/&lt;slug&gt;-&lt;id&gt; page.</param>
        /// <returns>The series without its episodes, or null when the page carries no TVSeries block.</returns>
        public static GoodShortSeries? ParseSeriesFromJsonLd(string? html)
        {
            foreach (var block in ParseJsonLdBlocks(html))
            {
                if (!IsType(block, "TVSeries"))
                {
                    continue;
                }

                var series = new GoodShortSeries
                {
                    Name = GetString(block, "name"),
                    Cover = GetString(block, "image"),
                    Overview = GetString(block, "description"),
                    EpisodeCount = GetInt(block, "numberOfEpisodes"),
                    PremiereDate = ParseIsoDate(GetString(block, "dateCreated"))
                };

                // "genre" is a plain string on this block rather than the array shape used elsewhere.
                var genre = GetString(block, "genre");
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    series.Genres = new[] { genre };
                }

                // JSON-LD carries no id of its own, so it is read from the first watch target, whose
                // path segment is "<slug>-<seriesId>".
                var watchUrl = GetNestedWatchUrl(block);
                series.SeriesId = GoodShortTitleMatcher.ExtractSeriesId(watchUrl) ?? string.Empty;

                return series;
            }

            return null;
        }

        /// <summary>
        /// Reads the episodes out of the ItemList JSON-LD of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML of a detail page.</param>
        /// <returns>The episodes the ItemList carries, which is only a prefix of the real list.</returns>
        public static IReadOnlyList<GoodShortEpisode> ParseEpisodesFromJsonLd(string? html)
        {
            var episodes = new List<GoodShortEpisode>();

            foreach (var block in ParseJsonLdBlocks(html))
            {
                if (!IsType(block, "ItemList") || block["itemListElement"] is not JsonArray items)
                {
                    continue;
                }

                foreach (var item in items)
                {
                    if (item is null || !IsType(item, "VideoObject"))
                    {
                        continue;
                    }

                    var number = GetInt(item, "position");
                    if (number <= 0)
                    {
                        // Without a position there is no episode number to store, and MulletaFlix
                        // episode identity is the number.
                        continue;
                    }

                    episodes.Add(new GoodShortEpisode
                    {
                        Number = number,
                        Name = GetString(item, "name"),
                        Thumbnail = GetFirstString(item, "thumbnailUrl"),
                        DurationSeconds = ParseIsoDurationSeconds(GetString(item, "duration"))
                    });
                }
            }

            return episodes;
        }

        /// <summary>
        /// Reads a search result list out of the rendered markup of the /search page.
        /// </summary>
        /// <param name="html">The raw HTML of the search page.</param>
        /// <returns>One stub series per drama anchor found.</returns>
        /// <remarks>
        /// This is the fallback for the search endpoint. It is worth keeping because the endpoint is
        /// undocumented, but it is a weak source: measured on the live site, /search?keyword=x
        /// returned the same six trending dramas for every keyword tried, so it produces candidates
        /// rather than matches and callers must rank them by title.
        /// </remarks>
        public static IReadOnlyList<GoodShortSeries> ParseSearchResultsFromHtml(string? html)
        {
            var results = new List<GoodShortSeries>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in DramaAnchorRegex.Matches(html))
            {
                var href = match.Groups["href"].Value;
                var seriesId = GoodShortTitleMatcher.ExtractSeriesId(href);
                if (string.IsNullOrEmpty(seriesId) || !seen.Add(seriesId))
                {
                    continue;
                }

                var body = match.Groups["body"].Value;
                var title = ItemTitleRegex.Match(body) is { Success: true } titleMatch
                    ? titleMatch.Groups["title"].Value
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = ImageAltRegex.Match(body) is { Success: true } altMatch
                        ? altMatch.Groups["alt"].Value
                        : string.Empty;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                results.Add(new GoodShortSeries
                {
                    SeriesId = seriesId,
                    Name = DecodeHtmlEntities(title).Trim(),
                    Slug = GoodShortTitleMatcher.ExtractSlug(href) ?? string.Empty
                });
            }

            return results;
        }

        /// <summary>
        /// Reads a series out of a /hwycreels/book/detail response.
        /// </summary>
        /// <param name="root">The parsed response body.</param>
        /// <returns>The series without its episodes, or null when the payload holds no book.</returns>
        public static GoodShortSeries? ParseSeriesFromApi(JsonNode? root)
        {
            if (root?["data"]?["book"] is not JsonNode book)
            {
                return null;
            }

            var seriesId = GetString(book, "bookId");
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return null;
            }

            var series = new GoodShortSeries
            {
                SeriesId = seriesId,
                Slug = GetString(book, "bookResourceUrl"),
                Name = GetString(book, "bookName"),
                Cover = GetString(book, "cover"),
                Overview = GetString(book, "introduction"),
                EpisodeCount = GetInt(book, "chapterCount"),
                Language = GetString(book, "language"),
                Genres = ParseGenres(book),
                PremiereDate = ParseApiDate(GetString(book, "lastChapterTime"))
            };

            var rating = GetDouble(book, "ratings");
            if (rating is > 0)
            {
                series.Rating = rating;
            }

            return series;
        }

        /// <summary>
        /// Reads one page of episodes out of a /hwycreels/chapter/page response.
        /// </summary>
        /// <param name="root">The parsed response body.</param>
        /// <returns>The page, with an empty episode list when the payload carries no records.</returns>
        public static GoodShortEpisodePage ParseEpisodePageFromApi(JsonNode? root)
        {
            var page = new GoodShortEpisodePage
            {
                Total = GetInt(root?["data"], "total"),
                Pages = GetInt(root?["data"], "pages")
            };

            if (root?["data"]?["records"] is not JsonArray records)
            {
                return page;
            }

            var episodes = new List<GoodShortEpisode>();

            foreach (var record in records)
            {
                if (record is null)
                {
                    continue;
                }

                var number = ParseEpisodeNumber(GetString(record, "chapterName"));
                if (number <= 0)
                {
                    // The zero-based "index" is the documented fallback for a chapter whose name is
                    // not the usual zero-padded number.
                    var index = GetInt(record, "index");
                    number = index >= 0 ? index + 1 : 0;
                }

                if (number <= 0)
                {
                    continue;
                }

                episodes.Add(new GoodShortEpisode
                {
                    EpisodeId = GetString(record, "id"),
                    Number = number,
                    Name = GetString(record, "chapterName"),
                    Thumbnail = GetString(record, "image"),
                    DurationSeconds = GetLong(record, "playTime")
                });
            }

            page.Episodes = episodes;
            return page;
        }

        /// <summary>
        /// Reads the series stubs out of a /hwycreels/book/search/suggest response.
        /// </summary>
        /// <param name="root">The parsed response body.</param>
        /// <returns>The candidates, most relevant first as ordered by GoodShort.</returns>
        public static IReadOnlyList<GoodShortSeries> ParseSearchResultsFromApi(JsonNode? root)
        {
            var results = new List<GoodShortSeries>();
            if (root?["data"]?["suggest"] is not JsonArray suggestions)
            {
                return results;
            }

            foreach (var item in suggestions)
            {
                if (item is null)
                {
                    continue;
                }

                var seriesId = GetString(item, "bookId");
                var name = GetString(item, "bookName");
                if (string.IsNullOrWhiteSpace(seriesId) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                results.Add(new GoodShortSeries
                {
                    SeriesId = seriesId,
                    Slug = GetString(item, "bookResourceUrl"),
                    Name = name,
                    Cover = GetString(item, "cover"),
                    Overview = GetString(item, "introduction"),
                    EpisodeCount = GetInt(item, "chapterCount"),
                    Language = GetString(item, "language"),
                    Genres = ParseGenres(item)
                });
            }

            return results;
        }

        /// <summary>
        /// Reads the "status" field GoodShort puts in every JSON response body.
        /// </summary>
        /// <param name="root">The parsed response body.</param>
        /// <returns>The status code, or -1 when the body is not a GoodShort envelope.</returns>
        /// <remarks>
        /// GoodShort answers HTTP 200 even for "Book not exists", so the transport status alone is
        /// not enough to tell success from failure.
        /// </remarks>
        public static int ParseApiStatus(JsonNode? root)
        {
            if (root is null || root["status"] is null)
            {
                return -1;
            }

            return GetInt(root, "status");
        }

        /// <summary>
        /// Parses a "PT1M21S" style ISO 8601 duration into seconds.
        /// </summary>
        /// <param name="value">The duration string.</param>
        /// <returns>The duration in seconds, or 0 when it could not be read.</returns>
        public static long ParseIsoDurationSeconds(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var match = Regex.Match(
                value,
                @"^P(?:(?<days>\d+)D)?T?(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+(?:\.\d+)?)S)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                return 0;
            }

            var total = 0.0;
            total += ReadComponent(match, "days") * 86400;
            total += ReadComponent(match, "hours") * 3600;
            total += ReadComponent(match, "minutes") * 60;
            total += ReadComponent(match, "seconds");

            return (long)total;
        }

        /// <summary>
        /// Parses the zero-padded chapter name GoodShort uses as the episode number.
        /// </summary>
        /// <param name="chapterName">The chapter name, for example "001".</param>
        /// <returns>The one-based episode number, or 0 when the name is not numeric.</returns>
        public static int ParseEpisodeNumber(string? chapterName)
        {
            if (string.IsNullOrWhiteSpace(chapterName))
            {
                return 0;
            }

            var trimmed = chapterName.Trim();
            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return 0;
            }

            return number > 0 ? number : 0;
        }

        private static double ReadComponent(Match match, string group)
        {
            if (!match.Groups[group].Success)
            {
                return 0;
            }

            return double.TryParse(
                match.Groups[group].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0;
        }

        private static IReadOnlyList<string> ParseGenres(JsonNode node)
        {
            // The book payload carries a single genreList object; the search and channel payloads
            // carry typeTwoNames, which is a plain string on some records and an array on others.
            if (node["genreList"] is JsonObject genreList)
            {
                var name = GetString(genreList, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return new[] { name };
                }
            }

            return GetStringArray(node, "typeTwoNames");
        }

        private static string? GetNestedWatchUrl(JsonNode block)
        {
            if (block["potentialAction"]?["target"] is JsonArray targets)
            {
                foreach (var target in targets)
                {
                    var url = GetString(target, "url");
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return url;
                    }
                }
            }

            return null;
        }

        private static bool IsType(JsonNode? node, string type)
        {
            return string.Equals(GetString(node, "@type"), type, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetString(JsonNode? node, string name)
        {
            var value = node?[name];
            if (value is null)
            {
                return string.Empty;
            }

            // A JSON-LD "@type" may legally be an array of types; take the first usable entry.
            if (value is JsonArray array)
            {
                foreach (var item in array)
                {
                    var text = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                return string.Empty;
            }

            try
            {
                return value.ToString();
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private static string GetFirstString(JsonNode? node, string name)
        {
            var value = node?[name];
            if (value is JsonArray array)
            {
                foreach (var item in array)
                {
                    var text = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                return string.Empty;
            }

            return GetString(node, name);
        }

        private static int GetInt(JsonNode? node, string name)
        {
            var raw = GetString(node, name);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static long GetLong(JsonNode? node, string name)
        {
            var raw = GetString(node, name);
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L;
        }

        private static double? GetDouble(JsonNode? node, string name)
        {
            var raw = GetString(node, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }

        private static IReadOnlyList<string> GetStringArray(JsonNode? node, string name)
        {
            var values = new List<string>();
            var value = node?[name];
            if (value is null)
            {
                return values;
            }

            if (value is JsonArray array)
            {
                foreach (var item in array)
                {
                    AddIfUseful(values, item?.ToString());
                }

                return values;
            }

            // Some records send the same field as a comma separated string.
            var raw = value.ToString();
            if (raw.Contains(',', StringComparison.Ordinal))
            {
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    AddIfUseful(values, part);
                }

                return values;
            }

            AddIfUseful(values, raw);
            return values;
        }

        private static void AddIfUseful(List<string> values, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        private static DateTime? ParseIsoDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed.UtcDateTime;
            }

            return null;
        }

        private static DateTime? ParseApiDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
            {
                return exact;
            }

            return ParseIsoDate(value);
        }

        private static string DecodeHtmlEntities(string value)
        {
            // The rendered titles only ever need these three, all of which appear in the live
            // catalogue ("Denying My Son's Guilt", "Sinking With My Step-sister").
            return value
                .Replace("&amp;", "&", StringComparison.Ordinal)
                .Replace("&#39;", "'", StringComparison.Ordinal)
                .Replace("&quot;", "\"", StringComparison.Ordinal)
                .Replace("&nbsp;", " ", StringComparison.Ordinal);
        }
    }
}
