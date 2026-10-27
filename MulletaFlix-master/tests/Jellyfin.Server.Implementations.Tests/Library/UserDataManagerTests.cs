using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Library;

public sealed class UserDataManagerTests : IDisposable
{
    private readonly DbContextOptions<MulletaFlixDbContext> _options;
    private readonly IMulletaFlixDatabaseProvider _dbProvider;
    private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;
    private readonly MulletaFlixDbContext _context;
    private readonly UserDataManager _userDataManager;

    public UserDataManagerTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        _options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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

        var config = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>();
        config.SetupGet(x => x.Configuration).Returns(new MediaBrowser.Model.Configuration.ServerConfiguration { CacheSize = 100 });

        _userDataManager = new UserDataManager(
            config.Object,
            new InMemoryDbContextFactory(_options, _dbProvider, _lockingBehavior));
    }

    public void Dispose()
    {
        _context.Dispose();
        _userDataManager.Dispose();
    }

    [Fact]
    public void SaveUserData_UpdatesExistingRowInsteadOfDuplicatingIt()
    {
        var user = new User("test", "auth", "reset");
        user.Id = Guid.NewGuid();

        var item = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Folder"
        };

        _context.Users.Add(user);
        _context.BaseItems.Add(new BaseItemEntity
        {
            Id = item.Id,
            Type = "Folder",
            Name = item.Name
        });
        _context.SaveChanges();

        var first = new UserItemData
        {
            Key = item.Id.ToString("N"),
            PlayCount = 1,
            Played = false,
            IsFavorite = false
        };

        _userDataManager.SaveUserData(user, item, first, UserDataSaveReason.UpdateUserRating, CancellationToken.None);

        var second = new UserItemData
        {
            Key = item.Id.ToString("N"),
            PlayCount = 2,
            Played = true,
            IsFavorite = true
        };

        _userDataManager.SaveUserData(user, item, second, UserDataSaveReason.UpdateUserRating, CancellationToken.None);

        var rows = _context.UserData.Where(x => x.ItemId == item.Id && x.UserId == user.Id).ToList();

        Assert.Single(rows);
        Assert.Equal(2, rows[0].PlayCount);
        Assert.True(rows[0].Played);
        Assert.True(rows[0].IsFavorite);
    }

    [Fact]
    public async Task SaveUserDataAsync_PersistsUserDataAndRehydratesItem()
    {
        var user = new User("test", "auth", "reset")
        {
            Id = Guid.NewGuid()
        };
        var item = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Folder"
        };

        _context.Users.Add(user);
        _context.BaseItems.Add(new BaseItemEntity
        {
            Id = item.Id,
            Type = "Folder",
            Name = item.Name
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var userData = new UserItemData
        {
            Key = item.Id.ToString("N"),
            PlayCount = 3,
            Played = true,
            PlaybackPositionTicks = 1234
        };

        await _userDataManager.SaveUserDataAsync(
            user,
            item,
            userData,
            UserDataSaveReason.PlaybackProgress,
            TestContext.Current.CancellationToken);

        var saved = await _context.UserData.SingleAsync(
            data => data.ItemId == item.Id && data.UserId == user.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, saved.PlayCount);
        Assert.True(saved.Played);
        Assert.Equal(1234, saved.PlaybackPositionTicks);
        Assert.Contains(item.UserData!, data => data.ItemId == item.Id && data.UserId == user.Id);
    }

    [Fact]
    public async Task SaveUserDataAsync_DoesNotBlockDifferentItemsWithSameHashStripe()
    {
        var user = new User("test", "auth", "reset") { Id = Guid.NewGuid() };
        var firstItem = new Folder { Id = Guid.NewGuid(), Name = "First" };
        var secondItem = new Folder { Id = Guid.NewGuid(), Name = "Second" };
        while ((firstItem.Id.GetHashCode() & 7) != (secondItem.Id.GetHashCode() & 7))
        {
            secondItem.Id = Guid.NewGuid();
        }

        _context.Users.Add(user);
        _context.BaseItems.AddRange(
            new BaseItemEntity { Id = firstItem.Id, Type = "Folder", Name = firstItem.Name },
            new BaseItemEntity { Id = secondItem.Id, Type = "Folder", Name = secondItem.Name });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var firstSaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void HoldFirstSave(object? _, UserDataSaveEventArgs args)
        {
            if (args.Item.Id == firstItem.Id)
            {
                firstSaveStarted.TrySetResult();
                releaseFirstSave.Task.GetAwaiter().GetResult();
            }
        }

        _userDataManager.UserDataSaved += HoldFirstSave;
        var firstSave = Task.Run(
            () => _userDataManager.SaveUserDataAsync(
                user,
                firstItem,
                new UserItemData { Key = firstItem.Id.ToString("N") },
                UserDataSaveReason.PlaybackProgress,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        Task secondSave;
        try
        {
            await firstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(firstSave.IsCompleted);
            secondSave = _userDataManager.SaveUserDataAsync(
                user,
                secondItem,
                new UserItemData { Key = secondItem.Id.ToString("N") },
                UserDataSaveReason.PlaybackProgress,
                TestContext.Current.CancellationToken);
            await secondSave.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseFirstSave.TrySetResult();
            _userDataManager.UserDataSaved -= HoldFirstSave;
        }

        await firstSave.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private sealed class InMemoryDbContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly DbContextOptions<MulletaFlixDbContext> _options;
        private readonly IMulletaFlixDatabaseProvider _dbProvider;
        private readonly IEntityFrameworkCoreLockingBehavior _lockingBehavior;

        public InMemoryDbContextFactory(
            DbContextOptions<MulletaFlixDbContext> options,
            IMulletaFlixDatabaseProvider dbProvider,
            IEntityFrameworkCoreLockingBehavior lockingBehavior)
        {
            _options = options;
            _dbProvider = dbProvider;
            _lockingBehavior = lockingBehavior;
        }

        public MulletaFlixDbContext CreateDbContext()
            => new(_options, NullLogger<MulletaFlixDbContext>.Instance, _dbProvider, _lockingBehavior);

        public ValueTask<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }

}
