using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Providers.Books.OpenLibrary;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Jellyfin.Providers.Tests.Books;

public class OpenLibraryProviderTests
{
    [Fact]
    public async Task GetImages_WorkWithCovers_ReturnsCoverById()
    {
        var provider = CreateProvider(
            new[]
            {
                new Uri("https://openlibrary.org/works/OL26415696W.json"),
            },
            """
            {
              "key": "/works/OL26415696W",
              "title": "20 Mil Leguas Submarinas",
              "covers": [123456]
            }
            """);

        var book = new Book();
        book.SetProviderId("OpenLibrary", "OL26415696W");

        var images = (await provider.GetImages(book, CancellationToken.None)).ToList();

        Assert.Single(images);
        Assert.Equal(ImageType.Primary, images[0].Type);
        Assert.Equal("https://covers.openlibrary.org/b/id/123456-L.jpg", images[0].Url);
    }

    [Fact]
    public async Task GetImages_WorkWithoutCovers_ReturnsEditionCoverById()
    {
        var provider = CreateProvider(new Dictionary<Uri, string>
        {
            [new Uri("https://openlibrary.org/works/OL26415696W.json")] = """
            {
              "key": "/works/OL26415696W",
              "title": "20 Mil Leguas Submarinas"
            }
            """,
            [new Uri("https://openlibrary.org/works/OL26415696W/editions.json?limit=25")] = """
            {
              "entries": [
                {
                  "key": "/books/OL123M",
                  "title": "20 Mil Leguas Submarinas",
                  "covers": [987654],
                  "isbn_13": ["9780000000000"]
                }
              ]
            }
            """
        });

        var book = new Book();
        book.SetProviderId("OpenLibrary", "OL26415696W");

        var images = (await provider.GetImages(book, CancellationToken.None)).ToList();

        Assert.Single(images);
        Assert.Equal(ImageType.Primary, images[0].Type);
        Assert.Equal("https://covers.openlibrary.org/b/id/987654-L.jpg", images[0].Url);
    }

    [Fact]
    public async Task GetImages_WorkWithoutAnyCovers_DoesNotReturnInvalidWorkOlidCover()
    {
        var provider = CreateProvider(new Dictionary<Uri, string>
        {
            [new Uri("https://openlibrary.org/works/OL26415696W.json")] = """
            {
              "key": "/works/OL26415696W",
              "title": "20 Mil Leguas Submarinas"
            }
            """,
            [new Uri("https://openlibrary.org/works/OL26415696W/editions.json?limit=25")] = """
            {
              "entries": [
                {
                  "key": "/books/OL123M",
                  "title": "20 Mil Leguas Submarinas"
                }
              ]
            }
            """
        });

        var book = new Book();
        book.SetProviderId("OpenLibrary", "OL26415696W");

        var images = (await provider.GetImages(book, CancellationToken.None)).ToList();

        Assert.Empty(images);
    }

    [Fact]
    public async Task GetImages_IsbnWithoutCovers_ReturnsOpenLibraryFallbackCover()
    {
        var provider = CreateProvider(
            new[]
            {
                new Uri("https://openlibrary.org/api/books?bibkeys=ISBN:9788571641304&format=json&jscmd=data"),
            },
            """
            {
              "ISBN:9788571641304": {
                "title": "1984",
                "identifiers": {
                  "isbn_13": ["9788571641304"]
                }
              }
            }
            """);

        var book = new Book();
        book.SetProviderId("ISBN", "9788571641304");

        var images = (await provider.GetImages(book, CancellationToken.None)).ToList();

        Assert.Single(images);
        Assert.Equal(ImageType.Primary, images[0].Type);
        Assert.Equal("https://covers.openlibrary.org/b/isbn/9788571641304-L.jpg?default=false", images[0].Url);
    }

