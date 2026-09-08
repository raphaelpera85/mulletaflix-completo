using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;

namespace MediaBrowser.Providers.Plugins.Tmdb.Movies
{
    /// <summary>
    /// Movie provider powered by TMDb.
    /// </summary>
    public class TmdbMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryManager _libraryManager;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbMovieProvider"/> class.
        /// </summary>
        /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="tmdbClientManager">The <see cref="TmdbClientManager"/>.</param>
        public TmdbMovieProvider(
            ILibraryManager libraryManager,
            TmdbClientManager tmdbClientManager,
            IHttpClientFactory httpClientFactory)
        {
            _libraryManager = libraryManager;
            _tmdbClientManager = tmdbClientManager;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public int Order => 1;

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.TryGetProviderId(MetadataProvider.Tmdb, out var id))
            {
                var movie = await _tmdbClientManager
                    .GetMovieAsync(
                        int.Parse(id, CultureInfo.InvariantCulture),
                        searchInfo.MetadataLanguage,
                        TmdbUtils.GetImageLanguagesParam(searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode),
                        searchInfo.MetadataCountryCode,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (movie is not null)
                {
                    var remoteResult = new RemoteSearchResult
                    {
                        Name = movie.Title ?? movie.OriginalTitle,
                        SearchProviderName = Name,
                        ImageUrl = _tmdbClientManager.GetPosterUrl(movie.PosterPath),
                        Overview = movie.Overview
                    };

                    if (movie.ReleaseDate is not null)
                    {
                        var releaseDate = movie.ReleaseDate.Value.ToUniversalTime();
                        remoteResult.PremiereDate = releaseDate;
                        remoteResult.ProductionYear = releaseDate.Year;
                    }

                    remoteResult.SetProviderId(MetadataProvider.Tmdb, movie.Id.ToString(CultureInfo.InvariantCulture));
                    remoteResult.TrySetProviderId(MetadataProvider.Imdb, movie.ImdbId);

                    return [remoteResult];
                }
            }

            IReadOnlyList<SearchMovie>? movieResults = null;
            if (searchInfo.TryGetProviderId(MetadataProvider.Imdb, out id))
            {
                var result = await _tmdbClientManager.FindByExternalIdAsync(
                    id,
                    FindExternalSource.Imdb,
                    TmdbUtils.GetImageLanguagesParam(searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode),
                    searchInfo.MetadataCountryCode,
                    cancellationToken).ConfigureAwait(false);
                movieResults = result?.MovieResults;
            }

            if (movieResults is null && searchInfo.TryGetProviderId(MetadataProvider.Tvdb, out id))
            {
                var result = await _tmdbClientManager.FindByExternalIdAsync(
                    id,
                    FindExternalSource.TvDb,
                    TmdbUtils.GetImageLanguagesParam(searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode),
                    searchInfo.MetadataCountryCode,
                    cancellationToken).ConfigureAwait(false);
                movieResults = result?.MovieResults;
            }

            if (movieResults is null)
            {
                var parsedName = _libraryManager.ParseName(searchInfo.Name);
                var targetYear = searchInfo.Year ?? parsedName.Year;
                var baseName = !string.IsNullOrWhiteSpace(parsedName.Name) ? parsedName.Name : searchInfo.Name;

                if (targetYear.HasValue && targetYear.Value > 0)
                {
                    foreach (var searchName in TmdbUtils.BuildSearchNameVariants(baseName))
                    {
                        movieResults = await _tmdbClientManager
                            .SearchMovieAsync(searchName, targetYear.Value, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken)
                            .ConfigureAwait(false);

                        if (movieResults is { Count: > 0 })
                        {
                            break;
                        }
                    }
                }

                if (movieResults is null || movieResults.Count == 0)
                {
                    foreach (var searchName in TmdbUtils.BuildSearchNameVariants(baseName))
                    {
                        movieResults = await _tmdbClientManager
                            .SearchMovieAsync(searchName, 0, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken)
                            .ConfigureAwait(false);

                        if (movieResults is { Count: > 0 })
                        {
                            break;
                        }
                    }
                }
            }

            if (movieResults is null)
            {
                return [];
            }

            var len = movieResults.Count;
            var remoteSearchResults = new List<RemoteSearchResult>(len);
            for (var i = 0; i < len; i++)
            {
                var movieResult = movieResults[i];
                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = movieResult.Title ?? movieResult.OriginalTitle,
                    ImageUrl = _tmdbClientManager.GetPosterUrl(movieResult.PosterPath),
                    Overview = movieResult.Overview,
                    SearchProviderName = Name
                };

                var releaseDate = movieResult.ReleaseDate?.ToUniversalTime();
                remoteSearchResult.PremiereDate = releaseDate;
                remoteSearchResult.ProductionYear = releaseDate?.Year;

                remoteSearchResult.SetProviderId(MetadataProvider.Tmdb, movieResult.Id.ToString(CultureInfo.InvariantCulture));
                remoteSearchResults.Add(remoteSearchResult);
            }

