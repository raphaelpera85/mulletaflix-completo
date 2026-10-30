using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Model.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Trickplay;
using Xunit;
using IConfigurationManager = MediaBrowser.Common.Configuration.IConfigurationManager;

namespace MulletaFlix.Server.Implementations.Tests.Trickplay;

public sealed class TrickplayManagerResolutionCacheTests : IDisposable
{
    private readonly DbContextOptions<MulletaFlixDbContext> _options;
    private readonly IMulletaFlixDatabaseProvider _databaseProvider;
    private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;
    private readonly MulletaFlixDbContext _context;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public TrickplayManagerResolutionCacheTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot)
            .Options;

        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
        _databaseProvider = databaseProvider.Object;

        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior.Setup(locking => locking.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>(static (_, action) => action());
        lockingBehavior.Setup(locking => locking.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
            .Callback<MulletaFlixDbContext, Func<Task>>(static (_, save) => save());
        _lockingBehavior = lockingBehavior.Object;

        _context = CreateContext();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task GetTrickplayResolutions_ConcurrentColdRequests_QueryDatabaseOnce()
    {
        var itemId = Guid.NewGuid();
        var factoryCallCount = 0;
        var firstFactoryCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => CreateContextAfterReleaseAsync());

        async Task<MulletaFlixDbContext> CreateContextAfterReleaseAsync()
        {
            Interlocked.Increment(ref factoryCallCount);
            firstFactoryCall.TrySetResult();
            await releaseFactory.Task.ConfigureAwait(false);
            return CreateContext();
        }

        var manager = CreateManager(factory.Object);
        var requests = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => manager.GetTrickplayResolutions(itemId)))
            .ToArray();

        await firstFactoryCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        releaseFactory.TrySetResult();
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result => Assert.Empty(result));
        Assert.All(results, result => Assert.Same(results[0], result));
    }

    [Fact]
    public async Task SaveTrickplayInfo_InvalidatesCachedResolutions()
    {
        var manager = CreateManager(CreateFactory().Object);
        var itemId = Guid.NewGuid();

        Assert.Empty(await manager.GetTrickplayResolutions(itemId));

        await manager.SaveTrickplayInfo(CreateInfo(itemId));

        var resolutions = await manager.GetTrickplayResolutions(itemId);
        Assert.Contains(320, resolutions.Keys);
    }

    private Mock<IDbContextFactory<MulletaFlixDbContext>> CreateFactory()
    {
        var factory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factory.Setup(provider => provider.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContext);
        return factory;
    }

    private TrickplayManager CreateManager(IDbContextFactory<MulletaFlixDbContext> factory)
    {
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
            factory,
            Mock.Of<IApplicationPaths>(),
            Mock.Of<IPathManager>(),
            _memoryCache);
    }

    private MulletaFlixDbContext CreateContext()
    {
        return new MulletaFlixDbContext(
            _options,
            NullLogger<MulletaFlixDbContext>.Instance,
            _databaseProvider,
            _lockingBehavior);
    }

    private static TrickplayInfo CreateInfo(Guid itemId)
    {
        return new TrickplayInfo
        {
            ItemId = itemId,
            Width = 320,
            Height = 180,
            TileWidth = 10,
            TileHeight = 10,
            ThumbnailCount = 100,
            Interval = 1000,
            Bandwidth = 1000
        };
    }
}
