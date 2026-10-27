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
using MulletaFlix.Testing;

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
    private const string TestUserPrefix = "mfx_intro_";
    private const string TestUserHost = "127.0.0.1";

    private ServiceProvider? _services;
    private string? _testUser;
    private string? _applicationConnectionString;

    /// <summary>Gets the scoped connection string used by the plugin services.</summary>
    internal string ApplicationConnectionString
        => _applicationConnectionString ?? throw new InvalidOperationException("MariaDB fixture is not configured.");

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
        if (!MariaDbTestEnvironment.IsConfigured)
        {
            return;
        }

        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        // A cold schema: nothing may assume a previous run created anything.
        Drop(adminConnection);
        CreateDatabase(adminConnection);
        _testUser = TestUserPrefix + Guid.NewGuid().ToString("N");
        MySqlConnector.MySqlConnectionStringBuilder applicationConnection;
        try
        {
            applicationConnection = CreateApplicationConnection(adminConnection, _testUser);
        }
        catch
        {
            Drop(adminConnection);
            _testUser = null;
            throw;
        }

        _applicationConnectionString = applicationConnection.ConnectionString;
        _services = BuildServices(applicationConnection);

        // Startup runs this from the hosted-service list. Resolving it by name keeps the
        // fixture to the database path: the plugin's other hosted services need Jellyfin
        // server internals that are out of scope here.
        var initializerType = typeof(IIntroSkipperDatabase).Assembly
            .GetType("IntroSkipper.Services.IntroSkipperDatabaseInitializer", throwOnError: true)!;
        try
        {
            var initializer = (IHostedService)ActivatorUtilities.CreateInstance(_services, initializerType);
            await initializer.StartAsync(CancellationToken.None);
        }
        catch
        {
            _services.Dispose();
            _services = null;
            DropTestUser(adminConnection, _testUser);
            Drop(adminConnection);
            _testUser = null;
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        _services = null;
        if (_testUser is not null)
        {
            var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
            DropTestUser(adminConnection, _testUser);
            using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
            _testUser = null;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Drops the plugin schema. Used before a run so a stale schema cannot make a broken
    /// creation path look healthy.
    /// </summary>
    public static void Drop()
    {
        Drop(MariaDbTestEnvironment.GetAdminConnection());
    }

    private static void Drop(MySqlConnector.MySqlConnectionStringBuilder adminConnection)
    {
        using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
        command.ExecuteNonQuery();
    }

    private static void CreateDatabase(MySqlConnector.MySqlConnectionStringBuilder adminConnection)
    {
        using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        command.ExecuteNonQuery();
    }

    private static MySqlConnector.MySqlConnectionStringBuilder CreateApplicationConnection(
        MySqlConnector.MySqlConnectionStringBuilder adminConnection,
        string userName)
    {
        var password = Guid.NewGuid().ToString("N");
        using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        try
        {
            command.CommandText = $"CREATE USER '{userName}'@'{TestUserHost}' IDENTIFIED BY '{password}';";
            command.ExecuteNonQuery();
            command.CommandText = $"GRANT ALL PRIVILEGES ON `{DatabaseName}`.* TO '{userName}'@'{TestUserHost}';";
            command.ExecuteNonQuery();
        }
        catch
        {
            command.CommandText = $"DROP USER IF EXISTS '{userName}'@'{TestUserHost}';";
            command.ExecuteNonQuery();
            throw;
        }

        return new MySqlConnector.MySqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = DatabaseName,
            UserID = userName,
            Password = password,
        };
    }

    private static void DropTestUser(MySqlConnector.MySqlConnectionStringBuilder adminConnection, string userName)
    {
        if (!userName.StartsWith(TestUserPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to drop a MariaDB user outside the test prefix.");
        }

        using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP USER IF EXISTS '{userName}'@'{TestUserHost}';";
        command.ExecuteNonQuery();
    }

    /// <summary>True when the schema exists.</summary>
    /// <param name="databaseName">Schema to probe.</param>
    /// <returns>Whether the schema exists.</returns>
    public static bool SchemaExists(string databaseName)
    {
        using var connection = new MySqlConnector.MySqlConnection(MariaDbTestEnvironment.GetAdminConnection().ConnectionString);
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
        using var connection = new MySqlConnector.MySqlConnection(MariaDbTestEnvironment.GetAdminConnection().ConnectionString);
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

    private static ServiceProvider BuildServices(MySqlConnector.MySqlConnectionStringBuilder applicationConnection)
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
                    new CustomDatabaseOption { Key = "server", Value = applicationConnection.Server },
                    new CustomDatabaseOption { Key = "port", Value = applicationConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new CustomDatabaseOption { Key = "user", Value = applicationConnection.UserID },
                    new CustomDatabaseOption { Key = "password", Value = applicationConnection.Password },
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
