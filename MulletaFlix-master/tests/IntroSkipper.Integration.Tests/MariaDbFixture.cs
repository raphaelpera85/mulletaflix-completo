// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Db;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MulletaFlix.Database.Implementations.DbConfiguration;
using MulletaFlix.Server.Implementations.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

/// <summary>
/// Hosts the IntroSkipper database layer on a real MariaDB instance. The service graph is
/// built by the plugin's own <c>PluginServiceRegistrator</c> — the same call the server
/// makes — so the tests exercise the production wiring, not a copy of it.
/// <para>
/// These tests exist because the facade's LINQ is translated by Pomelo/MySQL and the
/// provider has no offline translation check. Every collection operation the facade
/// performs (<c>Contains</c> over GUID sets, <c>ExecuteDelete</c>, raw multi-row upserts)
/// is therefore only proven by running it.
/// </para>
/// </summary>
public sealed class MariaDbFixture : IAsyncLifetime
{
    /// <summary>Dedicated plugin schema, matching <c>PluginServiceRegistrator</c>.</summary>
    public const string DatabaseName = "mulletaflix_introskipper";

    private ServiceProvider? _services;

    /// <summary>Gets the schema context factory configured by the plugin registration.</summary>
    public IDbContextFactory<IntroSkipperDbContext> IntroSkipperContexts
        => _services!.GetRequiredService<IDbContextFactory<IntroSkipperDbContext>>();

    /// <summary>Gets the detection-cache context factory configured by the plugin registration.</summary>
    public IDbContextFactory<DetectionCacheDbContext> CacheContexts
        => _services!.GetRequiredService<IDbContextFactory<DetectionCacheDbContext>>();

    /// <summary>Gets the segment facade under test.</summary>
    public IIntroSkipperDatabase Database
        => _services!.GetRequiredService<IIntroSkipperDatabase>();

    /// <summary>Gets the detection-cache facade under test.</summary>
    public IDetectionCacheDatabase Cache
        => _services!.GetRequiredService<IDetectionCacheDatabase>();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        // A cold schema: nothing may assume a previous run created anything.
        Drop();
        _services = BuildServices();

        // Startup runs this from the hosted-service list. Resolving it by name keeps the
        // fixture to the database path: the plugin's other hosted services need Jellyfin
        // server internals that are out of scope here.
        var initializerType = typeof(IIntroSkipperDatabase).Assembly
            .GetType("IntroSkipper.Services.IntroSkipperDatabaseInitializer", throwOnError: true)!;
        var initializer = (IHostedService)ActivatorUtilities.CreateInstance(_services, initializerType);
        await initializer.StartAsync(CancellationToken.None);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        _services = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Drops the plugin schema. Used before a run so a stale schema cannot make a broken
    /// creation path look healthy.
    /// </summary>
    public static void Drop()
    {
        using var connection = new MySqlConnector.MySqlConnection(ConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
        command.ExecuteNonQuery();
    }

    /// <summary>True when the schema exists.</summary>
    /// <param name="databaseName">Schema to probe.</param>
    /// <returns>Whether the schema exists.</returns>
    public static bool SchemaExists(string databaseName)
    {
        using var connection = new MySqlConnector.MySqlConnection(ConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @name;";
        command.Parameters.AddWithValue("@name", databaseName);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>Lists the tables of a schema.</summary>
    /// <param name="databaseName">Schema to list.</param>
    /// <returns>The table names.</returns>
    public static IReadOnlyList<string> Tables(string databaseName)
    {
        using var connection = new MySqlConnector.MySqlConnection(ConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = @name ORDER BY table_name;";
        command.Parameters.AddWithValue("@name", databaseName);
        var tables = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static string ConnectionString()
        => "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Connection Timeout=5;";

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        var configuration = new DatabaseConfigurationOptions
        {
            DatabaseType = "MulletaFlix-MySQL",
            LockingBehavior = DatabaseLockingBehaviorTypes.NoLock,
            CustomProviderOptions = new CustomDatabaseOptions
            {
                PluginName = string.Empty,
                PluginAssembly = string.Empty,
                ConnectionString = string.Empty,
                Options =
                [
                    new CustomDatabaseOption { Key = "server", Value = "127.0.0.1" },
                    new CustomDatabaseOption { Key = "port", Value = "3306" },
                    new CustomDatabaseOption { Key = "user", Value = "root" },
                    new CustomDatabaseOption { Key = "password", Value = string.Empty },
                ],
            },
        };

        // The plugin registration reads the server's database configuration and derives
        // the plugin schema from it; the mock stands in for the server config file.
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.Setup(manager => manager.GetConfiguration("database")).Returns(configuration);
        services.AddSingleton(configurationManager.Object);

        // The server registers the provider before any plugin runs; the plugin resolves
        // IMulletaFlixDatabaseProvider from the container.
        services.AddMulletaFlixDbContext(
            configurationManager.Object,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // The real registration path, including the database-name override.
        new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>());

        return services.BuildServiceProvider();
    }
}
