using System;
using System.IO;
using System.Reflection;
using Emby.Server.Implementations;
using MediaBrowser.Controller;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Xunit;

namespace MulletaFlix.Server.Tests;

public sealed class ApplicationHostBackupServiceRegistrationTests
{
    [Fact]
    public void RegisterServices_ResolvesBackupServiceWithoutConstructorAmbiguity()
    {
        var pluginsPath = Path.Combine(Path.GetTempPath(), "mulletaflix-backup-di", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginsPath);
        try
        {
            var paths = new ServerApplicationPaths(
                pluginsPath,
                Path.Combine(pluginsPath, "log"),
                Path.Combine(pluginsPath, "config"),
                Path.Combine(pluginsPath, "cache"),
                Path.Combine(pluginsPath, "web"));
            Directory.CreateDirectory(paths.LogDirectoryPath);
            Directory.CreateDirectory(paths.ConfigurationDirectoryPath);
            Directory.CreateDirectory(paths.CachePath);
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var host = new TestApplicationHost(
                paths,
                loggerFactory,
                Mock.Of<IStartupOptions>(),
                new ConfigurationBuilder().Build());
            var services = new ServiceCollection();
            host.RegisterForTest(services);
            services.AddSingleton<IServerApplicationPaths>(paths);
            services.AddLogging();
            services.AddSingleton(Mock.Of<IDbContextFactory<MulletaFlixDbContext>>());
            services.AddSingleton(Mock.Of<IMulletaFlixDatabaseProvider>());
            services.AddSingleton(Mock.Of<IHostApplicationLifetime>());

            using var provider = services.BuildServiceProvider();

            var backupService = provider.GetRequiredService<IBackupService>();

            Assert.IsType<BackupService>(backupService);
        }
        finally
        {
            Directory.Delete(pluginsPath, recursive: true);
        }
    }

    private sealed class TestApplicationHost(
        IServerApplicationPaths applicationPaths,
        ILoggerFactory loggerFactory,
        IStartupOptions options,
        IConfiguration startupConfig)
        : ApplicationHost(applicationPaths, loggerFactory, options, startupConfig)
    {
        protected override System.Collections.Generic.IEnumerable<System.Reflection.Assembly> GetAssembliesWithPartsInternal()
            => [];

        public void RegisterForTest(IServiceCollection services)
        {
            typeof(ApplicationHost)
                .GetField("<NetManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, Mock.Of<INetworkManager>());
            RegisterServices(services);
        }
    }
}
