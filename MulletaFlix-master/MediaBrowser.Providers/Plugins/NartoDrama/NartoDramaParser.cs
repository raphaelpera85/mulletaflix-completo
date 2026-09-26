using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Parses the server rendered HTML of narto-drama.com: its JSON-LD blocks, its detail page
    /// markup and its episode anchors.
    /// </summary>
    /// <remarks>
    /// NartoDrama is plain server rendered HTML with no API to reverse, so every value below was
    /// read off the live pages. Four facts shaped this class:
    /// the detail page carries four JSON-LD blocks (TVSeries, BreadcrumbList, Organization and a
    /// navigation ItemList) and only the first describes the series;
    /// the title in the TVSeries block is the library title plus a localized marketing marker
    /// (" - Streaming grátis" on pt-PT, " - Free Streaming" on en-US), which is why it is stripped;
    /// the synopsis exists twice, as a clean <c>div.movie-desc</c> that is only present on part of
    /// the catalogue and as a JSON-LD description that the site pads with the title in front and its
    /// own platform pitch behind;
    /// the episodes are anchors in the page itself, so one request yields metadata and the whole
    /// episode list.
    /// </remarks>
    public static class NartoDramaParser
    {
        /// <summary>
        /// The host that serves the posters. It is not the site host.
        /// </summary>
        private const string PosterHost = "https://img.nartodrama-api.online/poster/";

        /// <summary>
        /// The sentence the site pastes into the JSON-LD description when it has no real synopsis
        /// for a series.
        /// </summary>
        /// <remarks>
        /// Measured byte for byte identical on five different series pages that carry no
        /// <c>div.movie-desc</c>. Publishing it as a synopsis would describe the platform, not the
        /// series, so it is treated as "no synopsis" instead.
        /// </remarks>
        private const string PlatformPlaceholderDescription =
            "Plataforma gratuita para dramas curtos e mini séries com legendas em português no Narto Drama.";

        /// <summary>
        /// The marker the site inserts between the real synopsis and its own platform pitch.
        /// </summary>
        private const string PlatformPitchMarker = "Narto Drama - ";

        /// <summary>
        /// Matches one JSON-LD script block. The attribute order varies between pages, so the type
        /// attribute is matched wherever it appears in the tag rather than at a fixed offset.
        /// </summary>
        private static readonly Regex JsonLdRegex = new Regex(
            "<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the numeric content id the site publishes in its own meta tag.
        /// </summary>
        private static readonly Regex ContentIdRegex = new Regex(
            "<meta[^>]*name=[\"']nd-current-movie-id[\"'][^>]*content=[\"'](?<id>[0-9]+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the numeric content id inside the episode check script, which is the fallback
        /// when the meta tag is absent.
        /// </summary>
        private static readonly Regex EpGateIdRegex = new Regex(
            "_epGateKey\\s*=\\s*[^;]*?String\\(\\s*(?<id>[0-9]+)\\s*\\)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the cover URL the page advertises for social sharing.
        /// </summary>
        private static readonly Regex OgImageRegex = new Regex(
            "<meta[^>]*property=[\"']og:image[\"'][^>]*content=[\"'](?<value>[^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the clean title heading. It is already free of the marketing suffix, and it is
        /// the fallback when a page carries no TVSeries block.
        /// </summary>
        private static readonly Regex MovieTitleRegex = new Regex(
            "<h1[^>]*class=[\"'][^\"']*\\bmovie-title\\b[^\"']*[\"'][^>]*>(?<title>[^<]*)</h1>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the clean synopsis block. It is absent on part of the catalogue.
        /// </summary>
        private static readonly Regex MovieDescRegex = new Regex(
            "<div[^>]*class=[\"'][^\"']*\\bmovie-desc\\b[^\"']*[\"'][^>]*>(?<desc>.*?)</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches one tag pill, for example &lt;a class="movie-tag-pill"&gt;#Casamento&lt;/a&gt;.
        /// </summary>
        private static readonly Regex TagPillRegex = new Regex(
            "<a[^>]*class=[\"'][^\"']*\\bmovie-tag-pill\\b[^\"']*[\"'][^>]*>(?<tag>[^<]*)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches one episode anchor, for example
        /// &lt;a class="episode-item" href=".../37?lang=pt-PT" title="037"&gt;.
        /// </summary>
        /// <remarks>
        /// Anchored on the class rather than on the URL shape: the page also links episode URLs from
        /// its "Assistir episódio 1" button, its SEO block and its "you may like" rail, and only the
        /// class marks the real episode list.
        /// </remarks>
        private static readonly Regex EpisodeAnchorRegex = new Regex(
            "<a[^>]*class=[\"'][^\"']*\\bepisode-item\\b[^\"']*[\"'][^>]*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches one rendered search result card.
        /// </summary>
        private static readonly Regex SearchCardRegex = new Regex(
            "<article[^>]*class=[\"'][^\"']*\\bprovider-search-card\\b[^\"']*[\"'][^>]*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the watch URL a search result card carries.
        /// </summary>
        private static readonly Regex CardWatchUrlRegex = new Regex(
            "data-watch-url\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the displayed title a search result card carries.
        /// </summary>
        private static readonly Regex CardTitleRegex = new Regex(
            "data-search-title\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the truncated synopsis a search result card carries.
        /// </summary>
        private static readonly Regex CardDescriptionRegex = new Regex(
            "data-search-description\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the clickable overlay of a search result card, which carries the displayed title
        /// as its text when the data attribute is absent.
        /// </summary>
        private static readonly Regex CardOverlayRegex = new Regex(
            "<a[^>]*class=[\"'][^\"']*\\bcard-link-overlay\\b[^\"']*[\"'][^>]*>(?<title>[^<]*)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex EpisodeHrefRegex = new Regex(
            "href\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
            RegexOptions.CultureInvariant);

        private static readonly Regex EpisodeLabelRegex = new Regex(
            "title\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the episode total the page keeps in a script variable.
        /// </summary>
        private static readonly Regex EpisodeCountRegex = new Regex(
            "var\\s+currentEpCount\\s*=\\s*(?<count>[0-9]+)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// The characters that end a path segment in a href.
        /// </summary>
        private static readonly char[] QuerySeparators = { '?', '#' };

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
                    // A malformed block is not fatal: the site ships four per page and only the
                    // TVSeries one is needed here.
                }
            }

            return blocks;
        }

        /// <summary>
        /// Reads the series metadata out of the JSON-LD of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML of a /detail/watch/&lt;slug&gt; page.</param>
        /// <returns>The series, or null when the page carries no TVSeries block.</returns>
        public static NartoDramaSeries? ParseSeriesFromJsonLd(string? html)
        {
            foreach (var block in ParseJsonLdBlocks(html))
            {
                if (!IsType(block, "TVSeries"))
                {
                    continue;
                }

                var rawName = GetString(block, "name");
                var name = NartoDramaTitleMatcher.StripStreamingSuffix(rawName);

                return new NartoDramaSeries
                {
                    // The TVSeries block carries no slug of its own, only the canonical page URL.
                    Slug = NartoDramaTitleMatcher.ExtractSlug(GetString(block, "url")) ?? string.Empty,
                    Name = name,
                    Cover = GetString(block, "image"),
                    Overview = CleanDescription(GetString(block, "description"), name)
                };
            }

            return null;
        }

        /// <summary>
        /// Reads a complete series, including its episode list, out of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML of a detail page.</param>
        /// <param name="slug">The slug the page was requested with, used when the page does not name itself.</param>
        /// <returns>The series, or null when the page carries nothing this parser can read.</returns>
        public static NartoDramaSeries? ParseSeries(string? html, string? slug)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var series = ParseSeriesFromJsonLd(html) ?? new NartoDramaSeries();

            if (string.IsNullOrWhiteSpace(series.Slug) && !string.IsNullOrWhiteSpace(slug))
            {
                series.Slug = slug.Trim();
            }

            // The heading is a second, already clean title. It is only consulted when the JSON-LD
            // block is missing, because the structured block is the more stable of the two.
            if (string.IsNullOrWhiteSpace(series.Name))
            {
                series.Name = NartoDramaTitleMatcher.StripStreamingSuffix(ParseMovieTitle(html));
            }

            // div.movie-desc holds the synopsis without the title prefix and without the platform
            // pitch, so it wins over the JSON-LD description whenever the site rendered it. The
            // JSON-LD value parsed above is only a fallback: on part of the catalogue it is the
            // site's own platform blurb rather than a synopsis at all.
            var synopsis = ParseSynopsis(html);
            if (synopsis.Length > 0)
            {
                series.Overview = synopsis;
            }

            series.ContentId = ParseContentId(html);
            series.Cover = PickCover(series.Cover, html, series.ContentId);
            series.Tags = ParseTags(html);
            series.Episodes = ParseEpisodes(html);

            var episodeCount = ParseEpisodeCount(html);
            series.EpisodeCount = episodeCount > 0 ? episodeCount : series.Episodes.Count;

            if (string.IsNullOrWhiteSpace(series.Name)
                && string.IsNullOrWhiteSpace(series.Cover)
                && series.Episodes.Count == 0)
            {
                // The page answered but nothing in it is recognizable, which means the markup this
                // parser was written against has changed. Reported as "no series" so the caller can
                // go quiet instead of repeating the same parse failure for every library item.
                return null;
            }

            return series;
        }

        /// <summary>
        /// Reads the clean title heading of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The title, or an empty string.</returns>
        public static string ParseMovieTitle(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var match = MovieTitleRegex.Match(html);
            return match.Success ? DecodeHtmlEntities(match.Groups["title"].Value).Trim() : string.Empty;
        }

        /// <summary>
        /// Reads the clean synopsis block of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The synopsis, or an empty string when the page has no such block.</returns>
        public static string ParseSynopsis(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var match = MovieDescRegex.Match(html);
            return match.Success ? DecodeHtmlEntities(match.Groups["desc"].Value).Trim() : string.Empty;
        }

        /// <summary>
        /// Reads the tag pills of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The tag labels without their leading "#".</returns>
        public static IReadOnlyList<string> ParseTags(string? html)
        {
            var tags = new List<string>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return tags;
            }

            foreach (Match match in TagPillRegex.Matches(html))
            {
                var value = DecodeHtmlEntities(match.Groups["tag"].Value).Trim().TrimStart('#').Trim();
                if (value.Length > 0 && !tags.Contains(value))
                {
                    tags.Add(value);
                }
            }

            return tags;
        }

        /// <summary>
        /// Reads the numeric content id of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The id, or an empty string when the page exposes none.</returns>
        /// <remarks>
        /// The id never appears in the page URL, so it has to be read out of the markup. The meta
        /// tag is tried first and the episode check script second; both were verified on the live
        /// site, and the og:image URL carries the same number as a third source.
        /// </remarks>
        public static string ParseContentId(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var match = ContentIdRegex.Match(html);
            if (match.Success)
            {
                return match.Groups["id"].Value;
            }

            var gate = EpGateIdRegex.Match(html);
            return gate.Success ? gate.Groups["id"].Value : string.Empty;
        }

        /// <summary>
        /// Reads the episode list of a detail page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The episodes, ordered by number, without duplicates.</returns>
        public static IReadOnlyList<NartoDramaEpisode> ParseEpisodes(string? html)
        {
            var episodes = new List<NartoDramaEpisode>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return episodes;
            }

            var seen = new HashSet<int>();

            foreach (Match match in EpisodeAnchorRegex.Matches(html))
            {
                var tag = match.Value;

                var number = ParseEpisodeNumber(EpisodeHrefRegex.Match(tag));
                if (number <= 0 || !seen.Add(number))
                {
                    continue;
                }

                var labelMatch = EpisodeLabelRegex.Match(tag);

                episodes.Add(new NartoDramaEpisode
                {
                    Number = number,
                    Label = labelMatch.Success ? DecodeHtmlEntities(labelMatch.Groups["value"].Value).Trim() : string.Empty
                });
            }

            episodes.Sort((left, right) => left.Number.CompareTo(right.Number));
            return episodes;
        }

        /// <summary>
        /// Reads the episode total the page reports for itself.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The total, or 0 when the page states none.</returns>
        public static int ParseEpisodeCount(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return 0;
            }

            var match = EpisodeCountRegex.Match(html);
            if (!match.Success)
            {
                return 0;
            }

            return int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }

        /// <summary>
        /// Reads the search results out of the rendered markup of the /search page.
        /// </summary>
        /// <param name="html">The raw HTML of the search page.</param>
        /// <returns>One stub series per search result card, in the order the site ranked them.</returns>
        /// <remarks>
        /// This is the primary search source: the rendered card carries the displayed title, a
        /// truncated synopsis and the platform's own ranking. The page's CollectionPage JSON-LD also
        /// lists the same results and is used by <see cref="ParseSearchResultsFromJsonLd"/> when this
        /// markup is not recognizable.
        /// </remarks>
        public static IReadOnlyList<NartoDramaSeries> ParseSearchResults(string? html)
        {
            var results = new List<NartoDramaSeries>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in SearchCardRegex.Matches(html))
            {
                var tag = match.Value;

                var slug = NartoDramaTitleMatcher.ExtractSlug(ReadAttribute(CardWatchUrlRegex, tag));
                if (slug is null || !seen.Add(slug))
                {
                    continue;
                }

                var title = DecodeHtmlEntities(ReadAttribute(CardTitleRegex, tag)).Trim();
                if (title.Length == 0)
                {
                    // The card overlay repeats the displayed title in its text, which is what a
                    // reader sees when the data attribute is not rendered.
                    var overlay = CardOverlayRegex.Match(tag);
                    title = overlay.Success ? DecodeHtmlEntities(overlay.Groups["title"].Value).Trim() : string.Empty;
                }

                if (title.Length == 0)
                {
                    continue;
                }

                results.Add(new NartoDramaSeries
                {
                    Slug = slug,
                    Name = title,
                    Overview = DecodeHtmlEntities(ReadAttribute(CardDescriptionRegex, tag)).Trim()
                });
            }

            return results;
        }

        /// <summary>
        /// Reads the search results out of the CollectionPage JSON-LD of a search page.
        /// </summary>
        /// <param name="html">The raw HTML of the search page.</param>
        /// <returns>One stub series per list item that points at a detail page.</returns>
        public static IReadOnlyList<NartoDramaSeries> ParseSearchResultsFromJsonLd(string? html)
        {
            var results = new List<NartoDramaSeries>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var block in ParseJsonLdBlocks(html))
            {
                if (block["mainEntity"]?["itemListElement"] is not JsonArray items)
                {
                    continue;
                }

                foreach (var item in items)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    // The navigation block the site also ships is an ItemList of
                    // SiteNavigationElement entries; filtering on a watch URL keeps it out.
                    var slug = NartoDramaTitleMatcher.ExtractSlug(GetString(item, "url"));
                    if (slug is null || !seen.Add(slug))
                    {
                        continue;
                    }

                    var name = DecodeHtmlEntities(GetString(item, "name")).Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }

                    results.Add(new NartoDramaSeries
                    {
                        Slug = slug,
                        Name = name,
                        Cover = GetString(item, "image")
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Strips the noise the site wraps around a JSON-LD description.
        /// </summary>
        /// <param name="value">The raw description.</param>
        /// <param name="title">The clean series title, used to remove the title prefix.</param>
        /// <returns>The synopsis, or an empty string when the text holds no real synopsis.</returns>
        public static string CleanDescription(string? value, string? title)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = DecodeHtmlEntities(value).Trim();

            if (!string.IsNullOrWhiteSpace(title))
            {
                var prefix = title.Trim() + ".";
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(prefix.Length).Trim();
                }
            }

            // Everything from the platform pitch onwards describes NartoDrama, not the series.
            var pitch = text.IndexOf(PlatformPitchMarker, StringComparison.Ordinal);
            if (pitch >= 0)
            {
                text = text.Substring(0, pitch).Trim();
            }

            return string.Equals(text, PlatformPlaceholderDescription, StringComparison.Ordinal)
                ? string.Empty
                : text;
        }

        /// <summary>
        /// Builds the poster URL for a numeric content id.
        /// </summary>
        /// <param name="contentId">The numeric content id.</param>
        /// <returns>The cover URL, or an empty string when no id was supplied.</returns>
        /// <remarks>
        /// Only a fallback: the site serves both .jpg and .webp posters, so a cover the page
        /// published is always preferred over one rebuilt from the id.
        /// </remarks>
        public static string BuildPosterUrl(string? contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return string.Empty;
            }

            var id = contentId.Trim();
            if (!id.All(char.IsAsciiDigit))
            {
                return string.Empty;
            }

            return string.Concat(PosterHost, id, ".jpg");
        }

        private static string PickCover(string? jsonLdCover, string html, string contentId)
        {
            if (!string.IsNullOrWhiteSpace(jsonLdCover))
            {
                return jsonLdCover.Trim();
            }

            var ogImage = ReadAttribute(OgImageRegex, html);
            if (!string.IsNullOrWhiteSpace(ogImage))
            {
                return DecodeHtmlEntities(ogImage).Trim();
            }

            return BuildPosterUrl(contentId);
        }

        private static int ParseEpisodeNumber(Match hrefMatch)
        {
            if (!hrefMatch.Success)
            {
                return 0;
            }

            var href = hrefMatch.Groups["value"].Value;
            var cut = href.IndexOfAny(QuerySeparators);
            if (cut >= 0)
            {
                href = href.Substring(0, cut);
            }

            var segments = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return 0;
            }

            // The number is the last path segment: /detail/watch/<slug>/37.
            var last = segments[segments.Length - 1];
            return int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number
                : 0;
        }

        private static string ReadAttribute(Regex regex, string tag)
        {
            var match = regex.Match(tag);
            return match.Success ? match.Groups["value"].Value : string.Empty;
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

        private static string DecodeHtmlEntities(string value)
        {
            // The rendered text only ever needs these, all of which appear in the live catalogue:
            // "Assistir episódio 1 &amp; mais", "Don&#39;s", "&#34;O Rei&#34;" and "&nbsp;".
            return value
                .Replace("&amp;", "&", StringComparison.Ordinal)
                .Replace("&quot;", "\"", StringComparison.Ordinal)
                .Replace("&#39;", "'", StringComparison.Ordinal)
                .Replace("&#34;", "\"", StringComparison.Ordinal)
                .Replace("&nbsp;", " ", StringComparison.Ordinal)
                .Replace("&lt;", "<", StringComparison.Ordinal)
                .Replace("&gt;", ">", StringComparison.Ordinal);
        }
    }
}
