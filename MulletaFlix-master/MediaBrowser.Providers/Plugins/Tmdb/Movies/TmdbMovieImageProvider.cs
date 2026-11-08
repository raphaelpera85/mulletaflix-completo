using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.Search;

namespace MediaBrowser.Providers.Plugins.Tmdb.Movies
{
    /// <summary>
    /// Movie image provider powered by TMDb.
    /// </summary>
    public class TmdbMovieImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbMovieImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="tmdbClientManager">The <see cref="TmdbClientManager"/>.</param>
        public TmdbMovieImageProvider(IHttpClientFactory httpClientFactory, TmdbClientManager tmdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _tmdbClientManager = tmdbClientManager;
        }

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is Movie || item is Trailer;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item) =>
        [
            ImageType.Primary,
            ImageType.Backdrop,
            ImageType.Logo,
            ImageType.Thumb
        ];

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var language = item.GetPreferredMetadataLanguage();
            var countryCode = item.GetPreferredMetadataCountryCode();

            var movieTmdbId = 0;
            _ = int.TryParse(
                item.GetProviderId(MetadataProvider.Tmdb),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out movieTmdbId);
            if (movieTmdbId <= 0)
            {
                var movieImdbId = item.GetProviderId(MetadataProvider.Imdb);
                if (!string.IsNullOrEmpty(movieImdbId))
                {
                    var movieResult = await _tmdbClientManager.FindByExternalIdAsync(movieImdbId, FindExternalSource.Imdb, language, countryCode, cancellationToken).ConfigureAwait(false);
                    if (movieResult?.MovieResults is not null && movieResult.MovieResults.Count > 0)
                    {
                        movieTmdbId = movieResult.MovieResults[0].Id;
                    }
                }
            }

            if (movieTmdbId <= 0)
            {
                var queryName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.OriginalTitle;
                if (!string.IsNullOrWhiteSpace(queryName))
                {
                    IReadOnlyList<SearchMovie>? movieResults = null;
                    string? matchedQueryName = null;
                    var queryNames = new[] { item.Name, item.OriginalTitle }
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .SelectMany(TmdbUtils.BuildSearchNameVariants)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var targetYear = item.ProductionYear > 0 ? item.ProductionYear : null;

                    if (targetYear.HasValue)
                    {
                        foreach (var searchName in queryNames)
                        {
                            movieResults = await _tmdbClientManager
                                .SearchMovieAsync(searchName, targetYear.Value, language, countryCode, cancellationToken)
                                .ConfigureAwait(false);
                            if (movieResults is { Count: > 0 })
                            {
                                matchedQueryName = searchName;
                                break;
                            }
                        }
                    }

                    if (movieResults is not { Count: > 0 })
                    {
                        foreach (var searchName in queryNames)
                        {
                            movieResults = await _tmdbClientManager
                                .SearchMovieAsync(searchName, 0, language, countryCode, cancellationToken)
                                .ConfigureAwait(false);
                            if (movieResults is { Count: > 0 })
                            {
                                matchedQueryName = searchName;
                                break;
                            }
                        }
                    }

                    var bestMatch = movieResults is { Count: > 0 }
                        ? TmdbMovieProvider.FindBestMatch(movieResults, matchedQueryName ?? queryName, targetYear)
                        : null;
                    if (bestMatch is not null)
                    {
                        movieTmdbId = bestMatch.Id;
                    }
                }
            }

            if (movieTmdbId <= 0)
            {
                return [];
            }

            var movie = await _tmdbClientManager
                .GetMovieAsync(
                    movieTmdbId,
                    language,
                    TmdbUtils.GetImageLanguagesParam(language, countryCode),
                    countryCode,
                    cancellationToken)
                .ConfigureAwait(false);

            if (movie is null)
            {
                return [];
            }

            var posters = movie.Posters;
            var backdrops = movie.Backdrops;
            var logos = movie.Logos;
            var remoteImages = new List<RemoteImageInfo>((posters?.Count ?? 0) + (backdrops?.Count ?? 0) + (logos?.Count ?? 0));

            if (posters is not null)
            {
                remoteImages.AddRange(_tmdbClientManager.ConvertPostersToRemoteImageInfo(posters, language));
            }

            if (backdrops is not null)
            {
                remoteImages.AddRange(_tmdbClientManager.ConvertBackdropsToRemoteImageInfo(backdrops, language));
            }

            if (logos is not null)
            {
                remoteImages.AddRange(_tmdbClientManager.ConvertLogosToRemoteImageInfo(logos, language));
            }

            return remoteImages;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
