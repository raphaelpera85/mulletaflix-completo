using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Testing;
using MulletaFlix.Server.Implementations.Trickplay;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;
using IConfigurationManager = MediaBrowser.Common.Configuration.IConfigurationManager;

namespace MulletaFlix.Server.Implementations.Tests.Trickplay.Integration;

public sealed class TrickplayManagerDeleteMariaDbTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeleteTrickplayDataAsync_DeletesRequestedItemAndInvalidatesCachedResolutions()
    {
        using var database = new IsolatedMariaDbDatabase();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var manager = CreateManager(database, memoryCache);
        var itemToDelete = Guid.NewGuid();
        var itemToPreserve = Guid.NewGuid();

        await manager.SaveTrickplayInfo(CreateInfo(itemToDelete, 320));
        await manager.SaveTrickplayInfo(CreateInfo(itemToPreserve, 640));

        Assert.Contains(320, (await manager.GetTrickplayResolutions(itemToDelete)).Keys);
        Assert.Contains(640, (await manager.GetTrickplayResolutions(itemToPreserve)).Keys);
        Assert.Contains(itemToDelete.ToString(), (await manager.GetTrickplayManifest(new TestVideo(itemToDelete))).Keys);
        Assert.Contains(itemToPreserve.ToString(), (await manager.GetTrickplayManifest(new TestVideo(itemToPreserve))).Keys);

        await manager.DeleteTrickplayDataAsync(itemToDelete, CancellationToken.None);

        Assert.Empty(await manager.GetTrickplayResolutions(itemToDelete));
        Assert.Contains(640, (await manager.GetTrickplayResolutions(itemToPreserve)).Keys);
        Assert.Empty(await manager.GetTrickplayManifest(new TestVideo(itemToDelete)));
        Assert.Contains(itemToPreserve.ToString(), (await manager.GetTrickplayManifest(new TestVideo(itemToPreserve))).Keys);

        await using var verificationContext = database.CreateDbContext();
        Assert.Equal(0, await verificationContext.TrickplayInfos.CountAsync(info => info.ItemId == itemToDelete));
        Assert.Equal(1, await verificationContext.TrickplayInfos.CountAsync(info => info.ItemId == itemToPreserve));
    }

    private static TrickplayManager CreateManager(IsolatedMariaDbDatabase database, IMemoryCache memoryCache)
    {
        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => Task.FromResult(database.CreateDbContext()));

        var encodingHelper = new EncodingHelper(
            Mock.Of<IApplicationPaths>(),
            Mock.Of<IMediaEncoder>(),
            Mock.Of<ISubtitleEncoder>(),
            new ConfigurationBuilder().Build(),
            Mock.Of<IConfigurationManager>(),
            Mock.Of<IPathManager>());

        return new TrickplayManager(
            NullLogger<TrickplayManager>.Instance,
            Mock.Of<IMediaEncoder>(),
            Mock.Of<IFileSystem>(),
            encodingHelper,
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IImageEncoder>(),
            factory.Object,
            Mock.Of<IApplicationPaths>(),
            Mock.Of<IPathManager>(),
            memoryCache);
    }

    private static TrickplayInfo CreateInfo(Guid itemId, int width)
    {
        return new TrickplayInfo
        {
            ItemId = itemId,
            Width = width,
            Height = 180,
            TileWidth = 10,
            TileHeight = 10,
            ThumbnailCount = 100,
            Interval = 1000,
            Bandwidth = 1000
        };
    }

    private sealed class TestVideo : Video
    {
        public TestVideo(Guid itemId)
        {
            Id = itemId;
        }

        public override IReadOnlyList<MediaSourceInfo> GetMediaSources(bool enablePathSubstitution)
            => [new MediaSourceInfo { Id = Id.ToString() }];
    }

    private sealed class IsolatedMariaDbDatabase : IDisposable
    {
        private const string DatabasePrefix = "mfx_trickplay_test_";
        private readonly string _adminConnectionString;
        private readonly Mock<IMulletaFlixDatabaseProvider> _provider = new();
        private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior = new NoLockBehavior(
            NullLogger<NoLockBehavior>.Instance);

        public IsolatedMariaDbDatabase()
        {
            MariaDbTestEnvironment.SkipUnlessConfigured();
            var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
            _adminConnectionString = adminConnection.ConnectionString;
            DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");

            var databaseConnection = new MySqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = DatabaseName
            };
            Options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseMySql(databaseConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
                .Options;

            try
            {
                using var connection = new MySqlConnection(_adminConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                command.ExecuteNonQuery();

                _provider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
                using var context = CreateDbContext();
                context.Database.EnsureCreated();
            }
            catch
            {
                DropDatabase();
                throw;
            }
        }

        private string DatabaseName { get; }

        private DbContextOptions<MulletaFlixDbContext> Options { get; }

        public MulletaFlixDbContext CreateDbContext()
            => new(Options, NullLogger<MulletaFlixDbContext>.Instance, _provider.Object, _lockingBehavior);

        public void Dispose()
        {
            DropDatabase();
        }

        private void DropDatabase()
        {
            if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to drop a database outside the isolated Trickplay test prefix.");
            }

            using var connection = new MySqlConnection(_adminConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
        }
    }
}
