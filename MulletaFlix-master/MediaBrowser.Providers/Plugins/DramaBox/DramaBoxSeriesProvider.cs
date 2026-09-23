using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using MulletaFlix.Data.Enums;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// Provides series metadata from DramaBox, the short drama platform whose catalogue
    /// is not covered by TMDb, TVDB or MyDramaList.
    /// </summary>
    /// <remarks>
    /// DramaBox has no name search available to unauthenticated callers, so resolution happens in
    /// two ways: an exact book id extracted from a pasted drama URL, or a match against the locally
    /// crawled index (see <see cref="DramaBoxClient.RebuildIndexAsync"/>).
    /// </remarks>
    public class DramaBoxSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        /// <summary>
        /// The provider key used in provider id dictionaries.
        /// </summary>
        public const string ProviderKey = "DramaBox";

        private const int MaxSearchResults = 12;

        private readonly DramaBoxClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DramaBoxSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DramaBoxSeriesProvider"/> class.
        /// </summary>
        /// <param name="client">The DramaBox client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DramaBoxSeriesProvider(
            DramaBoxClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<DramaBoxSeriesProvider> logger)
        {
            _client = client;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => ProviderKey;

        /// <inheritdoc />
        public int Order => 3;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var providerId = searchInfo.GetProviderId(ProviderKey);
            var term = !string.IsNullOrWhiteSpace(providerId) ? providerId : searchInfo.Name;

            var matches = await _client.SearchAsync(term, MaxSearchResults, cancellationToken).ConfigureAwait(false);
            if (matches.Count == 0)
            {
                _logger.LogInformation("DramaBox found no candidate for {Term}", term);
                return results;
            }

            foreach (var match in matches)
            {
                var result = new RemoteSearchResult
                {
                    Name = match.Book.Name,
                    SearchProviderName = Name,
                    ImageUrl = DramaBoxTitleMatcher.BuildCoverUrl(match.Book.Cover, 360, 640),
                    Overview = match.Book.Overview,
                    PremiereDate = ParseShelfTime(match.Book.ShelfTime)
                };

                result.SetProviderId(ProviderKey, match.Book.BookId);
                results.Add(result);
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                QueriedById = false
            };

            var bookId = DramaBoxClient.ResolveBookId(info.GetProviderId(ProviderKey));
            if (string.IsNullOrEmpty(bookId))
            {
                var matches = await _client.SearchAsync(info.Name, 1, cancellationToken).ConfigureAwait(false);
                bookId = matches.FirstOrDefault()?.Book.BookId;
            }

            if (string.IsNullOrEmpty(bookId))
            {
                return result;
            }

            var book = await _client.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
            if (book is null)
            {
                return result;
            }

            result.QueriedById = true;

            var series = new Series();
            series.SetProviderId(ProviderKey, book.BookId);
            series.Name = book.Name;
            series.Overview = book.Overview;

            // DramaBox categories are localized; the free-form tags stay tags instead of polluting
            // the genre list with English labels.
            if (book.Genres.Count > 0)
            {
                series.Genres = book.Genres.ToArray();
            }

            if (book.Tags.Count > 0)
            {
                series.Tags = book.Tags.ToArray();
            }

            if (book.Rating.HasValue)
            {
                series.CommunityRating = (float)book.Rating.Value;
            }

            var premiereDate = ParseShelfTime(book.ShelfTime);
            if (premiereDate.HasValue)
            {
                series.PremiereDate = premiereDate.Value;
                series.ProductionYear = premiereDate.Value.Year;
            }

            result.Item = series;
            result.HasMetadata = true;

            foreach (var performer in book.Performers)
            {
                result.AddPerson(new PersonInfo
                {
                    Name = performer.Name,
                    Type = PersonKind.Actor,
                    ImageUrl = performer.Avatar
                });
            }

            _logger.LogInformation(
                "DramaBox filled metadata for {Name} (id {BookId}, {Episodes} episodes)",
                book.Name,
                book.BookId,
                book.ChapterCount);

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// Parses the DramaBox shelf time, which is the release timestamp.
        /// </summary>
        /// <param name="shelfTime">The raw value, for example 2026-01-29 10:00:26.</param>
        /// <returns>The parsed timestamp, or null.</returns>
        internal static DateTime? ParseShelfTime(string? shelfTime)
        {
            if (string.IsNullOrWhiteSpace(shelfTime))
            {
                return null;
            }

            const string Format = "yyyy-MM-dd HH:mm:ss";
            if (DateTime.TryParseExact(shelfTime, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            {
                return exact;
            }

            if (DateTime.TryParse(shelfTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var loose))
            {
                return loose;
            }

            return null;
        }
    }
}
