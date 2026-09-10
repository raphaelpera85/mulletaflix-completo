using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Emby.Server.Implementations;
using MulletaFlix.Server.Extensions;
using MulletaFlix.Server.Helpers;
using MulletaFlix.Server.ServerSetupApp;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Serilog;
using Serilog.Core;
using MySqlConnector;

namespace MulletaFlix.Server.Integration.Tests
{
    /// <summary>
    /// Factory for bootstrapping the MulletaFlix application in memory for functional end to end tests.
    /// </summary>
    public class MulletaFlixApplicationFactory : WebApplicationFactory<Startup>
    {
        private static readonly string _testPathRoot = Path.Combine(Path.GetTempPath(), "MulletaFlix-test-data");
        private readonly ConcurrentBag<IDisposable> _disposableComponents = new ConcurrentBag<IDisposable>();
        private string? _webHostPathRoot;

        /// <summary>
        /// Initializes static members of the <see cref="MulletaFlixApplicationFactory"/> class.
        /// </summary>
        static MulletaFlixApplicationFactory()
        {
            // Perform static initialization that only needs to happen once per test-run
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();
            StartupHelpers.PerformStaticInitialization();
        }

        /// <inheritdoc/>
        protected override IHostBuilder CreateHostBuilder()
        {
            return new HostBuilder();
        }

