using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// Fetches GoodShort series and episode data.
    /// </summary>
    /// <remarks>
    /// GoodShort's own backend endpoints answer unauthenticated JSON, which is what this client uses
    /// for everything it can. There are two facts about them that shape the code below:
    /// they are addressed as /hwycreels/... directly and a /api prefix answers 404;
    /// they always answer HTTP 200, even for a failure, and report the real outcome in the body's
    /// "status" field (<c>{"status":12000,"message":"Book not exists."}</c>), so a transport status
    /// check alone would silently turn a missing book into an empty series.
    /// The server side HTML remains as a fallback for the detail view. It is a strictly worse
    /// source — its JSON-LD ItemList listed 6 of the 37 episodes GoodShort itself reports for the
    /// first series checked — so it is only consulted when the endpoints have nothing to say.
    /// </remarks>
    public sealed class GoodShortClient : IDisposable
    {
        /// <summary>
        /// The site root. Both the JSON endpoints and the pages live on the same host.
        /// </summary>
        public const string SiteRoot = "https://www.goodshort.com";

        /// <summary>
        /// The one success code GoodShort uses in every JSON envelope.
        /// </summary>
        private const int ApiStatusSuccess = 0;

        /// <summary>
        /// The code GoodShort answers with, inside an HTTP 200, for a book id that does not exist.
        /// </summary>
        /// <remarks>
        /// Verified live: <c>{"status":12000,"message":"Book not exists.","success":false}</c>.
        /// Distinguished from a transport failure because it is an answer about the book rather than
        /// a sign that the site is down.
        /// </remarks>
        private const int ApiStatusBookMissing = 12000;

        /// <summary>
        /// The number of episodes requested per channel page. GoodShort has been observed to honour
        /// this exactly; the page loop below still walks <c>pages</c> rather than assuming one round trip.
        /// </summary>
        private const int EpisodePageSize = 50;

        /// <summary>
        /// A hard stop on the episode page loop. A malformed response reporting a huge or negative
        /// page count must not turn into an unbounded request loop against a third party site.
        /// </summary>
        private const int MaxEpisodePages = 40;

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        /// <summary>
        /// The Accept value the site's own front end sends to its JSON endpoints.
        /// </summary>
        private const string JsonAccept = "application/json, text/plain, */*";

        /// <summary>
        /// The Accept value a browser navigation sends, used for the HTML fallback. Without it the
        /// site is entitled to answer a page request with a serialized payload instead of markup.
        /// </summary>
        private const string HtmlAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

        /// <summary>
        /// How long the client stays quiet after the site refuses a request.
        /// </summary>
        /// <remarks>
        /// The same discipline as <c>MyDramaListSeriesImageProvider</c>. A refresh pipeline walks
        /// every series in the library, so a site that is refusing this HTTP stack would otherwise
        /// produce one warning per series and burn a round trip each time, for a result that cannot
        /// change within the same run.
        /// </remarks>
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Minimum spacing between outgoing requests. Every response is cached, so this only shapes
        /// the initial burst when a library is first refreshed.
        /// </summary>
        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(200);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoodShortClient> _logger;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, GoodShortSeries> _seriesCache = new Dictionary<string, GoodShortSeries>(StringComparer.Ordinal);

        private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
        private long _quietUntilTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoodShortClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public GoodShortClient(IHttpClientFactory httpClientFactory, ILogger<GoodShortClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

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
        /// Resolves a series id from any input a user may paste, such as a drama or episode URL.
        /// </summary>
        /// <param name="input">The raw input.</param>
        /// <returns>The series id, or null.</returns>
        public static string? ResolveSeriesId(string? input)
        {
            return GoodShortTitleMatcher.ExtractSeriesId(input);
        }

        /// <summary>
        /// Fetches one series, including its full episode list, by series id.
        /// </summary>
        /// <param name="seriesId">The numeric series id.</param>
        /// <param name="slug">
        /// The optional URL slug, used only if the JSON endpoints are unreachable and the HTML
        /// fallback has to run. A series page cannot be addressed by id alone.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The series, or null when it could not be read.</returns>
        public async Task<GoodShortSeries?> GetSeriesAsync(string? seriesId, string? slug, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return null;
            }

            lock (_cacheLock)
            {
                if (_seriesCache.TryGetValue(seriesId, out var cached))
                {
                    return cached;
                }
            }

            if (IsInFailureCooldown)
            {
                _logger.LogDebug("GoodShort is in its failure cooldown; skipping series {SeriesId}", seriesId);
                return null;
            }

            var knownSlug = !string.IsNullOrWhiteSpace(slug)
                ? slug
                : GoodShortTitleMatcher.ExtractSlug(seriesId);

            var fetched = await FetchFromApiAsync(seriesId, cancellationToken).ConfigureAwait(false);
            var series = fetched.Series;

            if (series is null && fetched.Answered)
            {
                // The endpoint answered and said this book does not exist. That is a definitive
                // answer about the book, not a failure of the site, so it must neither arm the
                // cooldown nor send the fallback chasing a page that cannot exist.
                _logger.LogInformation("GoodShort has no book with id {SeriesId}", seriesId);
                return null;
            }

            if (series is null)
            {
                _logger.LogWarning(
                    "GoodShort JSON endpoints returned nothing for series {SeriesId}; falling back to the page",
                    seriesId);

                series = await FetchFromHtmlAsync(seriesId, knownSlug, cancellationToken).ConfigureAwait(false);
            }

            if (series is null)
            {
                // Both sources failed, so the host is unreachable or has changed shape. Go quiet
                // instead of paying the same failure once per library item.
                EnterFailureCooldown("series " + seriesId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(series.Slug) && !string.IsNullOrWhiteSpace(knownSlug))
            {
                series.Slug = knownSlug;
            }

            lock (_cacheLock)
            {
                _seriesCache[seriesId] = series;
            }

            return series;
        }

        /// <summary>
        /// Searches GoodShort by title.
        /// </summary>
        /// <param name="query">The search text, or a pasted URL or bare id.</param>
        /// <param name="maxResults">The maximum number of results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked candidates.</returns>
        public async Task<IReadOnlyList<GoodShortMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<GoodShortMatch>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // A pasted URL or a bare id resolves exactly and needs no search at all.
            var directId = ResolveSeriesId(query);
            if (directId is not null)
            {
                var direct = await GetSeriesAsync(directId, GoodShortTitleMatcher.ExtractSlug(query), cancellationToken)
                    .ConfigureAwait(false);

                if (direct is not null)
                {
                    results.Add(new GoodShortMatch(direct, 1.0));
                    return results;
                }
            }

            if (IsInFailureCooldown)
            {
                _logger.LogDebug("GoodShort is in its failure cooldown; skipping the search for {Query}", query);
                return results;
            }

            var candidates = await SearchApiAsync(query, cancellationToken).ConfigureAwait(false);
            if (candidates is null)
            {
                candidates = await SearchHtmlAsync(query, cancellationToken).ConfigureAwait(false);
                if (candidates is null)
                {
                    EnterFailureCooldown("search for '" + query + "'");
                    return results;
                }
            }

            // GoodShort ranks its own search results, but the query may be a library folder name
            // rather than a platform title, so the local matcher re-ranks and the platform order
            // only breaks ties.
            var ranked = new List<GoodShortMatch>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var score = GoodShortTitleMatcher.Similarity(candidate.Name, query);

                if (score <= 0)
                {
                    // An unrankable candidate is still offered, because the user picked this
                    // provider deliberately and the platform reported it as a hit for the term.
                    score = 0.5;
                }

                ranked.Add(new GoodShortMatch(candidate, score));
            }

            return ranked
                .OrderByDescending(match => match.Score)
                .Take(maxResults)
                .ToList();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // The type is sealed and has no finalizer, so there is nothing for the garbage
            // collector to suppress and nothing to guard against a finalizer path.
            _requestLock.Dispose();
        }

        /// <summary>
        /// Fetches series metadata and its complete episode list from the JSON endpoints.
        /// </summary>
        /// <param name="seriesId">The series id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The series when one was read; otherwise an answer saying either that the endpoint
        /// reported no such book or that it could not be reached at all.
        /// </returns>
        private async Task<ApiAnswer> FetchFromApiAsync(string seriesId, CancellationToken cancellationToken)
        {
            var detail = await PostJsonAsync(
                "/hwycreels/book/detail",
                new Dictionary<string, object>(StringComparer.Ordinal) { ["bookId"] = seriesId },
                cancellationToken).ConfigureAwait(false);

            if (detail is null)
            {
                return ApiAnswer.Unreachable;
            }

            var status = GoodShortParser.ParseApiStatus(detail);
            if (status == ApiStatusBookMissing)
            {
                // The channel endpoint answers this for an unknown book.
                return ApiAnswer.NoSuchBook;
            }

            if (status != ApiStatusSuccess)
            {
                // An unrecognised envelope code is treated as a failure of the source rather than as
                // a verdict about the book, so the page fallback still gets its chance.
                _logger.LogWarning("GoodShort answered status {Status} for series {SeriesId}", status, seriesId);
                return ApiAnswer.Unreachable;
            }

            if (detail["data"]?["book"] is null)
            {
                // The detail endpoint signals an unknown book differently from the channel endpoint:
                // verified live, it answers HTTP 200 with status 0 and no book,
                // {"status":0,"data":{"seo404Vo":{"jumpType":4}},"message":"success"}.
                // A successful envelope without a book is that answer, not an outage.
                return ApiAnswer.NoSuchBook;
            }

            var series = GoodShortParser.ParseSeriesFromApi(detail);
            if (series is null)
            {
                // A book node that cannot be read is a change in shape rather than a verdict, so the
                // page fallback is still worth trying.
                _logger.LogWarning("GoodShort returned an unreadable book payload for series {SeriesId}", seriesId);
                return ApiAnswer.Unreachable;
            }

            var episodes = await FetchEpisodesAsync(seriesId, cancellationToken).ConfigureAwait(false);
            if (episodes is not null)
            {
                series.Episodes = episodes;
                if (series.EpisodeCount <= 0)
                {
                    series.EpisodeCount = episodes.Count;
                }

                return new ApiAnswer(true, series);
            }

            // The book payload answered but the channel listing did not. Its own chapterVoList is a
            // truncated copy of the same data, so it is still better than an episode-less series —
            // and the caller can tell the difference through EpisodesAreComplete.
            var fallbackEpisodes = GoodShortParser.ParseEpisodePageFromApi(
                new JsonObject { ["data"] = new JsonObject { ["records"] = detail["data"]?["chapterVoList"]?.DeepClone() } });

            series.Episodes = fallbackEpisodes.Episodes;

            _logger.LogWarning(
                "GoodShort listed only {Listed} of {Total} episodes for series {SeriesId}: the channel endpoint failed",
                series.Episodes.Count,
                series.EpisodeCount,
                seriesId);

            return new ApiAnswer(true, series);
        }

        /// <summary>
        /// Walks every page of the channel endpoint for one series.
        /// </summary>
        /// <param name="seriesId">The series id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The episodes, or null when the first page could not be read.</returns>
        private async Task<IReadOnlyList<GoodShortEpisode>?> FetchEpisodesAsync(string seriesId, CancellationToken cancellationToken)
        {
            var all = new List<GoodShortEpisode>();
            var pages = 1;

            for (var pageNumber = 1; pageNumber <= Math.Min(pages, MaxEpisodePages); pageNumber++)
            {
                var payload = await PostJsonAsync(
                    "/hwycreels/chapter/page",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["bookId"] = seriesId,
                        ["pageNo"] = pageNumber,
                        ["pageSize"] = EpisodePageSize
                    },
                    cancellationToken).ConfigureAwait(false);

                if (payload is null || GoodShortParser.ParseApiStatus(payload) != ApiStatusSuccess)
                {
                    return all.Count > 0 ? all : null;
                }

                var page = GoodShortParser.ParseEpisodePageFromApi(payload);

                // Trust the returned page count only after the first response, and clamp it so a
                // bogus value cannot drive the loop.
                if (pageNumber == 1 && page.Pages > 0)
                {
                    pages = Math.Min(page.Pages, MaxEpisodePages);
                }

                if (page.Episodes.Count == 0)
                {
                    break;
                }

                all.AddRange(page.Episodes);
            }

            return all
                .GroupBy(episode => episode.Number)
                .Select(group => group.First())
                .OrderBy(episode => episode.Number)
                .ToList();
        }

        /// <summary>
        /// Searches through the site's JSON suggest endpoint.
        /// </summary>
        /// <param name="query">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The candidates, or null when the endpoint could not be read.</returns>
        private async Task<IReadOnlyList<GoodShortSeries>?> SearchApiAsync(string query, CancellationToken cancellationToken)
        {
            var payload = await PostJsonAsync(
                "/hwycreels/book/search/suggest",
                new Dictionary<string, object>(StringComparer.Ordinal) { ["keyword"] = query },
                cancellationToken).ConfigureAwait(false);

            if (payload is null || GoodShortParser.ParseApiStatus(payload) != ApiStatusSuccess)
            {
                return null;
            }

            return GoodShortParser.ParseSearchResultsFromApi(payload);
        }

        /// <summary>
        /// Fetches the series page and reads its JSON-LD.
        /// </summary>
        /// <param name="seriesId">The series id, used when the page does not name it.</param>
        /// <param name="slug">The series slug.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The series, or null when the page could not be read.</returns>
        private async Task<GoodShortSeries?> FetchFromHtmlAsync(string seriesId, string? slug, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                _logger.LogWarning(
                    "Cannot use the GoodShort HTML fallback for series {SeriesId}: no slug was ever learned for it",
                    seriesId);
                return null;
            }

            var html = await GetHtmlAsync(
                string.Format(CultureInfo.InvariantCulture, "{0}/drama/{1}", SiteRoot, slug),
                cancellationToken).ConfigureAwait(false);

            if (html is null)
            {
                return null;
            }

            var series = GoodShortParser.ParseSeriesFromJsonLd(html);
            if (series is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(series.SeriesId))
            {
                series.SeriesId = seriesId;
            }

            series.Slug = slug;
            series.Episodes = GoodShortParser.ParseEpisodesFromJsonLd(html);

            return series;
        }

        /// <summary>
        /// Searches through the rendered markup of the search page.
        /// </summary>
        /// <param name="query">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The candidates, or null when the page could not be read.</returns>
        private async Task<IReadOnlyList<GoodShortSeries>?> SearchHtmlAsync(string query, CancellationToken cancellationToken)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/search?keyword={1}",
                SiteRoot,
                Uri.EscapeDataString(query));

            var html = await GetHtmlAsync(url, cancellationToken).ConfigureAwait(false);
            return html is null ? null : GoodShortParser.ParseSearchResultsFromHtml(html);
        }

        /// <summary>
        /// Reads the HTML of a page, without ever letting a transport failure escape.
        /// </summary>
        /// <param name="url">The absolute URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The body, or null when the request failed.</returns>
        private async Task<string?> GetHtmlAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", HtmlAccept);

                using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GoodShort request to {Url} failed with {Status}", url, (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GoodShort request to {Url} failed", url);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "GoodShort request to {Url} timed out", url);
                return null;
            }
        }

        /// <summary>
        /// Posts a JSON body to a GoodShort endpoint and parses the reply.
        /// </summary>
        /// <param name="path">The absolute path, for example /hwycreels/book/detail.</param>
        /// <param name="body">The request body fields.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The parsed reply, or null when the request failed or returned invalid JSON.</returns>
        private async Task<JsonNode?> PostJsonAsync(string path, Dictionary<string, object> body, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonSerializer.Serialize(body);

                using var request = new HttpRequestMessage(HttpMethod.Post, SiteRoot + path);

                // The endpoint refuses the request with 415 unless the media type is exactly
                // application/json, so the content type is set explicitly rather than left to
                // StringContent, which would append a charset parameter.
                request.Content = new StringContent(json, Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.TryAddWithoutValidation("Accept", JsonAccept);

                using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GoodShort request to {Path} failed with {Status}", path, (int)response.StatusCode);
                    return null;
                }

                // Read the body as a string first: a malformed body must be reported here rather
                // than surfacing as an unexpected exception from the JSON reader.
                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return null;
                }

                return JsonNode.Parse(payload);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GoodShort request to {Path} failed", path);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "GoodShort request to {Path} timed out", path);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "GoodShort returned an unreadable body for {Path}", path);
                return null;
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
                _logger.LogWarning(ex, "GoodShort request to {Url} failed", request.RequestUri);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "GoodShort request to {Url} timed out", request.RequestUri);
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
                _logger.LogDebug("GoodShort is still unavailable while resolving {Operation}.", operation);
                return;
            }

            _logger.LogWarning(
                "GoodShort did not answer while resolving {Operation}. Pausing lookups for {Minutes} minutes instead of retrying for every item.",
                operation,
                FailureCooldown.TotalMinutes);
        }

        /// <summary>
        /// The outcome of one attempt against the JSON endpoints.
        /// </summary>
        /// <remarks>
        /// The distinction exists because GoodShort reports a missing book with HTTP 200 and a
        /// non-zero envelope status. Without it, a library item carrying a stale id would look like
        /// a site outage: the client would arm its failure cooldown and stop answering for every
        /// other series too.
        /// </remarks>
        private readonly struct ApiAnswer
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ApiAnswer"/> struct.
            /// </summary>
            /// <param name="answered">Whether the endpoint answered at all.</param>
            /// <param name="series">The series, when one was read.</param>
            public ApiAnswer(bool answered, GoodShortSeries? series)
            {
                Answered = answered;
                Series = series;
            }

            /// <summary>
            /// Gets an answer meaning the endpoint could not be reached or could not be understood.
            /// </summary>
            public static ApiAnswer Unreachable => new ApiAnswer(false, null);

            /// <summary>
            /// Gets an answer meaning the endpoint reported that the book does not exist.
            /// </summary>
            public static ApiAnswer NoSuchBook => new ApiAnswer(true, null);

            /// <summary>
            /// Gets a value indicating whether the endpoint answered.
            /// </summary>
            public bool Answered { get; }

            /// <summary>
            /// Gets the series, or null when there was none to read.
            /// </summary>
            public GoodShortSeries? Series { get; }
        }
    }
}
