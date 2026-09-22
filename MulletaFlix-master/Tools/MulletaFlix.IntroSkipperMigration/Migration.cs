// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Data;
using IntroSkipper.Db;

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// Orchestrates the migration. Kept separate from <see cref="Program"/> so the paths a
/// person actually runs are the same ones the integration tests exercise: the CLI only
/// parses arguments and forwards here.
/// </summary>
public static class Migration
{
    /// <summary>
    /// Runs the migration.
    /// </summary>
    /// <param name="options">Parsed options.</param>
    /// <param name="output">Sink for the run log.</param>
    /// <returns>0 on success, 1 when the run collected errors, 2 for invalid input.</returns>
    public static async Task<int> RunAsync(Options options, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("MulletaFlix — IntroSkipper SQLite → MariaDB migration");
        output.WriteLine($"  origem de segmentos : {options.SegmentDatabasePath ?? "(não informado)"}");
        output.WriteLine($"  origem do cache     : {options.CacheDatabasePath ?? "(não informado)"}");
        output.WriteLine($"  destino             : {options.Server}:{options.Port}/{options.DatabaseName}");
        output.WriteLine($"  modo                : {(options.DryRun ? "simulação (não grava)" : "gravação")}");
        output.WriteLine($"  cache de detecção   : {(options.IncludeCache ? "migrar" : "ignorar")}");
        output.WriteLine();

        var sources = new List<string>();
        if (options.SegmentDatabasePath is not null)
        {
            sources.Add(options.SegmentDatabasePath);
        }

        if (options.IncludeCache && options.CacheDatabasePath is not null)
        {
            sources.Add(options.CacheDatabasePath);
        }

        foreach (var source in sources)
        {
            if (!File.Exists(source))
            {
                output.WriteLine($"error: arquivo não encontrado: {source}");
                return 2;
            }
        }

        if (sources.Count == 0)
        {
            output.WriteLine("error: informe ao menos uma origem (--segment-db e/ou --cache-db com --include-cache).");
            return 2;
        }

        await using var target = MigrationTarget.Create(options, output);
        await target.EnsureSchemaAsync().ConfigureAwait(false);

        var report = new MigrationReport(output);
        var affectedItems = new HashSet<Guid>();

        foreach (var source in sources)
        {
            var isCache = string.Equals(source, options.CacheDatabasePath, StringComparison.OrdinalIgnoreCase);
            output.WriteLine($"--- lendo {source}");
            using var legacy = LegacyReader.Open(source);

            var tables = legacy.Tables();
            if (tables.Count == 0)
            {
                output.WriteLine("    (nenhuma tabela encontrada — arquivo vazio ou não inicializado)");
                continue;
            }

            output.WriteLine($"    tabelas: {string.Join(", ", tables)}");

            if (!isCache)
            {
                foreach (var itemId in await MigrateSegmentsAsync(legacy, target, options, report).ConfigureAwait(false))
                {
                    affectedItems.Add(itemId);
                }

                await MigrateSeasonStatesAsync(legacy, target, options, report).ConfigureAwait(false);
                await MigrateAnalyzedItemsAsync(legacy, target, options, report).ConfigureAwait(false);
                await MigrateDisabledItemsAsync(legacy, target, options, report).ConfigureAwait(false);
                await MigrateSeasonAnalysisOverridesAsync(legacy, target, options, report).ConfigureAwait(false);
            }

            if (isCache)
            {
                await MigrateDetectionCacheAsync(legacy, target, options, report).ConfigureAwait(false);
            }
        }

        // Journaling runs once, after every source is loaded: the plugin's worker turns
        // these markers into Jellyfin MediaSegments rows. Every item that holds data after
        // the migration is enqueued, including the ones that were already present, so a run
        // that finds everything in place still heals a stale mirror.
        if (affectedItems.Count > 0)
        {
            output.WriteLine($"--- journaling ({affectedItems.Count} item(ns))");
            await target.EnqueueProjectionsAsync(affectedItems, options, report).ConfigureAwait(false);
        }

        output.WriteLine();
        report.Print(options.DryRun);

        return report.Errors == 0 ? 0 : 1;
    }