            var qName = !string.IsNullOrWhiteSpace(searchInfo.Name) ? searchInfo.Name : string.Empty;
            var qYear = searchInfo.Year;
            remoteSearchResults.Sort((a, b) =>
            {
                var scoreA = ScoreRemoteResult(a, qName, qYear);
                var scoreB = ScoreRemoteResult(b, qName, qYear);
                return scoreB.CompareTo(scoreA);
            });

            return remoteSearchResults;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var tmdbId = info.GetProviderId(MetadataProvider.Tmdb);
            var imdbId = info.GetProviderId(MetadataProvider.Imdb);
            var config = Plugin.Instance.Configuration;

            if (string.IsNullOrEmpty(tmdbId) && string.IsNullOrEmpty(imdbId))
            {
                // ParseName is required here.
                // Caller provides the filename with extension stripped and NOT the parsed filename
                var parsedName = _libraryManager.ParseName(info.Name);
                var targetYear = info.Year ?? parsedName.Year;
                var baseName = !string.IsNullOrWhiteSpace(parsedName.Name) ? parsedName.Name : info.Name;

                var candidateResults = new List<SearchMovie>();

                // 1. Busca inicial com ano estrito se disponível
                if (targetYear.HasValue && targetYear.Value > 0)
                {
                    foreach (var searchName in TmdbUtils.BuildSearchNameVariants(baseName))
                    {
                        var searchResults = await _tmdbClientManager.SearchMovieAsync(searchName, targetYear.Value, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken).ConfigureAwait(false);

                        if (searchResults is { Count: > 0 })
                        {
                            candidateResults.AddRange(searchResults);
                            break;
                        }
                    }
                }

                // 2. Fallback de busca aberta caso não encontre ou não tenha ano
                if (candidateResults.Count == 0)
                {
                    foreach (var searchName in TmdbUtils.BuildSearchNameVariants(baseName))
                    {
                        var searchResults = await _tmdbClientManager.SearchMovieAsync(searchName, 0, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken).ConfigureAwait(false);

                        if (searchResults is { Count: > 0 })
                        {
                            candidateResults.AddRange(searchResults);
                            break;
                        }
                    }
                }

                if (candidateResults.Count > 0)
                {
                    var bestMatch = FindBestMatch(candidateResults, baseName, targetYear);
                    if (bestMatch is not null)
                    {
                        tmdbId = bestMatch.Id.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            if (string.IsNullOrEmpty(tmdbId) && !string.IsNullOrEmpty(imdbId))
            {
                var movieResultFromImdbId = await _tmdbClientManager.FindByExternalIdAsync(imdbId, FindExternalSource.Imdb, info.MetadataLanguage, info.MetadataCountryCode, cancellationToken).ConfigureAwait(false);
                if (movieResultFromImdbId?.MovieResults?.Count > 0)
                {
                    tmdbId = movieResultFromImdbId.MovieResults[0].Id.ToString(CultureInfo.InvariantCulture);
                }
            }

            if (string.IsNullOrEmpty(tmdbId))
            {
                return new MetadataResult<Movie>();
            }

            var movieResult = await _tmdbClientManager
                .GetMovieAsync(Convert.ToInt32(tmdbId, CultureInfo.InvariantCulture), info.MetadataLanguage, TmdbUtils.GetImageLanguagesParam(info.MetadataLanguage, info.MetadataCountryCode), info.MetadataCountryCode, cancellationToken)
                .ConfigureAwait(false);

            if (movieResult is null)
            {
                return new MetadataResult<Movie>();
            }

            var movie = new Movie
            {
                Name = movieResult.Title ?? movieResult.OriginalTitle,
                OriginalTitle = movieResult.OriginalTitle,
                Overview = movieResult.Overview?.Replace("\n\n", "\n", StringComparison.InvariantCulture),
                Tagline = movieResult.Tagline,
                ProductionLocations = movieResult.ProductionCountries?.Select(pc => pc.Name).ToArray() ?? Array.Empty<string>()
            };
            var metadataResult = new MetadataResult<Movie>
            {
                HasMetadata = true,
                ResultLanguage = info.MetadataLanguage,
                Item = movie
            };

            movie.SetProviderId(MetadataProvider.Tmdb, tmdbId);
            movie.TrySetProviderId(MetadataProvider.Imdb, movieResult.ImdbId);
            if (movieResult.BelongsToCollection is not null)
            {
                movie.SetProviderId(MetadataProvider.TmdbCollection, movieResult.BelongsToCollection.Id.ToString(CultureInfo.InvariantCulture));
                movie.CollectionName = movieResult.BelongsToCollection.Name;
            }

            movie.CommunityRating = Convert.ToSingle(movieResult.VoteAverage);

            if (movieResult.Releases?.Countries is not null)
            {
                var releases = movieResult.Releases.Countries.Where(i => !string.IsNullOrWhiteSpace(i.Certification)).ToList();

                var ourRelease = releases.FirstOrDefault(c => string.Equals(c.Iso_3166_1, info.MetadataCountryCode, StringComparison.OrdinalIgnoreCase));

                if (ourRelease?.Certification is not null)
                {
                    movie.OfficialRating = TmdbUtils.BuildParentalRating(info.MetadataCountryCode, ourRelease.Certification);
                }
                else
                {
                    var usRelease = releases.FirstOrDefault(c => string.Equals(c.Iso_3166_1, "US", StringComparison.OrdinalIgnoreCase));
                    if (usRelease?.Certification is not null)
                    {
                        movie.OfficialRating = usRelease.Certification;
                    }
                }
            }

            movie.PremiereDate = movieResult.ReleaseDate;
            movie.ProductionYear = movieResult.ReleaseDate?.Year;

            if (movieResult.ProductionCompanies is not null)
            {
                movie.SetStudios(movieResult.ProductionCompanies.Select(c => c.Name));
            }

            var genres = movieResult.Genres;

            if (genres is not null)
            {
                foreach (var genre in genres.Select(g => g.Name).Trimmed())
                {
                    movie.AddGenre(genre);
                }
            }

            if (movieResult.Keywords?.Keywords is not null)
            {
                foreach (var keyword in movieResult.Keywords.Keywords)
                {
                    var name = keyword.Name;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        movie.AddTag(name);
                    }
                }
            }

            if (movieResult.Credits?.Cast is not null)
            {
                var castQuery = movieResult.Credits.Cast.AsEnumerable();

                if (config.HideMissingCastMembers)
                {
                    castQuery = castQuery.Where(a => !string.IsNullOrEmpty(a.ProfilePath));
                }

                castQuery = castQuery.OrderBy(a => a.Order).Take(config.MaxCastMembers);

                foreach (var actor in castQuery)
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
                        SortOrder = actor.Order
                    };

                    if (!string.IsNullOrWhiteSpace(actor.ProfilePath))
                    {
                        personInfo.ImageUrl = _tmdbClientManager.GetProfileUrl(actor.ProfilePath);
                    }

                    if (actor.Id > 0)
                    {
                        personInfo.SetProviderId(MetadataProvider.Tmdb, actor.Id.ToString(CultureInfo.InvariantCulture));
                    }

                    metadataResult.AddPerson(personInfo);
                }
            }

