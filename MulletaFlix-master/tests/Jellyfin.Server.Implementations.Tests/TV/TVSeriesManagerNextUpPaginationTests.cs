using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;
using Emby.Server.Implementations.TV;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.TV;

public sealed class TVSeriesManagerNextUpPaginationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetNextUp_FetchesAnotherSeriesBatchWhenNeeded(bool firstBatchHasEligibleEpisode)
    {
        var user = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth");
        var candidateDate = DateTime.UtcNow.AddDays(-1);
        var seriesKeys = Enumerable.Range(0, 51).Select(index => $"Series{index:D2}").ToArray();
        var requestedCursors = new List<(DateTime? Date, string? Key)>();
        var firstEpisode = new Episode { Name = "Episode 00" };
        var lastEpisode = new Episode { Name = "Episode 50" };

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetNextUpSeriesKeys(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyCollection<BaseItem>>(),
                It.IsAny<DateTime>()))
            .Returns((InternalItemsQuery query, IReadOnlyCollection<BaseItem> _, DateTime _) =>
            {
                requestedCursors.Add((query.NextUpLastPlayedDateAfter, query.NextUpSeriesKeyAfter));
                var candidates = seriesKeys.Select(key => (SeriesKey: key, LastPlayedDate: candidateDate));
                if (query.NextUpLastPlayedDateAfter.HasValue && query.NextUpSeriesKeyAfter is not null)
                {
                    candidates = candidates.Where(item => item.LastPlayedDate < query.NextUpLastPlayedDateAfter.Value
                        || (item.LastPlayedDate == query.NextUpLastPlayedDateAfter.Value
                            && string.CompareOrdinal(item.SeriesKey, query.NextUpSeriesKeyAfter) > 0));
                }

                return candidates
                    .Take(query.Limit ?? seriesKeys.Length)
                    .Select(item => (item.SeriesKey, item.LastPlayedDate))
                    .ToArray();
            });
        libraryManager
            .Setup(manager => manager.GetNextUpEpisodesBatch(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns((InternalItemsQuery _, IReadOnlyList<string> keys, bool _, bool _) => keys
                .Where(key => key == "Series50" || (firstBatchHasEligibleEpisode && key == "Series00"))
                .ToDictionary(key => key, key =>
                {
                    var episode = key == "Series00" ? firstEpisode : lastEpisode;
                    return new NextUpEpisodeBatchResult { LastWatched = episode, NextUp = episode };
                }));

        var userDataManager = new Mock<IUserDataManager>();
        userDataManager
            .Setup(manager => manager.GetUserData(user, It.IsAny<BaseItem>()))
            .Returns(new UserItemData { Key = string.Empty, LastPlayedDate = candidateDate });
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var manager = new TVSeriesManager(userDataManager.Object, libraryManager.Object, configurationManager.Object);

        var result = manager.GetNextUp(
            new NextUpQuery { User = user, Limit = 1, EnableTotalRecordCount = false },
            [],
            new DtoOptions());

        Assert.Equal([(null, (string?)null), (candidateDate, "Series49")], requestedCursors);
        Assert.Single(result.Items);
        Assert.Same(firstBatchHasEligibleEpisode ? firstEpisode : lastEpisode, result.Items[0]);
        Assert.Equal(0, result.TotalRecordCount);
    }

    [Fact]
    public void GetNextUp_StopsWhenLaterCandidateDatesCannotOutrankRequestedPage()
    {
        var user = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth");
        var firstCandidateDate = DateTime.UtcNow.AddDays(-1);
        var candidates = Enumerable.Range(0, 51)
            .Select(index => ($"Series{index:D2}", firstCandidateDate.AddMinutes(-index)))
            .ToArray();
        var episodes = candidates.ToDictionary(
            candidate => candidate.Item1,
            candidate => new Episode { Id = Guid.NewGuid(), Name = candidate.Item1 });
        var episodeDates = episodes.ToDictionary(
            entry => entry.Value.Id,
            entry => candidates.Single(candidate => candidate.Item1 == entry.Key).Item2);
        var requestedCursors = new List<(DateTime? Date, string? Key)>();

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetNextUpSeriesKeys(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyCollection<BaseItem>>(),
                It.IsAny<DateTime>()))
            .Returns((InternalItemsQuery query, IReadOnlyCollection<BaseItem> _, DateTime _) =>
            {
                requestedCursors.Add((query.NextUpLastPlayedDateAfter, query.NextUpSeriesKeyAfter));
                var orderedCandidates = candidates.AsEnumerable();
                if (query.NextUpLastPlayedDateAfter.HasValue && query.NextUpSeriesKeyAfter is not null)
                {
                    orderedCandidates = orderedCandidates.Where(item => item.Item2 < query.NextUpLastPlayedDateAfter.Value
                        || (item.Item2 == query.NextUpLastPlayedDateAfter.Value
                            && string.CompareOrdinal(item.Item1, query.NextUpSeriesKeyAfter) > 0));
                }

                return orderedCandidates
                    .Take(query.Limit ?? candidates.Length)
                    .ToArray();
            });
        libraryManager
            .Setup(manager => manager.GetNextUpEpisodesBatch(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns((InternalItemsQuery _, IReadOnlyList<string> keys, bool _, bool _) => keys
                .ToDictionary(key => key, key => new NextUpEpisodeBatchResult
                {
                    LastWatched = episodes[key],
                    NextUp = episodes[key]
                }));

        var userDataManager = new Mock<IUserDataManager>();
        userDataManager
            .Setup(manager => manager.GetUserData(user, It.IsAny<BaseItem>()))
            .Returns((MulletaFlix.Database.Implementations.Entities.User _, BaseItem item) =>
                new UserItemData { Key = string.Empty, LastPlayedDate = episodeDates[item.Id] });
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var manager = new TVSeriesManager(userDataManager.Object, libraryManager.Object, configurationManager.Object);

        var result = manager.GetNextUp(
            new NextUpQuery { User = user, Limit = 1, EnableTotalRecordCount = false },
            [],
            new DtoOptions());

        Assert.Equal([(DateTime?)null], requestedCursors.Select(cursor => cursor.Date));
        Assert.Equal([(string?)null], requestedCursors.Select(cursor => cursor.Key));
        Assert.Same(episodes["Series00"], Assert.Single(result.Items));
    }

    [Fact]
    public void GetNextUp_UsesCursorWhenRowsAreInsertedBeforeThePreviousPage()
    {
        var user = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth");
        var candidateDate = DateTime.UtcNow.AddDays(-1);
        var seriesKeys = Enumerable.Range(0, 51).Select(index => $"Series{index:D2}").ToArray();
        var requestedCursors = new List<(DateTime? Date, string? Key)>();
        var lastEpisode = new Episode { Name = "Episode 50" };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetNextUpSeriesKeys(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyCollection<BaseItem>>(),
                It.IsAny<DateTime>()))
            .Returns((InternalItemsQuery query, IReadOnlyCollection<BaseItem> _, DateTime _) =>
            {
                requestedCursors.Add((query.NextUpLastPlayedDateAfter, query.NextUpSeriesKeyAfter));
                var candidates = seriesKeys.Select(key => (SeriesKey: key, LastPlayedDate: candidateDate)).ToList();
                if (requestedCursors.Count > 1)
                {
                    candidates.Insert(0, ("Aardvark", candidateDate));
                }

                if (query.NextUpLastPlayedDateAfter.HasValue && query.NextUpSeriesKeyAfter is not null)
                {
                    candidates = candidates.Where(item => item.LastPlayedDate < query.NextUpLastPlayedDateAfter.Value
                        || (item.LastPlayedDate == query.NextUpLastPlayedDateAfter.Value
                            && string.CompareOrdinal(item.SeriesKey, query.NextUpSeriesKeyAfter) > 0)).ToList();
                }

                return candidates.Take(query.Limit ?? candidates.Count).ToArray();
            });
        libraryManager
            .Setup(manager => manager.GetNextUpEpisodesBatch(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns((InternalItemsQuery _, IReadOnlyList<string> keys, bool _, bool _) => keys
                .Where(key => key == "Series50")
                .ToDictionary(key => key, _ => new NextUpEpisodeBatchResult { LastWatched = lastEpisode, NextUp = lastEpisode }));

        var userDataManager = new Mock<IUserDataManager>();
        userDataManager.Setup(manager => manager.GetUserData(user, It.IsAny<BaseItem>()))
            .Returns(new UserItemData { Key = string.Empty, LastPlayedDate = candidateDate });
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var manager = new TVSeriesManager(userDataManager.Object, libraryManager.Object, configurationManager.Object);

        var result = manager.GetNextUp(
            new NextUpQuery { User = user, Limit = 1, EnableTotalRecordCount = false },
            [],
            new DtoOptions());

        Assert.Equal([(null, (string?)null), (candidateDate, "Series49")], requestedCursors);
        Assert.Same(lastEpisode, Assert.Single(result.Items));
    }

    [Fact]
    public void GetNextUp_WithExactCount_ResolvesAllCandidatesBeforeApplyingLimit()
    {
        var user = new MulletaFlix.Database.Implementations.Entities.User("testuser", "auth", "auth");
        var lastPlayedDate = DateTime.UtcNow.AddDays(-1);
        var episodes = new Dictionary<string, Episode>
        {
            ["SeriesA"] = new Episode { Name = "Episode A" },
            ["SeriesB"] = new Episode { Name = "Episode B" }
        };
        int? candidateLimit = -1;
        IReadOnlyList<string>? resolvedKeys = null;

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetNextUpSeriesKeys(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyCollection<BaseItem>>(),
                It.IsAny<DateTime>()))
            .Returns((InternalItemsQuery query, IReadOnlyCollection<BaseItem> _, DateTime _) =>
            {
                candidateLimit = query.Limit;
                return episodes.Keys.Select(key => (key, lastPlayedDate)).ToArray();
            });
        libraryManager
            .Setup(manager => manager.GetNextUpEpisodesBatch(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns((InternalItemsQuery _, IReadOnlyList<string> keys, bool _, bool _) =>
            {
                resolvedKeys = keys;
                return keys.ToDictionary(key => key, key => new NextUpEpisodeBatchResult
                {
                    LastWatched = episodes[key],
                    NextUp = episodes[key]
                });
            });

        var userDataManager = new Mock<IUserDataManager>();
        userDataManager
            .Setup(manager => manager.GetUserData(user, It.IsAny<BaseItem>()))
            .Returns(new UserItemData { Key = string.Empty, LastPlayedDate = lastPlayedDate });
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var manager = new TVSeriesManager(userDataManager.Object, libraryManager.Object, configurationManager.Object);

        var result = manager.GetNextUp(
            new NextUpQuery { User = user, Limit = 1, EnableTotalRecordCount = true },
            [],
            new DtoOptions());

        Assert.Null(candidateLimit);
        Assert.Equal(["SeriesA", "SeriesB"], resolvedKeys);
        Assert.Equal(2, result.TotalRecordCount);
        Assert.Single(result.Items);
    }
}
