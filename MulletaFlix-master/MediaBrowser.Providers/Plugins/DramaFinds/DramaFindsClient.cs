using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// Fetches DramaFinds data from the platform JSON API.
    /// </summary>
    /// <remarks>
    /// Unlike DramaBox, DramaFinds exposes a real unauthenticated search endpoint, so there is no
    /// crawled catalogue index and no index file: a title is resolved with one POST. The API is
    /// still picky about the caller and answers {"code":10,"msg":"GuestId Missing"} with HTTP 200
    /// when the Guest-Id header is absent, so the client owns the guest identity and the language
    /// headers, and both are produced once per process instead of per request.
    /// The search endpoint is fuzzy by substring rather than exact, which is why this class only
    /// ever returns scored candidates and never a bare "first row": see
    /// <see cref="DramaFindsTitleMatcher"/> for the measured scores behind that decision.
    /// </remarks>
    public sealed class DramaFindsClient : IDisposable
    {
        private const string ApiRoot = "https://api.dramafinds.com/api";
        private const string SiteRoot = "https://dramafinds.com";
        private const string Locale = "pt";
        private const string SearchPath = "/v1/short-dramas/search";
        private const string EpisodesPath = "/v1/short-dramas/episodes";

        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        /// <summary>
        /// How long the client stays quiet after the platform refuses a request.
        /// </summary>
        /// <remarks>
        /// The same failure mode already handled by the MyDramaList image provider: when the source
        /// is refusing this HTTP stack, retrying per library item cannot succeed. Without a cooldown
        /// a library match run writes one warning and burns one round trip for every unidentified
        /// series, which is exactly the noise this prevents. A library-wide match therefore goes
        /// quiet for half an hour after the first refusal rather than repeating it hundreds of times.
        /// </remarks>
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(30);

        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(400);

        /// <summary>
        /// The guest identity the API demands on every call, shaped exactly as the site generates
        /// it: the last four digits of the epoch in milliseconds plus four random characters.
        /// </summary>
        private static readonly string GuestId = CreateGuestId();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaFindsClient> _logger;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, DramaFindsDrama> _dramaCache = new Dictionary<string, DramaFindsDrama>(StringComparer.Ordinal);

        private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
        private long _quietUntilTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaFindsClient(IHttpClientFactory httpClientFactory, ILogger<DramaFindsClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets the guest identity presented on every request.
        /// </summary>
        public static string GuestIdentity => GuestId;

        /// <summary>
        /// Gets a value indicating whether the client is currently quiet because the platform
        /// refused a recent request.
        /// </summary>
        /// <remarks>
        /// Exposed so a library-wide job can stop iterating instead of walking every remaining item
        /// through a client that is deliberately answering nothing.
        /// </remarks>
        public bool IsInFailureCooldown
        {
            get
            {
                var quietUntil = Interlocked.Read(ref _quietUntilTicks);
                return quietUntil != 0 && new DateTime(quietUntil, DateTimeKind.Utc) > DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Resolves a drama id from any input a user may paste, such as a public episode URL.
        /// </summary>
        /// <param name="input">The raw input.</param>
        /// <returns>The drama id, or null.</returns>
        public static string? ResolveDramaId(string? input)
        {
            return DramaFindsTitleMatcher.ExtractDramaId(input);
        }

        /// <summary>
        /// Fetches one drama, including its synthesized episode list, by drama id.
        /// </summary>
        /// <param name="dramaId">The numeric drama id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The drama, or null when it could not be read.</returns>
        public async Task<DramaFindsDrama?> GetDramaAsync(string? dramaId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dramaId))
            {
                return null;
            }

            lock (_cacheLock)
            {
                if (_dramaCache.TryGetValue(dramaId, out var cached))
                {
                    return cached;
                }
            }

            var body = new JsonObject
            {
                // The detail endpoint rejects an encrypted payload with "dramaId is not numeric",
                // so the id is sent as the plain string the site itself sends.
                ["dramaId"] = dramaId,
                ["indexE"] = 0
            }.ToJsonString();

            var envelope = await PostAsync(EpisodesPath, body, "drama " + dramaId, cancellationToken).ConfigureAwait(false);
            var drama = DramaFindsParser.ParseDramaDetail(envelope);
            if (drama is null)
            {
                _logger.LogWarning("DramaFinds returned no drama payload for id {DramaId}", dramaId);
                return null;
            }

            lock (_cacheLock)
            {
                _dramaCache[dramaId] = drama;
            }

            return drama;
        }

        /// <summary>
        /// Searches the platform by title and returns only the candidates that are actually
        /// similar enough to be the same drama.
        /// </summary>
        /// <param name="query">The search text.</param>
        /// <param name="maxResults">The maximum number of results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked matches, which is empty when nothing scored high enough.</returns>
        public async Task<IReadOnlyList<DramaFindsMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<DramaFindsMatch>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // A pasted URL or a bare id resolves exactly and needs no search.
            var directId = ResolveDramaId(query);
            if (directId is not null)
            {
                var direct = await GetDramaAsync(directId, cancellationToken).ConfigureAwait(false);
                if (direct is not null)
                {
                    results.Add(new DramaFindsMatch(direct, 1.0));
                    return results;
                }
            }

            var body = new JsonObject
            {
                ["keyword"] = query,
                ["pageNum"] = 1,
                ["pageSize"] = maxResults
            }.ToJsonString();

            var envelope = await PostAsync(SearchPath, body, "search", cancellationToken).ConfigureAwait(false);

            // A search that matched nothing is a normal answer, not a failure: the envelope still
            // carries code 0 with an empty list, and some series simply do not exist on this
            // platform at all.
            if (envelope is null)
            {
                return results;
            }

            var ranked = new List<DramaFindsMatch>();
            foreach (var drama in DramaFindsParser.ParseSearchResults(envelope))
            {
                var score = DramaFindsTitleMatcher.Similarity(drama.Title, query);
                if (score >= DramaFindsTitleMatcher.MinimumCandidateScore)
                {
                    ranked.Add(new DramaFindsMatch(drama, score));
                }
            }

            // The platform keeps several ids for the same drama (47011 and 47218 are both
            // "99 Amuletos, 99 Desilusões"), so the order is made fully deterministic instead of
            // inheriting the API row order: score first, then the most recent edition, then the
            // better rated one, and finally the id so two equal rows can never swap between runs.
            return ranked
                .OrderByDescending(match => match.Score)
                .ThenByDescending(match => match.Drama.ReleaseDate, StringComparer.Ordinal)
                .ThenByDescending(match => match.Drama.Rating ?? 0)
                .ThenBy(match => match.Drama.DramaId, StringComparer.Ordinal)
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
        /// Builds the guest identity once per process, shaped like the site's own:
        /// the last four digits of the epoch in milliseconds plus four random characters.
        /// </summary>
        /// <returns>The eight character guest id.</returns>
        private static string CreateGuestId()
        {
            const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

            // The last four digits of the epoch are its remainder modulo ten thousand, padded so a
            // millisecond count ending in a zero still contributes four characters.
            var epoch = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000)
                .ToString("D4", CultureInfo.InvariantCulture);

            var characters = new char[4];
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            return epoch + new string(characters);
        }

        private async Task<JsonNode?> PostAsync(string path, string body, string operation, CancellationToken cancellationToken)
        {
            if (IsInFailureCooldown)
            {
                // The platform is known to be refusing this HTTP stack; skip the request entirely
                // instead of paying a round trip (and a log line) for every library item.
                return null;
            }

            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Serialize and pace the calls: a library match run issues one search per
                // unidentified series, and the platform must not see them back to back.
                var sinceLastRequest = DateTimeOffset.UtcNow - _lastRequestUtc;
                if (sinceLastRequest < RequestDelay)
                {
                    await Task.Delay(RequestDelay - sinceLastRequest, cancellationToken).ConfigureAwait(false);
                }

                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                using var request = new HttpRequestMessage(HttpMethod.Post, ApiRoot + path);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                request.Headers.TryAddWithoutValidation("Origin", SiteRoot);
                request.Headers.TryAddWithoutValidation("Referer", SiteRoot + "/" + Locale + "/");
                request.Headers.TryAddWithoutValidation("Guest-Id", GuestId);
                request.Headers.TryAddWithoutValidation("X-Language", Locale);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                _lastRequestUtc = DateTimeOffset.UtcNow;

                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var envelope = DramaFindsParser.ParseEnvelope(payload);

                if (!response.IsSuccessStatusCode)
                {
                    EnterFailureCooldown(
                        null,
                        operation,
                        string.Format(CultureInfo.InvariantCulture, "HTTP {0}", (int)response.StatusCode));
                    return null;
                }

                if (envelope is null)
                {
                    EnterFailureCooldown(null, operation, "unparseable body");
                    return null;
                }

                if (!DramaFindsParser.IsSuccess(envelope))
                {
                    // The API reports its own failures with HTTP 200, so the envelope code is the
                    // only reliable signal that the payload can be trusted.
                    EnterFailureCooldown(
                        null,
                        operation,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "code {0}: {1}",
                            DramaFindsParser.ReadCode(envelope),
                            DramaFindsParser.ReadMessage(envelope)));
                    return null;
                }

                return envelope;
            }
            catch (HttpRequestException ex)
            {
                EnterFailureCooldown(ex, operation, ex.Message);
                return null;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                EnterFailureCooldown(ex, operation, "timed out");
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
        /// <param name="exception">The failure, when one was thrown.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="detail">The concrete failure detail, for example "code 10: GuestId Missing".</param>
        private void EnterFailureCooldown(Exception? exception, string operation, string detail)
        {
            var quietUntil = DateTime.UtcNow.Add(FailureCooldown);
            var previous = Interlocked.Exchange(ref _quietUntilTicks, quietUntil.Ticks);

            if (previous != 0 && new DateTime(previous, DateTimeKind.Utc) > DateTime.UtcNow)
            {
                // Already quiet: this failure was expected and was reported by the call that armed
                // the cooldown. Logging it again is what produces one warning line per item.
                _logger.LogDebug(exception, "DramaFinds is still unavailable while resolving {Operation}.", operation);
                return;
            }

            _logger.LogWarning(
                exception,
                "DramaFinds refused a request while resolving {Operation} ({Detail}). Pausing lookups for {Minutes} minutes instead of retrying for every item.",
                operation,
                detail,
                FailureCooldown.TotalMinutes);
        }
    }
}
