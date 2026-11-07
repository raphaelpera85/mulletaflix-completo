// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
    public static async Task EnsureAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Creates the database when it is missing, and the full table set when the
        // schema is empty — including the first boot of a fresh installation.
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        // EnsureCreated leaves an existing schema untouched; generate DDL only for
        // this context's missing tables so existing plugin data is never dropped.
        var existingTables = await GetExistingTablesAsync(context, cancellationToken).ConfigureAwait(false);
        var modelTables = context.Model.GetRelationalModel().Tables;
        var missingTables = modelTables
            .Where(table => !existingTables.Contains(table.Name))
            .Select(table => table.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (missingTables.Count > 0)
        {
            var operations = context.GetService<IMigrationsModelDiffer>()
                .GetDifferences(null, context.GetService<IDesignTimeModel>().Model.GetRelationalModel())
                .Where(operation => operation switch
                {
                    CreateTableOperation table => missingTables.Contains(table.Name),
                    CreateIndexOperation index => missingTables.Contains(index.Table),
                    _ => false
                })
                .ToArray();
            var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.Model);
            foreach (var command in commands)
            {
                await context.Database.ExecuteSqlRawAsync(command.CommandText, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Confirms that every mapped table can serve the complete entity shape. This is used
    /// during backup preflight so a partial table fails before the primary database commits.
    /// </summary>
    public static async Task ValidateAsync(DbContext context, CancellationToken cancellationToken)
    {
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var validateSet = typeof(IntroSkipperSchema)
                .GetMethod(nameof(ValidateEntitySetAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);
            var validation = (Task)validateSet.Invoke(null, [context, cancellationToken])!;
            await validation.ConfigureAwait(false);
        }
    }

    private static Task ValidateEntitySetAsync<TEntity>(DbContext context, CancellationToken cancellationToken)
        where TEntity : class
        => context.Set<TEntity>().AsNoTracking().Take(0).LoadAsync(cancellationToken);

    private static async Task<HashSet<string>> GetExistingTablesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
                ? "SELECT name FROM sqlite_master WHERE type = 'table';"
                : "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE();";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                names.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (!wasOpen)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }

        return names;
    }
}
