using System;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Interfaces;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class ItemRepositoryTransientRetryTests
{
    [Theory]
    [InlineData(1205, true)]
    [InlineData(1213, true)]
    [InlineData(1062, false)]
    public void IsTransientMariaDbLockFailure_RecognizesOnlyLockFailures(int errorCode, bool expected)
    {
        var ctor = typeof(MySqlConnector.MySqlException).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(MySqlConnector.MySqlErrorCode), typeof(string)],
            null);
        var transient = (MySqlConnector.MySqlException)ctor!.Invoke([(MySqlConnector.MySqlErrorCode)errorCode, "simulated"]);
        var wrapped = new InvalidOperationException("outer", new InvalidOperationException("inner", transient));

        Assert.Equal(errorCode, transient.Number);
        Assert.Equal(expected, ItemPersistenceService.IsTransientMariaDbLockFailure(wrapped));
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task UpdatePeople_RetriesWholeReplacementAfterTransientCommitFailure(int errorCode)
    {
        await using var fixture = await Fixture.CreatePeopleAsync(errorCode);
        var itemId = await fixture.SeedPeopleAsync("old-role");
        var before = fixture.Factory.CreatedCount;
        var repository = new PeopleRepository(fixture.Factory, Mock.Of<IItemTypeLookup>());

        repository.UpdatePeople(itemId, [new PersonInfo { Name = "retry-person", Role = "new-role", Type = PersonKind.Actor }]);

        Assert.Equal(2, fixture.CommitCount);
        Assert.Equal(before + 2, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("new-role", await context.PeopleBaseItemMap.Where(map => map.ItemId == itemId).Select(map => map.Role).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdatePeople_StopsAfterThreeTransientAttemptsAndPreservesPriorMap()
    {
        await using var fixture = await Fixture.CreatePeopleAsync(1213, failureCount: 3);
        var itemId = await fixture.SeedPeopleAsync("old-role");
        var before = fixture.Factory.CreatedCount;
        var repository = new PeopleRepository(fixture.Factory, Mock.Of<IItemTypeLookup>());

        Assert.Throws<InvalidOperationException>(() =>
            repository.UpdatePeople(itemId, [new PersonInfo { Name = "retry-person", Role = "new-role", Type = PersonKind.Actor }]));

        Assert.Equal(3, fixture.CommitCount);
        Assert.Equal(before + 3, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("old-role", await context.PeopleBaseItemMap.Where(map => map.ItemId == itemId).Select(map => map.Role).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdatePeople_DoesNotRetryPermanentDatabaseFailure()
    {
        await using var fixture = await Fixture.CreatePeopleAsync(1062);
        var itemId = await fixture.SeedPeopleAsync("old-role");
        var before = fixture.Factory.CreatedCount;
        var repository = new PeopleRepository(fixture.Factory, Mock.Of<IItemTypeLookup>());

        Assert.Throws<InvalidOperationException>(() =>
            repository.UpdatePeople(itemId, [new PersonInfo { Name = "retry-person", Role = "new-role", Type = PersonKind.Actor }]));

        Assert.Equal(1, fixture.CommitCount);
        Assert.Equal(before + 1, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("old-role", await context.PeopleBaseItemMap.Where(map => map.ItemId == itemId).Select(map => map.Role).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task SaveChaptersAsync_RetriesWholeReplacementAfterTransientLockFailure(int errorCode)
    {
        await using var fixture = await Fixture.CreateAsync(errorCode);
        var itemId = await fixture.SeedChapterAsync("old");
        var before = fixture.Factory.CreatedCount;
        var repository = new ChapterRepository(fixture.Factory, Mock.Of<IImageProcessor>());

        await repository.SaveChaptersAsync(itemId, [new ChapterInfo { Name = "new", StartPositionTicks = 20 }], TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.CommitCount);
        Assert.Equal(before + 2, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(["new"], await context.Chapters.Where(chapter => chapter.ItemId == itemId).Select(chapter => chapter.Name).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task SaveChapters_RetriesWholeReplacementAfterTransientLockFailure(int errorCode)
    {
        await using var fixture = await Fixture.CreateAsync(errorCode);
        var itemId = await fixture.SeedChapterAsync("old");
        var before = fixture.Factory.CreatedCount;
        var repository = new ChapterRepository(fixture.Factory, Mock.Of<IImageProcessor>());

        repository.SaveChapters(itemId, [new ChapterInfo { Name = "new", StartPositionTicks = 20 }]);

        Assert.Equal(2, fixture.CommitCount);
        Assert.Equal(before + 2, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(["new"], await context.Chapters.Where(chapter => chapter.ItemId == itemId).Select(chapter => chapter.Name).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task SaveMediaStreamsAsync_RetriesWholeReplacementAfterTransientLockFailure(int errorCode)
    {
        await using var fixture = await Fixture.CreateAsync(errorCode);
        var itemId = await fixture.SeedStreamAsync("old");
        var before = fixture.Factory.CreatedCount;
        var appHost = new Mock<IServerApplicationHost>();
        appHost.Setup(host => host.ReverseVirtualPath(It.IsAny<string>())).Returns((string value) => value);
        var repository = new MediaStreamRepository(fixture.Factory, appHost.Object, Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>());

        await repository.SaveMediaStreamsAsync(itemId, [new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac" }], TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.CommitCount);
        Assert.Equal(before + 2, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("aac", await context.MediaStreamInfos.Where(stream => stream.ItemId == itemId).Select(stream => stream.Codec).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1213)]
    public async Task SaveMediaStreams_RetriesWholeReplacementAfterTransientLockFailure(int errorCode)
    {
        await using var fixture = await Fixture.CreateAsync(errorCode);
        var itemId = await fixture.SeedStreamAsync("old");
        var before = fixture.Factory.CreatedCount;
        var appHost = new Mock<IServerApplicationHost>();
        appHost.Setup(host => host.ReverseVirtualPath(It.IsAny<string>())).Returns((string value) => value);
        var repository = new MediaStreamRepository(fixture.Factory, appHost.Object, Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>());

        repository.SaveMediaStreams(itemId, [new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac" }], TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.CommitCount);
        Assert.Equal(before + 2, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("aac", await context.MediaStreamInfos.Where(stream => stream.ItemId == itemId).Select(stream => stream.Codec).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChaptersAsync_StopsAfterThreeTransientAttemptsAndPreservesPriorRows()
    {
        await using var fixture = await Fixture.CreateAsync(1205, failureCount: 3);
        var itemId = await fixture.SeedChapterAsync("old");
        var before = fixture.Factory.CreatedCount;
        var repository = new ChapterRepository(fixture.Factory, Mock.Of<IImageProcessor>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveChaptersAsync(itemId, [new ChapterInfo { Name = "new", StartPositionTicks = 20 }], TestContext.Current.CancellationToken));

        Assert.Equal(3, fixture.CommitCount);
        Assert.Equal(before + 3, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(["old"], await context.Chapters.Where(chapter => chapter.ItemId == itemId).Select(chapter => chapter.Name).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveMediaStreamsAsync_DoesNotRetryPermanentDatabaseFailure()
    {
        await using var fixture = await Fixture.CreateAsync(1062);
        var itemId = await fixture.SeedStreamAsync("old");
        var before = fixture.Factory.CreatedCount;
        var appHost = new Mock<IServerApplicationHost>();
        appHost.Setup(host => host.ReverseVirtualPath(It.IsAny<string>())).Returns((string value) => value);
        var repository = new MediaStreamRepository(fixture.Factory, appHost.Object, Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveMediaStreamsAsync(itemId, [new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "aac" }], TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.CommitCount);
        Assert.Equal(before + 1, fixture.Factory.CreatedCount);
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal("old", await context.MediaStreamInfos.Where(stream => stream.ItemId == itemId).Select(stream => stream.Codec).SingleAsync(TestContext.Current.CancellationToken));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FailTransactionCommitInterceptor _interceptor;

        private Fixture(
            SqliteConnection connection,
            TestContextFactory factory,
            FailTransactionCommitInterceptor interceptor)
        {
            _connection = connection;
            Factory = factory;
            _interceptor = interceptor;
        }

        public TestContextFactory Factory { get; }

        public int CommitCount => _interceptor.CommitCount;

        public static async Task<Fixture> CreateAsync(int errorCode, int failureCount = 1)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var interceptor = new FailTransactionCommitInterceptor(errorCode, failureCount);
            var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;
            var provider = Mock.Of<IMulletaFlixDatabaseProvider>();
            var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
            locking.Setup(value => value.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, save) => save());
            locking.Setup(value => value.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
                .Callback<MulletaFlixDbContext, Func<Task>>((_, save) => save());
            var factory = new TestContextFactory(options, provider, locking.Object);
            var fixture = new Fixture(connection, factory, interceptor);
            await using (var context = factory.CreateDbContext())
            {
                await context.Database.EnsureCreatedAsync();
            }

            return fixture;
        }

        public static async Task<Fixture> CreatePeopleAsync(int errorCode, int failureCount = 1)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var peopleInterceptor = new FailTransactionCommitInterceptor(errorCode, failureCount);
            var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(peopleInterceptor)
                .Options;
            var provider = Mock.Of<IMulletaFlixDatabaseProvider>();
            var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
            locking.Setup(value => value.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
                .Callback<MulletaFlixDbContext, Action>((_, save) => save());
            locking.Setup(value => value.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
                .Callback<MulletaFlixDbContext, Func<Task>>((_, save) => save());
            var factory = new TestContextFactory(options, provider, locking.Object);
            var fixture = new Fixture(connection, factory, peopleInterceptor);
            await using (var context = factory.CreateDbContext())
            {
                await context.Database.EnsureCreatedAsync();
            }

            return fixture;
        }

        public async Task<Guid> SeedChapterAsync(string name)
        {
            var itemId = await SeedItemAsync();
            await using var context = Factory.CreateDbContext();
            context.Chapters.Add(new Chapter
            {
                ItemId = itemId,
                Item = null!,
                ChapterIndex = 0,
                StartPositionTicks = 10,
                Name = name,
            });
            await context.SaveChangesAsync();
            _interceptor.Arm();
            return itemId;
        }

        public async Task<Guid> SeedStreamAsync(string codec)
        {
            var itemId = await SeedItemAsync();
            await using var context = Factory.CreateDbContext();
            context.MediaStreamInfos.Add(new MediaStreamInfo
            {
                ItemId = itemId,
                Item = null!,
                StreamIndex = 0,
                StreamType = MediaStreamTypeEntity.Audio,
                Codec = codec,
            });
            await context.SaveChangesAsync();
            _interceptor.Arm();
            return itemId;
        }

        public async Task<Guid> SeedPeopleAsync(string role)
        {
            var itemId = Guid.NewGuid();
            var item = new BaseItemEntity { Id = itemId, Type = "Movie", Name = "retry-test" };
            var person = new People { Id = Guid.NewGuid(), Name = "retry-person", PersonType = PersonKind.Actor.ToString() };
            await using var context = Factory.CreateDbContext();
            context.BaseItems.Add(item);
            context.Peoples.Add(person);
            context.PeopleBaseItemMap.Add(new PeopleBaseItemMap
            {
                ItemId = itemId,
                Item = item,
                PeopleId = person.Id,
                People = person,
                ListOrder = 0,
                SortOrder = 0,
                Role = role,
            });
            await context.SaveChangesAsync();
            _interceptor.Arm();
            return itemId;
        }

        private async Task<Guid> SeedItemAsync()
        {
            var itemId = Guid.NewGuid();
            await using var context = Factory.CreateDbContext();
            context.BaseItems.Add(new BaseItemEntity { Id = itemId, Type = "Movie", Name = "retry-test" });
            await context.SaveChangesAsync();
            return itemId;
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class TestContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly DbContextOptions<MulletaFlixDbContext> _options;
        private readonly IMulletaFlixDatabaseProvider _provider;
        private readonly IEntityFrameworkCoreLockingBehavior _locking;

        public TestContextFactory(DbContextOptions<MulletaFlixDbContext> options, IMulletaFlixDatabaseProvider provider, IEntityFrameworkCoreLockingBehavior locking)
        {
            _options = options;
            _provider = provider;
            _locking = locking;
        }

        public int CreatedCount { get; private set; }

        public MulletaFlixDbContext CreateDbContext()
        {
            CreatedCount++;
            return new MulletaFlixDbContext(_options, NullLogger<MulletaFlixDbContext>.Instance, _provider, _locking);
        }

        public Task<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class FailTransactionCommitInterceptor : DbTransactionInterceptor
    {
        private readonly int _errorCode;
        private readonly int _failureCount;
        private bool _armed;
        private int _commitCount;

        public int CommitCount => Volatile.Read(ref _commitCount);

        public FailTransactionCommitInterceptor(int errorCode, int failureCount)
        {
            _errorCode = errorCode;
            _failureCount = failureCount;
        }

        public void Arm() => _armed = true;

        public override InterceptionResult TransactionCommitting(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            ThrowOnTransientCommit();
            return result;
        }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            ThrowOnTransientCommit();
            return ValueTask.FromResult(result);
        }

        private void ThrowOnTransientCommit()
        {
            if (!_armed || Interlocked.Increment(ref _commitCount) > _failureCount)
            {
                return;
            }

            var ctor = typeof(MySqlConnector.MySqlException).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                [typeof(MySqlConnector.MySqlErrorCode), typeof(string)],
                null);
            var transient = (MySqlConnector.MySqlException)ctor!.Invoke([(MySqlConnector.MySqlErrorCode)_errorCode, "simulated lock failure"]);
            throw new InvalidOperationException("Simulated MariaDB transaction commit failure.", transient);

        }
    }
}
