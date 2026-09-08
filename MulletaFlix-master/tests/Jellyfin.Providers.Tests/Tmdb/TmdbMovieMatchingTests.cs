using System;
using System.Collections.Generic;
using MediaBrowser.Providers.Plugins.Tmdb.Movies;
using TMDbLib.Objects.Search;
using Xunit;

namespace MulletaFlix.Providers.Tests.Tmdb
{
    public class TmdbMovieMatchingTests
    {
        [Fact]
        public void FindBestMatch_PicksFullTitleAndMatchingYear_OverPartialTitleAndDifferentYear()
        {
            var candidates = new List<SearchMovie>
            {
                new SearchMovie
                {
                    Id = 100,
                    Title = "Gladiador",
                    OriginalTitle = "Gladiator",
                    ReleaseDate = new DateTime(2000, 5, 5),
                    VoteCount = 18000
                },
                new SearchMovie
                {
                    Id = 200,
                    Title = "Gladiador II",
                    OriginalTitle = "Gladiator II",
                    ReleaseDate = new DateTime(2024, 11, 14),
                    VoteCount = 2500
                }
            };

            var match = TmdbMovieProvider.FindBestMatch(candidates, "Gladiador II", 2024);

            Assert.NotNull(match);
            Assert.Equal(200, match.Id);
            Assert.Equal("Gladiador II", match.Title);
        }

        [Fact]
        public void FindBestMatch_PicksFullCompoundTitle_OverSingleWordPrefix()
        {
            var candidates = new List<SearchMovie>
            {
                new SearchMovie
                {
                    Id = 1,
                    Title = "Deadpool",
                    OriginalTitle = "Deadpool",
                    ReleaseDate = new DateTime(2016, 2, 9),
                    VoteCount = 29000
                },
                new SearchMovie
                {
                    Id = 2,
                    Title = "Deadpool & Wolverine",
                    OriginalTitle = "Deadpool & Wolverine",
                    ReleaseDate = new DateTime(2024, 7, 24),
                    VoteCount = 4000
                }
            };

            var match = TmdbMovieProvider.FindBestMatch(candidates, "Deadpool & Wolverine", 2024);

            Assert.NotNull(match);
            Assert.Equal(2, match.Id);
            Assert.Equal("Deadpool & Wolverine", match.Title);
        }

        [Fact]
        public void FindBestMatch_SameTitle_SelectsCorrectYear()
        {
            var candidates = new List<SearchMovie>
            {
                new SearchMovie
                {
                    Id = 1989,
                    Title = "Batman",
                    OriginalTitle = "Batman",
                    ReleaseDate = new DateTime(1989, 6, 23),
                    VoteCount = 7500
                },
                new SearchMovie
                {
                    Id = 2022,
                    Title = "The Batman",
                    OriginalTitle = "The Batman",
                    ReleaseDate = new DateTime(2022, 3, 1),
                    VoteCount = 9500
                }
            };

            var match1989 = TmdbMovieProvider.FindBestMatch(candidates, "Batman", 1989);
            var match2022 = TmdbMovieProvider.FindBestMatch(candidates, "Batman", 2022);

            Assert.NotNull(match1989);
            Assert.Equal(1989, match1989.Id);

            Assert.NotNull(match2022);
            Assert.Equal(2022, match2022.Id);
        }

        [Fact]
        public void FindBestMatch_DoesNotAcceptDifferentTitleWithSharedConjunction()
        {
            var candidates = new List<SearchMovie>
            {
                new SearchMovie
                {
                    Id = 2023,
                    Title = "Bonnie & Clyde",
                    OriginalTitle = "Bonnie & Clyde",
                    ReleaseDate = new DateTime(2023, 1, 1),
                    VoteCount = 1000
                }
            };

            var match = TmdbMovieProvider.FindBestMatch(candidates, "Johnny e Clyde", 2023);

            Assert.Null(match);
        }

        [Fact]
        public void ComputeYearScore_PenalizesSeverely_WhenYearsDivergeSignificantly()
        {
            var exactScore = TmdbMovieProvider.ComputeYearScore(2024, 2024);
            var diff1Score = TmdbMovieProvider.ComputeYearScore(2024, 2023);
            var diff5Score = TmdbMovieProvider.ComputeYearScore(2024, 2019);
            var diff24Score = TmdbMovieProvider.ComputeYearScore(2024, 2000);

            Assert.Equal(60.0, exactScore);
            Assert.Equal(20.0, diff1Score);
            Assert.True(diff5Score < 0);
            Assert.True(diff24Score <= -30.0);
        }
    }
}
