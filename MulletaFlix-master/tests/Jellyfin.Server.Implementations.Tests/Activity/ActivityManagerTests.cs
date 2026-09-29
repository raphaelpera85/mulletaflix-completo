using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Activity;

namespace MulletaFlix.Server.Implementations.Tests.Activity;

/// <summary>
/// Tests for <see cref="ActivityManager"/>, specifically the caller-scoped filtering that backs
/// the user-facing "my requests" endpoint (UserFeedbackController.GetMyMediaRequests), which must
/// never leak another user's activity log entries.
/// </summary>
public sealed class ActivityManagerTests : IDisposable
{
    private readonly DbContextOptions<MulletaFlixDbContext> _options;
    private readonly IMulletaFlixDatabaseProvider _dbProvider;
    private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;
    private readonly MulletaFlixDbContext _context;

    public ActivityManagerTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot)
            .Options;

        var dbProvider = new Mock<IMulletaFlixDatabaseProvider>();
        dbProvider.Setup(p => p.OnModelCreating(It.IsAny<ModelBuilder>()));
        _dbProvider = dbProvider.Object;

        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior.Setup(l => l.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>(static (_, action) => action());
        lockingBehavior.Setup(l => l.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
            .Callback<MulletaFlixDbContext, Func<Task>>(static (_, func) => func());
        _lockingBehavior = lockingBehavior.Object;

        _context = new MulletaFlixDbContext(
            _options,
            NullLogger<MulletaFlixDbContext>.Instance,
            _dbProvider,
            _lockingBehavior);

        _context.Database.EnsureCreated();
    }

    public void Dispose() => _context.Dispose();

    private MulletaFlixDbContext CreateContext() => new(_options, NullLogger<MulletaFlixDbContext>.Instance, _dbProvider, _lockingBehavior);

    private ActivityManager CreateManager()
    {
        var factoryMock = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => CreateContext());
        return new ActivityManager(factoryMock.Object);
    }

    [Fact]
    public async Task GetPagedResultAsync_UserIdFilter_OnlyReturnsThatUsersOwnEntries()
    {
        var manager = CreateManager();

        var requester = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        await manager.CreateAsync(new ActivityLog("Solicitação de mídia: Requester's Movie", "MediaRequest", requester));
        await manager.CreateAsync(new ActivityLog("Solicitação de mídia: Other User's Movie", "MediaRequest", otherUser));

        var result = await manager.GetPagedResultAsync(new ActivityLogQuery { UserId = requester, Type = "MediaRequest", Limit = 100 });

        Assert.Single(result.Items);
        Assert.Equal(requester, result.Items[0].UserId);
        Assert.Equal("Solicitação de mídia: Requester's Movie", result.Items[0].Name);
    }

    [Fact]
    public async Task GetPagedResultAsync_UserIdFilter_CombinesWithTypeFilter()
    {
        var manager = CreateManager();

        var requester = Guid.NewGuid();

        await manager.CreateAsync(new ActivityLog("Solicitação de mídia: A Movie", "MediaRequest", requester));
        await manager.CreateAsync(new ActivityLog("Problema reportado: A Movie", "PlaybackIssue", requester));

        var result = await manager.GetPagedResultAsync(new ActivityLogQuery { UserId = requester, Type = "MediaRequest", Limit = 100 });

        Assert.Single(result.Items);
        Assert.Equal("MediaRequest", result.Items[0].Type);
    }
}
