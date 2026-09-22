// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-FileCopyrightText: 2026 AbandonedCart
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Data;
using Microsoft.EntityFrameworkCore;

namespace IntroSkipper.Db;

/// <summary>
/// Bulk maintenance operations of <see cref="IntroSkipperDatabase"/> spanning
/// segments, analysis records and season state.
/// </summary>
internal sealed partial class IntroSkipperDatabase
{
    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Guid>> GetStaleTimestampEpisodeIdsAsync(
        IEnumerable<Guid> enabledEpisodeIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> enabledIds = enabledEpisodeIds.ToHashSet();

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        // The retained set is a set variable, which Pomelo translates as a plain
        // NOT IN over the bound values.
        return await db.Segments
            .Where(s => !enabledIds.Contains(s.ItemId))
            .Select(s => s.ItemId)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> CleanStaleAutomaticSegmentsAsync(
        IEnumerable<Guid> itemIds,
        AnalysisMode mode,
        string configHash,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> ids = itemIds.ToHashSet();
        if (ids.Count == 0)
        {
            return 0;
        }

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        // Credits-derived previews carry the credits hash (the credits pass produces
        // them), so the credits pass judges their staleness and the preview pass leaves
        // them alone; every other automatic row belongs to its own mode's pass.
        // A row with an empty hash makes no staleness claim and is kept: restored
        // tombstones drop their hash on purpose, and legacy imports without a recorded
        // hash are replaced by re-analysis rather than deleted ahead of it.
        // The NOT EXISTS guard keeps the facade from ever deleting automatic rows of a
        // type the user has an active row for — the analyzers skip such items, so
        // nothing would regenerate the rows (same guard as ResetItemsForReanalysisAsync;
        // callers additionally pre-filter user-provided items as an optimization).
        var staleRows = db.Segments
            .Where(s => ids.Contains(s.ItemId)
                && s.Source != SegmentSource.User
                && s.State == SegmentState.Active
                && s.ConfigHash != string.Empty
                && s.ConfigHash != configHash
                && ((s.Source != SegmentSource.CreditsDerived && s.Type == mode)
                    || (s.Source == SegmentSource.CreditsDerived && mode == AnalysisMode.Credits))
                && !db.Segments.Any(u => u.ItemId == s.ItemId
                    && u.Type == s.Type
                    && u.Source == SegmentSource.User
                    && u.State == SegmentState.Active));

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Journaled with the delete; see docs/segment-database-v2.md.
            var (removed, _) = await DeleteSegmentsAndJournalAsync(db, staleRows, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return removed;
        }
    }

    /// <inheritdoc/>
    public async Task<int> EraseItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> ids = itemIds.ToHashSet();
        if (ids.Count == 0)
        {
            return 0;
        }

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Journal every addressed item, rows or not: an erase is the user saying
            // "this item must serve nothing", and an item with zero plugin rows can
            // still hold ghost Jellyfin rows (a past projection whose plugin rows were
            // since lost) that only a projection heals. Erases are explicit user
            // actions over bounded id sets, so the extra markers cost one no-op sync each.
            var removedSegments = await db.Segments
                // The predicate is a set variable. Naming it and taking the set's
                // Contains is what keeps the expression translator away from the
                // ReadOnlySpan optimization, which it cannot evaluate.
                .Where(s => ids.Contains(s.ItemId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await db.AnalyzedItems
                .Where(a => ids.Contains(a.ItemId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await EnqueueProjectionsAsync(db, ids, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return removedSegments;
        }
    }

    /// <inheritdoc/>
    public async Task ResetItemsForReanalysisAsync(
        IEnumerable<Guid> itemIds,
        IReadOnlyCollection<AnalysisMode> modes,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> ids = itemIds.ToHashSet();
        var modeArray = modes.ToArray();
        if (ids.Count == 0 || modeArray.Length == 0)
        {
            return;
        }

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var doomedRows = db.Segments
                .Where(s => ids.Contains(s.ItemId)
                    && modeArray.Contains(s.Type)
                    && s.Source != SegmentSource.User
                    && s.State == SegmentState.Active
                    && !db.Segments.Any(u => u.ItemId == s.ItemId
                        && u.Type == s.Type
                        && u.Source == SegmentSource.User
                        && u.State == SegmentState.Active));

            // Journaled with the delete; see docs/segment-database-v2.md.
            await DeleteSegmentsAndJournalAsync(db, doomedRows, cancellationToken).ConfigureAwait(false);

            // Without their records the items are NotAnalyzed on this pass (or a later
            // one) instead of being stranded as NoSegments.
            await db.AnalyzedItems
                .Where(a => ids.Contains(a.ItemId) && modeArray.Contains(a.Type))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Guid>> GetStaleSeasonIdsAsync(IEnumerable<Guid> retainedSeasonIds, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> retainedIds = retainedSeasonIds.ToHashSet();

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        return await db.SeasonStates
            .Where(s => !retainedIds.Contains(s.SeasonId))
            .Select(s => s.SeasonId)
            .Union(db.SeasonAnalysisOverrides
                .Where(s => !retainedIds.Contains(s.SeasonId))
                .Select(s => s.SeasonId))
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Guid>> GetStaleItemStateIdsAsync(IReadOnlyCollection<Guid> retainedItemIds, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> retainedIds = retainedItemIds.ToHashSet();

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        // One UNION statement; the set operator already deduplicates.
        return await db.DisabledItems
            .Where(e => !retainedIds.Contains(e.ItemId))
            .Select(e => e.ItemId)
            .Union(db.AnalyzedItems
                .Where(a => !retainedIds.Contains(a.ItemId))
                .Select(a => a.ItemId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CleanSeasonStateAsync(IEnumerable<Guid> seasonIds, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> retainedIds = seasonIds.ToHashSet();

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        // Single NOT-IN delete over the retained set, which keeps every existing key
        // and removes the rest in one statement.
        await db.SeasonStates
            .Where(s => !retainedIds.Contains(s.SeasonId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.SeasonAnalysisOverrides
            .Where(s => !retainedIds.Contains(s.SeasonId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CleanItemStateAsync(IReadOnlyCollection<Guid> retainedItemIds, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> retainedIds = retainedItemIds.ToHashSet();

        await InitializeAsync().ConfigureAwait(false);
        using var db = _contextFactory.CreateDbContext();

        // Single NOT-IN delete over the retained set, so the item count is unbounded.
        await db.DisabledItems
            .Where(e => !retainedIds.Contains(e.ItemId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.AnalyzedItems
            .Where(a => !retainedIds.Contains(a.ItemId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
