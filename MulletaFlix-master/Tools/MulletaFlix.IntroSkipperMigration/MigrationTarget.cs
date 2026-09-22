// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Db;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// The MariaDB side of the migration: creates the plugin schema through the plugin's own
/// model and inserts the migrated rows.
/// <para>
/// Inserts never overwrite: a row whose key already exists is skipped, so re-running the
/// tool — or running it after the plugin already re-analysed an item — cannot destroy
/// newer data. Segment rows are also checked against the range invariant the plugin's
/// schema enforces, so a corrupt legacy row is reported instead of aborting the run.
/// </para>
/// </summary>
internal sealed class MigrationTarget : IAsyncDisposable
{
    private const int BatchSize = 400;

    private readonly IntroSkipperDbContext _db;
    private readonly string _connectionString;
    private readonly Options _options;
    private readonly TextWriter _output;
    private DetectionCacheDbContext? _cache;
    private bool _targetTableReachable = true;

    private MigrationTarget(IntroSkipperDbContext db, string connectionString, Options options, TextWriter output)
    {
        _db = db;
        _connectionString = connectionString;
        _options = options;
        _output = output;
    }

    /// <summary>Creates the contexts against the configured MariaDB schema.</summary>
    /// <param name="options">Parsed options.</param>
    /// <returns>The target.</returns>
    public static MigrationTarget Create(Options options, TextWriter output)
    {
        var connectionString =
            $"Server={options.Server};Port={options.Port};User ID={options.User};Password={options.Password};" +
            $"Database={options.DatabaseName};CharSet=utf8mb4;SslMode=None;";

        var builder = new DbContextOptionsBuilder<IntroSkipperDbContext>();
        builder.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 2)));
        return new MigrationTarget(new IntroSkipperDbContext(builder.Options), connectionString, options, output);
    }

    /// <summary>Creates the plugin schema when it is missing.</summary>
    /// <returns>A task that completes when the schema exists.</returns>
    public async Task EnsureSchemaAsync()
    {
        if (_options.DryRun)
        {
            // A dry run must not create anything, and it must not query a schema that does
            // not exist yet — that would fail instead of reporting.
            _targetTableReachable = await _db.Database.CanConnectAsync().ConfigureAwait(false);
            _output.WriteLine(_targetTableReachable
                ? $"    destino '{_options.DatabaseName}' responde; nada será gravado."
                : $"    destino '{_options.DatabaseName}' não responde; nenhuma linha seria encontrada.");
            return;
        }

        await IntroSkipperSchema.EnsureAsync(_db, CancellationToken.None).ConfigureAwait(false);

        // The cache table belongs to its own context; creating it here keeps a migration
        // into a brand-new schema self-sufficient.
        var cache = CacheContext;
        cache.EnsureSchema();
    }

    /// <summary>
    /// Gets the plugin's detection-cache context. Reusing it means the cache rows are
    /// written through the same model the plugin reads, instead of a second mapping.
    /// </summary>
    private DetectionCacheDbContext CacheContext
    {
        get
        {
            if (_cache is null)
            {
                var builder = new DbContextOptionsBuilder<DetectionCacheDbContext>();
                builder.UseMySql(_connectionString, new MariaDbServerVersion(new Version(11, 4, 2)));
                _cache = new DetectionCacheDbContext(builder.Options);
            }

            return _cache;
        }
    }

    /// <summary>Inserts the segments, skipping rows the schema would reject.</summary>
    /// <param name="rows">Segments read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public async Task InsertSegmentsAsync(IReadOnlyList<DbSegment> rows, Options options, MigrationReport report)
    {
        var usable = new List<DbSegment>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Id == Guid.Empty || row.ItemId == Guid.Empty)
            {
                report.Warn($"Segments: linha sem Id/ItemId ignorada (Id={row.Id}, ItemId={row.ItemId}).");
                continue;
            }

            // Mirrors CK_Segments_Range; inserting it would abort the whole run.
            if (row.StartTicks < 0 || row.EndTicks <= row.StartTicks)
            {
                report.Warn($"Segments: faixa inválida ignorada ({row.StartTicks}..{row.EndTicks}) do item {row.ItemId}.");
                continue;
            }

            usable.Add(row);
        }

        await InsertAsync(
            usable,
            row => _db.Segments.Any(s =>
                s.Id == row.Id
                || (s.ItemId == row.ItemId && s.Type == row.Type && s.StartTicks == row.StartTicks && s.EndTicks == row.EndTicks)),
            row => _db.Segments.Add(row),
            options,
            report,
            "Segments").ConfigureAwait(false);
    }

    /// <summary>Inserts the season states.</summary>
    /// <param name="rows">Rows read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public Task InsertSeasonStatesAsync(IReadOnlyList<DbSeasonState> rows, Options options, MigrationReport report)
        => InsertAsync(
            rows.Where(row => row.SeasonId != Guid.Empty).ToList(),
            row => _db.SeasonStates.Any(s => s.SeasonId == row.SeasonId && s.Type == row.Type),
            row => _db.SeasonStates.Add(row),
            options,
            report,
            "SeasonStates");

    /// <summary>Inserts the per-item analysis records.</summary>
    /// <param name="rows">Rows read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public Task InsertAnalyzedItemsAsync(IReadOnlyList<DbAnalyzedItem> rows, Options options, MigrationReport report)
        => InsertAsync(
            rows.Where(row => row.ItemId != Guid.Empty).ToList(),
            row => _db.AnalyzedItems.Any(a => a.ItemId == row.ItemId && a.Type == row.Type),
            row => _db.AnalyzedItems.Add(row),
            options,
            report,
            "AnalyzedItems");

    /// <summary>Inserts the disabled-item flags.</summary>
    /// <param name="rows">Rows read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public Task InsertDisabledItemsAsync(IReadOnlyList<DbDisabledItem> rows, Options options, MigrationReport report)
        => InsertAsync(
            rows.Where(row => row.ItemId != Guid.Empty).ToList(),
            row => _db.DisabledItems.Any(d => d.ItemId == row.ItemId),
            row => _db.DisabledItems.Add(row),
            options,
            report,
            "DisabledItems");

    /// <summary>Inserts the per-season analysis-window overrides.</summary>
    /// <param name="rows">Rows read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public Task InsertSeasonAnalysisOverridesAsync(IReadOnlyList<DbSeasonAnalysisOverride> rows, Options options, MigrationReport report)
        => InsertAsync(
            rows.Where(row => row.SeasonId != Guid.Empty).ToList(),
            row => _db.SeasonAnalysisOverrides.Any(o => o.SeasonId == row.SeasonId),
            row => _db.SeasonAnalysisOverrides.Add(row),
            options,
            report,
            "SeasonAnalysisOverrides");

    /// <summary>Inserts the detection-cache entries.</summary>
    /// <param name="rows">Rows read from the legacy database.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the rows are processed.</returns>
    public async Task InsertDetectionCacheAsync(IReadOnlyList<DbDetectionCache> rows, Options options, MigrationReport report)
    {
        var usable = rows.Where(row => row.ItemId != Guid.Empty).ToList();
        if (usable.Count == 0)
        {
            report.Set("DetectionCache", 0, 0);
            _output.WriteLine("    DetectionCache: nada a migrar.");
            return;
        }

        if (options.DryRun is false && !_targetTableReachable)
        {
            // Only a dry run can reach here with an unreachable target.
            report.Set("DetectionCache", usable.Count, usable.Count);
            _output.WriteLine($"    DetectionCache: {usable.Count} linha(s) seriam inseridas (destino inexistente).");
            return;
        }

        var cache = CacheContext;

        if (options.DryRun)
        {
            var wouldInsert = usable.Count(row => !IsCached(cache, row));
            report.Set("DetectionCache", usable.Count, wouldInsert);
            _output.WriteLine($"    DetectionCache: {wouldInsert} de {usable.Count} linha(s) seriam inseridas.");
            return;
        }

        var inserted = 0;
        var skipped = 0;
        foreach (var chunk in usable.Chunk(BatchSize))
        {
            var fresh = new List<DbDetectionCache>(chunk.Length);
            foreach (var row in chunk)
            {
                if (IsCached(cache, row))
                {
                    skipped++;
                    continue;
                }

                cache.DetectionCache.Add(row);
                fresh.Add(row);
            }

            if (fresh.Count == 0)
            {
                continue;
            }

            try
            {
                await cache.SaveChangesAsync().ConfigureAwait(false);
                inserted += fresh.Count;
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
            {
                cache.ChangeTracker.Clear();
                foreach (var row in fresh)
                {
                    if (IsCached(cache, row))
                    {
                        skipped++;
                        continue;
                    }

                    cache.DetectionCache.Add(row);
                    try
                    {
                        await cache.SaveChangesAsync().ConfigureAwait(false);
                        inserted++;
                    }
                    catch (DbUpdateException inner) when (inner.InnerException is MySqlException { Number: 1062 })
                    {
                        cache.ChangeTracker.Clear();
                        skipped++;
                        report.Warn("DetectionCache: linha duplicada ignorada.");
                    }
                }
            }

            cache.ChangeTracker.Clear();
        }

        report.Set("DetectionCache", usable.Count, inserted);
        _output.WriteLine($"    DetectionCache: {inserted} inserida(s), {skipped} já existente(s) ou conflitante(s).");
    }

    private static bool IsCached(DetectionCacheDbContext cache, DbDetectionCache row)
        => cache.DetectionCache.Any(c =>
            c.ItemId == row.ItemId && c.Mode == row.Mode && c.Type == row.Type && c.Start == row.Start && c.End == row.End);

    /// <summary>
    /// Journals the given items for projection, reusing the plugin's own marker rule: an
    /// existing marker bumps its version with the due time cleared, a missing one is
    /// inserted. Without this the migrated segments would sit in MariaDB but never reach
    /// Jellyfin's MediaSegments table.
    /// </summary>
    /// <param name="itemIds">Items whose servable image changed.</param>
    /// <param name="options">Parsed options.</param>
    /// <param name="report">Report to update.</param>
    /// <returns>A task that completes when the markers are written.</returns>
    public async Task EnqueueProjectionsAsync(IReadOnlyCollection<Guid> itemIds, Options options, MigrationReport report)
    {
        var ids = itemIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        if (options.DryRun)
        {
            _output.WriteLine($"    projeção: {ids.Length} item(ns) seriam marcados para sincronizar com o Jellyfin.");
            return;
        }

        // Parameters, never spliced values: the key text must match what the plugin writes.
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var parameters = new List<MySqlParameter>(chunk.Length);
            var values = new List<string>(chunk.Length);
            for (var i = 0; i < chunk.Length; i++)
            {
                var name = $"@id{i}";
                values.Add($"({name}, 1, 0, NULL, NULL)");
                parameters.Add(new MySqlParameter(name, chunk[i]));
            }

            var sql =
                "INSERT INTO `ProjectionQueue` (`ItemId`, `Version`, `AttemptCount`, `NextAttemptAt`, `Failure`) " +
                $"VALUES {string.Join(", ", values)} " +
                "ON DUPLICATE KEY UPDATE `Version` = `Version` + 1, `NextAttemptAt` = NULL;";

            await _db.Database.ExecuteSqlRawAsync(sql, parameters).ConfigureAwait(false);
        }

        report.ProjectedItems += ids.Length;
        _output.WriteLine($"    projeção: {ids.Length} item(ns) marcados para sincronizar com o Jellyfin.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_cache is not null)
        {
            await _cache.DisposeAsync().ConfigureAwait(false);
        }

        await _db.DisposeAsync().ConfigureAwait(false);
    }

    private async Task InsertAsync<T>(
        IReadOnlyList<T> rows,
        Func<T, bool> exists,
        Action<T> add,
        Options options,
        MigrationReport report,
        string table)
        where T : class
    {
        if (rows.Count == 0)
        {
            report.Set(table, 0, 0);
            _output.WriteLine($"    {table}: nada a migrar.");
            return;
        }

        // An unreachable target can only be a dry run against a schema that does not exist
        // yet: every row would be inserted, and querying would fail rather than report.
        if (!_targetTableReachable)
        {
            report.Set(table, rows.Count, rows.Count);
            _output.WriteLine($"    {table}: {rows.Count} linha(s) seriam inseridas (destino inexistente).");
            return;
        }

        if (options.DryRun)
        {
            var wouldInsert = rows.Count(row => !exists(row));
            report.Set(table, rows.Count, wouldInsert);
            _output.WriteLine($"    {table}: {wouldInsert} de {rows.Count} linha(s) seriam inseridas.");
            return;
        }

        var inserted = 0;
        var skipped = 0;
        foreach (var chunk in rows.Chunk(BatchSize))
        {
            var fresh = new List<T>(chunk.Length);
            foreach (var row in chunk)
            {
                if (exists(row))
                {
                    skipped++;
                    continue;
                }

                add(row);
                fresh.Add(row);
            }

            if (fresh.Count == 0)
            {
                continue;
            }

            try
            {
                await _db.SaveChangesAsync().ConfigureAwait(false);
                inserted += fresh.Count;
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
            {
                // A row raced an existing key between the check and the save (or the range
                // index caught a duplicate range under a different id). Save them one at a
                // time so the conflicting row is skipped and the rest still land.
                _db.ChangeTracker.Clear();
                foreach (var row in fresh)
                {
                    if (exists(row))
                    {
                        skipped++;
                        continue;
                    }

                    add(row);
                    try
                    {
                        await _db.SaveChangesAsync().ConfigureAwait(false);
                        inserted++;
                    }
                    catch (DbUpdateException inner) when (inner.InnerException is MySqlException { Number: 1062 })
                    {
                        _db.ChangeTracker.Clear();
                        skipped++;
                        report.Warn($"{table}: linha duplicada ignorada.");
                    }
                }
            }

            _db.ChangeTracker.Clear();
        }

        report.Set(table, rows.Count, inserted);
        _output.WriteLine($"    {table}: {inserted} inserida(s), {skipped} já existente(s) ou conflitante(s).");
    }
}
