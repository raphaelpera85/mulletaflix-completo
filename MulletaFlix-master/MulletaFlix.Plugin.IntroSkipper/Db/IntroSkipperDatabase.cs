// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-FileCopyrightText: 2026 AbandonedCart
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntroSkipper.Db;

/// <summary>
/// Default implementation of <see cref="IIntroSkipperDatabase"/>.
/// The implementation is split across partial class files by concern:
/// <list type="bullet">
/// <item><description><c>IntroSkipperDatabase.cs</c> — lifecycle (initialization gate, schema creation, rebuild).</description></item>
/// <item><description><c>IntroSkipperDatabase.Segments.cs</c> — <see cref="DbSegment"/> reads and writes.</description></item>
/// <item><description><c>IntroSkipperDatabase.SeasonStates.cs</c> — <see cref="DbSeasonState"/> reads and writes and the queue-verification snapshot.</description></item>
/// <item><description><c>IntroSkipperDatabase.AnalyzedItems.cs</c> — <see cref="DbAnalyzedItem"/> reads and writes.</description></item>
/// <item><description><c>IntroSkipperDatabase.DisabledItems.cs</c> — <see cref="DbDisabledItem"/> reads and writes.</description></item>
/// <item><description><c>IntroSkipperDatabase.Maintenance.cs</c> — bulk cleanup operations spanning several tables.</description></item>
/// </list>
/// The facade is stateless apart from the retryable initialization gate: every operation
/// creates a fresh <see cref="IntroSkipperDbContext"/> from the injected factory.
/// </summary>
internal sealed partial class IntroSkipperDatabase : IIntroSkipperDatabase
{
    private readonly IDbContextFactory<IntroSkipperDbContext> _contextFactory;
    private readonly ILogger _logger;
    private readonly RetryableInitializationGate _initialization;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroSkipperDatabase"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory used to create database contexts.</param>
    /// <param name="logger">Logger.</param>
    public IntroSkipperDatabase(IDbContextFactory<IntroSkipperDbContext> contextFactory, ILogger<IntroSkipperDatabase> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        // Task.Run is load-bearing, not redundant: InitializeCoreAsync performs schema
        // creation (a blocking round trip on first start), so invoking the factory inline
        // would make every concurrent first-touch caller — including purely async ones on
        // the playback hot path — block its thread on the Lazy monitor until the factory's
        // first incomplete await. Dispatching to the thread pool makes the factory return a
        // Task immediately, so waiters genuinely await instead of blocking.
        _initialization = new RetryableInitializationGate(() => Task.Run(InitializeCoreAsync));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every public data operation awaits this first, which guarantees that no query can
    /// observe the database before the schema exists, regardless of whether the eager
    /// initializer (hosted service) has already run.
    /// </remarks>
    public Task InitializeAsync()
        => _initialization.AwaitValueAsync(ex => LogDatabaseInitializationError(_logger, ex));

    /// <inheritdoc/>
    public async Task RebuildDatabaseAsync(bool forceCleanOnBackupFailure = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Rebuild exists to recover from exactly this state — a database whose
            // schema cannot be created — so a failed gate must not make it unreachable.
            // The rebuild recreates the schema itself, and the failed attempt was
            // already reset, so the next operation re-initializes against the result.
            LogRebuildingWithoutInitialization(_logger, ex);
        }

        using var db = _contextFactory.CreateDbContext();
        await db.RebuildDatabaseAsync(_contextFactory.CreateDbContext, forceCleanOnBackupFailure, cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializeCoreAsync()
    {
        using var db = _contextFactory.CreateDbContext();
        await IntroSkipperSchema.EnsureAsync(db, cancellationToken: default).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database initialization failed; the next database operation will retry")]
    private static partial void LogDatabaseInitializationError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database initialization failed; proceeding with the requested rebuild, which recreates the schema")]
    private static partial void LogRebuildingWithoutInitialization(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping credits for episode {EpisodeId}: detected segment overlaps with introduction")]
    private static partial void LogCreditsOverlapWithIntro(ILogger logger, Guid episodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping automatic {Mode} segment for item {ItemId}: overlaps a segment the user deleted")]
    private static partial void LogAutoSegmentSuppressedByTombstone(ILogger logger, Data.AnalysisMode mode, Guid itemId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping automatic {Mode} segment for item {ItemId}: overlaps a user-provided segment")]
    private static partial void LogAutoSegmentSkippedForUserOverlap(ILogger logger, Data.AnalysisMode mode, Guid itemId);
}
