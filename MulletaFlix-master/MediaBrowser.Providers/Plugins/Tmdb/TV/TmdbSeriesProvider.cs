using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace MediaBrowser.Providers.Plugins.Tmdb.TV
{
    /// <summary>
    /// TV series provider powered by TheMovieDb.
    /// </summary>
    public class TmdbSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryManager _libraryManager;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbSeriesProvider"/> class.
        /// </summary>
        /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="tmdbClientManager">The <see cref="TmdbClientManager"/>.</param>
        public TmdbSeriesProvider(
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            TmdbClientManager tmdbClientManager)
        {
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _tmdbClientManager = tmdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public int Order => 1;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
                && int.TryParse(tmdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTmdbId)
                && parsedTmdbId > 0)
            {
                var series = await _tmdbClientManager
                    .GetSeriesAsync(parsedTmdbId, searchInfo.MetadataLanguage, TmdbUtils.GetImageLanguagesParam(searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode), searchInfo.MetadataCountryCode, cancellationToken)
                    .ConfigureAwait(false);

                if (series is not null)
                {
                    var remoteResult = MapTvShowToRemoteSearchResult(series);

                    return new[] { remoteResult };
                }
            }

            if (searchInfo.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                var findResult = await _tmdbClientManager
                    .FindByExternalIdAsync(imdbId, FindExternalSource.Imdb, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken)
                    .ConfigureAwait(false);

                var tvResults = findResult?.TvResults;
                if (tvResults is { Count: > 0 })
                {
                    var imdbIdResults = new RemoteSearchResult[tvResults.Count];
                    for (var i = 0; i < tvResults.Count; i++)
                    {
                        var remoteResult = MapSearchTvToRemoteSearchResult(tvResults[i]);
                        remoteResult.SetProviderId(MetadataProvider.Imdb, imdbId);
                        imdbIdResults[i] = remoteResult;
                    }

                    return imdbIdResults;
                }
            }

            if (searchInfo.TryGetProviderId(MetadataProvider.Tvdb, out var tvdbId))
            {
                var findResult = await _tmdbClientManager
                    .FindByExternalIdAsync(tvdbId, FindExternalSource.TvDb, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken)
                    .ConfigureAwait(false);

                var tvResults = findResult?.TvResults;
                if (tvResults is { Count: > 0 })
                {
                    var tvIdResults = new RemoteSearchResult[tvResults.Count];
                    for (var i = 0; i < tvResults.Count; i++)
                    {
                        var remoteResult = MapSearchTvToRemoteSearchResult(tvResults[i]);
                        remoteResult.SetProviderId(MetadataProvider.Tvdb, tvdbId);
                        tvIdResults[i] = remoteResult;
                    }

                    return tvIdResults;
                }
            }

            IReadOnlyList<SearchTv>? tvSearchResults = null;
            foreach (var searchName in TmdbUtils.BuildSearchNameVariants(searchInfo.Name))
            {
                // Prefer a year-filtered search. Two shows can share a name and differ only by year
                // ("A Agencia (2020)" vs "A Agencia (2024)"), and without the filter both folders
                // resolve to whichever entry TMDb ranks first, which is how two different series ended
                // up sharing provider ids and therefore a presentation unique key.
                if (searchInfo.Year.HasValue)
                {
                    tvSearchResults = await _tmdbClientManager.SearchSeriesAsync(searchName, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, searchInfo.Year.Value, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }

                if (tvSearchResults is not { Count: > 0 })
                {
                    // Fall back to the unfiltered search so a folder year that disagrees with TMDb
                    // does not cost the match entirely.
                    tvSearchResults = await _tmdbClientManager.SearchSeriesAsync(searchName, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }

                if (tvSearchResults is { Count: > 0 })
                {
                    break;
                }
            }

            if (tvSearchResults is null)
            {
                return [];
            }

            var remoteResults = new RemoteSearchResult[tvSearchResults.Count];
            for (var i = 0; i < tvSearchResults.Count; i++)
            {
                remoteResults[i] = MapSearchTvToRemoteSearchResult(tvSearchResults[i]);
            }

            return remoteResults;
        }

        private RemoteSearchResult MapTvShowToRemoteSearchResult(TvShow series)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = series.Name ?? series.OriginalName,
                SearchProviderName = Name,
                ImageUrl = _tmdbClientManager.GetPosterUrl(series.PosterPath),
                Overview = series.Overview
            };

            remoteResult.SetProviderId(MetadataProvider.Tmdb, series.Id.ToString(CultureInfo.InvariantCulture));
            if (series.ExternalIds is not null)
            {
                remoteResult.TrySetProviderId(MetadataProvider.Imdb, series.ExternalIds.ImdbId);

                remoteResult.TrySetProviderId(MetadataProvider.Tvdb, series.ExternalIds.TvdbId);
            }

            remoteResult.PremiereDate = series.FirstAirDate?.ToUniversalTime();
            remoteResult.ProductionYear = series.FirstAirDate?.Year;

            return remoteResult;
        }

        private RemoteSearchResult MapSearchTvToRemoteSearchResult(SearchTv series)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = series.Name ?? series.OriginalName,
                SearchProviderName = Name,
                ImageUrl = _tmdbClientManager.GetPosterUrl(series.PosterPath),
                Overview = series.Overview
            };

            remoteResult.SetProviderId(MetadataProvider.Tmdb, series.Id.ToString(CultureInfo.InvariantCulture));
            remoteResult.PremiereDate = series.FirstAirDate?.ToUniversalTime();
            remoteResult.ProductionYear = series.FirstAirDate?.Year;

            return remoteResult;
        }

        public static async Task<int> ResolveTmdbIdAsync(BaseItem item, TmdbClientManager tmdbClientManager, CancellationToken cancellationToken)
        {
            if (int.TryParse(
                    item.GetProviderId(MetadataProvider.Tmdb),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tmdbId)
                && tmdbId > 0)
            {
                return tmdbId;
            }

            var queryName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.OriginalTitle;
            if (string.IsNullOrWhiteSpace(queryName))
            {
                return 0;
            }

            IReadOnlyList<SearchTv>? searchResults = null;
            string? matchedQueryName = null;
            var queryNames = new[] { item.Name, item.OriginalTitle }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .SelectMany(TmdbUtils.BuildSearchNameVariants)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var targetYear = item.ProductionYear > 0 ? item.ProductionYear : null;

            foreach (var searchName in queryNames)
            {
                if (targetYear.HasValue)
                {
                    searchResults = await tmdbClientManager.SearchSeriesAsync(
                        searchName,
                        item.GetPreferredMetadataLanguage(),
                        item.GetPreferredMetadataCountryCode(),
                        targetYear.Value,
                        cancellationToken).ConfigureAwait(false);
                }

                if (searchResults is not { Count: > 0 })
                {
                    searchResults = await tmdbClientManager.SearchSeriesAsync(
                        searchName,
                        item.GetPreferredMetadataLanguage(),
                        item.GetPreferredMetadataCountryCode(),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (searchResults is { Count: > 0 })
                {
                    matchedQueryName = searchName;
                    break;
                }
            }

            var bestMatch = searchResults is { Count: > 0 }
                ? FindBestMatch(searchResults, matchedQueryName ?? queryName, targetYear)
                : null;
            return bestMatch?.Id ?? 0;
        }

        public static SearchTv? FindBestMatch(IEnumerable<SearchTv> candidates, string queryName, int? queryYear)
        {
            var queryVariants = TmdbUtils.BuildSearchNameVariants(queryName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            SearchTv? best = null;
            var bestScore = double.MinValue;

            foreach (var candidate in candidates)
            {
                var titleMatches = TmdbUtils.BuildSearchNameVariants(candidate.Name ?? string.Empty)
                    .Concat(TmdbUtils.BuildSearchNameVariants(candidate.OriginalName ?? string.Empty))
                    .Any(queryVariants.Contains);
                if (!titleMatches)
                {
                    continue;
                }

                var score = queryYear.HasValue && candidate.FirstAirDate?.Year == queryYear.Value ? 15.0 : 0.0;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                QueriedById = true
            };

            var tmdbId = info.GetProviderId(MetadataProvider.Tmdb);
            if (!int.TryParse(tmdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeriesTmdbId) || parsedSeriesTmdbId <= 0)
            {
                tmdbId = null;
            }

            if (string.IsNullOrEmpty(tmdbId) && info.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                var searchResult = await _tmdbClientManager.FindByExternalIdAsync(imdbId, FindExternalSource.Imdb, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken).ConfigureAwait(false);
                if (searchResult?.TvResults?.Count > 0)
                {
                    tmdbId = searchResult.TvResults[0].Id.ToString(CultureInfo.InvariantCulture);
                }
            }

            if (string.IsNullOrEmpty(tmdbId) && info.TryGetProviderId(MetadataProvider.Tvdb, out var tvdbId))
            {
                var searchResult = await _tmdbClientManager.FindByExternalIdAsync(tvdbId, FindExternalSource.TvDb, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken).ConfigureAwait(false);
                if (searchResult?.TvResults?.Count > 0)
                {
                    tmdbId = searchResult.TvResults[0].Id.ToString(CultureInfo.InvariantCulture);
                }
            }

            if (string.IsNullOrEmpty(tmdbId))
            {
                result.QueriedById = false;
                // ParseName is required here.
                // Caller provides the filename with extension stripped and NOT the parsed filename
                var parsedName = _libraryManager.ParseName(info.Name);
                foreach (var searchName in TmdbUtils.BuildSearchNameVariants(parsedName.Name))
                {
                    var searchYear = info.Year ?? parsedName.Year;
                    var searchResults = await _tmdbClientManager.SearchSeriesAsync(searchName, info.MetadataLanguage, info.MetadataCountryCode, searchYear ?? 0, cancellationToken).ConfigureAwait(false);

                    if (searchResults is not { Count: > 0 } && searchYear.HasValue)
                    {
                        // Fall back to the unfiltered search so a folder year that disagrees with TMDb
                        // does not cost the match entirely.
                        searchResults = await _tmdbClientManager.SearchSeriesAsync(searchName, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }

                    if (searchResults?.Count > 0)
                    {
                        tmdbId = searchResults[0].Id.ToString(CultureInfo.InvariantCulture);
                        break;
                    }
                }
            }

            if (!int.TryParse(tmdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tmdbIdInt) || tmdbIdInt <= 0)
            {
                return result;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var tvShow = await _tmdbClientManager
                .GetSeriesAsync(tmdbIdInt, info.MetadataLanguage, TmdbUtils.GetImageLanguagesParam(info.MetadataLanguage, info.MetadataCountryCode), info.MetadataCountryCode, cancellationToken)
                .ConfigureAwait(false);

            if (tvShow is null)
            {
                return result;
            }

            result = new MetadataResult<Series>
            {
                Item = MapTvShowToSeries(tvShow, info.MetadataCountryCode),
                ResultLanguage = info.MetadataLanguage ?? tvShow.OriginalLanguage
            };

            foreach (var person in GetPersons(tvShow))
            {
                result.AddPerson(person);
            }

            result.HasMetadata = result.Item is not null;

            return result;
        }

        private static Series MapTvShowToSeries(TvShow seriesResult, string preferredCountryCode)
        {
            var series = new Series
            {
                Name = seriesResult.Name,
                OriginalTitle = seriesResult.OriginalName
            };

            series.SetProviderId(MetadataProvider.Tmdb, seriesResult.Id.ToString(CultureInfo.InvariantCulture));

            series.CommunityRating = Convert.ToSingle(seriesResult.VoteAverage);

            series.Overview = seriesResult.Overview;

            if (seriesResult.Networks is not null)
            {
                series.Studios = seriesResult.Networks.Select(i => i.Name).ToArray();
            }

            if (seriesResult.Genres is not null)
            {
                series.Genres = seriesResult.Genres.Select(i => i.Name).ToArray();
            }

            if (seriesResult.Keywords?.Results is not null)
            {
                foreach (var result in seriesResult.Keywords.Results)
                {
                    var name = result.Name;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        series.AddTag(name);
                    }
                }
            }

            series.HomePageUrl = seriesResult.Homepage;

            series.RunTimeTicks = seriesResult.EpisodeRunTime?.Select(i => TimeSpan.FromMinutes(i).Ticks).FirstOrDefault();

            if (Emby.Naming.TV.TvParserHelpers.TryParseSeriesStatus(seriesResult.Status, out var seriesStatus))
            {
                series.Status = seriesStatus;
            }

            series.EndDate = seriesResult.LastAirDate;
            series.PremiereDate = seriesResult.FirstAirDate;
            series.ProductionYear = seriesResult.FirstAirDate?.Year;

            var ids = seriesResult.ExternalIds;
            if (ids is not null)
            {
                series.TrySetProviderId(MetadataProvider.Imdb, ids.ImdbId);
                series.TrySetProviderId(MetadataProvider.TvRage, ids.TvrageId);
                series.TrySetProviderId(MetadataProvider.Tvdb, ids.TvdbId);
            }

            var contentRatings = seriesResult.ContentRatings?.Results ?? new List<ContentRating>();

            var ourRelease = contentRatings.FirstOrDefault(c => string.Equals(c.Iso_3166_1, preferredCountryCode, StringComparison.OrdinalIgnoreCase));
            var usRelease = contentRatings.FirstOrDefault(c => string.Equals(c.Iso_3166_1, "US", StringComparison.OrdinalIgnoreCase));
            var minimumRelease = contentRatings.FirstOrDefault();

            if (ourRelease?.Rating is not null)
            {
                series.OfficialRating = TmdbUtils.BuildParentalRating(preferredCountryCode, ourRelease.Rating);
            }
            else if (usRelease?.Rating is not null)
            {
                series.OfficialRating = usRelease.Rating;
            }
            else if (minimumRelease?.Rating is not null)
            {
                series.OfficialRating = minimumRelease.Rating;
            }

            if (seriesResult.Videos?.Results is not null)
            {
                foreach (var video in seriesResult.Videos.Results)
                {
                    if (TmdbUtils.IsTrailerType(video))
                    {
                        series.AddTrailerUrl("https://www.youtube.com/watch?v=" + video.Key);
                    }
                }
            }

            if (!string.IsNullOrEmpty(seriesResult.OriginalLanguage))
            {
                series.OriginalLanguage = seriesResult.OriginalLanguage;
            }

            return series;
        }

        private IEnumerable<PersonInfo> GetPersons(TvShow seriesResult)
        {
            var config = Plugin.Instance.Configuration;

            if (seriesResult.Credits?.Cast is not null)
            {
                IEnumerable<Cast> castQuery = seriesResult.Credits.Cast.OrderBy(a => a.Order);

                if (config.HideMissingCastMembers)
                {
                    castQuery = castQuery.Where(a => !string.IsNullOrEmpty(a.ProfilePath));
                }

                foreach (var actor in castQuery.Take(config.MaxCastMembers))
                {
                    if (string.IsNullOrWhiteSpace(actor.Name))
                    {
                        continue;
                    }

                    var personInfo = new PersonInfo
                    {
                        Name = actor.Name.Trim(),
                        Role = actor.Character?.Trim() ?? string.Empty,
                        Type = PersonKind.Actor,
                        SortOrder = actor.Order,
                        // NOTE: Null values are filtered out above
                        ImageUrl = _tmdbClientManager.GetProfileUrl(actor.ProfilePath!)
                    };

                    if (actor.Id > 0)
                    {
                        personInfo.SetProviderId(MetadataProvider.Tmdb, actor.Id.ToString(CultureInfo.InvariantCulture));
                    }

                    yield return personInfo;
                }
            }

            if (seriesResult.Credits?.Crew is not null)
            {
                var crewQuery = seriesResult.Credits.Crew
                    .Select(crewMember => new
                    {
                        CrewMember = crewMember,
                        PersonType = TmdbUtils.MapCrewToPersonType(crewMember)
                    })
                    .Where(entry => TmdbUtils.WantedCrewKinds.Contains(entry.PersonType));

                if (config.HideMissingCrewMembers)
                {
                    crewQuery = crewQuery.Where(entry => !string.IsNullOrEmpty(entry.CrewMember.ProfilePath));
                }

                foreach (var entry in crewQuery.Take(config.MaxCrewMembers))
                {
                    var crewMember = entry.CrewMember;

                    if (string.IsNullOrWhiteSpace(crewMember.Name))
                    {
                        continue;
                    }

                    var personInfo = new PersonInfo
                    {
                        Name = crewMember.Name.Trim(),
                        Role = crewMember.Job?.Trim() ?? string.Empty,
                        Type = entry.PersonType,
                        // NOTE: Null values are filtered out above
                        ImageUrl = _tmdbClientManager.GetProfileUrl(crewMember.ProfilePath!)
                    };

                    if (crewMember.Id > 0)
                    {
                        personInfo.SetProviderId(MetadataProvider.Tmdb, crewMember.Id.ToString(CultureInfo.InvariantCulture));
                    }

                    yield return personInfo;
                }
            }
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
