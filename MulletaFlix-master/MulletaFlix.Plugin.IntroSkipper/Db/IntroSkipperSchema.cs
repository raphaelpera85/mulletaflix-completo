// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace IntroSkipper.Db;

/// <summary>
/// Creates the tables of one plugin context inside the shared plugin schema.
/// <para>
/// <c>Database.EnsureCreated()</c> cannot be used directly when a schema holds more than
/// one context: EF creates the tables only when the database holds no table at all
/// (<c>RelationalDatabaseCreator.EnsureCreated</c> skips <c>CreateTables</c> as soon as
/// <c>HasTables()</c> is true). The segment context and the detection-cache context share
/// the plugin schema, so whichever initialized second would silently create nothing and
/// every later query would fail with "table doesn't exist". This helper owns the
/// creation per context so the two can converge in any order.
/// </para>
/// </summary>
public static class IntroSkipperSchema
{
    /// <summary>
    /// Ensures the context's tables exist, creating only what that context owns.
    /// </summary>
    /// <param name="context">Context whose model defines the tables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task EnsureAsync(DbContext context, CancellationToken cancellationToken)    {
        // Creates the database when it is missing, and the full table set when the
        // schema is empty — including the first boot of a fresh installation.
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        // The database already existed, so EnsureCreated left it untouched. Create this
        // context's tables without assuming the schema is empty; the creator emits
        // CREATE TABLE per model table, which is exactly the set this context owns.
        var probeTable = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .First(table => !string.IsNullOrEmpty(table))!;
        if (!await TableExistsAsync(context, probeTable, cancellationToken).ConfigureAwait(false))
        {
            await context.GetService<IRelationalDatabaseCreator>()
                .CreateTablesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<bool> TableExistsAsync(DbContext context, string tableName, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @table;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@table";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (!wasOpen)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }
}
