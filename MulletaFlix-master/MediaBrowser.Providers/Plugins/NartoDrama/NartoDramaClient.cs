using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Fetches NartoDrama series and episode data.
    /// </summary>
    /// <remarks>
    /// NartoDrama is plain HTML with no API behind it, so this client does exactly two things: read
    /// a detail page, which yields the metadata and the whole episode list in one request, and read
    /// the search page. Both responses are cached, and the client goes quiet for
    /// <see cref="FailureCooldown"/> when the site answers with something other than a page it can
    /// read, because a refresh pipeline walks every series in the library and would otherwise pay
    /// the same failing round trip once per item.
    /// </remarks>
    public sealed class NartoDramaClient : IDisposable
    {
        /// <summary>
        /// The site root.
        /// </summary>
        public const string SiteRoot = "https://narto-drama.com";

        /// <summary>
        /// The locale every request asks for. The page, its title suffix and its synopsis are all
        /// localized, so pinning the locale is what keeps the stored metadata in one language.
        /// </summary>
        private const string DefaultLocale = "pt-PT";

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        /// <summary>
        /// The Accept value a browser navigation sends. Without it the site is entitled to answer a
        /// page request with a serialized payload instead of markup.
        /// </summary>
        private const string HtmlAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

        /// <summary>
        /// How long the client stays quiet after the site refuses a request.
        /// </summary>
        /// <remarks>
        /// The same discipline as <c>MyDramaListSeriesImageProvider</c> and <c>GoodShortClient</c>.
        /// A refresh pipeline walks every series in the library, so a site that is refusing this HTTP
        /// stack would otherwise produce one warning per series and burn a round trip each time, for
        /// an answer that cannot change within the same run.
        /// </remarks>
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Minimum spacing between outgoing requests. Every response is cached, so this only shapes
        /// the initial burst when a library is first refreshed.
        /// </summary>
        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(200);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NartoDramaClient> _logger;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, NartoDramaSeries> _seriesCache = new Dictionary<string, NartoDramaSeries>(StringComparer.Ordinal);

        private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
        private long _quietUntilTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="NartoDramaClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NartoDramaClient(IHttpClientFactory httpClientFactory, ILogger<NartoDramaClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// How one page fetch ended.
        /// </summary>
        private enum FetchStatus
        {
            /// <summary>
            /// The page was read.
            /// </summary>
            Success,

            /// <summary>
            /// The site answered 404, which is a definitive answer for that URL.
            /// </summary>
            NotFound,

            /// <summary>
            /// The site refused the request, timed out, or answered with an unreadable page.
            /// </summary>
            Failed
        }

        /// <summary>
        /// Gets the locale used for every request.
        /// </summary>
        public static string Locale => DefaultLocale;

        /// <summary>
        /// Gets a value indicating whether the client is currently quiet because the site refused a
        /// recent request.
        /// </summary>
        internal bool IsInFailureCooldown
        {
            get
            {
                var quietUntil = Interlocked.Read(ref _quietUntilTicks);
                return quietUntil != 0 && new DateTime(quietUntil, DateTimeKind.Utc) > DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Resolves a series slug from any input a user may paste, such as a detail or episode URL.
        /// </summary>
        /// <param name="input">The raw input.</param>
        /// <returns>The slug, or null.</returns>
        public static string? ResolveSlug(string? input)
        {
            return NartoDramaTitleMatcher.ExtractSlug(input);
        }

        /// <summary>
        /// Fetches one series, including its episode list, by slug.
        /// </summary>
        /// <param name="slug">The series slug.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The series, or null when it could not be read.</returns>
        public async Task<NartoDramaSeries?> GetSeriesAsync(string? slug, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var trimmed = slug.Trim();

            lock (_cacheLock)
            {
                if (_seriesCache.TryGetValue(trimmed, out var cached))
                {
                    return cached;
                }
            }

            if (IsInFailureCooldown)
            {
                _logger.LogDebug("NartoDrama is in its failure cooldown; skipping series {Slug}", trimmed);
                return null;
            }

            var url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/detail/watch/{1}?lang={2}",
                SiteRoot,
                Uri.EscapeDataString(trimmed),
                DefaultLocale);

            var fetch = await GetHtmlAsync(url, cancellationToken).ConfigureAwait(false);

            if (fetch.Status == FetchStatus.NotFound)
            {
                // A 404 is the site's definitive answer for this slug, not a failure of the site, so
                // it must not arm the cooldown: a mistyped or stale slug would otherwise silence the
                // provider for every other series for half an hour.
                _logger.LogInformation("NartoDrama has no series at {Slug}", trimmed);
                return null;
            }

            if (fetch.Status != FetchStatus.Success)
            {
                EnterFailureCooldown("series " + trimmed);
                return null;
            }

            var series = NartoDramaParser.ParseSeries(fetch.Body, trimmed);
            if (series is null)
            {
                // The page answered but nothing in it is recognizable, which means the markup this
                // parser was written against has changed. Go quiet instead of paying the same parse
                // failure once per library item.
                EnterFailureCooldown("series " + trimmed);
                return null;
            }

            lock (_cacheLock)
            {
                _seriesCache[trimmed] = series;
                _seriesCache[series.Slug] = series;
            }

            return series;
        }

        /// <summary>
        /// Searches NartoDrama by title.
        /// </summary>
        /// <param name="query">The search text, or a pasted URL or bare slug.</param>
        /// <param name="maxResults">The maximum number of results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked candidates.</returns>
        public async Task<IReadOnlyList<NartoDramaMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<NartoDramaMatch>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // A pasted URL or a bare slug resolves exactly and needs no search at all.
            var directSlug = ResolveSlug(query);
            if (directSlug is not null)
            {
                var direct = await GetSeriesAsync(directSlug, cancellationToken).ConfigureAwait(false);
                if (direct is not null)
                {
                    results.Add(new NartoDramaMatch(direct, 1.0));
                    return results;
                }
            }

            if (IsInFailureCooldown)
            {
                _logger.LogDebug("NartoDrama is in its failure cooldown; skipping the search for {Query}", query);
                return results;
            }

            var url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/search?q={1}&lang={2}",
                SiteRoot,
                Uri.EscapeDataString(query.Trim()),
                DefaultLocale);

            var fetch = await GetHtmlAsync(url, cancellationToken).ConfigureAwait(false);
            if (fetch.Status != FetchStatus.Success)
            {
                // Once the search page cannot be read there is nothing left to resolve a title
                // against, so the whole client goes quiet rather than retrying per item.
                EnterFailureCooldown("the search for '" + query.Trim() + "'");
                return results;
            }

            var candidates = NartoDramaParser.ParseSearchResults(fetch.Body);
            if (candidates.Count == 0)
            {
                candidates = NartoDramaParser.ParseSearchResultsFromJsonLd(fetch.Body);
            }

            if (candidates.Count == 0)
            {
                // The page rendered but held no result this parser recognizes. An empty result set
                // is a legitimate answer for a term the catalogue does not have, so this is only
                // logged; unlike an unreadable page it does not arm the cooldown.
                _logger.LogInformation("NartoDrama found no candidate for {Query}", query.Trim());
                return results;
            }

            // The site ranks its own results, but the query is usually a library folder name rather
            // than a platform title, so the local matcher re-ranks and the platform order only
            // breaks ties. Scores are published as measured and are not floored, because the search
            // page was measured to answer with fallback hits for a term the catalogue does not
            // contain — "Chuva Negra" comes back as "Charli XCX: Alone Together (2022)" — so a floor
            // would make every unrelated candidate look like a plausible match. The caller decides
            // what to do with a low score; <see cref="NartoDramaSeriesProvider"/> uses a threshold
            // for its automatic lookup while the Identify dialog offers everything the site returned.
            var ranked = new List<NartoDramaMatch>();
            foreach (var candidate in candidates)
            {
                ranked.Add(new NartoDramaMatch(candidate, NartoDramaTitleMatcher.Similarity(candidate.Name, query)));
            }

            return ranked
                .OrderByDescending(match => match.Score)
                .Take(maxResults)
                .ToList();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _requestLock.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Reads the HTML of a page, without ever letting a transport failure escape.
        /// </summary>
        /// <param name="url">The absolute URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The fetch outcome. Success carries the body; the other outcomes carry nothing.</returns>
        private async Task<FetchResult> GetHtmlAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", HtmlAccept);

                using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    return FetchResult.Failed;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // A 404 is an answer, not an outage.
                    return FetchResult.NotFound;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NartoDrama request to {Url} failed with {Status}", url, (int)response.StatusCode);
                    return FetchResult.Failed;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(body) ? FetchResult.Failed : new FetchResult(FetchStatus.Success, body);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "NartoDrama request to {Url} failed", url);
                return FetchResult.Failed;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "NartoDrama request to {Url} timed out", url);
                return FetchResult.Failed;
            }
        }

        /// <summary>
        /// Sends one request, pacing it against the previous one.
        /// </summary>
        /// <param name="request">The request. Ownership transfers to this method.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response, or null when the transport itself failed.</returns>
        private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var sinceLastRequest = DateTimeOffset.UtcNow - _lastRequestUtc;
                if (sinceLastRequest < RequestDelay)
                {
                    await Task.Delay(RequestDelay - sinceLastRequest, cancellationToken).ConfigureAwait(false);
                }

                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

                var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                _lastRequestUtc = DateTimeOffset.UtcNow;
                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "NartoDrama request to {Url} failed", request.RequestUri);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "NartoDrama request to {Url} timed out", request.RequestUri);
                return null;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        /// <summary>
        /// Arms the quiet period, reporting the reason once instead of once per item.
        /// </summary>
        /// <param name="operation">The operation that failed.</param>
        private void EnterFailureCooldown(string operation)
        {
            var quietUntil = DateTime.UtcNow.Add(FailureCooldown);
            var previous = Interlocked.Exchange(ref _quietUntilTicks, quietUntil.Ticks);

            if (previous != 0 && new DateTime(previous, DateTimeKind.Utc) > DateTime.UtcNow)
            {
                // Already quiet: the failure was expected and was reported by the call that armed
                // the cooldown. Logging it again is what produced one line per library item in the
                // MyDramaList case this mirrors.
                _logger.LogDebug("NartoDrama is still unavailable while resolving {Operation}.", operation);
                return;
            }

            _logger.LogWarning(
                "NartoDrama did not answer while resolving {Operation}. Pausing lookups for {Minutes} minutes instead of retrying for every item.",
                operation,
                FailureCooldown.TotalMinutes);
        }

        /// <summary>
        /// The outcome of one page fetch.
        /// </summary>
        private sealed class FetchResult
        {
            /// <summary>
            /// A fetch that could not read the page.
            /// </summary>
            public static readonly FetchResult Failed = new FetchResult(FetchStatus.Failed, null);

            /// <summary>
            /// A fetch the site answered with 404.
            /// </summary>
            public static readonly FetchResult NotFound = new FetchResult(FetchStatus.NotFound, null);

            /// <summary>
            /// Initializes a new instance of the <see cref="FetchResult"/> class.
            /// </summary>
            /// <param name="status">How the fetch ended.</param>
            /// <param name="body">The response body, when there was one.</param>
            public FetchResult(FetchStatus status, string? body)
            {
                Status = status;
                Body = body;
            }

            /// <summary>
            /// Gets how the fetch ended.
            /// </summary>
            public FetchStatus Status { get; }

            /// <summary>
            /// Gets the response body, or null.
            /// </summary>
            public string? Body { get; }
        }
    }
}
