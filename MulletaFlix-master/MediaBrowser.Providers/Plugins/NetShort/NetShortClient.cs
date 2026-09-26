using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Talks to the NetShort JSON API and, when that API is unavailable, to the series page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things make this source harder than its DramaBox sibling. First, every API call must carry
    /// an encrypted body and a visitor token (see <see cref="NetShortCrypto"/>); a plain-text body is
    /// refused with HTTP 500. Second, the API is the only way to search — the public search page is
    /// client rendered — so title resolution has to go through
    /// <c>/web/short_play/search/keyword/seo</c>.
    /// </para>
    /// <para>
    /// The series page is server rendered and carries a complete schema.org payload, so it is the
    /// fallback when the API refuses or fails. That path is deliberately reachable with nothing but a
    /// series id: <c>/{locale}/full-episodes/{id}</c> answers 200 with a valid JSON-LD block, so a
    /// series stays resolvable while the encrypted API is down. Its limits are that it lists only the
    /// first page of episode numbers and carries no per-episode thumbnail, which is why the API is
    /// always tried first.
    /// </para>
    /// <para>
    /// The visitor session lives in memory only. A device code is minted once per process, and a
    /// restart registers a fresh visitor — the same thing the site does for a new browser profile.
    /// Visitor accounts are disposable (the account carries no coins and reports itself as new), so
    /// persisting one would buy nothing but a file to keep consistent.
    /// </para>
    /// </remarks>
    public sealed class NetShortClient : IDisposable
    {
        /// <summary>
        /// The site root.
        /// </summary>
        public const string SiteRoot = "https://netshort.com";

        /// <summary>
        /// The locale NetShort serves Portuguese metadata under, as the API spells it.
        /// </summary>
        public const string Locale = "pt_PT";

        /// <summary>
        /// The API root, served from the same origin as the pages.
        /// </summary>
        private const string ApiRoot = SiteRoot + "/prod-web-api";

        /// <summary>
        /// The locale as it appears in URLs, which is the shorter form.
        /// </summary>
        private const string PathLocale = "pt";

        private const string ClientVersion = "1.2.0";
        private const string OsHeader = "4";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        private const int SuccessCode = 200;
        private const int UnauthorizedCode = 401;

        /// <summary>
        /// How many resolved series are kept. A library of NetShort series is small, but a long run
        /// of refreshes must not grow the cache without bound.
        /// </summary>
        private const int SeriesCacheLimit = 512;

        /// <summary>
        /// How long the provider stays quiet after NetShort refuses a request.
        /// </summary>
        /// <remarks>
        /// Same pattern as <c>MyDramaListSeriesImageProvider</c>: once the site is refusing this HTTP
        /// stack, retrying per item cannot succeed — it only writes one ERROR line per item and burns
        /// a round trip. Thirty minutes outlasts a rollout and still lets a transient outage recover
        /// within the hour.
        /// </remarks>
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(30);

        /// <summary>
        /// The visitor token is valid for seven days; refresh it a day early rather than risk a run
        /// that dies halfway through because the token expired.
        /// </summary>
        private static readonly TimeSpan TokenRefreshAfter = TimeSpan.FromDays(6);

        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Serialization options for request bodies.
        /// </summary>
        /// <remarks>
        /// The default encoder escapes every non-ASCII character, which would turn "perdões" into
        /// "perd\u00F5es". Both forms are valid JSON, but only the raw form reproduces the bytes the
        /// site itself sends, and matching the site exactly is what makes the captured ciphertexts
        /// usable as a regression test. HTML-sensitive characters stay escaped.
        /// </remarks>
        private static readonly JsonSerializerOptions PayloadJsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetShortClient> _logger;
        private readonly string _deviceCode = CreateDeviceCode();

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, NetShortSeries> _seriesCache = new Dictionary<string, NetShortSeries>(StringComparer.Ordinal);

        private string? _token;
        private DateTimeOffset _tokenCreatedUtc = DateTimeOffset.MinValue;
        private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
        private long _quietUntilTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetShortClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NetShortClient(IHttpClientFactory httpClientFactory, ILogger<NetShortClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets a value indicating whether the client is currently quiet because NetShort refused a
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
        /// Resolves a series id from any input a user may paste, such as a NetShort URL.
        /// </summary>
        /// <param name="input">The raw input.</param>
        /// <returns>The series id, or null.</returns>
        public static string? ResolveSeriesId(string? input)
        {
            return NetShortTitleMatcher.ExtractSeriesId(input);
        }

        /// <summary>
        /// Fetches one series, including its episode list, by series id.
        /// </summary>
        /// <param name="seriesId">The numeric series id, or any NetShort URL that carries it.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The series, or null when it could not be read.</returns>
        public async Task<NetShortSeries?> GetSeriesAsync(string? seriesId, CancellationToken cancellationToken)
        {
            var id = ResolveSeriesId(seriesId);
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            lock (_cacheLock)
            {
                if (_seriesCache.TryGetValue(id, out var cached))
                {
                    return cached;
                }
            }

            if (IsInFailureCooldown)
            {
                return null;
            }

            NetShortSeries? series = null;
            Exception? failure = null;

            try
            {
                var body = new JsonObject
                {
                    ["type"] = "drama",
                    ["language"] = Locale,
                    ["shortPlayId"] = id
                }.ToJsonString(PayloadJsonOptions);

                // This endpoint returns the complete episode list for every series that was checked
                // here, from 36 up to 89 episodes, so the paginated episode_info endpoint is not
                // needed and is deliberately not called.
                var envelope = await PostApiAsync(NetShortApi.SeriesDetail, body, cancellationToken).ConfigureAwait(false);
                series = NetShortParser.ParseSeriesDetail(envelope);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                failure = ex;
            }

            if (series is null)
            {
                // Only the encrypted API carries per-episode thumbnails, but it is also the fragile
                // half. The rendered page needs nothing but the id.
                try
                {
                    series = await GetSeriesFromPageAsync(id, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsTransportFailure(ex))
                {
                    failure ??= ex;
                }

                if (series is null)
                {
                    if (failure is not null)
                    {
                        EnterFailureCooldown(failure, "series " + id);
                    }

                    return null;
                }

                if (failure is not null)
                {
                    _logger.LogDebug(failure, "NetShort fell back to the rendered page for series {SeriesId}", id);
                }
            }

            CacheSeries(id, series);
            return series;
        }

        /// <summary>
        /// Searches the NetShort catalogue by title.
        /// </summary>
        /// <param name="query">The search text.</param>
        /// <param name="maxResults">The maximum number of results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked matches, or an empty list when the API is unavailable.</returns>
        public async Task<IReadOnlyList<NetShortMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<NetShortMatch>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // A pasted URL or a bare id resolves exactly and needs no search.
            var directId = ResolveSeriesId(query);
            if (directId is not null)
            {
                var direct = await GetSeriesAsync(directId, cancellationToken).ConfigureAwait(false);
                if (direct is not null)
                {
                    results.Add(new NetShortMatch(direct, 1.0));
                    return results;
                }
            }

            if (IsInFailureCooldown)
            {
                return results;
            }

            JsonNode? envelope;
            try
            {
                var body = new JsonObject
                {
                    ["queryKeyword"] = query,
                    ["pageQuery"] = new JsonObject
                    {
                        ["pageNum"] = 1,
                        ["pageSize"] = Math.Clamp(maxResults, 1, 50)
                    }
                }.ToJsonString(PayloadJsonOptions);

                envelope = await PostApiAsync(NetShortApi.Search, body, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                EnterFailureCooldown(ex, "search");
                return results;
            }

            if (envelope is null)
            {
                return results;
            }

            foreach (var series in NetShortParser.ParseSearchResults(envelope))
            {
                results.Add(new NetShortMatch(series, NetShortTitleMatcher.Similarity(series.Name, query)));
            }

            return results
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Series.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Resolves the best catalogue match for a library title.
        /// </summary>
        /// <param name="title">The library title.</param>
        /// <param name="minimumScore">The minimum score to accept.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The match, or null when nothing scored high enough.</returns>
        public async Task<NetShortMatch?> MatchByTitleAsync(string? title, double minimumScore, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            // NetShort ranks its own search results, so the score is only used to reject a confident
            // but wrong top hit.
            var matches = await SearchAsync(title, 5, cancellationToken).ConfigureAwait(false);
            foreach (var match in matches)
            {
                if (match.Score >= minimumScore)
                {
                    return match;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _requestLock.Dispose();
            GC.SuppressFinalize(this);
        }

        private static bool IsTransportFailure(Exception exception)
        {
            return exception is HttpRequestException or TaskCanceledException or JsonException or FormatException or CryptographicException;
        }

        private static string CreateDeviceCode()
        {
            // The site's device code is 32 lowercase hex characters, a dash, and the epoch in
            // milliseconds. The server echoes it back on the visitor account it creates.
            Span<byte> bytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(bytes);

            return string.Concat(
                Convert.ToHexStringLower(bytes),
                "-",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        }

        private void CacheSeries(string seriesId, NetShortSeries series)
        {
            lock (_cacheLock)
            {
                if (_seriesCache.Count >= SeriesCacheLimit)
                {
                    _seriesCache.Clear();
                }

                _seriesCache[seriesId] = series;
            }
        }

        private async Task<NetShortSeries?> GetSeriesFromPageAsync(string seriesId, CancellationToken cancellationToken)
        {
            // The site answers this id-only URL with 200 and a full schema.org block, so the fallback
            // needs no slug and no search.
            var url = string.Concat(SiteRoot, "/", PathLocale, "/full-episodes/", seriesId);
            var html = await GetHtmlAsync(url, cancellationToken).ConfigureAwait(false);
            if (html is null)
            {
                return null;
            }

            var series = NetShortParser.ParseJsonLdSeries(html);
            if (series is not null)
            {
                _logger.LogInformation(
                    "NetShort filled series {SeriesId} from the rendered page ({ListedEpisodes} episode numbers available)",
                    seriesId,
                    series.Episodes.Count);
            }

            return series;
        }

        /// <summary>
        /// Reads a page from the site.
        /// </summary>
        /// <param name="url">The absolute URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The HTML, or null when the page does not exist. A failure to reach the site at all is
        /// thrown, so the caller can tell "no such page" from "the site is refusing us".
        /// </returns>
        /// <exception cref="HttpRequestException">The site could not be reached, or answered with a status other than 404.</exception>
        private async Task<string?> GetHtmlAsync(string url, CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await PaceAsync(cancellationToken).ConfigureAwait(false);

                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                using var message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                message.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

                using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
                _lastRequestUtc = DateTimeOffset.UtcNow;

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(string.Format(
                        CultureInfo.InvariantCulture,
                        "NetShort page {0} answered {1}",
                        url,
                        (int)response.StatusCode));
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private async Task<JsonNode?> PostApiAsync(string path, string body, CancellationToken cancellationToken)
        {
            var token = await GetTokenAsync(false, cancellationToken).ConfigureAwait(false);
            var envelope = await SendEncryptedAsync(path, body, token, cancellationToken).ConfigureAwait(false);
            if (envelope is null)
            {
                return null;
            }

            if (NetShortParser.ParseResultCode(envelope) == UnauthorizedCode)
            {
                // The visitor token expired mid-run. Re-login once and replay the call.
                token = await GetTokenAsync(true, cancellationToken).ConfigureAwait(false);
                if (token is null)
                {
                    return null;
                }

                envelope = await SendEncryptedAsync(path, body, token, cancellationToken).ConfigureAwait(false);
            }

            if (envelope is null)
            {
                return null;
            }

            var code = NetShortParser.ParseResultCode(envelope);
            if (code != SuccessCode)
            {
                // A business-level code is not a transport failure: the API is healthy and is telling
                // us this id or term has no result, so no cooldown is armed here.
                _logger.LogWarning(
                    "NetShort answered {Code} for {Path}: {Message}",
                    code,
                    path,
                    NetShortParser.ParseResultMessage(envelope));
                return null;
            }

            return envelope;
        }

        private async Task<string?> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh
                && !string.IsNullOrEmpty(_token)
                && DateTimeOffset.UtcNow - _tokenCreatedUtc < TokenRefreshAfter)
            {
                return _token;
            }

            var body = new JsonObject
            {
                ["deviceCode"] = _deviceCode,
                ["os"] = "windows"
            }.ToJsonString(PayloadJsonOptions);

            var envelope = await SendEncryptedAsync(NetShortApi.VisitorLogin, body, null, cancellationToken).ConfigureAwait(false);
            if (envelope is null || NetShortParser.ParseResultCode(envelope) != SuccessCode)
            {
                return null;
            }

            var token = envelope["data"]?["token"]?.ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("NetShort visitor_login returned no token");
                return null;
            }

            _token = token;
            _tokenCreatedUtc = DateTimeOffset.UtcNow;

            return token;
        }

        private async Task<JsonNode?> SendEncryptedAsync(string path, string body, string? token, CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await PaceAsync(cancellationToken).ConfigureAwait(false);

                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                using var request = new HttpRequestMessage(HttpMethod.Post, ApiRoot + path);

                // Every header below is one the site's own client sends. The API keys off
                // encrypt-key, Device-Code and version, so dropping any of them is not an option.
                request.Headers.TryAddWithoutValidation("encrypt-key", NetShortCrypto.CreateRequestKeyHeader());
                request.Headers.TryAddWithoutValidation("OS", OsHeader);
                request.Headers.TryAddWithoutValidation("canary", "v1");
                request.Headers.TryAddWithoutValidation("version", ClientVersion);
                request.Headers.TryAddWithoutValidation("Device-Code", _deviceCode);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                request.Headers.TryAddWithoutValidation("Origin", SiteRoot);
                request.Headers.TryAddWithoutValidation("Referer", SiteRoot + "/" + PathLocale + "/");

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                }

                request.Content = new StringContent(NetShortCrypto.EncryptPayload(body), Encoding.UTF8, "application/json");

                // Content-Language has to be added to the content, not to the request. It is a content
                // header, so HttpRequestHeaders.TryAddWithoutValidation silently refuses it and returns
                // false, and losing it is invisible in the response: the API simply searches its
                // default locale and answers an English catalogue to a Portuguese query. The wire
                // bytes are identical whichever collection carries it.
                request.Content.Headers.TryAddWithoutValidation("Content-Language", Locale);

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                _lastRequestUtc = DateTimeOffset.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NetShort {Path} failed with {Status}", path, (int)response.StatusCode);
                    throw new HttpRequestException(string.Format(
                        CultureInfo.InvariantCulture,
                        "NetShort {0} answered {1}",
                        path,
                        (int)response.StatusCode));
                }

                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return ParseResponse(response, text);
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private JsonNode? ParseResponse(HttpResponseMessage response, string text)
        {
            var keyHeader = GetHeader(response, "encrypt-key");
            if (!string.IsNullOrEmpty(keyHeader))
            {
                // A response is only encrypted when the server sends the key; without the header the
                // body is plain JSON, which is what the site's own client assumes too.
                text = NetShortCrypto.DecryptPayload(text, NetShortCrypto.UnwrapResponseKey(keyHeader));
            }

            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        private static string? GetHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                return values.FirstOrDefault();
            }

            return response.Content.Headers.TryGetValues(name, out var contentValues)
                ? contentValues.FirstOrDefault()
                : null;
        }

        private async Task PaceAsync(CancellationToken cancellationToken)
        {
            var sinceLastRequest = DateTimeOffset.UtcNow - _lastRequestUtc;
            if (sinceLastRequest < RequestDelay)
            {
                await Task.Delay(RequestDelay - sinceLastRequest, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Arms the quiet period, reporting the reason once instead of once per item.
        /// </summary>
        /// <param name="exception">The failure.</param>
        /// <param name="operation">The operation that failed.</param>
        private void EnterFailureCooldown(Exception exception, string operation)
        {
            var quietUntil = DateTime.UtcNow.Add(FailureCooldown);
            var previous = Interlocked.Exchange(ref _quietUntilTicks, quietUntil.Ticks);

            if (previous != 0 && new DateTime(previous, DateTimeKind.Utc) > DateTime.UtcNow)
            {
                // Already quiet: this failure was expected and was reported by the call that armed
                // the cooldown. Reporting it again is what produced one ERROR line per item elsewhere.
                _logger.LogDebug(exception, "NetShort is still unavailable while resolving {Operation}.", operation);
                return;
            }

            _logger.LogWarning(
                exception,
                "NetShort refused a request while resolving {Operation}. Pausing lookups for {Minutes} minutes instead of retrying for every item.",
                operation,
                FailureCooldown.TotalMinutes);
        }

        /// <summary>
        /// The API paths this provider uses.
        /// </summary>
        private static class NetShortApi
        {
            /// <summary>
            /// Creates (or reuses) a visitor account and returns the bearer token.
            /// </summary>
            public const string VisitorLogin = "/web/auth/visitor_login";

            /// <summary>
            /// Keyword search over the catalogue.
            /// </summary>
            public const string Search = "/web/short_play/search/keyword/seo";

            /// <summary>
            /// Series detail, including the full episode list.
            /// </summary>
            public const string SeriesDetail = "/web/v4/short_play/detail_info/cascade_label";
        }
    }
}
