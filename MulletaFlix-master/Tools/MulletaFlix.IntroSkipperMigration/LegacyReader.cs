// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// Read-only reader over one legacy SQLite database.
/// <para>
/// Every access goes through <c>pragma_table_info</c> rather than the migration history:
/// the plugin went through six schema versions, and a database can predate
/// <c>AnalyzedItems.FileVersion</c>, <c>DisabledItems.SeasonId</c> or the whole
/// <c>SeasonAnalysisOverrides</c> table. Reading by column presence lets one tool handle
/// every shape instead of one reader per version.
/// </para>
/// <para>
/// The file is opened with <c>Mode=ReadOnly</c> and <c>Pooling=False</c>: a live <c>-wal</c>
/// left behind by the old plugin is read through, and no connection ever checkpoints or
/// truncates, so the source bytes stay untouched and downgrading remains possible.
/// </para>
/// </summary>
internal sealed class LegacyReader : IDisposable
{
    private readonly SqliteConnection _connection;

    private LegacyReader(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Opens a legacy database read-only.</summary>
    /// <param name="path">Path of the database file.</param>
    /// <returns>The reader.</returns>
    public static LegacyReader Open(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return new LegacyReader(connection);
    }

    /// <summary>Lists the user tables of the database.</summary>
    /// <returns>The table names.</returns>
    public IReadOnlyList<string> Tables()
    {
        var tables = new List<string>();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%' ORDER BY name;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>Whether the database has the table.</summary>
    /// <param name="table">Table name.</param>
    /// <returns>True when the table exists.</returns>
    public bool HasTable(string table) => Columns(table).Count > 0;

    /// <summary>
    /// Reads the table, projecting each row through <paramref name="project"/>. Rows the
    /// projection rejects (missing key columns, unusable payload) are counted as skipped.
    /// </summary>
    /// <typeparam name="T">Projected row type.</typeparam>
    /// <param name="table">Table name.</param>
    /// <param name="project">Row projection.</param>
    /// <returns>The projected rows.</returns>
    public List<T> Read<T>(string table, Func<ColumnReader, T?> project)
        where T : class
    {
        var columns = Columns(table);
        if (columns.Count == 0)
        {
            return [];
        }

        var rows = new List<T>();
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT * FROM \"{table}\";";
        using var reader = command.ExecuteReader();
        var reader2 = new ColumnReader(reader, columns);
        while (reader.Read())
        {
            if (project(reader2) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private List<string> Columns(string table)
    {
        var columns = new List<string>();
        using var command = _connection.CreateCommand();

        // pragma_table_info is a table-valued function, so the name can be bound.
        command.CommandText = "SELECT name FROM pragma_table_info(@table);";
        command.Parameters.AddWithValue("@table", table);
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(0));
            }
        }
        catch (SqliteException)
        {
            // Not a table (or not a database at all): treated as absent by the caller.
            return [];
        }

        return columns;
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Typed access to one row, tolerating columns that only exist in some schema
    /// versions. Values are read as their storage class, because the pre-v2 in-place
    /// repair era produced rows whose declared column types do not always match what
    /// SQLite stored.
    /// </summary>
    internal sealed class ColumnReader
    {
        private readonly SqliteDataReader _reader;
        private readonly Dictionary<string, int> _ordinals;

        public ColumnReader(SqliteDataReader reader, IReadOnlyList<string> columns)
        {
            _reader = reader;
            _ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                _ordinals[columns[i]] = i;
            }
        }

        /// <summary>Whether the row carries the column.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>True when present.</returns>
        public bool Contains(string name) => _ordinals.ContainsKey(name);

        /// <summary>Reads a GUID, accepting text or blob storage.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or <see cref="Guid.Empty"/> when null/unusable.</returns>
        public Guid GetGuid(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return Guid.Empty;
            }

            if (_reader.GetValue(ordinal) is byte[] blob && blob.Length == 16)
            {
                return new Guid(blob);
            }

            var text = Convert.ToString(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return Guid.TryParse(text, out var parsed) ? parsed : Guid.Empty;
        }

        /// <summary>Reads a list of GUIDs stored as JSON.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The parsed ids; empty when null or malformed.</returns>
        public IEnumerable<Guid> GetGuidList(string name)
        {
            var json = GetString(name);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>Reads an integer.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or 0 when null.</returns>
        public int GetInt(string name) => (int)(GetNullableLong(name) ?? 0);

        /// <summary>Reads a nullable integer.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or null.</returns>
        public int? GetNullableInt(string name)
        {
            var value = GetNullableLong(name);
            return value is null ? null : (int)value.Value;
        }

        /// <summary>Reads a long.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or 0 when null.</returns>
        public long GetLong(string name) => GetNullableLong(name) ?? 0;

        /// <summary>Reads a nullable long.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or null.</returns>
        public long? GetNullableLong(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToInt64(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a nullable boolean (stored as 0/1).</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or null.</returns>
        public bool? GetNullableBool(string name)
        {
            var value = GetNullableLong(name);
            return value is null ? null : value.Value != 0;
        }

        /// <summary>Reads a nullable double.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or null.</returns>
        public double? GetNullableDouble(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDouble(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a string.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The value, or null.</returns>
        public string? GetString(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToString(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a blob.</summary>
        /// <param name="name">Column name.</param>
        /// <returns>The bytes, or null.</returns>
        public byte[]? GetBytes(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return null;
            }

            return _reader.GetValue(ordinal) as byte[];
        }

        /// <summary>
        /// Reads a timestamp. SQLite stored these as ISO text, and the provider also
        /// accepted numeric ticks in some builds, so both are handled. Values are
        /// normalised to UTC because the plugin writes and compares UTC.
        /// </summary>
        /// <param name="name">Column name.</param>
        /// <returns>The timestamp; <see cref="DateTime.UnixEpoch"/> when unusable.</returns>
        public DateTime GetDateTime(string name)
        {
            var ordinal = Ordinal(name);
            if (ordinal < 0 || _reader.IsDBNull(ordinal))
            {
                return DateTime.UnixEpoch;
            }

            var value = _reader.GetValue(ordinal);
            switch (value)
            {
                case string text when DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed):
                    return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                case long ticks when ticks > 0:
                    try
                    {
                        return new DateTime(ticks, DateTimeKind.Utc);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return DateTime.UnixEpoch;
                    }

                default:
                    var converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return converted > 0 ? new DateTime(converted, DateTimeKind.Utc) : DateTime.UnixEpoch;
            }
        }

        private int Ordinal(string name) => _ordinals.TryGetValue(name, out var ordinal) ? ordinal : -1;
    }
}
