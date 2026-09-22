// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using IntroSkipper.Data;
using IntroSkipper.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IntroSkipper.Integration.Tests;

/// <summary>
/// Builds a legacy SQLite database in the test's own temporary directory, so the
/// migration can be exercised against the shapes a real installation may hold — including
/// the pre-versioning shape that lacks <c>AnalyzedItems.FileVersion</c> and
/// <c>DisabledItems.SeasonId</c>.
/// </summary>
internal sealed class LegacySqliteBuilder : IDisposable
{
    private readonly string _directory;

    private LegacySqliteBuilder(string directory)
    {
        _directory = directory;
    }

    /// <summary>Gets the path of the segment database.</summary>
    public string SegmentDatabasePath => Path.Combine(_directory, "introskipper-v2.db");

    /// <summary>Gets the path of the cache database.</summary>
    public string CacheDatabasePath => Path.Combine(_directory, "introskipper-cache.db");

    /// <summary>Gets the path of a database that does not exist.</summary>
    public string MissingDatabasePath => Path.Combine(_directory, "does-not-exist.db");

    /// <summary>Creates a builder with its own temporary directory.</summary>
    /// <returns>The builder.</returns>
    public static LegacySqliteBuilder Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MulletaFlix-legacy-migration-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12]);
        Directory.CreateDirectory(directory);
        return new LegacySqliteBuilder(directory);
    }

    /// <summary>
    /// Creates the segment database in the newest legacy shape: every table the plugin had
    /// after its last SQLite migration, including <c>FileVersion</c> and
    /// <c>SeasonAnalysisOverrides</c>.
    /// </summary>
    /// <param name="includeFileVersion">Whether <c>AnalyzedItems.FileVersion</c> exists.</param>
    /// <param name="includeSeasonOverrides">Whether <c>SeasonAnalysisOverrides</c> exists.</param>
    /// <param name="includeDisabledSeasonId">Whether the dropped <c>DisabledItems.SeasonId</c> exists.</param>
    public void CreateSegmentDatabase(
        bool includeFileVersion = true,
        bool includeSeasonOverrides = true,
        bool includeDisabledSeasonId = false)
    {
        using var connection = Open(SegmentDatabasePath);
        Execute(connection, """
            CREATE TABLE "Segments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Segments" PRIMARY KEY,
                "ItemId" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "StartTicks" INTEGER NOT NULL,
                "EndTicks" INTEGER NOT NULL,
                "Source" INTEGER NOT NULL,
                "State" INTEGER NOT NULL,
                "ConfigHash" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Segments_ItemId_Type_StartTicks_EndTicks"
                ON "Segments" ("ItemId", "Type", "StartTicks", "EndTicks");
            """);

        Execute(connection, $"""
            CREATE TABLE "SeasonStates" (
                "SeasonId" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Action" INTEGER NOT NULL,
                "SettledReanalysisEpisodeIds" TEXT NOT NULL,
                CONSTRAINT "PK_SeasonStates" PRIMARY KEY ("SeasonId", "Type")
            );
            """);

        Execute(connection, includeFileVersion
            ? """
              CREATE TABLE "AnalyzedItems" (
                  "ItemId" TEXT NOT NULL,
                  "Type" INTEGER NOT NULL,
                  "ConfigHash" TEXT NOT NULL,
                  "FileVersion" INTEGER NULL,
                  CONSTRAINT "PK_AnalyzedItems" PRIMARY KEY ("ItemId", "Type")
              );
              """
            : """
              CREATE TABLE "AnalyzedItems" (
                  "ItemId" TEXT NOT NULL,
                  "Type" INTEGER NOT NULL,
                  "ConfigHash" TEXT NOT NULL,
                  CONSTRAINT "PK_AnalyzedItems" PRIMARY KEY ("ItemId", "Type")
              );
              """);

        Execute(connection, includeDisabledSeasonId
            ? """
              CREATE TABLE "DisabledItems" (
                  "ItemId" TEXT NOT NULL CONSTRAINT "PK_DisabledItems" PRIMARY KEY,
                  "SeasonId" TEXT NULL
              );
              """
            : """
              CREATE TABLE "DisabledItems" (
                  "ItemId" TEXT NOT NULL CONSTRAINT "PK_DisabledItems" PRIMARY KEY
              );
              """);

        if (includeSeasonOverrides)
        {
            Execute(connection, """
                CREATE TABLE "SeasonAnalysisOverrides" (
                    "SeasonId" TEXT NOT NULL CONSTRAINT "PK_SeasonAnalysisOverrides" PRIMARY KEY,
                    "AnalysisPercent" INTEGER NULL,
                    "AnalysisLengthLimit" INTEGER NULL,
                    "PreviewFromCreditsEnd" INTEGER NULL
                );
                """);
        }
    }

    /// <summary>Creates the detection-cache database.</summary>
    public void CreateCacheDatabase()
    {
        using var connection = Open(CacheDatabasePath);
        Execute(connection, """
            CREATE TABLE "DetectionCache" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DetectionCache" PRIMARY KEY AUTOINCREMENT,
                "ItemId" TEXT NOT NULL,
                "Mode" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "Start" REAL NOT NULL,
                "End" REAL NOT NULL,
                "Data" BLOB NOT NULL,
                "ConfigHash" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_DetectionCache_Unique"
                ON "DetectionCache" ("ItemId", "Mode", "Type", "Start", "End");
            """);
    }

    /// <summary>Inserts one segment row.</summary>
    /// <param name="id">Segment id.</param>
    /// <param name="itemId">Item id.</param>
    /// <param name="type">Analysis mode.</param>
    /// <param name="startTicks">Start ticks.</param>
    /// <param name="endTicks">End ticks.</param>
    /// <param name="source">Segment source.</param>
    /// <param name="state">Segment state.</param>
    public void AddSegment(Guid id, Guid itemId, AnalysisMode type, long startTicks, long endTicks, SegmentSource source, SegmentState state = SegmentState.Active)
    {
        using var connection = Open(SegmentDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Segments" ("Id", "ItemId", "Type", "StartTicks", "EndTicks", "Source", "State", "ConfigHash", "CreatedAt", "UpdatedAt")
            VALUES (@id, @itemId, @type, @start, @end, @source, @state, @hash, @created, @updated);
            """;
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@itemId", itemId.ToString());
        command.Parameters.AddWithValue("@type", (int)type);
        command.Parameters.AddWithValue("@start", startTicks);
        command.Parameters.AddWithValue("@end", endTicks);
        command.Parameters.AddWithValue("@source", (int)source);
        command.Parameters.AddWithValue("@state", (int)state);
        command.Parameters.AddWithValue("@hash", "legacy-hash");
        command.Parameters.AddWithValue("@created", "2026-09-01T10:00:00.0000000Z");
        command.Parameters.AddWithValue("@updated", "2026-09-02T11:30:00.0000000Z");
        command.ExecuteNonQuery();
    }

    /// <summary>Inserts one analyzed-item row.</summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="type">Analysis mode.</param>
    /// <param name="configHash">Configuration hash.</param>
    /// <param name="fileVersion">File version, when the column exists.</param>
    public void AddAnalyzedItem(Guid itemId, AnalysisMode type, string configHash, long? fileVersion = null)
    {
        using var connection = Open(SegmentDatabasePath);
        using var command = connection.CreateCommand();
        var hasVersion = ColumnExists(connection, "AnalyzedItems", "FileVersion");
        command.CommandText = hasVersion
            ? "INSERT INTO \"AnalyzedItems\" (\"ItemId\", \"Type\", \"ConfigHash\", \"FileVersion\") VALUES (@itemId, @type, @hash, @version);"
            : "INSERT INTO \"AnalyzedItems\" (\"ItemId\", \"Type\", \"ConfigHash\") VALUES (@itemId, @type, @hash);";
        command.Parameters.AddWithValue("@itemId", itemId.ToString());
        command.Parameters.AddWithValue("@type", (int)type);
        command.Parameters.AddWithValue("@hash", configHash);
        if (hasVersion)
        {
            command.Parameters.AddWithValue("@version", (object?)fileVersion ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>Inserts one season-state row.</summary>
    /// <param name="seasonId">Season id.</param>
    /// <param name="type">Analysis mode.</param>
    /// <param name="action">Analyzer action.</param>
    /// <param name="settled">Settled episode ids (stored as JSON).</param>
    public void AddSeasonState(Guid seasonId, AnalysisMode type, AnalyzerAction action, IEnumerable<Guid>? settled = null)
    {
        using var connection = Open(SegmentDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "SeasonStates" ("SeasonId", "Type", "Action", "SettledReanalysisEpisodeIds")
            VALUES (@seasonId, @type, @action, @settled);
            """;
        command.Parameters.AddWithValue("@seasonId", seasonId.ToString());
        command.Parameters.AddWithValue("@type", (int)type);
        command.Parameters.AddWithValue("@action", (int)action);
        command.Parameters.AddWithValue("@settled", System.Text.Json.JsonSerializer.Serialize(settled ?? []));
        command.ExecuteNonQuery();
    }

    /// <summary>Inserts one disabled-item row.</summary>
    /// <param name="itemId">Item id.</param>
    public void AddDisabledItem(Guid itemId)
    {
        using var connection = Open(SegmentDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO \"DisabledItems\" (\"ItemId\") VALUES (@itemId);";
        command.Parameters.AddWithValue("@itemId", itemId.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>Inserts one season analysis-override row.</summary>
    /// <param name="seasonId">Season id.</param>
    /// <param name="analysisPercent">Percentage override.</param>
    /// <param name="analysisLengthLimit">Length limit override.</param>
    /// <param name="previewFromCreditsEnd">Preview override, when the column exists.</param>
    public void AddSeasonAnalysisOverride(Guid seasonId, int? analysisPercent, int? analysisLengthLimit, bool? previewFromCreditsEnd = null)
    {
        using var connection = Open(SegmentDatabasePath);
        using var command = connection.CreateCommand();
        var hasPreview = ColumnExists(connection, "SeasonAnalysisOverrides", "PreviewFromCreditsEnd");
        command.CommandText = hasPreview
            ? """
              INSERT INTO "SeasonAnalysisOverrides" ("SeasonId", "AnalysisPercent", "AnalysisLengthLimit", "PreviewFromCreditsEnd")
              VALUES (@seasonId, @percent, @limit, @preview);
              """
            : """
              INSERT INTO "SeasonAnalysisOverrides" ("SeasonId", "AnalysisPercent", "AnalysisLengthLimit")
              VALUES (@seasonId, @percent, @limit);
              """;
        command.Parameters.AddWithValue("@seasonId", seasonId.ToString());
        command.Parameters.AddWithValue("@percent", (object?)analysisPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("@limit", (object?)analysisLengthLimit ?? DBNull.Value);
        if (hasPreview)
        {
            command.Parameters.AddWithValue("@preview", previewFromCreditsEnd is null ? DBNull.Value : previewFromCreditsEnd.Value ? 1 : 0);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>Inserts one detection-cache row.</summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="mode">Analysis mode.</param>
    /// <param name="type">Cache entry type.</param>
    /// <param name="data">Payload.</param>
    /// <param name="configHash">Configuration hash.</param>
    public void AddDetectionCache(Guid itemId, AnalysisMode mode, CacheEntryType type, byte[] data, string configHash)
    {
        using var connection = Open(CacheDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DetectionCache" ("ItemId", "Mode", "Type", "Start", "End", "Data", "ConfigHash")
            VALUES (@itemId, @mode, @type, 0, 300, @data, @hash);
            """;
        command.Parameters.AddWithValue("@itemId", itemId.ToString());
        command.Parameters.AddWithValue("@mode", (int)mode);
        command.Parameters.AddWithValue("@type", (int)type);
        command.Parameters.AddWithValue("@data", data);
        command.Parameters.AddWithValue("@hash", configHash);
        command.ExecuteNonQuery();
    }

    /// <summary>Counts the rows of a table.</summary>
    /// <param name="databasePath">Database file.</param>
    /// <param name="table">Table name.</param>
    /// <returns>The row count.</returns>
    public static long CountRows(string databasePath, string table)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp file must not fail a test run.
        }
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column;";
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }
}
