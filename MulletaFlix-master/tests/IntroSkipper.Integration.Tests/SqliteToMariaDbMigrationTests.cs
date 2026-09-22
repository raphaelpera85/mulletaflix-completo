// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Data;
using IntroSkipper.Db;
using MulletaFlix.Tools.IntroSkipperMigration;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace IntroSkipper.Integration.Tests;

/// <summary>
/// Drives the SQLite → MariaDB migration end to end against a real MariaDB and a real
/// SQLite file built in the test's own directory.
/// <para>
/// This is the only automated proof that the migration works: it exercises the shapes a
/// live installation may hold (with and without <c>AnalyzedItems.FileVersion</c>, with and
/// without <c>SeasonAnalysisOverrides</c>), the range-invariant rejection, idempotency and
/// the projection journaling the plugin needs to publish the migrated segments.
/// </para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SqliteToMariaDbMigrationTests
{
    /// <summary>Schema used only by this fixture, so it cannot touch the plugin's schema.</summary>
    private const string TargetDatabase = "mulletaflix_introskipper_migration_test";

    private const string ConnectionString =
        "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Connection Timeout=5;";

    public SqliteToMariaDbMigrationTests()
    {
        DropTarget();
    }

    [Fact]
    public async Task MigratesEveryTablePreservingIdsAndUserIntent()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();

        var itemId = Guid.NewGuid();
        var (segmentId, tombstoneId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var seasonId = Guid.NewGuid();
        var analyzedItemId = Guid.NewGuid();
        var disabledItemId = Guid.NewGuid();
        var settledEpisodeId = Guid.NewGuid();

        legacy.AddSegment(segmentId, itemId, AnalysisMode.Introduction, 10_000_000, 40_000_000, SegmentSource.Chromaprint);
        legacy.AddSegment(tombstoneId, itemId, AnalysisMode.Credits, 50_000_000, 80_000_000, SegmentSource.User, SegmentState.Suppressed);
        legacy.AddSeasonState(seasonId, AnalysisMode.Introduction, AnalyzerAction.Default, [settledEpisodeId]);
        legacy.AddAnalyzedItem(analyzedItemId, AnalysisMode.Introduction, "cfg-hash", fileVersion: 638_000_000_000_000_000);
        legacy.AddDisabledItem(disabledItemId);
        legacy.AddSeasonAnalysisOverride(seasonId, 25, 12, true);

        var status = await RunAsync(legacy);
        Assert.Equal(0, status);

        using var db = OpenTarget();

        var stored = db.Segments.AsNoTracking().Where(s => s.ItemId == itemId).OrderBy(s => s.StartTicks).ToList();
        Assert.Equal(2, stored.Count);

        // Ids are shared with Jellyfin's MediaSegments rows, so they must survive verbatim.
        Assert.Equal(segmentId, stored[0].Id);
        Assert.Equal(10_000_000, stored[0].StartTicks);
        Assert.Equal(40_000_000, stored[0].EndTicks);
        Assert.Equal(SegmentSource.Chromaprint, stored[0].Source);
        Assert.Equal(SegmentState.Active, stored[0].State);
        Assert.Equal("legacy-hash", stored[0].ConfigHash);
        Assert.Equal(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), stored[0].CreatedAt);

        // A tombstone records a user deletion; losing it lets re-analysis resurrect the segment.
        Assert.Equal(tombstoneId, stored[1].Id);
        Assert.Equal(SegmentState.Suppressed, stored[1].State);
        Assert.Equal(SegmentSource.User, stored[1].Source);

        var state = Assert.Single(db.SeasonStates.AsNoTracking().Where(s => s.SeasonId == seasonId));
        Assert.Equal(AnalysisMode.Introduction, state.Type);
        Assert.Equal([settledEpisodeId], state.SettledReanalysisEpisodeIds);

        var analyzed = Assert.Single(db.AnalyzedItems.AsNoTracking().Where(a => a.ItemId == analyzedItemId));
        Assert.Equal("cfg-hash", analyzed.ConfigHash);
        Assert.Equal(638_000_000_000_000_000, analyzed.FileVersion);

        Assert.True(db.DisabledItems.AsNoTracking().Any(d => d.ItemId == disabledItemId));

        var overrides = Assert.Single(db.SeasonAnalysisOverrides.AsNoTracking().Where(o => o.SeasonId == seasonId));
        Assert.Equal(25, overrides.AnalysisPercent);
        Assert.Equal(12, overrides.AnalysisLengthLimit);
        Assert.True(overrides.PreviewFromCreditsEnd);
    }

    [Fact]
    public async Task MigratesThePreVersioningShape()
    {
        using var legacy = LegacySqliteBuilder.Create();

        // The initial schema: no AnalyzedItems.FileVersion, no SeasonAnalysisOverrides, and
        // DisabledItems still carrying the SeasonId that a later migration dropped.
        legacy.CreateSegmentDatabase(includeFileVersion: false, includeSeasonOverrides: false, includeDisabledSeasonId: true);

        var itemId = Guid.NewGuid();
        var analyzedItemId = Guid.NewGuid();
        legacy.AddSegment(Guid.CreateVersion7(), itemId, AnalysisMode.Introduction, 1, 2, SegmentSource.Chromaprint);
        legacy.AddAnalyzedItem(analyzedItemId, AnalysisMode.Introduction, "old-hash");
        legacy.AddDisabledItem(Guid.NewGuid());

        var status = await RunAsync(legacy);
        Assert.Equal(0, status);

        using var db = OpenTarget();
        Assert.Single(db.Segments.AsNoTracking().Where(s => s.ItemId == itemId));

        // No recorded version means "matches any file", which is the pre-versioning rule.
        Assert.Null(Assert.Single(db.AnalyzedItems.AsNoTracking().Where(a => a.ItemId == analyzedItemId)).FileVersion);
        Assert.Single(db.DisabledItems.AsNoTracking());
    }

    [Fact]
    public async Task SkipsRowsThatViolateTheRangeInvariant()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();

        var goodItem = Guid.NewGuid();
        var badItem = Guid.NewGuid();
        legacy.AddSegment(Guid.CreateVersion7(), goodItem, AnalysisMode.Introduction, 100, 200, SegmentSource.Chromaprint);

        // The FK-free SQLite schema accepted these; the MariaDB schema has CK_Segments_Range.
        legacy.AddSegment(Guid.CreateVersion7(), badItem, AnalysisMode.Introduction, 500, 500, SegmentSource.Chromaprint);
        legacy.AddSegment(Guid.CreateVersion7(), badItem, AnalysisMode.Introduction, 900, 800, SegmentSource.Chromaprint);

        var output = new StringWriter();
        var status = await Migration.RunAsync(OptionsFor(legacy), output);

        // The run must complete: one corrupt row cannot abort the migration.
        Assert.Equal(0, status);
        Assert.Contains("faixa inválida ignorada", output.ToString(), StringComparison.Ordinal);

        using var db = OpenTarget();
        Assert.Single(db.Segments.AsNoTracking().Where(s => s.ItemId == goodItem));
        Assert.Empty(db.Segments.AsNoTracking().Where(s => s.ItemId == badItem));
    }

    [Fact]
    public async Task IsIdempotentAndNeverOverwritesTheTarget()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();

        var itemId = Guid.NewGuid();
        var segmentId = Guid.CreateVersion7();
        legacy.AddSegment(segmentId, itemId, AnalysisMode.Introduction, 10, 20, SegmentSource.Chromaprint);
        legacy.AddAnalyzedItem(itemId, AnalysisMode.Introduction, "legacy-hash");

        Assert.Equal(0, await RunAsync(legacy));

        // A user edits the migrated row after the fact: the second run must not undo it.
        using (var db = OpenTarget())
        {
            var row = db.Segments.Single(s => s.Id == segmentId);
            row.EndTicks = 25;
            db.SaveChanges();
        }

        var output = new StringWriter();
        Assert.Equal(0, await Migration.RunAsync(OptionsFor(legacy), output));

        using (var db = OpenTarget())
        {
            Assert.Single(db.Segments.AsNoTracking().Where(s => s.ItemId == itemId));
            Assert.Equal(25, db.Segments.AsNoTracking().Single(s => s.Id == segmentId).EndTicks);
            Assert.Single(db.AnalyzedItems.AsNoTracking().Where(a => a.ItemId == itemId));
        }

        Assert.Contains("já existente", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task JournalsEveryAffectedItemForProjection()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        legacy.AddSegment(Guid.CreateVersion7(), first, AnalysisMode.Introduction, 10, 20, SegmentSource.Chromaprint);
        legacy.AddSegment(Guid.CreateVersion7(), first, AnalysisMode.Credits, 30, 40, SegmentSource.Chromaprint);
        legacy.AddSegment(Guid.CreateVersion7(), second, AnalysisMode.Introduction, 10, 20, SegmentSource.Chromaprint);

        Assert.Equal(0, await RunAsync(legacy));

        using var db = OpenTarget();
        var queued = db.ProjectionQueue.AsNoTracking().Select(q => q.ItemId).ToList();

        // One marker per item, not per segment: this is what makes the plugin publish the
        // migrated rows to Jellyfin's MediaSegments table.
        Assert.Equal(2, queued.Count);
        Assert.Contains(first, queued);
        Assert.Contains(second, queued);
        Assert.All(db.ProjectionQueue.AsNoTracking().ToList(), q => Assert.Equal(1, q.Version));
    }

    [Fact]
    public async Task MigratesTheDetectionCacheOnlyWhenAskedAndUsesItsOwnContext()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();
        legacy.CreateCacheDatabase();

        var itemId = Guid.NewGuid();
        legacy.AddSegment(Guid.CreateVersion7(), itemId, AnalysisMode.Introduction, 10, 20, SegmentSource.Chromaprint);
        legacy.AddDetectionCache(itemId, AnalysisMode.Introduction, CacheEntryType.Chromaprint, [7, 8, 9], "cache-hash");

        // Without --include-cache the cache file is not even opened.
        Assert.Equal(0, await RunAsync(legacy, includeCache: false));
        using (var cache = OpenCache())
        {
            Assert.Empty(cache.DetectionCache.AsNoTracking());
        }

        var output = new StringWriter();
        Assert.Equal(0, await Migration.RunAsync(OptionsFor(legacy, includeCache: true), output));

        using (var cache = OpenCache())
        {
            var entry = Assert.Single(cache.DetectionCache.AsNoTracking());
            Assert.Equal(itemId, entry.ItemId);
            Assert.Equal(CacheEntryType.Chromaprint, entry.Type);
            Assert.Equal([7, 8, 9], entry.Data);
            Assert.Equal("cache-hash", entry.ConfigHash);
            Assert.Equal(300, entry.End);
        }
    }

    [Fact]
    public async Task DryRunWritesNothing()
    {
        using var legacy = LegacySqliteBuilder.Create();
        legacy.CreateSegmentDatabase();

        legacy.AddSegment(Guid.CreateVersion7(), Guid.NewGuid(), AnalysisMode.Introduction, 10, 20, SegmentSource.Chromaprint);

        var output = new StringWriter();
        var status = await Migration.RunAsync(OptionsFor(legacy, dryRun: true), output);

        Assert.Equal(0, status);
        Assert.Contains("nada foi gravado", output.ToString(), StringComparison.Ordinal);

        // A dry run must not even create the schema.
        Assert.False(SchemaExists(TargetDatabase));
    }

    [Fact]
    public async Task FailsClearlyWhenTheSourceIsMissing()
    {
        using var legacy = LegacySqliteBuilder.Create();
        var options = new Options
        {
            SegmentDatabasePath = legacy.MissingDatabasePath,
            DatabaseName = TargetDatabase,
        };

        var output = new StringWriter();
        var status = await Migration.RunAsync(options, output);

        Assert.Equal(2, status);
        Assert.Contains("arquivo não encontrado", output.ToString(), StringComparison.Ordinal);
    }

    private static Task<int> RunAsync(LegacySqliteBuilder legacy, bool includeCache = false, bool dryRun = false)
        => Migration.RunAsync(OptionsFor(legacy, includeCache, dryRun), new StringWriter());

    private static Options OptionsFor(LegacySqliteBuilder legacy, bool includeCache = false, bool dryRun = false)
        => new()
        {
            SegmentDatabasePath = legacy.SegmentDatabasePath,
            CacheDatabasePath = includeCache ? legacy.CacheDatabasePath : null,
            IncludeCache = includeCache,
            DatabaseName = TargetDatabase,
            DryRun = dryRun,
        };

    private static IntroSkipperDbContext OpenTarget()
    {
        var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<IntroSkipperDbContext>();
        builder.UseMySql(
            $"Server=127.0.0.1;Port=3306;User ID=root;Password=;Database={TargetDatabase};CharSet=utf8mb4;SslMode=None;",
            new MariaDbServerVersion(new Version(11, 4, 2)));
        return new IntroSkipperDbContext(builder.Options);
    }

    private static DetectionCacheDbContext OpenCache()
    {
        var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DetectionCacheDbContext>();
        builder.UseMySql(
            $"Server=127.0.0.1;Port=3306;User ID=root;Password=;Database={TargetDatabase};CharSet=utf8mb4;SslMode=None;",
            new MariaDbServerVersion(new Version(11, 4, 2)));
        return new DetectionCacheDbContext(builder.Options);
    }

    private static void DropTarget()
    {
        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{TargetDatabase}`;";
        command.ExecuteNonQuery();
    }

    private static bool SchemaExists(string databaseName)
    {
        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @name;";
        command.Parameters.AddWithValue("@name", databaseName);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }
}
