using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Reads the NetShort API envelopes and the JSON-LD block of a series page.
    /// </summary>
    /// <remarks>
    /// The API wraps everything in <c>{"code":200,"msg":"...","data":...}</c> and the shape of
    /// <c>data</c> differs per endpoint, so every value goes through a defensive accessor. The
    /// JSON-LD reader exists for the fallback path: the series page is server rendered and its
    /// <c>schema.org</c> payload carries the title, synopsis, cover, genres and episode count even
    /// when the encrypted API is unreachable.
    /// </remarks>
    public static class NetShortParser
    {
        private const string JsonLdMarker = "application/ld+json";

        /// <summary>
        /// Reads the status code of an API envelope.
        /// </summary>
        /// <param name="envelope">The parsed response body.</param>
        /// <returns>The code, or -1 when the payload carries none.</returns>
        public static int ParseResultCode(JsonNode? envelope)
        {
            return GetInt(envelope, "code", -1);
        }

        /// <summary>
        /// Reads the human readable message of an API envelope, for logging.
        /// </summary>
        /// <param name="envelope">The parsed response body.</param>
        /// <returns>The message, possibly empty.</returns>
        public static string ParseResultMessage(JsonNode? envelope)
        {
            return GetString(envelope, "msg");
        }

        /// <summary>
        /// Parses a keyword search envelope into series. Search results never carry episodes.
        /// </summary>
        /// <param name="envelope">The parsed response body.</param>
        /// <returns>The series found, in the order NetShort ranked them.</returns>
        public static IReadOnlyList<NetShortSeries> ParseSearchResults(JsonNode? envelope)
        {
            var results = new List<NetShortSeries>();
            if (envelope?["data"]?["list"] is not JsonArray list)
            {
                return results;
            }

            foreach (var node in list)
            {
                if (node is null)
                {
                    continue;
                }

                var series = ParseSeriesSummary(node);
                if (!string.IsNullOrEmpty(series.SeriesId))
                {
                    results.Add(series);
                }
            }

            return results;
        }

        /// <summary>
        /// Parses the v4 detail envelope into a series, including its episode list.
        /// </summary>
        /// <param name="envelope">The parsed response body.</param>
        /// <returns>The series, or null when the payload holds no series.</returns>
        public static NetShortSeries? ParseSeriesDetail(JsonNode? envelope)
        {
            var data = envelope?["data"];
            if (data is null)
            {
                return null;
            }

            var series = ParseSeriesSummary(data);
            if (string.IsNullOrEmpty(series.SeriesId))
            {
                return null;
            }

            series.PagePath = GetString(data, "shortPlayUrl");
            series.Episodes = ParseEpisodeArray(data["videoEpisodeInfos"]);
            series.EpisodeCount = Math.Max(series.EpisodeCount, series.Episodes.Count);

            return series;
        }

        /// <summary>
        /// Extracts the first schema.org TVSeries node from a NetShort page.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The series, or null when the page carries no JSON-LD series.</returns>
        public static NetShortSeries? ParseJsonLdSeries(string? html)
        {
            var node = ParseJsonLdGraph(html);
            if (node is null)
            {
                return null;
            }

            var series = new NetShortSeries
            {
                Name = GetString(node, "name"),
                Overview = GetString(node, "description"),
                Cover = GetString(node, "image"),
                Genres = GetStringArray(node, "genre"),
                EpisodeCount = GetInt(node, "numberOfEpisodes", 0),
                PagePath = GetString(node, "url")
            };

            // The series id only appears in the @id and in the page URL, never as its own property.
            series.SeriesId = NetShortTitleMatcher.ExtractSeriesId(GetString(node, "@id")) ?? string.Empty;

            if (string.IsNullOrEmpty(series.SeriesId))
            {
                return null;
            }

            series.Episodes = ParseJsonLdEpisodes(node);

            // The schema.org ItemList is paginated on the site, so it can list fewer episodes than
            // numberOfEpisodes. The real numbers it does carry are still worth keeping.
            if (series.Episodes.Count > 0 && series.EpisodeCount < series.Episodes.Count)
            {
                series.EpisodeCount = series.Episodes.Count;
            }

            return series;
        }

        private static JsonNode? ParseJsonLdGraph(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var markerIndex = html.IndexOf(JsonLdMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return null;
            }

            var openTag = html.IndexOf('>', markerIndex);
            if (openTag < 0)
            {
                return null;
            }

            var closeTag = html.IndexOf("</script>", openTag, StringComparison.Ordinal);
            if (closeTag < 0)
            {
                return null;
            }

            var payload = html.Substring(openTag + 1, closeTag - openTag - 1);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                var graph = JsonNode.Parse(payload)?["@graph"];
                if (graph is not JsonArray nodes)
                {
                    return null;
                }

                foreach (var candidate in nodes)
                {
                    if (candidate is not null
                        && string.Equals(GetString(candidate, "@type"), "TVSeries", StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }

                return null;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private static IReadOnlyList<NetShortEpisode> ParseJsonLdEpisodes(JsonNode seriesNode)
        {
            var episodes = new List<NetShortEpisode>();
            if (seriesNode["hasPart"]?["itemListElement"] is not JsonArray items)
            {
                return episodes;
            }

            foreach (var listItem in items)
            {
                var item = listItem?["item"];
                if (item is null)
                {
                    continue;
                }

                var number = GetInt(item, "episodeNumber", 0);
                if (number <= 0)
                {
                    continue;
                }

                episodes.Add(new NetShortEpisode
                {
                    Number = number,
                    Cover = string.Empty
                });
            }

            return episodes;
        }

        private static NetShortSeries ParseSeriesSummary(JsonNode? node)
        {
            // Search results highlight the matched term with a <span>, so the plain field wins when
            // the platform sends one.
            var name = GetString(node, "shortPlayNameNoHL");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = StripMarkup(GetString(node, "shortPlayName"));
            }

            return new NetShortSeries
            {
                SeriesId = GetString(node, "shortPlayId"),
                Name = name,
                Overview = GetString(node, "shotIntroduce"),
                Cover = GetString(node, "shortPlayCover"),
                PagePath = GetString(node, "shortPlayNameUrl"),
                Genres = ParseGenres(node),
                PremiereDate = ParsePublishTime(node)
            };
        }

        private static IReadOnlyList<NetShortEpisode> ParseEpisodeArray(JsonNode? node)
        {
            var episodes = new List<NetShortEpisode>();
            if (node is not JsonArray array)
            {
                return episodes;
            }

            foreach (var entry in array)
            {
                if (entry is null)
                {
                    continue;
                }

                var number = GetInt(entry, "episodeNo", 0);
                if (number <= 0)
                {
                    continue;
                }

                var cover = GetString(entry, "episodeCover");
                if (string.IsNullOrWhiteSpace(cover))
                {
                    cover = GetString(entry, "episodeWebCover");
                }

                episodes.Add(new NetShortEpisode
                {
                    EpisodeId = GetString(entry, "episodeId"),
                    Number = number,
                    Cover = cover,
                    Locked = GetBool(entry, "isLock")
                });
            }

            return episodes;
        }

        private static IReadOnlyList<string> ParseGenres(JsonNode? node)
        {
            // The detail payload carries an object of label name to label URL; the search payload a
            // comma separated string.
            var genres = new List<string>();
            if (node?["shortPlayLabels"] is JsonObject labels)
            {
                foreach (var label in labels)
                {
                    if (!string.IsNullOrWhiteSpace(label.Key))
                    {
                        genres.Add(label.Key);
                    }
                }

                return genres;
            }

            foreach (var value in GetString(node, "labelNames").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                genres.Add(value);
            }

            return genres;
        }

        private static DateTime? ParsePublishTime(JsonNode? node)
        {
            var raw = GetString(node, "publishTime");
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>
        /// Drops the search highlighting markup NetShort wraps around the matched term.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The title without tags.</returns>
        private static string StripMarkup(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length);
            var insideTag = false;

            foreach (var character in value)
            {
                if (character == '<')
                {
                    insideTag = true;
                }
                else if (character == '>')
                {
                    insideTag = false;
                }
                else if (!insideTag)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string GetString(JsonNode? node, string name)
        {
            var value = node?[name];
            if (value is null)
            {
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

        private static int GetInt(JsonNode? node, string name, int fallback)
        {
            var raw = GetString(node, name);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static bool GetBool(JsonNode? node, string name)
        {
            var raw = GetString(node, name);
            return bool.TryParse(raw, out var parsed) && parsed;
        }

        private static IReadOnlyList<string> GetStringArray(JsonNode? node, string name)
        {
            var values = new List<string>();
            if (node?[name] is not JsonArray array)
            {
                return values;
            }

            foreach (var item in array)
            {
                var value = item?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }
    }
}