        private static void ResetTestDatabase()
        {
            // The production MariaDB may not be running during CI; this reset is best-effort.
            // If the connection fails, the server startup will surface the real error.
            try
            {
                using var connection = new MySqlConnection("Server=localhost;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Connection Timeout=2;");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP DATABASE IF EXISTS `mulletaflix_test`; CREATE DATABASE `mulletaflix_test` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ResetTestDatabase failed (continuing): {ex.Message}");
            }
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Skip ffmpeg check for testing
            Environment.SetEnvironmentVariable("MulletaFlix_FFMPEG__NOVALIDATION", "true");
            Environment.SetEnvironmentVariable("MulletaFlix_DISABLE_RUNTIME_METRICS", "true");
            // Isolate tests from any production MulletaFlix database: tests run
            // against their own database that is dropped/recreated per run.
            Environment.SetEnvironmentVariable("MulletaFlix_DATABASE_NAME", "mulletaflix_test");
            ResetTestDatabase();
            // Specify the startup command line options
            var commandLineOpts = new StartupOptions();

            // Use a temporary directory for the application paths
            var webHostPathRoot = Path.Combine(_testPathRoot, "test-host-" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            _webHostPathRoot = webHostPathRoot;
            Directory.CreateDirectory(Path.Combine(webHostPathRoot, "logs"));
            Directory.CreateDirectory(Path.Combine(webHostPathRoot, "config"));
            Directory.CreateDirectory(Path.Combine(webHostPathRoot, "cache"));
            Directory.CreateDirectory(Path.Combine(webHostPathRoot, "MulletaFlix-web"));

            // Pre-initialize the system configuration expected by the legacy
            // network migration. A fresh test host otherwise reaches
            // CreateNetworkConfiguration with a path that does not exist,
            // producing a flaky DirectoryNotFoundException during startup.
            File.WriteAllText(
                Path.Combine(webHostPathRoot, "config", "system.xml"),
                "<ServerConfiguration />");

            var appPaths = new ServerApplicationPaths(
                webHostPathRoot,
                Path.Combine(webHostPathRoot, "logs"),
                Path.Combine(webHostPathRoot, "config"),
                Path.Combine(webHostPathRoot, "cache"),
                Path.Combine(webHostPathRoot, "MulletaFlix-web"));

            // Create the logging config file
            // TODO: We shouldn't need to do this since we are only logging to console
            StartupHelpers.InitLoggingConfigFile(appPaths).GetAwaiter().GetResult();

            // Create a copy of the application configuration to use for startup
            var startupConfig = Program.CreateAppConfiguration(commandLineOpts, appPaths);

            MariaDbProcessManager.StartMariaDbAsync(appPaths, NullLogger.Instance).GetAwaiter().GetResult();

            // The host owns the logger factory registered during service setup.
            // Reusing a disposable SerilogLoggerFactory here allowed the test host
            // to dispose it before migrations completed, causing startup failures.
            ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

            // Create the app host and initialize it
            var appHost = new TestAppHost(
                appPaths,
                loggerFactory,
                commandLineOpts,
                startupConfig);
            _disposableComponents.Add(appHost);

            builder.ConfigureServices(services => appHost.Init(services))
                .ConfigureWebHostBuilder(appHost, startupConfig, appPaths, NullLogger.Instance)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder
                        .SetBasePath(appPaths.ConfigurationDirectoryPath)
                        .AddInMemoryCollection(ConfigurationOptions.DefaultConfiguration)
                        .AddEnvironmentVariables("MulletaFlix_")
                        .AddInMemoryCollection(commandLineOpts.ConvertToConfig());
                })
                .ConfigureServices(e => e
                    .AddSingleton<IStartupLogger, NullStartupLogger<object>>()
                    .AddTransient(typeof(IStartupLogger<>), typeof(NullStartupLogger<>))
                    .AddSingleton(e)
                    .RemoveAll<ILoggerFactory>()
                    .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance));
        }

        /// <inheritdoc/>
        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = builder.Build();
            var appHost = (TestAppHost)host.Services.GetRequiredService<IApplicationHost>();
            appHost.ServiceProvider = host.Services;
            var applicationPaths = appHost.ServiceProvider.GetRequiredService<IApplicationPaths>();

            // Some startup services can recreate/normalize application paths while
            // the host is being built. Re-assert the migration precondition at the
            // exact point where startup migrations begin, so a fresh test host
            // cannot race with a missing configuration directory.
            Directory.CreateDirectory(applicationPaths.ConfigurationDirectoryPath);
            if (!File.Exists(applicationPaths.SystemConfigurationFilePath))
            {
                File.WriteAllText(applicationPaths.SystemConfigurationFilePath, "<ServerConfiguration />");
            }

            Program.ApplyStartupMigrationAsync((ServerApplicationPaths)applicationPaths, appHost.ServiceProvider.GetRequiredService<IConfiguration>(), new()).GetAwaiter().GetResult();
            Program.ApplyCoreMigrationsAsync(appHost.ServiceProvider, Migrations.Stages.MulletaFlixMigrationStageTypes.CoreInitialisation).GetAwaiter().GetResult();
            appHost.InitializeServices(Mock.Of<IConfiguration>()).GetAwaiter().GetResult();
            Program.ApplyCoreMigrationsAsync(appHost.ServiceProvider, Migrations.Stages.MulletaFlixMigrationStageTypes.AppInitialisation).GetAwaiter().GetResult();
            host.Start();

            appHost.RunStartupTasksAsync().GetAwaiter().GetResult();

            return host;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // WebApplicationFactory owns the host and must be disposed before its
            // application-path directory can be removed on Windows. It also owns
            // the application host registered in the service provider, so disposing
            // _disposableComponents here would dispose the same host twice.
            base.Dispose(disposing);

            MariaDbProcessManager.StopMariaDb(NullLogger.Instance);
            _disposableComponents.Clear();

            if (_webHostPathRoot is not null)
            {
                try
                {
                    if (Directory.Exists(_webHostPathRoot))
                    {
                        Directory.Delete(_webHostPathRoot, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to clean integration test host directory '{_webHostPathRoot}': {ex.Message}");
                }
                finally
                {
                    _webHostPathRoot = null;
                }
            }
        }

        private sealed class NullStartupLogger<TCategory> : IStartupLogger<TCategory>
        {
            public StartupLogTopic? Topic => null;

            public IStartupLogger BeginGroup(FormattableString logEntry)
            {
                return this;
            }

            public IStartupLogger<TCategory1> BeginGroup<TCategory1>(FormattableString logEntry)
            {
                return new NullStartupLogger<TCategory1>();
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return NullLogger.Instance.BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return NullLogger.Instance.IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                NullLogger.Instance.Log(logLevel, eventId, state, exception, formatter);
            }

            public Microsoft.Extensions.Logging.ILogger With(Microsoft.Extensions.Logging.ILogger logger)
            {
                return this;
            }

            public IStartupLogger<TCategory1> With<TCategory1>(Microsoft.Extensions.Logging.ILogger logger)
            {
                return new NullStartupLogger<TCategory1>();
            }

            IStartupLogger<TCategory> IStartupLogger<TCategory>.BeginGroup(FormattableString logEntry)
            {
                return new NullStartupLogger<TCategory>();
            }

            IStartupLogger IStartupLogger.With(Microsoft.Extensions.Logging.ILogger logger)
            {
                return this;
            }

            IStartupLogger<TCategory> IStartupLogger<TCategory>.With(Microsoft.Extensions.Logging.ILogger logger)
            {
                return this;
            }
        }
    }
}
