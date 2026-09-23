using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Fetches DramaBox data and maintains the local title to book id index.
    /// </summary>
    /// <remarks>
    /// DramaBox exposes no search endpoint to unauthenticated callers, so name resolution is
    /// served from an index crawled out of the public category listings. The crawl is bounded and
    /// polite: the whole Portuguese catalogue is 111 listing pages of 12 books, and requests are
    /// serialized with a fixed delay.
    /// </remarks>
    public sealed class DramaBoxClient : IDisposable
    {
        private const string SiteRoot = "https://www.dramabox.com";
        private const string DefaultLocale = "pt";
        private const string IndexFileName = "index.json";
        private const int IndexFileVersion = 1;

        /// <summary>
        /// How many listing pages in a row may fail before the crawl gives up. The live catalogue is
        /// 111 pages, so tolerating a few isolated failures keeps a complete index while still
        /// stopping when the site is genuinely unreachable.
        /// </summary>
        private const int MaxConsecutiveFailures = 3;

        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(600);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerApplicationPaths _applicationPaths;
        private readonly ILogger<DramaBoxClient> _logger;

        private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, DramaBoxBook> _bookCache = new Dictionary<string, DramaBoxBook>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _indexTokens = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private DramaBoxIndexDocument? _index;
        private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="applicationPaths">The application paths, used to persist the index.</param>
        /// <param name="logger">The logger.</param>
        public DramaBoxClient(
            IHttpClientFactory httpClientFactory,
            IServerApplicationPaths applicationPaths,
            ILogger<DramaBoxClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        /// <summary>
        /// Gets the locale used for crawls and lookups.
        /// </summary>
        public static string Locale => DefaultLocale;

        /// <summary>
        /// Gets the path of the persisted index file.
        /// </summary>
        public string IndexFilePath => Path.Combine(_applicationPaths.CachePath, "dramabox", IndexFileName);

        /// <summary>
        /// Fetches one book, including its episode list, by book id.
        /// </summary>
        /// <param name="bookId">The numeric book id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The book, or null when it could not be read.</returns>
        public async Task<DramaBoxBook?> GetBookAsync(string? bookId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(bookId))
            {
                return null;
            }

            lock (_cacheLock)
            {
                if (_bookCache.TryGetValue(bookId, out var cached))
                {
                    return cached;
                }
            }

            var pageProps = await GetPagePropsAsync(BuildBookUrl(bookId), cancellationToken).ConfigureAwait(false);
            var book = DramaBoxParser.ParseBookDetail(pageProps);
            if (book is null)
            {
                _logger.LogWarning("DramaBox returned no book payload for id {BookId}", bookId);
                return null;
            }

            // The listing index already holds a good synopsis and cover for most books; prefer the
            // detail payload but keep the indexed values when the detail page omits them.
            var indexed = FindInIndexById(bookId);
            if (indexed is not null)
            {
                if (string.IsNullOrWhiteSpace(book.Name))
                {
                    book.Name = indexed.Name;
                }

                if (string.IsNullOrWhiteSpace(book.Overview))
                {
                    book.Overview = indexed.Overview;
                }

                if (string.IsNullOrWhiteSpace(book.Cover))
                {
                    book.Cover = indexed.Cover;
                }

                if (book.Rating is null)
                {
                    book.Rating = indexed.Rating;
                }

                if (book.Genres.Count == 0)
                {
                    book.Genres = indexed.Genres;
                }

                if (book.Tags.Count == 0)
                {
                    book.Tags = indexed.Tags;
                }

                if (string.IsNullOrWhiteSpace(book.ShelfTime))
                {
                    book.ShelfTime = indexed.ShelfTime;
                }
            }

            lock (_cacheLock)
            {
                _bookCache[bookId] = book;
            }

            return book;
        }

        /// <summary>
        /// Resolves a book id from any input a user may paste, such as a drama URL.
        /// </summary>
        /// <param name="input">The raw input.</param>
        /// <returns>The book id, or null.</returns>
        public static string? ResolveBookId(string? input)
        {
            return DramaBoxTitleMatcher.ExtractBookId(input);
        }

        /// <summary>
        /// Finds the best index match for a library title, without touching the network.
        /// </summary>
        /// <param name="title">The library title.</param>
        /// <param name="minimumScore">The minimum score to accept.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The match, or null when nothing scored high enough.</returns>
        public async Task<DramaBoxMatch?> MatchByTitleAsync(string? title, double minimumScore, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index is null || index.Books.Count == 0)
            {
                return null;
            }

            DramaBoxBook? best = null;
            var bestScore = 0.0;

            lock (_cacheLock)
            {
                foreach (var book in index.Books)
                {
                    if (!_indexTokens.TryGetValue(book.BookId, out var tokens))
                    {
                        continue;
                    }

                    var score = DramaBoxTitleMatcher.Similarity(tokens, title);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = book;
                    }
                }
            }

            if (best is null || bestScore < minimumScore)
            {
                return null;
            }

            return new DramaBoxMatch(best, bestScore);
        }

        /// <summary>
        /// Searches the local index by title.
        /// </summary>
        /// <param name="query">The search text.</param>
        /// <param name="maxResults">The maximum number of results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked matches.</returns>
        public async Task<IReadOnlyList<DramaBoxMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<DramaBoxMatch>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // A pasted URL or a bare id resolves exactly and needs no index.
            var directId = ResolveBookId(query);
            if (directId is not null)
            {
                var book = await GetBookAsync(directId, cancellationToken).ConfigureAwait(false);
                if (book is not null)
                {
                    results.Add(new DramaBoxMatch(book, 1.0));
                    return results;
                }
            }

            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index is null || index.Books.Count == 0)
            {
                return results;
            }

            var ranked = new List<DramaBoxMatch>();
            lock (_cacheLock)
            {
                foreach (var book in index.Books)
                {
                    if (!_indexTokens.TryGetValue(book.BookId, out var tokens))
                    {
                        continue;
                    }

                    var score = DramaBoxTitleMatcher.Similarity(tokens, query);
                    if (score > 0.34)
                    {
                        ranked.Add(new DramaBoxMatch(book, score));
                    }
                }
            }

            return ranked
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Book.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Loads the index from memory, falling back to the file on disk. Never crawls.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The index, or null when none has been built yet.</returns>
        public async Task<DramaBoxIndexDocument?> LoadIndexAsync(CancellationToken cancellationToken)
        {
            lock (_cacheLock)
            {
                if (_index is not null)
                {
                    return _index;
                }
            }

            var path = IndexFilePath;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                await using var stream = File.OpenRead(path);
                var document = await JsonSerializer.DeserializeAsync<DramaBoxIndexDocument>(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (document is not null)
                {
                    SetIndex(document);
                }

                return document;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read the DramaBox index at {Path}", path);
                return null;
            }
        }

        /// <summary>
        /// Crawls the whole catalogue and persists the index.
        /// </summary>
        /// <param name="progress">Optional progress reporter, from 0 to 1.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The freshly built index.</returns>
        public async Task<DramaBoxIndexDocument> RebuildIndexAsync(IProgress<double>? progress, CancellationToken cancellationToken)
        {
            await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var books = new Dictionary<string, DramaBoxBook>(StringComparer.Ordinal);
                var pageCount = 0;
                var consecutiveFailures = 0;
                var stoppedEarly = false;

                for (var page = 1; pageCount == 0 || page <= pageCount; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pageProps = await GetPagePropsAsync(BuildBrowseUrl(page), cancellationToken).ConfigureAwait(false);
                    if (pageProps is null)
                    {
                        consecutiveFailures++;

                        // A single transient failure used to abort the whole crawl and leave the index
                        // roughly a third of the catalogue short. Skip the page instead, and only give
                        // up once the failures are clearly not transient.
                        _logger.LogWarning(
                            "DramaBox listing page {Page} returned no payload ({Failures}/{Max} consecutive)",
                            page,
                            consecutiveFailures,
                            MaxConsecutiveFailures);

                        if (consecutiveFailures >= MaxConsecutiveFailures)
                        {
                            _logger.LogError(
                                "DramaBox crawl stopped at page {Page}: {Failures} consecutive failures",
                                page,
                                consecutiveFailures);
                            stoppedEarly = true;
                            break;
                        }

                        continue;
                    }

                    consecutiveFailures = 0;

                    if (pageCount == 0)
                    {
                        pageCount = DramaBoxParser.ParsePageCount(pageProps);
                        if (pageCount <= 0)
                        {
                            pageCount = 1;
                        }

                        _logger.LogInformation("DramaBox index crawl found {PageCount} listing pages", pageCount);
                    }

                    foreach (var book in DramaBoxParser.ParseBookList(pageProps))
                    {
                        books[book.BookId] = book;
                    }

                    progress?.Report(Math.Min(1.0, (double)page / pageCount));
                }

                if (stoppedEarly)
                {
                    // Merge with whatever was already indexed so an interrupted crawl can never shrink
                    // the catalogue and silently lose matches.
                    var existing = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        foreach (var book in existing.Books)
                        {
                            books.TryAdd(book.BookId, book);
                        }

                        _logger.LogWarning(
                            "DramaBox crawl was incomplete; merged with the {Previous} previously indexed books",
                            existing.Books.Count);
                    }
                }

                var document = new DramaBoxIndexDocument
                {
                    BuiltUtc = DateTimeOffset.UtcNow,
                    Locale = DefaultLocale,
                    Books = books.Values
                        .OrderBy(book => book.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };

                await SaveIndexAsync(document, cancellationToken).ConfigureAwait(false);

                SetIndex(document);

                _logger.LogInformation("DramaBox index built with {Count} books", document.Books.Count);
                return document;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        private async Task SaveIndexAsync(DramaBoxIndexDocument document, CancellationToken cancellationToken)
        {
            var path = IndexFilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a temporary file first so an interrupted crawl cannot leave a truncated index.
            var temporaryPath = path + "." + IndexFileVersion.ToString(CultureInfo.InvariantCulture) + ".tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }

        private DramaBoxBook? FindInIndexById(string bookId)
        {
            lock (_cacheLock)
            {
                return _index?.Books.FirstOrDefault(book => string.Equals(book.BookId, bookId, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Publishes a new index and pre-tokenizes every title once. Matching one library title
        /// against the whole catalogue would otherwise re-tokenize each catalogue entry on every
        /// comparison, which is roughly 3.4 million tokenizations for this library.
        /// </summary>
        /// <param name="document">The new index.</param>
        private void SetIndex(DramaBoxIndexDocument document)
        {
            lock (_cacheLock)
            {
                _index = document;
                _indexTokens.Clear();

                foreach (var book in document.Books)
                {
                    _indexTokens[book.BookId] = DramaBoxTitleMatcher.CreateTokenSet(book.Name);
                }
            }
        }

        private async Task<System.Text.Json.Nodes.JsonNode?> GetPagePropsAsync(string url, CancellationToken cancellationToken)
        {
            var html = await GetHtmlAsync(url, cancellationToken).ConfigureAwait(false);
            return DramaBoxParser.ParsePageProps(html);
        }

        private async Task<string?> GetHtmlAsync(string url, CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Serialize and pace the crawl: the whole point is to stay a polite client.
                var sinceLastRequest = DateTimeOffset.UtcNow - _lastRequestUtc;
                if (sinceLastRequest < RequestDelay)
                {
                    await Task.Delay(RequestDelay - sinceLastRequest, cancellationToken).ConfigureAwait(false);
                }

                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                _lastRequestUtc = DateTimeOffset.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("DramaBox request to {Url} failed with {Status}", url, (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "DramaBox request to {Url} failed", url);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "DramaBox request to {Url} timed out", url);
                return null;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private static string BuildBookUrl(string bookId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/drama/{2}", SiteRoot, DefaultLocale, bookId);
        }

        private static string BuildBrowseUrl(int page)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/browse/all/{2}",
                SiteRoot,
                DefaultLocale,
                page.ToString(CultureInfo.InvariantCulture));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _indexLock.Dispose();
            _requestLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// A ranked candidate produced by a DramaBox lookup.
    /// </summary>
    public sealed class DramaBoxMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxMatch"/> class.
        /// </summary>
        /// <param name="book">The matched book.</param>
        /// <param name="score">The similarity score.</param>
        public DramaBoxMatch(DramaBoxBook book, double score)
        {
            Book = book;
            Score = score;
        }

        /// <summary>
        /// Gets the matched book.
        /// </summary>
        public DramaBoxBook Book { get; }

        /// <summary>
        /// Gets the similarity score from 0 to 1.
        /// </summary>
        public double Score { get; }
    }
}
