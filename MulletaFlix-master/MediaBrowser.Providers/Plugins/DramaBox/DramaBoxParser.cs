using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Parses the JSON that the DramaBox Next.js pages embed in their __NEXT_DATA__ script tag.
    /// </summary>
    /// <remarks>
    /// DramaBox has no unauthenticated JSON API: the backend host (sapi.dramaboxdb.com) answers
    /// 403 without the app signature, and the site's own /search page returns an empty result set
    /// server side. The public drama and category pages, however, are server rendered and carry
    /// the complete payload. Every value is read through a defensive accessor because the shape
    /// differs between the detail payload and the listing payload.
    /// </remarks>
    public static class DramaBoxParser
    {
        private const string NextDataMarker = "__NEXT_DATA__";

        /// <summary>
        /// Extracts the pageProps node from a DramaBox HTML document.
        /// </summary>
        /// <param name="html">The raw HTML.</param>
        /// <returns>The pageProps node, or null when the document has no payload.</returns>
        public static JsonNode? ParsePageProps(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var markerIndex = html.IndexOf(NextDataMarker, StringComparison.Ordinal);
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
                return JsonNode.Parse(payload)?["props"]?["pageProps"];
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses a drama detail page into a book that carries its episode list.
        /// </summary>
        /// <param name="pageProps">The pageProps node.</param>
        /// <returns>The book, or null when the payload holds no bookInfo.</returns>
        public static DramaBoxBook? ParseBookDetail(JsonNode? pageProps)
        {
            var bookInfo = pageProps?["bookInfo"];
            if (bookInfo is null)
            {
                return null;
            }

            var book = ParseBook(bookInfo);
            if (string.IsNullOrEmpty(book.BookId))
            {
                return null;
            }

            var chapters = new List<DramaBoxChapter>();
            if (pageProps?["chapterList"] is JsonArray chapterArray)
            {
                foreach (var chapterNode in chapterArray)
                {
                    if (chapterNode is null)
                    {
                        continue;
                    }

                    chapters.Add(new DramaBoxChapter
                    {
                        Id = GetString(chapterNode, "id"),
                        Index = GetInt(chapterNode, "index"),
                        Name = GetString(chapterNode, "name"),
                        Cover = GetString(chapterNode, "cover"),
                        DurationMs = GetLong(chapterNode, "duration"),
                        Unlock = GetBool(chapterNode, "unlock"),
                        M3u8 = GetBool(chapterNode, "m3u8Flag")
                    });
                }
            }

            book.Chapters = chapters;
            if (book.ChapterCount <= 0)
            {
                book.ChapterCount = chapters.Count;
            }

            return book;
        }

        /// <summary>
        /// Parses a category listing page into books. Listing books never carry episodes.
        /// </summary>
        /// <param name="pageProps">The pageProps node.</param>
        /// <returns>The books found on the page.</returns>
        public static IReadOnlyList<DramaBoxBook> ParseBookList(JsonNode? pageProps)
        {
            var books = new List<DramaBoxBook>();
            if (pageProps?["bookList"] is not JsonArray bookArray)
            {
                return books;
            }

            foreach (var bookNode in bookArray)
            {
                if (bookNode is null)
                {
                    continue;
                }

                var book = ParseBook(bookNode);
                if (!string.IsNullOrEmpty(book.BookId))
                {
                    books.Add(book);
                }
            }

            return books;
        }

        /// <summary>
        /// Reads the total page count of a listing payload.
        /// </summary>
        /// <param name="pageProps">The pageProps node.</param>
        /// <returns>The page count, or 0 when absent.</returns>
        public static int ParsePageCount(JsonNode? pageProps)
        {
            return GetInt(pageProps, "pages");
        }

        private static DramaBoxBook ParseBook(JsonNode node)
        {
            return new DramaBoxBook
            {
                BookId = GetString(node, "bookId"),
                Name = GetString(node, "bookName"),
                NameEn = GetString(node, "bookNameEn"),
                Cover = GetString(node, "cover"),
                Overview = GetString(node, "introduction"),
                ChapterCount = GetInt(node, "chapterCount"),
                Language = GetString(node, "language"),
                SimpleLanguage = GetString(node, "simpleLanguage"),
                Rating = GetDouble(node, "ratings"),
                ShelfTime = GetString(node, "shelfTime"),
                Genres = GetStringArray(node, "typeTwoNames"),
                Tags = GetStringArray(node, "tags"),
                Performers = ParsePerformers(node)
            };
        }

        private static IReadOnlyList<DramaBoxPerformer> ParsePerformers(JsonNode node)
        {
            var performers = new List<DramaBoxPerformer>();
            if (node["performerList"] is not JsonArray performerArray)
            {
                return performers;
            }

            foreach (var performerNode in performerArray)
            {
                if (performerNode is null)
                {
                    continue;
                }

                var name = GetString(performerNode, "performerName");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                performers.Add(new DramaBoxPerformer
                {
                    Name = name,
                    FormatName = GetString(performerNode, "performerFormatName"),
                    Avatar = GetString(performerNode, "performerAvatar")
                });
            }

            return performers;
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
