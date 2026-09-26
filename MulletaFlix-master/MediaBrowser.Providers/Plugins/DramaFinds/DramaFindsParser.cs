using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Parses the DramaFinds JSON API envelopes.
    /// </summary>
    /// <remarks>
    /// The API answers HTTP 200 even for its own failures, carrying {"code":10,"msg":"GuestId
    /// Missing"} instead of a 4xx, so every caller has to read <see cref="ReadCode"/> before
    /// trusting a payload. The search rows and the detail payload also do not share a shape: the
    /// search carries the episode count in "episodes" and reports "totalCount" only on the detail,
    /// which in turn reports "episodes" as null. Every value is therefore read through a defensive
    /// accessor.
    /// </remarks>
    public static class DramaFindsParser
    {
        private const int SuccessCode = 0;

        /// <summary>
        /// Parses a raw API body into its envelope node.
        /// </summary>
        /// <param name="json">The raw body.</param>
        /// <returns>The envelope, or null when the body is empty or not JSON.</returns>
        public static JsonNode? ParseEnvelope(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the API status code from an envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns>The code, or 0 when absent.</returns>
        public static int ReadCode(JsonNode? envelope)
        {
            return GetInt(envelope, "code");
        }

        /// <summary>
        /// Reads the API status message from an envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns>The message, or an empty string.</returns>
        public static string ReadMessage(JsonNode? envelope)
        {
            return GetString(envelope, "msg");
        }

        /// <summary>
        /// Reports whether an envelope carries a usable payload.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns>True when the payload can be trusted.</returns>
        public static bool IsSuccess(JsonNode? envelope)
        {
            return envelope is not null && ReadCode(envelope) == SuccessCode;
        }

        /// <summary>
        /// Parses the drama rows of a search response.
        /// </summary>
        /// <param name="envelope">The envelope of POST /v1/short-dramas/search.</param>
        /// <returns>The dramas, which is empty when the search matched nothing.</returns>
        public static IReadOnlyList<DramaFindsDrama> ParseSearchResults(JsonNode? envelope)
        {
            var results = new List<DramaFindsDrama>();
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

                var drama = ParseDrama(node, "episodes");
                if (!string.IsNullOrEmpty(drama.DramaId))
                {
                    results.Add(drama);
                }
            }

            return results;
        }

        /// <summary>
        /// Parses a drama detail payload into a drama that carries its synthesized episodes.
        /// </summary>
        /// <param name="envelope">The envelope of POST /v1/short-dramas/episodes.</param>
        /// <returns>The drama, or null when the payload holds no drama.</returns>
        public static DramaFindsDrama? ParseDramaDetail(JsonNode? envelope)
        {
            if (envelope?["data"] is not JsonNode data)
            {
                return null;
            }

            var drama = ParseDrama(data, "totalCount");
            if (string.IsNullOrEmpty(drama.DramaId))
            {
                return null;
            }

            drama.Episodes = BuildEpisodes(drama);
            return drama;
        }

        /// <summary>
        /// Synthesizes the episode list of a drama.
        /// </summary>
        /// <param name="drama">The drama, which must carry its episode count and title.</param>
        /// <returns>Episodes 1 to the episode count, in order.</returns>
        public static IReadOnlyList<DramaFindsEpisode> BuildEpisodes(DramaFindsDrama drama)
        {
            var episodes = new List<DramaFindsEpisode>();
            for (var number = 1; number <= drama.EpisodeCount; number++)
            {
                episodes.Add(new DramaFindsEpisode
                {
                    Number = number,
                    Name = string.Format(CultureInfo.InvariantCulture, "Episódio {0}", number),
                    Url = DramaFindsTitleMatcher.BuildEpisodeUrl(drama.DramaId, drama.Title, number)
                });
            }

            return episodes;
        }

        private static DramaFindsDrama ParseDrama(JsonNode node, string episodeCountField)
        {
            return new DramaFindsDrama
            {
                DramaId = GetString(node, "dramaId"),
                Md5Id = GetString(node, "md5Id"),
                Title = GetString(node, "title"),
                Overview = GetString(node, "description"),
                Cover = DramaFindsTitleMatcher.StripAuthKey(GetString(node, "thumbnailUrl")),
                Rating = GetDouble(node, "rating"),
                EpisodeCount = GetInt(node, episodeCountField),
                ReleaseDate = GetString(node, "releaseDate"),
                Country = GetString(node, "country"),
                ViewsCount = GetLong(node, "viewsCount"),
                LikesCount = GetLong(node, "likesCount"),
                FreeCount = GetInt(node, "freeCount"),
                Tags = GetStringArray(node, "tags")
            };
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
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
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