    [Fact]
    public async Task GetSearchResults_IsbnOnly_FindsCityscapeEdition()
    {
        var searchUrl = new Uri("https://openlibrary.org/search.json?isbn=9780786939398&fields=key,title,author_name,first_publish_year,isbn,cover_edition_key,cover_i&limit=10");
        var provider = CreateProvider(
            new[] { searchUrl },
            """
            {
              "docs": [{
                "key": "/works/OL14951688W",
                "title": "Cityscape",
                "author_name": ["Ari Marmell"],
                "first_publish_year": 2006,
                "isbn": ["9780786939398", "0786939397"],
                "cover_edition_key": "OL8144537M",
                "cover_i": 547543
              }]
            }
            """);
        var info = new BookInfo();
        info.ProviderIds["ISBN"] = "978-0-7869-3939-8";

        var results = (await provider.GetSearchResults(info, CancellationToken.None)).ToList();

        var result = Assert.Single(results);
        Assert.Equal("Cityscape", result.Name);
        Assert.Equal(2006, result.ProductionYear);
        Assert.Equal("OL8144537M", result.GetProviderId("OpenLibrary"));
        Assert.Equal("9780786939398", result.GetProviderId("ISBN"));
        Assert.Equal("https://covers.openlibrary.org/b/olid/OL8144537M-M.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task GetSearchResults_OpenLibraryEditionUrl_FindsBook()
    {
        var provider = CreateProvider(
            new[] { new Uri("https://openlibrary.org/books/OL8144537M.json") },
            """
            {
              "key": "/books/OL8144537M",
              "title": "Cityscape",
              "publish_date": "November 7, 2006",
              "isbn_13": ["9780786939398"],
              "covers": [547543]
            }
            """);

        var results = (await provider.GetSearchResults(
            new BookInfo { Name = "https://openlibrary.org/books/OL8144537M/Cityscape" },
            CancellationToken.None)).ToList();

        var result = Assert.Single(results);
        Assert.Equal("Cityscape", result.Name);
        Assert.Equal("OL8144537M", result.GetProviderId("OpenLibrary"));
        Assert.Equal("9780786939398", result.GetProviderId("ISBN"));
        Assert.Equal("https://covers.openlibrary.org/b/id/547543-L.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task GetSearchResults_NoisyLibraryTitle_SearchesTitleSuffix()
    {
        var searchUrl = new Uri("https://openlibrary.org/search.json?title=Cityscape&fields=key,title,author_name,first_publish_year,isbn,cover_edition_key,cover_i&limit=10");
        var provider = CreateProvider(
            new[] { searchUrl },
            """
            {
              "docs": [{
                "key": "/works/OL14951688W",
                "title": "Cityscape",
                "first_publish_year": 2006,
                "isbn": ["9780786939398"],
                "cover_edition_key": "OL8144537M",
                "cover_i": 547543
              }]
            }
            """);

        var results = (await provider.GetSearchResults(
            new BookInfo { Name = "D&D 3.5 - Livro Cityscape" },
            CancellationToken.None)).ToList();

        Assert.Equal("Cityscape", Assert.Single(results).Name);
    }

    [Fact]
    public async Task GetMetadata_Isbn_UsesCurrentEditionJsonFormat()
    {
        var responses = new Dictionary<Uri, string>
        {
            [new Uri("https://openlibrary.org/search.json?isbn=9780786939398&fields=key,title,author_name,first_publish_year,isbn,cover_edition_key,cover_i&limit=10")] = """
                {"docs":[{"key":"/works/OL14951688W","title":"Cityscape","first_publish_year":2006,"isbn":["9780786939398"],"cover_edition_key":"OL8144537M","cover_i":547543}]}
                """,
            [new Uri("https://openlibrary.org/books/OL8144537M.json")] = """
                {"key":"/books/OL8144537M","title":"Cityscape","publish_date":"November 7, 2006","publishers":["Wizards of the Coast"],"subjects":["Fantasy games"],"number_of_pages":160,"isbn_13":["9780786939398"],"isbn_10":["0786939397"],"covers":[547543],"authors":[{"key":"/authors/OL2876059A"}],"works":[{"key":"/works/OL14951688W"}]}
                """,
            [new Uri("https://openlibrary.org/authors/OL2876059A.json")] = """
                {"name":"Ari Marmell"}
                """
        };
        var provider = CreateProvider(responses);
        var info = new BookInfo();
        info.ProviderIds["ISBN"] = "9780786939398";

        var metadata = await provider.GetMetadata(info, CancellationToken.None);

        Assert.True(metadata.HasMetadata);
        Assert.Equal("Cityscape", metadata.Item!.Name);
        Assert.Equal(2006, metadata.Item.ProductionYear);
        Assert.Equal("Wizards of the Coast", Assert.Single(metadata.Item.Studios));
        Assert.Equal("OL8144537M", metadata.Item.GetProviderId("OpenLibrary"));
        Assert.Equal("9780786939398", metadata.Item.GetProviderId("ISBN"));
        Assert.Equal("https://covers.openlibrary.org/b/id/547543-L.jpg", Assert.Single(metadata.RemoteImages).Url);
        Assert.Equal("Ari Marmell", Assert.Single(metadata.People!).Name);
    }

    private static OpenLibraryProvider CreateProvider(Uri[] expectedUris, string responseBody)
        => CreateProvider(expectedUris.ToDictionary(uri => uri, _ => responseBody));

    private static OpenLibraryProvider CreateProvider(IReadOnlyDictionary<Uri, string> responses)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                Assert.NotNull(request.RequestUri);
                Assert.True(responses.TryGetValue(request.RequestUri!, out var responseBody), $"Unexpected request URI: {request.RequestUri}");
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(responseBody)
                });
            });

        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler.Object));

        return new OpenLibraryProvider(httpClientFactory.Object, NullLogger<OpenLibraryProvider>.Instance);
    }
}
