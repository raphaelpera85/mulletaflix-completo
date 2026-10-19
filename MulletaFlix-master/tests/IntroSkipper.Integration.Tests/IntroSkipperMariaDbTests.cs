// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Data;
using IntroSkipper.SegmentChanges;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MulletaFlix.Testing;

namespace IntroSkipper.Integration.Tests;

/// <summary>
/// Serializes the MariaDB tests: they share one plugin schema, and several operations
/// under test (mode-wide erase, staleness cleanup) are deliberately instance-wide.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MariaDbCollection
{
    /// <summary>Collection name.</summary>
    public const string Name = "MariaDB";
}

/// <summary>
/// Proves the IntroSkipper facade against MariaDB. The plugin no longer has a private
/// database file, so the dedicated schema and every translated statement are the only
/// contract, and none of it is checked by a compiler.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class IntroSkipperMariaDbTests : IClassFixture<MariaDbFixture>
{
    private readonly MariaDbFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroSkipperMariaDbTests"/> class.
    /// </summary>
    /// <param name="fixture">MariaDB fixture.</param>
    public IntroSkipperMariaDbTests(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SchemaCreationTargetsTheDedicatedDatabase()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        // Segment first: the cache must still be able to create its own table afterwards.
        await _fixture.Database.InitializeAsync();
        Assert.True(_fixture.Cache.TryInitialize());

        Assert.True(MariaDbFixture.SchemaExists(MariaDbFixture.DatabaseName));

        var tables = MariaDbFixture.Tables(MariaDbFixture.DatabaseName);
        foreach (var expected in new[]
        {
            "Segments",
            "SeasonStates",
            "SeasonAnalysisOverrides",
            "AnalyzedItems",
            "DisabledItems",
            "ProjectionQueue",
            "ProjectionExternalOperations",
            "DetectionCache",
        })
        {
            Assert.Contains(expected, tables, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CacheCreationFirstStillLeavesTheSegmentSchema()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        // The reverse order of the test above. EF's EnsureCreated only creates tables
        // when the schema holds none, so an ordering-independent creation is the only
        // way both contexts can live in one schema.
        Assert.True(_fixture.Cache.TryInitialize());
        await _fixture.Database.InitializeAsync();

        var tables = MariaDbFixture.Tables(MariaDbFixture.DatabaseName);
        Assert.Contains("Segments", tables, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DetectionCache", tables, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SchemaCreationLeavesTheMainDatabaseAlone()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        Assert.True(_fixture.Cache.TryInitialize());

        var mainTables = MariaDbFixture.Tables("mulletaflix");
        Assert.DoesNotContain("Segments", mainTables, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SeasonStates", mainTables, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("DetectionCache", mainTables, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoSegmentsRoundTrip()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var itemId = Guid.NewGuid();

        var written = await _fixture.Database.ReplaceAutoSegmentsAsync(
            itemId,
            AnalysisMode.Introduction,
            [new Segment(itemId, new TimeRange(12.5, 42.5))],
            SegmentSource.Chromaprint,
            "hash-a");

        Assert.Equal(1, written);

        var stored = await _fixture.Database.GetSegmentsAsync(itemId);
        var row = Assert.Single(stored);
        Assert.Equal(AnalysisMode.Introduction, row.Type);
        Assert.Equal(SegmentSource.Chromaprint, row.Source);
        Assert.Equal(125000000, row.StartTicks);
        Assert.Equal(425000000, row.EndTicks);
    }

    [Fact]
    public async Task EraseItemsBindsTheIdSetAsOneParameter()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var erased = Guid.NewGuid();
        var kept = Guid.NewGuid();
        await WriteIntroductionAsync(erased);
        await WriteIntroductionAsync(kept);

        // This is the exact operation that failed in production with
        // "Translation of method 'System.ReadOnlySpan<System.Guid>.op_Implicit' failed".
        var removed = await _fixture.Database.EraseItemsAsync([erased]);

        Assert.Equal(1, removed);
        Assert.Empty(await _fixture.Database.GetSegmentsAsync(erased));
        Assert.Single(await _fixture.Database.GetSegmentsAsync(kept));
    }

    [Fact]
    public async Task ResetItemsForReanalysisBindsModeSetWithoutSpanEvaluation()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var changed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        await WriteIntroductionAsync(changed, cancellationToken);
        await WriteIntroductionAsync(kept, cancellationToken);
        await _fixture.Database.ApplyChangeAsync(
            new AddUserSegmentIntent(
                changed,
                AnalysisMode.Introduction,
                TimeSpan.FromSeconds(90).Ticks,
                TimeSpan.FromSeconds(105).Ticks),
            cancellationToken: cancellationToken);
        await _fixture.Database.ReplaceAutoSegmentsAsync(
            changed,
            AnalysisMode.Credits,
            [new Segment(changed, new TimeRange(50, 75))],
            SegmentSource.Chromaprint,
            "hash-credits",
            cancellationToken);
        await _fixture.Database.MarkItemsAnalyzedAsync(AnalysisMode.Introduction, [(changed, (long?)null), (kept, null)], "hash-intro", cancellationToken);
        await _fixture.Database.MarkItemsAnalyzedAsync(AnalysisMode.Credits, [(changed, (long?)null)], "hash-credits", cancellationToken);

        // Reset only Credits so Introduction remains analyzed. The mode set must
        // remain a normal EF query parameter instead of using ReadOnlySpan.Contains.
        await _fixture.Database.ResetItemsForReanalysisAsync([changed], [AnalysisMode.Credits], cancellationToken);

        var changedSegments = await _fixture.Database.GetSegmentsAsync(changed, cancellationToken: cancellationToken);
        Assert.Contains(changedSegments, segment => segment.Source == SegmentSource.User);
        Assert.Contains(changedSegments, segment => segment.Type == AnalysisMode.Introduction && segment.Source == SegmentSource.Chromaprint);
        Assert.DoesNotContain(changedSegments, segment => segment.Type == AnalysisMode.Credits);
        Assert.Single(await _fixture.Database.GetSegmentsAsync(kept, cancellationToken: cancellationToken));

        using var db = _fixture.IntroSkipperContexts.CreateDbContext();
        Assert.Contains(await db.AnalyzedItems.ToListAsync(cancellationToken), item => item.ItemId == changed && item.Type == AnalysisMode.Introduction);
        Assert.DoesNotContain(await db.AnalyzedItems.ToListAsync(cancellationToken), item => item.ItemId == changed && item.Type == AnalysisMode.Credits);
        Assert.Single(await db.AnalyzedItems.Where(item => item.ItemId == kept).ToListAsync(cancellationToken));

        // Reanalyzing Introduction must retain the user's segment and its matching
        // automatic segment while clearing only that mode's analyzed marker.
        await _fixture.Database.ResetItemsForReanalysisAsync([changed], [AnalysisMode.Introduction], cancellationToken);

        changedSegments = await _fixture.Database.GetSegmentsAsync(changed, cancellationToken: cancellationToken);
        Assert.Contains(changedSegments, segment => segment.Source == SegmentSource.User);
        Assert.Contains(changedSegments, segment => segment.Type == AnalysisMode.Introduction && segment.Source == SegmentSource.Chromaprint);
        Assert.Empty(await db.AnalyzedItems.Where(item => item.ItemId == changed).ToListAsync(cancellationToken));
        Assert.Single(await db.AnalyzedItems.Where(item => item.ItemId == kept).ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StaleTimestampLookupKeepsTheRetainedSetAsOneParameter()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var retained = Guid.NewGuid();
        var stale = Guid.NewGuid();
        await WriteIntroductionAsync(retained);
        await WriteIntroductionAsync(stale);

        var staleIds = await _fixture.Database.GetStaleTimestampEpisodeIdsAsync([retained]);

        // Other items in the shared schema are stale by design; assert on this item's pair.
        Assert.Contains(stale, staleIds);
        Assert.DoesNotContain(retained, staleIds);
    }

    [Fact]
    public async Task StaleSeasonAndItemStateLookupsSurviveLargeRetainedSets()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var seasonId = Guid.NewGuid();
        var episodeIds = Enumerable.Range(0, 250).Select(_ => Guid.NewGuid()).ToArray();
        await _fixture.Database.SetAnalyzerActionAsync(
            seasonId,
            new Dictionary<AnalysisMode, AnalyzerAction> { [AnalysisMode.Introduction] = AnalyzerAction.Default });
        await _fixture.Database.MarkItemsAnalyzedAsync(
            AnalysisMode.Introduction,
            episodeIds.Select(id => (id, (long?)null)),
            "hash-a");

        var retainedSeasonIds = await _fixture.Database.GetStaleSeasonIdsAsync([seasonId]);
        Assert.DoesNotContain(seasonId, retainedSeasonIds);

        var orphanedSeasons = await _fixture.Database.GetStaleSeasonIdsAsync([Guid.NewGuid()]);
        Assert.Contains(seasonId, orphanedSeasons);

        var stateIds = await _fixture.Database.GetStaleItemStateIdsAsync(episodeIds);
        Assert.DoesNotContain(episodeIds[0], stateIds);

        await _fixture.Database.CleanItemStateAsync(episodeIds);
        var orphaned = await _fixture.Database.GetStaleItemStateIdsAsync([Guid.NewGuid()]);
        Assert.Contains(episodeIds[0], orphaned);
    }

    [Fact]
    public async Task DeleteSegmentsByModeJournalsTheProjection()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        await _fixture.Database.InitializeAsync();
        var itemId = Guid.NewGuid();
        await WriteIntroductionAsync(itemId);

        var affected = await _fixture.Database.DeleteSegmentsByModeAsync(AnalysisMode.Introduction);

        Assert.Contains(itemId, affected);
        var work = await _fixture.Database.ReadProjectionWorkAsync(itemId, CancellationToken.None);
        Assert.NotNull(work);

        // The auto-segment write journals the item once and the erase journals it again,
        // so the marker holds the second version.
        Assert.Equal(2, work!.Value.Item.Version);
    }

    [Fact]
    public async Task DetectionCacheUpsertFindsAndDeletesOnMariaDb()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        Assert.True(_fixture.Cache.TryInitialize());

        var itemId = Guid.NewGuid();
        _fixture.Cache.Upsert(itemId, AnalysisMode.Introduction, CacheEntryType.Chromaprint, 0, 300, [1, 2, 3], "hash-a");

        var found = _fixture.Cache.FindEntry(itemId, AnalysisMode.Introduction, CacheEntryType.Chromaprint, 0, 300);
        Assert.NotNull(found);
        Assert.Equal([1, 2, 3], found!.Data);

        // Same key upserts instead of duplicating.
        _fixture.Cache.Upsert(itemId, AnalysisMode.Introduction, CacheEntryType.Chromaprint, 0, 300, [4, 5], "hash-b");
        using (var db = _fixture.CacheContexts.CreateDbContext())
        {
            Assert.Equal(1, db.DetectionCache.Count(e => e.ItemId == itemId));
        }

        var stale = await _fixture.Cache.GetStaleItemIdsAsync(new HashSet<Guid> { Guid.NewGuid() });
        Assert.Contains(itemId, stale);

        Assert.Equal(1, await _fixture.Cache.DeleteForItemsAsync([itemId]));
        Assert.Null(_fixture.Cache.FindEntry(itemId, AnalysisMode.Introduction, CacheEntryType.Chromaprint, 0, 300));
    }

    [Fact]
    public async Task PluginRegistrationPublishesFactoriesThatReachThePluginSchema()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();
        // The registration under test is the plugin's own; this pins the connection the
        // factories actually open, so the schema asserted everywhere else is the schema
        // production would use.
        await _fixture.Database.InitializeAsync();
        var pluginTables = MariaDbFixture.Tables(MariaDbFixture.DatabaseName);
        Assert.NotEmpty(pluginTables);

        var options = _fixture.IntroSkipperContexts.CreateDbContext().Database.GetDbConnection().ConnectionString;
        Assert.Contains($"Database={MariaDbFixture.DatabaseName}", options, StringComparison.Ordinal);
    }

    private Task WriteIntroductionAsync(Guid itemId, CancellationToken cancellationToken = default)
        => _fixture.Database.ReplaceAutoSegmentsAsync(
            itemId,
            AnalysisMode.Introduction,
            [new Segment(itemId, new TimeRange(10, 40))],
            SegmentSource.Chromaprint,
            "hash-a",
            cancellationToken);
}