    private static async Task<IReadOnlyCollection<Guid>> MigrateSegmentsAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("Segments"))
        {
            report.Note("Segments", "tabela ausente na origem");
            return [];
        }

        var rows = legacy.Read("Segments", columns =>
        {
            if (!columns.Contains("Id") || !columns.Contains("ItemId"))
            {
                return null;
            }

            return new DbSegment
            {
                Id = columns.GetGuid("Id"),
                ItemId = columns.GetGuid("ItemId"),
                Type = (AnalysisMode)columns.GetInt("Type"),
                StartTicks = columns.GetLong("StartTicks"),
                EndTicks = columns.GetLong("EndTicks"),
                Source = (SegmentSource)columns.GetInt("Source"),
                State = (SegmentState)columns.GetInt("State"),
                ConfigHash = columns.GetString("ConfigHash") ?? string.Empty,
                CreatedAt = columns.GetDateTime("CreatedAt"),
                UpdatedAt = columns.GetDateTime("UpdatedAt"),
            };
        });

        await target.InsertSegmentsAsync(rows, options, report).ConfigureAwait(false);

        // Tombstones and user rows matter as much as active ones: they are what stops
        // re-analysis from resurrecting a segment the user deleted.
        return rows.Where(row => row.ItemId != Guid.Empty).Select(row => row.ItemId).Distinct().ToArray();
    }

    private static async Task MigrateSeasonStatesAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("SeasonStates"))
        {
            report.Note("SeasonStates", "tabela ausente na origem");
            return;
        }

        var rows = legacy.Read("SeasonStates", columns =>
        {
            if (!columns.Contains("SeasonId"))
            {
                return null;
            }

            return new DbSeasonState(
                columns.GetGuid("SeasonId"),
                (AnalysisMode)columns.GetInt("Type"),
                (AnalyzerAction)columns.GetInt("Action"),
                columns.GetGuidList("SettledReanalysisEpisodeIds"));
        });

        await target.InsertSeasonStatesAsync(rows, options, report).ConfigureAwait(false);
    }

    private static async Task MigrateAnalyzedItemsAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("AnalyzedItems"))
        {
            report.Note("AnalyzedItems", "tabela ausente na origem");
            return;
        }

        var rows = legacy.Read("AnalyzedItems", columns =>
        {
            if (!columns.Contains("ItemId"))
            {
                return null;
            }

            var row = new DbAnalyzedItem(
                columns.GetGuid("ItemId"),
                (AnalysisMode)columns.GetInt("Type"),
                columns.GetString("ConfigHash") ?? string.Empty);

            // FileVersion only exists from the versioning migration onwards; older shapes
            // left it null, which means "matches any file" for the queue verification.
            row.RestoreFileVersion(columns.GetNullableLong("FileVersion"));
            return row;
        });

        await target.InsertAnalyzedItemsAsync(rows, options, report).ConfigureAwait(false);
    }

    private static async Task MigrateDisabledItemsAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("DisabledItems"))
        {
            report.Note("DisabledItems", "tabela ausente na origem");
            return;
        }

        var rows = legacy.Read("DisabledItems", columns =>
            columns.Contains("ItemId") ? new DbDisabledItem(columns.GetGuid("ItemId")) : null);

        await target.InsertDisabledItemsAsync(rows, options, report).ConfigureAwait(false);
    }

    private static async Task MigrateSeasonAnalysisOverridesAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("SeasonAnalysisOverrides"))
        {
            // The table only exists from its own migration onwards, and its absence means
            // no season ever had an override — nothing to carry over.
            report.Note("SeasonAnalysisOverrides", "tabela ausente na origem");
            return;
        }

        var rows = legacy.Read("SeasonAnalysisOverrides", columns =>
        {
            if (!columns.Contains("SeasonId"))
            {
                return null;
            }

            return new DbSeasonAnalysisOverride
            {
                SeasonId = columns.GetGuid("SeasonId"),
                AnalysisPercent = columns.GetNullableInt("AnalysisPercent"),
                AnalysisLengthLimit = columns.GetNullableInt("AnalysisLengthLimit"),

                // Added by a later migration; absent means inherit.
                PreviewFromCreditsEnd = columns.Contains("PreviewFromCreditsEnd")
                    ? columns.GetNullableBool("PreviewFromCreditsEnd")
                    : null,
            };
        });

        await target.InsertSeasonAnalysisOverridesAsync(rows, options, report).ConfigureAwait(false);
    }

    private static async Task MigrateDetectionCacheAsync(LegacyReader legacy, MigrationTarget target, Options options, MigrationReport report)
    {
        if (!legacy.HasTable("DetectionCache"))
        {
            report.Note("DetectionCache", "tabela ausente na origem");
            return;
        }

        var rows = legacy.Read("DetectionCache", columns =>
        {
            if (!columns.Contains("ItemId") || !columns.Contains("Data"))
            {
                return null;
            }

            var data = columns.GetBytes("Data");
            if (data is null || data.Length == 0)
            {
                return null;
            }

            return new DbDetectionCache(
                columns.GetGuid("ItemId"),
                (AnalysisMode)columns.GetInt("Mode"),
                (CacheEntryType)columns.GetInt("Type"),
                data,
                columns.GetNullableDouble("Start") ?? 0,
                columns.GetNullableDouble("End") ?? 0,
                columns.GetString("ConfigHash") ?? string.Empty);
        });

        await target.InsertDetectionCacheAsync(rows, options, report).ConfigureAwait(false);
    }
}
