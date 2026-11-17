using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.MediaSegments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.MediaSegments;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.MediaSegments;

public sealed class MediaSegmentManagerTests
{
    [Fact]
    public async Task GetSegmentsAsync_FiltersByProviderId()
    {
        var itemId = Guid.NewGuid();
        var provider = new TestSegmentProvider();
        var factory = CreateFactory();
        var manager = new MediaSegmentManager(NullLogger<MediaSegmentManager>.Instance, factory, [provider]);
        var providerId = manager.GetSupportedProviders(new Video { Id = itemId }).Single().Id;

        await using (var db = factory.CreateDbContext())
        {
            Assert.Empty(db.MediaSegments);
            db.MediaSegments.Add(new MediaSegment
            {
                Id = Guid.NewGuid(),
                ItemId = itemId,
                Type = MediaSegmentType.Intro,
                StartTicks = 0,
                EndTicks = 1,
                SegmentProviderId = providerId
            });
            await db.SaveChangesAsync();
            Assert.Equal(providerId, (await db.MediaSegments.SingleAsync()).SegmentProviderId);
        }

        var result = await manager.GetSegmentsAsync(new Video { Id = itemId }, null, new LibraryOptions());

        Assert.Single(result);
    }

    private static TestContextFactory CreateFactory() => new();

    private static MulletaFlixDbContext CreateContext(InMemoryDatabaseRoot root)
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase("MediaSegmentManagerTests", root)
            .Options;
        var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
        databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<Microsoft.EntityFrameworkCore.ModelBuilder>()));
        var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
        lockingBehavior
            .Setup(locking => locking.OnSaveChangesAsync(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Func<Task>>()))
            .Returns((MulletaFlixDbContext _, Func<Task> saveChanges) => saveChanges());
        var context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            databaseProvider.Object,
            lockingBehavior.Object);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly InMemoryDatabaseRoot _root = new();

        public MulletaFlixDbContext CreateDbContext() => CreateContext(_root);

        public Task<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class TestSegmentProvider : IMediaSegmentProvider
    {
        public string Name => "IntroSkipper test provider";

        public Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(MediaSegmentGenerationRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MediaSegmentDto>>([]);

        public ValueTask<bool> Supports(BaseItem item) => ValueTask.FromResult(true);

        public Task CleanupExtractedData(Guid itemId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
