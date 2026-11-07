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
    /// <summary>Gets a unique plugin schema for this fixture instance.</summary>
    public string DatabaseName { get; } = $"mfx_intro_test_{Guid.NewGuid():N}";

    private ServiceProvider? _services;
    private MySqlConnector.MySqlConnectionStringBuilder? _adminConnection;
    private string? _originalTestDatabaseName;
    private bool _testDatabaseNameOverridden;

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
        _adminConnection = adminConnection;
        _originalTestDatabaseName = Environment.GetEnvironmentVariable("MULLETAFLIX_TEST_INTROSKIPPER_DATABASE");
        Environment.SetEnvironmentVariable("MULLETAFLIX_TEST_INTROSKIPPER_DATABASE", DatabaseName);
        _testDatabaseNameOverridden = true;

        try
        {
            // A cold, test-only schema: nothing may assume a previous run created anything.
            Drop(adminConnection, DatabaseName);
            _services = BuildServices(adminConnection);

            // Startup runs this from the hosted-service list. Resolving it by name keeps the
            // fixture to the database path: the plugin's other hosted services need Jellyfin
            // server internals that are out of scope here.
            var initializerType = typeof(IIntroSkipperDatabase).Assembly
                .GetType("IntroSkipper.Services.IntroSkipperDatabaseInitializer", throwOnError: true)!;
            var initializer = (IHostedService)ActivatorUtilities.CreateInstance(_services, initializerType);
            await initializer.StartAsync(CancellationToken.None);

            // Startup's warm-up is intentionally bounded and can continue in the
            // background after its timeout. Await both shared gates before allowing
            // teardown to drop this fixture's schema.
            await _services.GetRequiredService<IIntroSkipperDatabase>().InitializeAsync();
            if (!_services.GetRequiredService<IDetectionCacheDatabase>().TryInitialize())
            {
                throw new InvalidOperationException("IntroSkipper detection-cache schema initialization failed.");
            }
        }
        catch (Exception initializationFailure)
        {
            var cleanupFailures = RunCleanupActions(
                () =>
                {
                    try
                    {
                        _services?.Dispose();
                    }
                    finally
                    {
                        _services = null;
                    }
                },
                () => Drop(adminConnection, DatabaseName),
                () =>
                {
                    _adminConnection = null;
                    RestoreTestDatabaseName();
                });

            if (cleanupFailures.Count > 0)
            {
                throw CombineInitializationFailure(initializationFailure, cleanupFailures);
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        var cleanupFailures = RunCleanupActions(
            () =>
            {
                try
                {
                    _services?.Dispose();
                }
                finally
                {
                    _services = null;
                }
            },
            () =>
            {
                if (_adminConnection is not null)
                {
                    Drop(_adminConnection, DatabaseName);
                }
            },
            () =>
            {
                _adminConnection = null;
                RestoreTestDatabaseName();
            });

        if (cleanupFailures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        }

        if (cleanupFailures.Count > 1)
        {
            throw new AggregateException("Disposing and cleaning the isolated IntroSkipper MariaDB fixture both failed.", cleanupFailures);
        }

        return ValueTask.CompletedTask;
    }

    private void RestoreTestDatabaseName()
    {
        if (_testDatabaseNameOverridden)
        {
            Environment.SetEnvironmentVariable("MULLETAFLIX_TEST_INTROSKIPPER_DATABASE", _originalTestDatabaseName);
            _originalTestDatabaseName = null;
            _testDatabaseNameOverridden = false;
        }
    }

    /// <summary>
    /// Drops the plugin schema. Used before a run so a stale schema cannot make a broken
    /// creation path look healthy.
    /// </summary>
    internal static List<Exception> RunCleanupActions(params Action[] actions)
    {
        var failures = new List<Exception>();
        foreach (var action in actions)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    internal static Exception CombineInitializationFailure(Exception initializationFailure, IReadOnlyList<Exception> cleanupFailures)
    {
        if (cleanupFailures.Count == 0)
        {
            return initializationFailure;
        }

        var failures = new List<Exception>(cleanupFailures.Count + 1) { initializationFailure };
        failures.AddRange(cleanupFailures);
        return new AggregateException("Initializing and cleaning the isolated IntroSkipper MariaDB fixture both failed.", failures);
    }

    private static void Drop(MySqlConnector.MySqlConnectionStringBuilder adminConnection, string databaseName)
    {
        using var connection = new MySqlConnector.MySqlConnection(adminConnection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`;";
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

    private static ServiceProvider BuildServices(MySqlConnector.MySqlConnectionStringBuilder adminConnection)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns("Testing");
        services.AddSingleton(hostEnvironment.Object);

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
                    new CustomDatabaseOption { Key = "server", Value = adminConnection.Server },
                    new CustomDatabaseOption { Key = "port", Value = adminConnection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new CustomDatabaseOption { Key = "user", Value = adminConnection.UserID },
                    new CustomDatabaseOption { Key = "password", Value = adminConnection.Password },
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
