using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Helpers;
using MulletaFlix.Server.Implementations.Item;
using MySqlConnector;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public class MariaDbProcessManagerLifecycleTests
{
    private readonly ITestOutputHelper _output;

    public MariaDbProcessManagerLifecycleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task StartStopStart_UsesTheSameIsolatedDataDirectory()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The embedded MariaDB runtime currently ships as a Windows portable package.");

        var runtimeDirectory = Path.Combine(Path.GetDirectoryName(typeof(MariaDbProcessManager).Assembly.Location)!, "mariadb", "bin");
        Assert.SkipUnless(
            File.Exists(Path.Combine(runtimeDirectory, "mysqld.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysql_install_db.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysqladmin.exe")),
            "Stage the portable MariaDB runtime beside the test assembly to run this lifecycle test.");

        var previousPort = Environment.GetEnvironmentVariable("MULLETAFLIX_DB_PORT");
        var previousDatabase = Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME");
        var port = GetFreePort();
        Assert.NotEqual(3306, port);
        var dataPath = Path.Combine(Path.GetTempPath(), $"MfxMariaDbLifecycle-{Guid.NewGuid():N}");
        var databaseName = $"mfx_lifecycle_{Guid.NewGuid():N}";
        var paths = new ServerApplicationPaths(
            dataPath,
            Path.Combine(dataPath, "logs"),
            Path.Combine(dataPath, "config"),
            Path.Combine(dataPath, "cache"),
            Path.Combine(dataPath, "web"));

        Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", port.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", databaseName);
        var markerTable = $"lifecycle_{Guid.NewGuid():N}";
        try
        {
            await MariaDbProcessManager.StartMariaDbAsync(paths, NullLogger.Instance, requireEmbeddedProcess: true, TestContext.Current.CancellationToken);
            await ExecuteAsync(port, $"CREATE TABLE `{databaseName}`.`{markerTable}` (id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(port, $"INSERT INTO `{databaseName}`.`{markerTable}` (id) VALUES (1)");
            await MariaDbProcessManager.StopMariaDbAsync(NullLogger.Instance, TestContext.Current.CancellationToken);
            await MariaDbProcessManager.StartMariaDbAsync(paths, NullLogger.Instance, requireEmbeddedProcess: true, TestContext.Current.CancellationToken);
            Assert.Equal(1, await ScalarAsync(port, $"SELECT COUNT(*) FROM `{databaseName}`.`{markerTable}`"));
            await ExecuteAsync(port, $"DROP TABLE `{databaseName}`.`{markerTable}`");
            await MariaDbProcessManager.StopMariaDbAsync(NullLogger.Instance, TestContext.Current.CancellationToken);
        }
        finally
        {
            try
            {
                await MariaDbProcessManager.StopMariaDbAsync(NullLogger.Instance, CancellationToken.None);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", previousPort);
                Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", previousDatabase);
                if (Directory.Exists(dataPath))
                {
                    Directory.Delete(dataPath, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The embedded MariaDB runtime currently ships as a Windows portable package.");

        var runtimeDirectory = Path.Combine(Path.GetDirectoryName(typeof(MariaDbProcessManager).Assembly.Location)!, "mariadb", "bin");
        Assert.SkipUnless(
            File.Exists(Path.Combine(runtimeDirectory, "mysqld.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysql_install_db.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysqladmin.exe")),
            "Stage the portable MariaDB runtime beside the test assembly to run this database test.");

        var previousPort = Environment.GetEnvironmentVariable("MULLETAFLIX_DB_PORT");
        var previousDatabase = Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME");
        var port = GetFreePort();
        Assert.NotEqual(3306, port);
        var dataPath = Path.Combine(Path.GetTempPath(), $"MfxNextUpMariaDb-{Guid.NewGuid():N}");
        var paths = new ServerApplicationPaths(
            dataPath,
            Path.Combine(dataPath, "logs"),
            Path.Combine(dataPath, "config"),
            Path.Combine(dataPath, "cache"),
            Path.Combine(dataPath, "web"));
        Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", port.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", "mfx_nextup_runtime_test");

        try
        {
            await MariaDbProcessManager.StartMariaDbAsync(
                paths,
                NullLogger.Instance,
                requireEmbeddedProcess: true,
                TestContext.Current.CancellationToken);

            var provider = new MySqlDatabaseProvider(NullLogger<MySqlDatabaseProvider>.Instance);
            var executedCommands = new List<string>();
            var optionsBuilder = new DbContextOptionsBuilder<MulletaFlixDbContext>();
            optionsBuilder.LogTo(
                message => executedCommands.Add(message),
                [RelationalEventId.CommandExecuted],
                LogLevel.Information);
            var rankedQueryCapture = new RankedQueryCaptureInterceptor();
            provider.Initialise(optionsBuilder, new DatabaseConfigurationOptions
            {
                DatabaseType = "MulletaFlix-MySQL",
                LockingBehavior = DatabaseLockingBehaviorTypes.NoLock,
                CustomProviderOptions = new CustomDatabaseOptions
                {
                    PluginName = string.Empty,
                    PluginAssembly = string.Empty,
                    ConnectionString = string.Empty,
                    Options =
                    [
                        new CustomDatabaseOption { Key = "server", Value = "127.0.0.1" },
                        new CustomDatabaseOption { Key = "port", Value = port.ToString(CultureInfo.InvariantCulture) },
                        new CustomDatabaseOption { Key = "user", Value = "root" },
                        new CustomDatabaseOption { Key = "password", Value = string.Empty },
                        new CustomDatabaseOption { Key = "database", Value = "mulletaflix" },
                    ],
                },
            });
            optionsBuilder.AddInterceptors(rankedQueryCapture);
            var options = optionsBuilder.Options;
            var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
            lockingBehavior
                .Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, save) => save());

            MulletaFlixDbContext CreateContext() => new(
                options,
                NullLogger<MulletaFlixDbContext>.Instance,
                provider,
                lockingBehavior.Object);

            var userId = Guid.NewGuid();
            const string seriesKey = "MariaDbNextUpSeries";
            var user = new MulletaFlix.Database.Implementations.Entities.User("nextup-test", "auth", "auth")
            {
                Id = userId
            };
            var tiedLastPlayedDate = DateTime.UtcNow;
            var watched = CreateEpisode(seriesKey, 1, 1);
            var beforeLastWatched = CreateEpisode(seriesKey, 1, 2);
            var lastWatched = CreateEpisode(seriesKey, 1, 3);
            var expectedNext = CreateEpisode(seriesKey, 1, 4);
            var later = CreateEpisode(seriesKey, 1, 5);
            var virtualEpisode = CreateEpisode(seriesKey, 1, 6);
            virtualEpisode.IsVirtualItem = true;
            var special = CreateEpisode(seriesKey, 0, 1);
            const string unplayedSeriesKey = "MariaDbNextUpUnplayedSeries";
            var firstUnplayed = CreateEpisode(unplayedSeriesKey, 1, 1);
            var laterUnplayed = CreateEpisode(unplayedSeriesKey, 1, 2);
            const string nullAfterWatchedSeriesKey = "MariaDbNextUpNullAfterWatchedSeries";
            var nullAfterWatched = CreateEpisode(nullAfterWatchedSeriesKey, null, null);
            var watchedBeforeNull = CreateEpisode(nullAfterWatchedSeriesKey, 1, 1);
            const string partialLatestSeriesKey = "MariaDbNextUpPartialLatestSeries";
            var partialLatestNullCandidate = CreateEpisode(partialLatestSeriesKey, null, null);
            var partialLatestEarlierWatched = CreateEpisode(partialLatestSeriesKey, 1, 1);
            var partialLatestWatched = CreateEpisode(partialLatestSeriesKey, 2, null);
            const string nullFirstSeriesKey = "MariaDbNextUpNullFirstSeries";
            var nullFirst = CreateEpisode(nullFirstSeriesKey, null, null);
            var numberedAfterNull = CreateEpisode(nullFirstSeriesKey, 1, 1);
            const string nullUnplayedSeriesKey = "MariaDbNextUpNullUnplayedCandidateSeries";
            var watchedBeforeNullCandidate = CreateEpisode(nullUnplayedSeriesKey, 1, 5);
            var nullUnplayedCandidate = CreateEpisode(nullUnplayedSeriesKey, null, null);
            var numberedAfterNullCandidate = CreateEpisode(nullUnplayedSeriesKey, 1, 6);
            using (var seed = CreateContext())
            {
                seed.Database.EnsureCreated();
                seed.Users.Add(user);
                seed.BaseItems.AddRange(
                    watched,
                    beforeLastWatched,
                    lastWatched,
                    expectedNext,
                    later,
                    virtualEpisode,
                    special,
                    firstUnplayed,
                    laterUnplayed,
                    nullAfterWatched,
                    watchedBeforeNull,
                    partialLatestNullCandidate,
                    partialLatestEarlierWatched,
                    partialLatestWatched,
                    nullFirst,
                    numberedAfterNull,
                    watchedBeforeNullCandidate,
                    nullUnplayedCandidate,
                    numberedAfterNullCandidate);
                seed.UserData.AddRange(
                    new UserData
                    {
                        UserId = userId,
                        ItemId = watched.Id,
                        Item = watched,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate,
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = lastWatched.Id,
                        Item = lastWatched,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate,
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = watchedBeforeNull.Id,
                        Item = watchedBeforeNull,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = DateTime.UtcNow.AddMinutes(-2),
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = partialLatestEarlierWatched.Id,
                        Item = partialLatestEarlierWatched,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = DateTime.UtcNow.AddMinutes(-3),
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = partialLatestWatched.Id,
                        Item = partialLatestWatched,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = DateTime.UtcNow.AddMinutes(-4),
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = watchedBeforeNullCandidate.Id,
                        Item = watchedBeforeNullCandidate,
                        User = user,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = DateTime.UtcNow.AddMinutes(-5),
                        Played = true
                    });
                seed.SaveChanges();
            }

            var contextFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
            contextFactory.Setup(factory => factory.CreateDbContext()).Returns(CreateContext);
            var itemTypeLookup = new Mock<IItemTypeLookup>();
            itemTypeLookup.SetupGet(lookup => lookup.BaseItemKindNames)
                .Returns(new Dictionary<BaseItemKind, string> { [BaseItemKind.Episode] = "Episode" });
            var queryHelpers = new Mock<IItemQueryHelpers>();
            queryHelpers
                .Setup(helpers => helpers.ApplyAccessFiltering(
                    It.IsAny<MulletaFlixDbContext>(),
                    It.IsAny<IQueryable<BaseItemEntity>>(),
                    It.IsAny<InternalItemsQuery>()))
                .Returns<MulletaFlixDbContext, IQueryable<BaseItemEntity>, InternalItemsQuery>((_, query, _) => query);
            queryHelpers
                .Setup(helpers => helpers.ApplyNavigations(It.IsAny<IQueryable<BaseItemEntity>>(), It.IsAny<InternalItemsQuery>()))
                .Returns<IQueryable<BaseItemEntity>, InternalItemsQuery>((query, _) => query);
            var deserializedIds = new List<Guid>();
            queryHelpers
                .Setup(helpers => helpers.DeserializeBaseItem(It.IsAny<BaseItemEntity>(), It.IsAny<bool>()))
                .Callback<BaseItemEntity, bool>((item, _) => deserializedIds.Add(item.Id))
                .Returns((BaseItemEntity item, bool _) => new Episode { Id = item.Id });

            var service = new NextUpService(contextFactory.Object, itemTypeLookup.Object, queryHelpers.Object);
            var result = service.GetNextUpEpisodesBatch(
                new InternalItemsQuery { User = new User("nextup-test", "auth", "auth") { Id = userId } },
                [seriesKey, unplayedSeriesKey, nullAfterWatchedSeriesKey, partialLatestSeriesKey, nullFirstSeriesKey, nullUnplayedSeriesKey],
                includeSpecials: true,
                includeWatchedForRewatching: false);

            Assert.Contains(expectedNext.Id, deserializedIds);
            Assert.DoesNotContain(beforeLastWatched.Id, deserializedIds);
            Assert.DoesNotContain(later.Id, deserializedIds);
            Assert.DoesNotContain(virtualEpisode.Id, deserializedIds);
            Assert.Contains(special.Id, deserializedIds);
            Assert.Contains(firstUnplayed.Id, deserializedIds);
            Assert.DoesNotContain(laterUnplayed.Id, deserializedIds);
            Assert.DoesNotContain(nullAfterWatched.Id, deserializedIds);
            Assert.Contains(partialLatestNullCandidate.Id, deserializedIds);
            Assert.DoesNotContain(partialLatestEarlierWatched.Id, deserializedIds);
            Assert.Contains(nullFirst.Id, deserializedIds);
            Assert.DoesNotContain(numberedAfterNull.Id, deserializedIds);
            Assert.Equal(numberedAfterNullCandidate.Id, result[nullUnplayedSeriesKey].NextUp?.Id);
            Assert.Contains(seriesKey, result.Keys);
            Assert.Contains(unplayedSeriesKey, result.Keys);
            Assert.Contains(nullAfterWatchedSeriesKey, result.Keys);
            Assert.Contains(partialLatestSeriesKey, result.Keys);
            Assert.Contains(nullFirstSeriesKey, result.Keys);
            Assert.Equal(expectedNext.Id, result[seriesKey].NextUp?.Id);
            Assert.Equal(lastWatched.Id, result[seriesKey].LastWatched?.Id);
            Assert.Equal(firstUnplayed.Id, result[unplayedSeriesKey].NextUp?.Id);
            Assert.Equal(partialLatestNullCandidate.Id, result[partialLatestSeriesKey].NextUp?.Id);
            var rankedSelections = executedCommands
                .Where(command => command.Contains("ROW_NUMBER() OVER", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.True(rankedSelections.Length >= 2, "Expected SQL ranking for both latest position and next unplayed episode.");
            Assert.Contains(rankedSelections, command => command.Contains("<= 1", StringComparison.Ordinal));
            var capturedRankedQuery = Assert.IsType<CapturedQuery>(rankedQueryCapture.Query);
            var actualPlan = await AnalyzeQueryAsync(port, capturedRankedQuery);
            Assert.Contains("r_rows", actualPlan, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BaseItems", actualPlan, StringComparison.OrdinalIgnoreCase);
            using var planDocument = JsonDocument.Parse(actualPlan);
            var tableMetrics = EnumerateTableMetrics(planDocument.RootElement).ToArray();
            var observedRows = tableMetrics
                .Where(table => table.TryGetProperty("r_rows", out var rows) && rows.ValueKind == JsonValueKind.Number)
                .Select(table => table.GetProperty("r_rows").GetDouble())
                .ToArray();
            Assert.Contains(19d, observedRows);
            Assert.Contains(5d, observedRows);
            var queryDurationMs = planDocument.RootElement
                .GetProperty("query_block")
                .GetProperty("r_total_time_ms")
                .GetDouble();
            _output.WriteLine(
                "NextUp MariaDB ANALYZE: observed row counts={0}, query time={1:F3} ms (6 series, 19 episode rows seeded).",
                string.Join(", ", observedRows.Select(rows => rows.ToString(CultureInfo.InvariantCulture))),
                queryDurationMs);

            var duplicatePositionEpisode = CreateEpisode(seriesKey, 1, 3);
            duplicatePositionEpisode.Id = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
            const string nullDateSeriesKey = "MariaDbNextUpNullDateTieSeries";
            var nullDatedLaterPosition = CreateEpisode(nullDateSeriesKey, 1, 9);
            var nonNullDatedEarlierPosition = CreateEpisode(nullDateSeriesKey, 1, 1);
            const string nullPositionSeriesKey = "MariaDbNextUpNullPositionTieSeries";
            var nullPositionEpisode = CreateEpisode(nullPositionSeriesKey, null, null);
            var positionedEpisode = CreateEpisode(nullPositionSeriesKey, 1, 1);
            using (var tieSeed = CreateContext())
            {
                var tieUser = tieSeed.Users.Single(existingUser => existingUser.Id == userId);
                var laterItem = tieSeed.BaseItems.Single(existingItem => existingItem.Id == later.Id);
                tieSeed.BaseItems.AddRange(
                    duplicatePositionEpisode,
                    nullDatedLaterPosition,
                    nonNullDatedEarlierPosition,
                    nullPositionEpisode,
                    positionedEpisode);
                tieSeed.UserData.AddRange(
                    new UserData
                    {
                        UserId = userId,
                        ItemId = later.Id,
                        Item = laterItem,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate.AddMinutes(-1),
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = duplicatePositionEpisode.Id,
                        Item = duplicatePositionEpisode,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate,
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = nullDatedLaterPosition.Id,
                        Item = nullDatedLaterPosition,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = null,
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = nonNullDatedEarlierPosition.Id,
                        Item = nonNullDatedEarlierPosition,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate.AddMinutes(-1),
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = nullPositionEpisode.Id,
                        Item = nullPositionEpisode,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate,
                        Played = true
                    },
                    new UserData
                    {
                        UserId = userId,
                        ItemId = positionedEpisode.Id,
                        Item = positionedEpisode,
                        User = tieUser,
                        CustomDataKey = string.Empty,
                        LastPlayedDate = tiedLastPlayedDate,
                        Played = true
                    });
                tieSeed.SaveChanges();
            }

            rankedQueryCapture.Reset();
            var rewatchResult = service.GetNextUpEpisodesBatch(
                new InternalItemsQuery { User = new User("nextup-test", "auth", "auth") { Id = userId } },
                [seriesKey, nullDateSeriesKey, nullPositionSeriesKey],
                includeSpecials: false,
                includeWatchedForRewatching: true);
            Assert.Equal(duplicatePositionEpisode.Id, rewatchResult[seriesKey].LastWatchedForRewatching?.Id);
            Assert.Equal(later.Id, rewatchResult[seriesKey].NextPlayedForRewatching?.Id);
            Assert.Equal(nonNullDatedEarlierPosition.Id, rewatchResult[nullDateSeriesKey].LastWatchedForRewatching?.Id);
            Assert.Equal(positionedEpisode.Id, rewatchResult[nullPositionSeriesKey].LastWatchedForRewatching?.Id);

            var capturedRewatchQuery = Assert.IsType<CapturedQuery>(rankedQueryCapture.Query);
            Assert.Contains("LastPlayedDate", capturedRewatchQuery.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ROW_NUMBER() OVER", capturedRewatchQuery.CommandText, StringComparison.OrdinalIgnoreCase);
            var rewatchPlan = await AnalyzeQueryAsync(port, capturedRewatchQuery);
            using var rewatchPlanDocument = JsonDocument.Parse(rewatchPlan);
            Assert.True(
                rewatchPlanDocument.RootElement.TryGetProperty("query_block", out var rewatchQueryBlock),
                "Expected ANALYZE JSON to contain query_block for the final ranked rewatch query.");
            var rewatchTableMetrics = EnumerateTableMetrics(rewatchQueryBlock).ToArray();
            Assert.NotEmpty(rewatchTableMetrics);
            var rewatchObservedRows = rewatchTableMetrics
                .Where(table => table.TryGetProperty("r_rows", out var rows) && rows.ValueKind == JsonValueKind.Number)
                .Select(table => table.GetProperty("r_rows").GetDouble())
                .ToArray();
            Assert.NotEmpty(rewatchObservedRows);
            var rewatchQueryDurationMs = rewatchQueryBlock.GetProperty("r_total_time_ms").GetDouble();
            _output.WriteLine(
                "NextUp rewatch MariaDB ANALYZE: observed row counts={0}, query time={1:F3} ms.",
                string.Join(", ", rewatchObservedRows.Select(rows => rows.ToString(CultureInfo.InvariantCulture))),
                rewatchQueryDurationMs);
        }
        finally
        {
            try
            {
                await MariaDbProcessManager.StopMariaDbAsync(NullLogger.Instance, CancellationToken.None);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", previousPort);
                Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", previousDatabase);
                if (Directory.Exists(dataPath))
                {
                    Directory.Delete(dataPath, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task GetLatestMedia_MariaDbMeasuresDuplicateMovieAndSeriesScan()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The embedded MariaDB runtime currently ships as a Windows portable package.");

        var runtimeDirectory = Path.Combine(Path.GetDirectoryName(typeof(MariaDbProcessManager).Assembly.Location)!, "mariadb", "bin");
        Assert.SkipUnless(
            File.Exists(Path.Combine(runtimeDirectory, "mysqld.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysql_install_db.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "mysqladmin.exe")),
            "Stage the portable MariaDB runtime beside the test assembly to run this query measurement.");

        const int distinctMovies = 100;
        const int versionsPerMovie = 100;
        const int requestedLimit = 20;
        const int batchSize = 500;
        var port = GetFreePort();
        Assert.NotEqual(3306, port);
        var previousPort = Environment.GetEnvironmentVariable("MULLETAFLIX_DB_PORT");
        var previousDatabase = Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME");
        var dataPath = Path.Combine(Path.GetTempPath(), $"MfxLatestMediaMariaDb-{Guid.NewGuid():N}");
        var paths = new ServerApplicationPaths(
            dataPath,
            Path.Combine(dataPath, "logs"),
            Path.Combine(dataPath, "config"),
            Path.Combine(dataPath, "cache"),
            Path.Combine(dataPath, "web"));
        Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", port.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", "mfx_latestmedia_measurement");

        try
        {
            await MariaDbProcessManager.StartMariaDbAsync(
                paths,
                NullLogger.Instance,
                requireEmbeddedProcess: true,
                TestContext.Current.CancellationToken);

            var provider = new MySqlDatabaseProvider(NullLogger<MySqlDatabaseProvider>.Instance);
            var queryCapture = new LatestMovieQueryCaptureInterceptor();
            var optionsBuilder = new DbContextOptionsBuilder<MulletaFlixDbContext>();
            provider.Initialise(optionsBuilder, new DatabaseConfigurationOptions
            {
                DatabaseType = "MulletaFlix-MySQL",
                LockingBehavior = DatabaseLockingBehaviorTypes.NoLock,
                CustomProviderOptions = new CustomDatabaseOptions
                {
                    PluginName = string.Empty,
                    PluginAssembly = string.Empty,
                    ConnectionString = string.Empty,
                    Options =
                    [
                        new CustomDatabaseOption { Key = "server", Value = "127.0.0.1" },
                        new CustomDatabaseOption { Key = "port", Value = port.ToString(CultureInfo.InvariantCulture) },
                        new CustomDatabaseOption { Key = "user", Value = "root" },
                        new CustomDatabaseOption { Key = "password", Value = string.Empty },
                        new CustomDatabaseOption { Key = "database", Value = "mulletaflix" },
                    ],
                },
            });
            optionsBuilder.AddInterceptors(queryCapture);
            var options = optionsBuilder.Options;
            var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
            lockingBehavior
                .Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, save) => save());
            MulletaFlixDbContext CreateContext() => new(
                options,
                NullLogger<MulletaFlixDbContext>.Instance,
                provider,
                lockingBehavior.Object);

            using (var seed = CreateContext())
            {
                seed.Database.EnsureCreated();
                var batch = new List<BaseItemEntity>(batchSize);
                var baseDate = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
                for (var version = 0; version < versionsPerMovie; version++)
                {
                    for (var movie = 0; movie < distinctMovies; movie++)
                    {
                        batch.Add(new BaseItemEntity
                        {
                            Id = Guid.NewGuid(),
                            Type = typeof(Movie).FullName!,
                            Name = $"Synthetic movie {movie:D3} version {version:D3}",
                            PresentationUniqueKey = $"SyntheticMovie{movie:D3}",
                            DateCreated = baseDate.AddTicks(-(((long)movie * versionsPerMovie) + version)),
                            IsFolder = false
                        });

                        if (batch.Count == batchSize)
                        {
                            seed.BaseItems.AddRange(batch);
                            seed.SaveChanges();
                            seed.ChangeTracker.Clear();
                            batch.Clear();
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    seed.BaseItems.AddRange(batch);
                    seed.SaveChanges();
                }
            }

            var contexts = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
            contexts.Setup(factory => factory.CreateDbContext()).Returns(CreateContext);
            var itemTypeLookup = new Mock<IItemTypeLookup>();
            itemTypeLookup.SetupGet(lookup => lookup.BaseItemKindNames)
                .Returns(new Dictionary<BaseItemKind, string>
                {
                    [BaseItemKind.Movie] = typeof(Movie).FullName!,
                    [BaseItemKind.Episode] = typeof(Episode).FullName!,
                    [BaseItemKind.Series] = typeof(Series).FullName!
                });
            var repository = new BaseItemRepository(
                contexts.Object,
                Mock.Of<IServerApplicationHost>(),
                itemTypeLookup.Object,
                Mock.Of<IServerConfigurationManager>(manager => manager.Configuration == new ServerConfiguration()),
                NullLogger<BaseItemRepository>.Instance);

            IReadOnlyList<BaseItem> result = [];
            var elapsed = new List<double>(3);
            for (var sample = 0; sample < 3; sample++)
            {
                var timer = Stopwatch.StartNew();
                result = repository.GetLatestItemList(
                    new InternalItemsQuery
                    {
                        IncludeItemTypes = [BaseItemKind.Movie],
                        Limit = requestedLimit,
                        SkipDeserialization = true
                    },
                    CollectionType.movies);
                timer.Stop();
                elapsed.Add(timer.Elapsed.TotalMilliseconds);
            }

            Assert.Equal(requestedLimit, result.Count);
            Assert.NotNull(queryCapture.Query);
            var plan = await AnalyzeQueryAsync(port, queryCapture.Query!);
            using var planDocument = JsonDocument.Parse(plan);
            var queryBlock = planDocument.RootElement.GetProperty("query_block");
            var observedRows = EnumerateTableMetrics(queryBlock)
                .Where(table => table.TryGetProperty("r_rows", out var rows) && rows.ValueKind == JsonValueKind.Number)
                .Select(table => table.GetProperty("r_rows").GetDouble())
                .ToArray();
            Assert.NotEmpty(observedRows);
            Assert.Contains(observedRows, rows => rows >= 9_000d);
            var analyzeMs = queryBlock.GetProperty("r_total_time_ms").GetDouble();
            _output.WriteLine(
                "LatestMedia movies MariaDB measurement: seeded={0} versions ({1} keys x {2} versions), limit={3}, observed SQL rows={4}, ANALYZE full-query={5:F3} ms, endpoint wall samples=[{6}] ms. ANALYZE consumes the full ordered candidate query; endpoint timings stop after enough distinct keys and are host-specific.",
                checked(distinctMovies * versionsPerMovie),
                distinctMovies,
                versionsPerMovie,
                requestedLimit,
                string.Join(", ", observedRows.Select(rows => rows.ToString(CultureInfo.InvariantCulture))),
                analyzeMs,
                string.Join(", ", elapsed.Select(milliseconds => milliseconds.ToString("F3", CultureInfo.InvariantCulture))));

            const int distinctSeries = 100;
            const int episodesPerSeries = 100;
            queryCapture.Reset();
            using (var seed = CreateContext())
            {
                var batch = new List<BaseItemEntity>(batchSize);
                var seriesIds = Enumerable.Range(0, distinctSeries).Select(_ => Guid.NewGuid()).ToArray();
                for (var series = 0; series < distinctSeries; series++)
                {
                    batch.Add(new BaseItemEntity
                    {
                        Id = seriesIds[series],
                        Type = typeof(Series).FullName!,
                        Name = $"Synthetic series {series:D3}",
                        IsFolder = true,
                        IsSeries = true
                    });
                }

                for (var series = 0; series < distinctSeries; series++)
                {
                    for (var episode = 0; episode < episodesPerSeries; episode++)
                    {
                        batch.Add(new BaseItemEntity
                        {
                            Id = Guid.NewGuid(),
                            Type = typeof(Episode).FullName!,
                            Name = $"Synthetic series {series:D3} episode {episode:D3}",
                            SeriesId = seriesIds[series],
                            DateCreated = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)
                                .AddTicks(-(((long)series * episodesPerSeries) + episode)),
                            IsFolder = false
                        });

                        if (batch.Count >= batchSize)
                        {
                            seed.BaseItems.AddRange(batch);
                            seed.SaveChanges();
                            seed.ChangeTracker.Clear();
                            batch.Clear();
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    seed.BaseItems.AddRange(batch);
                    seed.SaveChanges();
                }
            }

            IReadOnlyList<BaseItem> seriesResult = [];
            var seriesElapsed = new List<double>(3);
            for (var sample = 0; sample < 3; sample++)
            {
                var timer = Stopwatch.StartNew();
                seriesResult = repository.GetLatestItemList(
                    new InternalItemsQuery
                    {
                        IncludeItemTypes = [BaseItemKind.Episode],
                        Limit = requestedLimit,
                        SkipDeserialization = true
                    },
                    CollectionType.tvshows);
                timer.Stop();
                seriesElapsed.Add(timer.Elapsed.TotalMilliseconds);
            }

            Assert.Equal(requestedLimit, seriesResult.Count);
            Assert.NotNull(queryCapture.Query);
            var seriesPlan = await AnalyzeQueryAsync(port, queryCapture.Query!);
            using var seriesPlanDocument = JsonDocument.Parse(seriesPlan);
            var seriesQueryBlock = seriesPlanDocument.RootElement.GetProperty("query_block");
            var seriesObservedRows = EnumerateTableMetrics(seriesQueryBlock)
                .Where(table => table.TryGetProperty("r_rows", out var rows) && rows.ValueKind == JsonValueKind.Number)
                .Select(table => table.GetProperty("r_rows").GetDouble())
                .ToArray();
            Assert.NotEmpty(seriesObservedRows);
            Assert.Contains(seriesObservedRows, rows => rows >= 18_000d);
            var seriesAnalyzeMs = seriesQueryBlock.GetProperty("r_total_time_ms").GetDouble();
            _output.WriteLine(
                "LatestMedia series MariaDB measurement: seeded={0} episodes ({1} series x {2} episodes), limit={3}, observed SQL rows={4}, ANALYZE full-query={5:F3} ms, endpoint wall samples=[{6}] ms. ANALYZE consumes the full ordered candidate query; endpoint timings stop after enough distinct series and are host-specific.",
                distinctSeries * episodesPerSeries,
                distinctSeries,
                episodesPerSeries,
                requestedLimit,
                string.Join(", ", seriesObservedRows.Select(rows => rows.ToString(CultureInfo.InvariantCulture))),
                seriesAnalyzeMs,
                string.Join(", ", seriesElapsed.Select(milliseconds => milliseconds.ToString("F3", CultureInfo.InvariantCulture))));
        }
        finally
        {
            try
            {
                await MariaDbProcessManager.StopMariaDbAsync(NullLogger.Instance, CancellationToken.None);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_DB_PORT", previousPort);
                Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", previousDatabase);
                if (Directory.Exists(dataPath))
                {
                    Directory.Delete(dataPath, recursive: true);
                }
            }
        }
    }

    private static BaseItemEntity CreateEpisode(string seriesKey, int? season, int? episode)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "Episode",
            SeriesPresentationUniqueKey = seriesKey,
            ParentIndexNumber = season,
            IndexNumber = episode
        };

    private static async Task<string> AnalyzeQueryAsync(int port, CapturedQuery query)
    {
        await using var connection = new MySqlConnection($"Server=127.0.0.1;Port={port};User ID=root;Password=;Database=mulletaflix;SslMode=None;Connection Timeout=2;");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"ANALYZE FORMAT=JSON {query.CommandText}";
        foreach (var captured in query.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = captured.Name;
            parameter.DbType = captured.DbType;
            parameter.Value = captured.Value;
            command.Parameters.Add(parameter);
        }

        return Convert.ToString(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            CultureInfo.InvariantCulture)!;
    }

    private static IEnumerable<JsonElement> EnumerateTableMetrics(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("table_name", out _))
            {
                yield return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var table in EnumerateTableMetrics(property.Value))
                {
                    yield return table;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var table in EnumerateTableMetrics(item))
                {
                    yield return table;
                }
            }
        }
    }

    private static async Task ExecuteAsync(int port, string sql)
    {
        await using var connection = new MySqlConnection($"Server=127.0.0.1;Port={port};User ID=root;Password=;SslMode=None;Connection Timeout=2;");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> ScalarAsync(int port, string sql)
    {
        await using var connection = new MySqlConnection($"Server=127.0.0.1;Port={port};User ID=root;Password=;SslMode=None;Connection Timeout=2;");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record CapturedParameter(string Name, System.Data.DbType DbType, object Value);

    private sealed record CapturedQuery(string CommandText, IReadOnlyList<CapturedParameter> Parameters);

    private sealed class RankedQueryCaptureInterceptor : DbCommandInterceptor
    {
        public CapturedQuery? Query { get; private set; }

        public void Reset() => Query = null;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("ROW_NUMBER() OVER", StringComparison.OrdinalIgnoreCase))
            {
                Query = new CapturedQuery(
                    command.CommandText,
                    command.Parameters
                        .Cast<DbParameter>()
                        .Select(parameter => new CapturedParameter(
                            parameter.ParameterName,
                            parameter.DbType,
                            parameter.Value))
                        .ToArray());
            }

            return result;
        }
    }

    private sealed class LatestMovieQueryCaptureInterceptor : DbCommandInterceptor
    {
        public CapturedQuery? Query { get; private set; }

        public void Reset() => Query = null;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            if (Query is null
                && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && (command.CommandText.Contains("PresentationUniqueKey", StringComparison.OrdinalIgnoreCase)
                    || command.CommandText.Contains("SeriesId", StringComparison.OrdinalIgnoreCase))
                && command.CommandText.Contains("DateCreated", StringComparison.OrdinalIgnoreCase)
                && !command.CommandText.Contains(" IN (", StringComparison.OrdinalIgnoreCase))
            {
                Query = new CapturedQuery(
                    command.CommandText,
                    command.Parameters
                        .Cast<DbParameter>()
                        .Select(parameter => new CapturedParameter(
                            parameter.ParameterName,
                            parameter.DbType,
                            parameter.Value ?? DBNull.Value))
                        .ToArray());
            }

            return result;
        }
    }
}