            if (movieResult.Credits?.Crew is not null)
            {
                var crewQuery = movieResult.Credits.Crew
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

                crewQuery = crewQuery.Take(config.MaxCrewMembers);

                foreach (var entry in crewQuery)
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
                        Type = entry.PersonType
                    };

                    if (!string.IsNullOrWhiteSpace(crewMember.ProfilePath))
                    {
                        personInfo.ImageUrl = _tmdbClientManager.GetProfileUrl(crewMember.ProfilePath);
                    }

                    if (crewMember.Id > 0)
                    {
                        personInfo.SetProviderId(MetadataProvider.Tmdb, crewMember.Id.ToString(CultureInfo.InvariantCulture));
                    }

                    metadataResult.AddPerson(personInfo);
                }
            }

            if (movieResult.Videos?.Results is not null)
            {
                var trailers = new List<MediaUrl>();

                var sortedVideos = movieResult.Videos.Results
                    .OrderByDescending(video => string.Equals(video.Type, "trailer", StringComparison.OrdinalIgnoreCase));

                foreach (var video in sortedVideos)
                {
                    if (!TmdbUtils.IsTrailerType(video))
                    {
                        continue;
                    }

                    trailers.Add(new MediaUrl
                    {
                        Url = string.Format(CultureInfo.InvariantCulture, "https://www.youtube.com/watch?v={0}", video.Key),
                        Name = video.Name
                    });
                }

                movie.RemoteTrailers = trailers;
            }

            if (!string.IsNullOrEmpty(movieResult.OriginalLanguage))
            {
                movie.OriginalLanguage = movieResult.OriginalLanguage;
            }

            return metadataResult;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        public static SearchMovie? FindBestMatch(IEnumerable<SearchMovie> candidates, string queryName, int? queryYear)
        {
            SearchMovie? best = null;
            double bestScore = double.MinValue;
            double bestTitleScore = double.MinValue;

            foreach (var candidate in candidates)
            {
                var titleScore = ComputeTitleScore(queryName, candidate.Title, candidate.OriginalTitle);
                var score = ScoreCandidate(candidate, queryName, queryYear);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTitleScore = titleScore;
                    best = candidate;
                }
            }

            // O ano sozinho não pode transformar um título parcialmente
            // coincidente em uma escolha válida. Isso evita, por exemplo,
            // reconhecer "Johnny e Clyde" como "Bonnie e Clyde" porque ambos
            // compartilham "e Clyde" e o ano de lançamento.
            if (bestScore < 30.0 || bestTitleScore < 60.0)
            {
                return null;
            }

            return best;
        }

        public static double ScoreCandidate(SearchMovie candidate, string queryName, int? queryYear)
        {
            var titleScore = ComputeTitleScore(queryName, candidate.Title, candidate.OriginalTitle);
            var yearScore = ComputeYearScore(queryYear, candidate.ReleaseDate?.Year);
            var popularityBonus = Math.Min(5.0, candidate.VoteCount > 50 ? 5.0 : candidate.VoteCount / 10.0);

            return titleScore + yearScore + popularityBonus;
        }

        public static double ComputeTitleScore(string queryName, string? candidateTitle, string? candidateOriginalTitle)
        {
            var q = NormalizeTitleForMatching(queryName);
            if (string.IsNullOrWhiteSpace(q))
            {
                return 0.0;
            }

            var score1 = ComputeSingleTitleScore(q, NormalizeTitleForMatching(candidateTitle));
            var score2 = ComputeSingleTitleScore(q, NormalizeTitleForMatching(candidateOriginalTitle));

            return Math.Max(score1, score2);
        }

        private static double ComputeSingleTitleScore(string q, string c)
        {
            if (string.IsNullOrWhiteSpace(c))
            {
                return 0.0;
            }

            if (string.Equals(q, c, StringComparison.OrdinalIgnoreCase))
            {
                return 100.0;
            }

            var qWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cWords = c.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (qWords.Length == 0 || cWords.Length == 0)
            {
                return 0.0;
            }

            int matched = 0;
            foreach (var qw in qWords)
            {
                if (Array.Exists(cWords, cw => string.Equals(qw, cw, StringComparison.OrdinalIgnoreCase)))
                {
                    matched++;
                }
            }

            if (matched == 0)
            {
                return 0.0;
            }

            double recall = (double)matched / qWords.Length;
            double precision = (double)matched / cWords.Length;
            double f1 = 2 * (precision * recall) / (precision + recall);
            double score = f1 * 80.0;

            if (recall >= 0.99)
            {
                score += 10.0;
            }

            if (cWords.Length < qWords.Length)
            {
                score -= (qWords.Length - cWords.Length) * 15.0;
            }

            return Math.Max(0.0, score);
        }

        public static double ComputeYearScore(int? queryYear, int? candidateYear)
        {
            if (!queryYear.HasValue || queryYear.Value <= 0)
            {
                return 0.0;
            }

            if (!candidateYear.HasValue || candidateYear.Value <= 0)
            {
                return -15.0;
            }

            var diff = Math.Abs(queryYear.Value - candidateYear.Value);
            if (diff == 0)
            {
                return 60.0;
            }

            if (diff == 1)
            {
                return 20.0;
            }

            if (diff == 2)
            {
                return -20.0;
            }

            return Math.Max(-100.0, -60.0 - ((diff - 2) * 10.0));
        }

        public static string NormalizeTitleForMatching(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var t = title.Trim();
            t = Regex.Replace(t, @"(?i)[\(\[\{]\s*(?:leg|legendado|legendada|dublado|dublada|dub|sub|subbed|subs|multi[- ]?subs|pt[- ]?br|ptbr|br|brasil|latino|audio[- ]?latino|audio[- ]?original|original[- ]?audio|dual[- ]?audio|dual|nacional|multi|portugu[eê]s)\s*[\)\]\}]", " ", RegexOptions.CultureInvariant);
            t = Regex.Replace(t, @"(?i)(?<=^|[\s\._-])(?:leg|legendado|legendada|dublado|dublada|dub|sub|subbed|subs|multi[- ]?subs|pt[- ]?br|ptbr|latino|audio[- ]?latino|audio[- ]?original|original[- ]?audio|dual[- ]?audio|dual|nacional)(?=[\s\._-]|$)", " ", RegexOptions.CultureInvariant);
            t = t.Replace("&", " e ", StringComparison.Ordinal);
            t = Regex.Replace(t, @"[^\p{L}\p{N}\s]", " ", RegexOptions.CultureInvariant);
            t = t.RemoveDiacritics();
            t = Regex.Replace(t, @"\s+", " ", RegexOptions.CultureInvariant).Trim().ToLowerInvariant();

            var articles = new[] { "o ", "a ", "os ", "as ", "um ", "uma ", "the " };
            foreach (var art in articles)
            {
                if (t.StartsWith(art, StringComparison.Ordinal))
                {
                    t = t[art.Length..].Trim();
                    break;
                }
            }

            return t;
        }

        private static double ScoreRemoteResult(RemoteSearchResult result, string queryName, int? queryYear)
        {
            var titleScore = ComputeTitleScore(queryName, result.Name, null);
            var yearScore = ComputeYearScore(queryYear, result.ProductionYear);
            return titleScore + yearScore;
        }
    }
}
