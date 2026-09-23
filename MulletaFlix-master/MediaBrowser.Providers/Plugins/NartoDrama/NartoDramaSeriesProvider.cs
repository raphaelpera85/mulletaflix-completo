using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// Provides series metadata from NartoDrama, the short drama aggregator whose catalogue is not
    /// covered by TMDb, TVDB or MyDramaList.
    /// </summary>
    /// <remarks>
    /// Resolution happens in two ways: an exact slug extracted from a pasted detail or episode URL,
    /// or a match through NartoDrama's own search page ranked with the local title matcher.
    /// </remarks>
    public class NartoDramaSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        /// <summary>
        /// The provider key used in provider id dictionaries.
        /// </summary>
        public const string ProviderKey = "NartoDrama";

        private const int MaxSearchResults = 12;

        /// <summary>
        /// Minimum similarity for the automatic lookup that runs when a series carries no NartoDrama id.
        /// </summary>
        /// <remarks>
        /// Measured on the live site: the search page answers with fallback hits for a term the
        /// catalogue does not contain, so an unknown title comes back as an unrelated film rather
        /// than as nothing. Taking the first hit unconditionally would rename an unrelated library
        /// series. The threshold was chosen from real cases: an identical title scores 1, the same
        /// title with the library's "(Dublado)" marker scores 1 once the marker is normalized, and a
        /// title contained in a longer platform title scores between 0.90 and 0.95.
        /// </remarks>
        private const double AutoMatchThreshold = 0.9;

        private readonly NartoDramaClient _client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NartoDramaSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NartoDramaSeriesProvider"/> class.
        /// </summary>
        /// <param name="client">The NartoDrama client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public NartoDramaSeriesProvider(
            NartoDramaClient client,
            IHttpClientFactory httpClientFactory,
            ILogger<NartoDramaSeriesProvider> logger)
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
                _logger.LogInformation("NartoDrama found no candidate for {Term}", term);
                return results;
            }

            foreach (var match in matches)
            {
                var result = new RemoteSearchResult
                {
                    Name = match.Series.Name,
                    SearchProviderName = Name,
                    ImageUrl = match.Series.Cover,
                    Overview = match.Series.Overview
                };

                result.SetProviderId(ProviderKey, match.Series.Slug);
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

            var slug = NartoDramaClient.ResolveSlug(info.GetProviderId(ProviderKey));
            if (string.IsNullOrEmpty(slug))
            {
                var matches = await _client.SearchAsync(info.Name, 1, cancellationToken).ConfigureAwait(false);
                var best = matches.FirstOrDefault();

                // An explicit provider id is trusted as given, but a title is only trusted when it
                // actually looks like the platform's title: the site returns unrelated fallback hits.
                if (best is null || best.Score < AutoMatchThreshold)
                {
                    _logger.LogInformation(
                        "NartoDrama found no confident match for {Name} (best score {Score})",
                        info.Name,
                        best?.Score ?? 0);
                    return result;
                }

                slug = best.Series.Slug;
            }

            if (string.IsNullOrEmpty(slug))
            {
                return result;
            }

            var series = await _client.GetSeriesAsync(slug, cancellationToken).ConfigureAwait(false);
            if (series is null)
            {
                return result;
            }

            result.QueriedById = true;

            var item = new Series();

            // The page's own canonical slug is preferred over the one that was pasted, so a URL that
            // used a percent encoded accented variant normalizes to the catalogue's form.
            item.SetProviderId(ProviderKey, series.Slug);
            item.Name = series.Name;
            item.Overview = series.Overview;

            // NartoDrama's labels are its own hashtags rather than a fixed genre vocabulary, so they
            // are published as tags instead of polluting the genre list.
            if (series.Tags.Count > 0)
            {
                item.Tags = series.Tags.ToArray();
            }

            // The page carries no release date anywhere: no datePublished in its JSON-LD, no time
            // element, no year in the metadata block. PremiereDate and ProductionYear are therefore
            // left for the other providers to fill rather than invented here.

            result.Item = item;
            result.HasMetadata = true;

            _logger.LogInformation(
                "NartoDrama filled metadata for {Name} (slug {Slug}, {Episodes} of {Reported} episodes)",
                series.Name,
                series.Slug,
                series.Episodes.Count,
                series.EpisodeCount);

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
