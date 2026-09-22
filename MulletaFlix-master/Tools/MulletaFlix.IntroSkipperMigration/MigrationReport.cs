// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// Collects what the migration did, per table, so the run ends with an auditable summary
/// instead of a silent success.
/// </summary>
internal sealed class MigrationReport
{
    private readonly TextWriter _output;
    private readonly List<(string Table, int Read, int Inserted, string? Note)> _rows = [];
    private readonly List<string> _warnings = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationReport"/> class.
    /// </summary>
    /// <param name="output">Sink for the run log.</param>
    public MigrationReport(TextWriter output) => _output = output;

    /// <summary>Gets the number of items journaled for projection.</summary>
    public int ProjectedItems { get; set; }

    /// <summary>Gets the number of warnings collected.</summary>
    public int Warnings => _warnings.Count;

    /// <summary>Gets the number of hard errors.</summary>
    public int Errors { get; private set; }

    /// <summary>Records a table outcome.</summary>
    /// <param name="table">Table name.</param>
    /// <param name="read">Rows read from the source.</param>
    /// <param name="inserted">Rows written to the target.</param>
    public void Set(string table, int read, int inserted) => _rows.Add((table, read, inserted, null));

    /// <summary>Records a table that was not migrated, with the reason.</summary>
    /// <param name="table">Table name.</param>
    /// <param name="note">Reason.</param>
    public void Note(string table, string note) => _rows.Add((table, 0, 0, note));

    /// <summary>Records a warning.</summary>
    /// <param name="message">Message.</param>
    public void Warn(string message) => _warnings.Add(message);

    /// <summary>Prints the summary.</summary>
    /// <param name="dryRun">Whether nothing was written.</param>
    public void Print(bool dryRun)
    {
        _output.WriteLine("==================== RESUMO ====================");
        foreach (var (table, read, inserted, note) in _rows)
        {
            _output.WriteLine(note is null
                ? $"  {table,-26} lidas {read,7}  inseridas {inserted,7}"
                : $"  {table,-26} {note}");
        }

        _output.WriteLine($"  {"itens para projeção",-26} {ProjectedItems,7}");
        _output.WriteLine($"  {"avisos",-26} {Warnings,7}");

        if (_warnings.Count > 0)
        {
            _output.WriteLine();
            _output.WriteLine("Avisos:");
            foreach (var warning in _warnings)
            {
                _output.WriteLine($"  - {warning}");
            }
        }

        _output.WriteLine("================================================");
        if (dryRun)
        {
            _output.WriteLine("Simulação concluída: nada foi gravado no MariaDB.");
        }
        else if (Errors == 0)
        {
            _output.WriteLine("Migração concluída. O plugin vai sincronizar os itens marcados com o Jellyfin.");
        }
        else
        {
            _output.WriteLine($"Migração concluída com {Errors} erro(s).");
        }
    }
}
