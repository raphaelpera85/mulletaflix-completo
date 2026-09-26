using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Providers.Plugins.DramaBox;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.DramaBox
{
    /// <summary>
    /// Live verification against the real DramaBox site.
    /// </summary>
    /// <remarks>
    /// The type name deliberately contains "Integration" so the CI filter
    /// (FullyQualifiedName!~Integration) skips it: it reaches the public internet and must never
    /// gate a build. Run it on demand with
    /// <c>dotnet test --filter "FullyQualifiedName~DramaBoxLiveIntegration"</c>.
    /// It exists because every other test in this folder uses a captured payload, and a captured
    /// payload cannot prove that the crawler still parses what the site serves today.
    /// </remarks>
    public sealed class DramaBoxLiveIntegrationTests : IDisposable
    {
        private readonly string _cachePath = Path.Combine(
            Path.GetTempPath(),
            "dramabox-live-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task RebuildIndex_FindsTheRealCatalogueAndMatchesRealLibraryTitles()
        {
            var client = CreateClient();

            var index = await client.RebuildIndexAsync(null, CancellationToken.None);

            // The Portuguese catalogue reported 111 listing pages of 12 books when this was written.
            Assert.True(
                index.Books.Count > 500,
                $"Expected a substantial catalogue but the crawl produced {index.Books.Count} books.");
            Assert.True(File.Exists(client.IndexFilePath), "The crawl did not persist the index file.");

            // The exact title from the drama URL the user reported.
            var exact = await client.MatchByTitleAsync("3.2.1, Adeus e Ponto Final", 0.92, CancellationToken.None);
            Assert.NotNull(exact);
            Assert.Equal("42000002641", exact!.Book.BookId);

            // The same series as MulletaFlix actually stored it, with the separator dot folded to a
            // space by the naming pipeline. This is the case that was failing to be identified.
            var asStored = await client.MatchByTitleAsync("3.2 1, Adeus e Ponto Final", 0.92, CancellationToken.None);
            Assert.NotNull(asStored);
            Assert.Equal("42000002641", asStored!.Book.BookId);

            // A pasted URL resolves exactly and needs no index.
            var byUrl = await client.GetBookAsync(
                DramaBoxClient.ResolveBookId("https://www.dramabox.com/pt/drama/42000002641/321-Adeus-e-Ponto-Final"),
                CancellationToken.None);
            Assert.NotNull(byUrl);
            Assert.Equal("3.2.1, Adeus e Ponto Final", byUrl!.Name);
            Assert.Equal(56, byUrl.Chapters.Count);

            // Episode data the episode provider depends on.
            var firstChapter = byUrl.Chapters[0];
            Assert.Equal(0, firstChapter.Index);
            Assert.True(firstChapter.DurationMs > 0, "The live chapter carried no duration.");
            Assert.False(string.IsNullOrWhiteSpace(firstChapter.Cover), "The live chapter carried no cover.");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_cachePath))
                {
                    Directory.Delete(_cachePath, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }

            GC.SuppressFinalize(this);
        }

        private DramaBoxClient CreateClient()
        {
            var paths = new Mock<IServerApplicationPaths>();
            paths.SetupGet(path => path.CachePath).Returns(_cachePath);

            // A real HttpClient rather than a mocked handler: the point of this test is the wire.
            // One instance is shared for the whole crawl, mirroring IHttpClientFactory pooling;
            // handing out a fresh handler per request exhausts sockets partway through 111 pages.
            var httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(factory => factory.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            return new DramaBoxClient(
                httpClientFactory.Object,
                paths.Object,
                new Mock<ILogger<DramaBoxClient>>().Object);
        }
    }
}
