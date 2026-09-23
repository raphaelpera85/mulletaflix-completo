using System.Linq;
using System.Text.Json;
using MediaBrowser.Providers.Plugins.DramaBox;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaBox
{
    public class DramaBoxParserTests
    {
        /// <summary>
        /// Trimmed but structurally faithful copy of the real detail payload for book 42000002641
        /// ("3.2.1, Adeus e Ponto Final"). The field names, the Chinese episode names and the
        /// zero-based chapter indexes are exactly what the live page returns.
        /// </summary>
        private const string DetailHtml = """
            <!DOCTYPE html><html lang="pt"><head><title>DramaBox</title></head><body>
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{
              "bookInfo":{
                "bookId":"42000002641",
                "bookName":"3.2.1, Adeus e Ponto Final",
                "bookNameEn":"3-2-1-Farewell-Forever",
                "cover":"https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000004501/42000004501.jpg@w=360&h=640",
                "viewCount":4565929,
                "followCount":112161,
                "introduction":"A decisão de Júlia está tomada: ela vai deixar o Walter.",
                "chapterCount":56,
                "labels":["Strong Female Lead","Love Triangle"],
                "tags":["Strong Female Lead","Love Triangle"],
                "typeTwoNames":["Liderança Feminina"],
                "language":"PORTUGUESE",
                "simpleLanguage":"pt",
                "shelfTime":"2026-01-29 10:00:26",
                "firstShelfTime":"2026-01-29 10:00:26",
                "performerList":[
                  {"performerId":"1","performerName":"Cayman Cardiff","performerFormatName":"Cayman-Cardiff","performerAvatar":"https://hwztchapter.dramaboxdb.com/data/cppartner/8x8/12188/12188.jpg","videoCount":3},
                  {"performerId":"2","performerName":"Kylie Karson","performerFormatName":"Kylie-Karson","performerAvatar":"https://hwztchapter.dramaboxdb.com/data/cppartner/9x2/29329/29329.jpg","videoCount":2}
                ]
              },
              "chapterList":[
                {"id":"700157514","name":"第一集","index":0,"unlock":true,"mp4":"https://hwvideoseo.dramaboxdb.com/a.mp4?Expires=1790168400","m3u8Flag":false,"cover":"https://thwztvideo.dramaboxdb.com/80/1x0/10x5/105x4/10540000024/700327408_2/700327408.mp4.jpg@w=100&h=135","chapterPrice":0,"duration":134489},
                {"id":"700157515","name":"第二集","index":1,"unlock":true,"mp4":"https://hwvideoseo.dramaboxdb.com/b.mp4?Expires=1790168400","m3u8Flag":false,"cover":"https://thwztvideo.dramaboxdb.com/90/1x0/10x5/105x4/10540000024/700327409_2/700327409.mp4.jpg@w=100&h=135","chapterPrice":0,"duration":133050}
              ],
              "locale":"pt"
            }}}</script>
            </body></html>
            """;

        private const string ListingHtml = """
            <!DOCTYPE html><html lang="pt"><head><title>DramaBox</title></head><body>
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{
              "types":[{"id":0,"name":"all","replaceName":"all"},{"id":628,"name":"Liderança Feminina","replaceName":"liderança-feminina"}],
              "bookList":[
                {
                  "bookId":"42000027795",
                  "bookName":"Renascida das Cinzas: A Vingança contra Meus Irmãos",
                  "cover":"https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000027795/42000027795.jpg@w=240&h=400",
                  "ratings":9.3,
                  "introduction":"Traída pelos quatro irmãos adotivos que mais amava.",
                  "tags":["Strong Female Lead","Revenge"],
                  "chapterCount":42,
                  "typeTwoNames":["Liderança Feminina"],
                  "language":"PORTUGUESE",
                  "simpleLanguage":"pt",
                  "shelfTime":"2026-09-22 11:00:00",
                  "status":"PUBLISHED"
                },
                {
                  "bookId":"42000002641",
                  "bookName":"3.2.1, Adeus e Ponto Final",
                  "cover":"https://thwztchapter.dramaboxdb.com/data/cppartner/4x2/42x0/420x0/42000004501/42000004501.jpg@w=240&h=400",
                  "introduction":"A decisão de Júlia está tomada.",
                  "chapterCount":56,
                  "typeTwoNames":["Liderança Feminina"],
                  "language":"PORTUGUESE",
                  "simpleLanguage":"pt"
                }
              ],
              "pageNo":1,
              "pages":111
            }}}</script>
            </body></html>
            """;

        [Fact]
        public void ParsePageProps_ExtractsPayloadFromTheScriptTag()
        {
            var pageProps = DramaBoxParser.ParsePageProps(DetailHtml);

            Assert.NotNull(pageProps);
            Assert.Equal("42000002641", pageProps!["bookInfo"]!["bookId"]!.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html><body>no payload here</body></html>")]
        [InlineData("<script id=\"__NEXT_DATA__\" type=\"application/json\">not json</script>")]
        public void ParsePageProps_ReturnsNullInsteadOfThrowing(string html)
        {
            Assert.Null(DramaBoxParser.ParsePageProps(html));
        }

        [Fact]
        public void ParseBookDetail_ReadsEveryFieldTheProvidersNeed()
        {
            var pageProps = DramaBoxParser.ParsePageProps(DetailHtml);
            var book = DramaBoxParser.ParseBookDetail(pageProps);

            Assert.NotNull(book);
            Assert.Equal("42000002641", book!.BookId);
            Assert.Equal("3.2.1, Adeus e Ponto Final", book.Name);
            Assert.Equal("3-2-1-Farewell-Forever", book.NameEn);
            Assert.Equal(56, book.ChapterCount);
            Assert.Equal("PORTUGUESE", book.Language);
            Assert.Equal("pt", book.SimpleLanguage);
            Assert.Equal("2026-01-29 10:00:26", book.ShelfTime);
            Assert.StartsWith("A decisão de Júlia", book.Overview, System.StringComparison.Ordinal);
            Assert.Contains("thwztchapter.dramaboxdb.com", book.Cover, System.StringComparison.Ordinal);
            Assert.Equal(new[] { "Liderança Feminina" }, book.Genres);
            Assert.Equal(new[] { "Strong Female Lead", "Love Triangle" }, book.Tags);
        }

        [Fact]
        public void ParseBookDetail_ReadsCastWithAvatars()
        {
            var book = DramaBoxParser.ParseBookDetail(DramaBoxParser.ParsePageProps(DetailHtml));

            Assert.NotNull(book);
            Assert.Equal(2, book!.Performers.Count);
            Assert.Equal("Cayman Cardiff", book.Performers[0].Name);
            Assert.Equal("Cayman-Cardiff", book.Performers[0].FormatName);
            Assert.Contains("12188.jpg", book.Performers[0].Avatar, System.StringComparison.Ordinal);
            Assert.Equal("Kylie Karson", book.Performers[1].Name);
        }

        [Fact]
        public void ParseBookDetail_ReadsChaptersWithZeroBasedIndexesAndMillisecondDurations()
        {
            var book = DramaBoxParser.ParseBookDetail(DramaBoxParser.ParsePageProps(DetailHtml));

            Assert.NotNull(book);
            Assert.Equal(2, book!.Chapters.Count);

            var first = book.Chapters[0];
            Assert.Equal(0, first.Index);
            Assert.Equal("700157514", first.Id);
            Assert.Equal(134489, first.DurationMs);
            Assert.True(first.Unlock);
            Assert.False(first.M3u8);
            Assert.Contains("700327408.mp4.jpg", first.Cover, System.StringComparison.Ordinal);

            Assert.Equal(1, book.Chapters[1].Index);

            // The episode names DramaBox returns are Chinese even on the Portuguese pages, which is
            // why the episode provider derives "Episódio N" instead of copying this value.
            Assert.Equal("第一集", first.Name);
        }

        [Fact]
        public void ParseBookList_ReadsListingFieldsIncludingRating()
        {
            var pageProps = DramaBoxParser.ParsePageProps(ListingHtml);
            var books = DramaBoxParser.ParseBookList(pageProps);

            Assert.Equal(2, books.Count);
            Assert.Equal("42000027795", books[0].BookId);
            Assert.Equal("Renascida das Cinzas: A Vingança contra Meus Irmãos", books[0].Name);
            Assert.Equal(9.3, books[0].Rating);
            Assert.Equal(42, books[0].ChapterCount);
            Assert.Equal("Liderança Feminina", books[0].Genres.Single());

            // A listing carries no episodes; those only exist on the detail page.
            Assert.Empty(books[0].Chapters);
            Assert.Null(books[1].Rating);
        }

        [Fact]
        public void ParsePageCount_ReadsTheTotalPageCount()
        {
            Assert.Equal(111, DramaBoxParser.ParsePageCount(DramaBoxParser.ParsePageProps(ListingHtml)));
        }

        [Fact]
        public void ParseBookDetail_ReturnsNullWhenThePayloadHasNoBook()
        {
            Assert.Null(DramaBoxParser.ParseBookDetail(DramaBoxParser.ParsePageProps(ListingHtml)));
            Assert.Null(DramaBoxParser.ParseBookDetail(null));
        }

        /// <summary>
        /// Guards the on-disk index cache: the model exposes IReadOnlyList collections, which must
        /// survive a serialize/deserialize round trip or every lookup after a restart sees no books.
        /// </summary>
        [Fact]
        public void IndexDocument_SurvivesAJsonRoundTrip()
        {
            var book = DramaBoxParser.ParseBookDetail(DramaBoxParser.ParsePageProps(DetailHtml));
            var document = new DramaBoxIndexDocument
            {
                BuiltUtc = System.DateTimeOffset.UtcNow,
                Locale = "pt",
                Books = new System.Collections.Generic.List<DramaBoxBook> { book! }
            };

            var json = JsonSerializer.Serialize(document);
            var restored = JsonSerializer.Deserialize<DramaBoxIndexDocument>(json);

            Assert.NotNull(restored);
            var restoredBook = restored!.Books.Single();
            Assert.Equal("42000002641", restoredBook.BookId);
            Assert.Equal("3.2.1, Adeus e Ponto Final", restoredBook.Name);
            Assert.Equal(new[] { "Liderança Feminina" }, restoredBook.Genres);
            Assert.Equal(new[] { "Strong Female Lead", "Love Triangle" }, restoredBook.Tags);
            Assert.Equal(2, restoredBook.Performers.Count);
            Assert.Equal("Cayman Cardiff", restoredBook.Performers[0].Name);
            Assert.Equal(2, restoredBook.Chapters.Count);
            Assert.Equal(134489, restoredBook.Chapters[0].DurationMs);
        }
    }
}
