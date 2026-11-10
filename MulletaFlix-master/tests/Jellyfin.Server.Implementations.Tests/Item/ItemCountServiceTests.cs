using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class ItemCountServiceTests
{
    [Fact]
    public void GetItemCounts_GroupsKnownTypesAndPreservesTotal()
    {
        var queryHelpers = new Mock<IItemQueryHelpers>();
        queryHelpers
            .Setup(helpers => helpers.TranslateQuery(
                It.IsAny<IQueryable<BaseItemEntity>>(),
                It.IsAny<MulletaFlixDbContext>(),
                It.IsAny<InternalItemsQuery>()))
            .Returns((IQueryable<BaseItemEntity> query, MulletaFlixDbContext _, InternalItemsQuery _) => query);

        var lookup = new Dictionary<BaseItemKind, string>
        {
            [BaseItemKind.MusicAlbum] = "MusicAlbum",
            [BaseItemKind.MusicArtist] = "MusicArtist",
            [BaseItemKind.Episode] = "Episode",
            [BaseItemKind.Movie] = "Movie",
            [BaseItemKind.MusicVideo] = "MusicVideo",
            [BaseItemKind.LiveTvProgram] = "LiveTvProgram",
            [BaseItemKind.Series] = "Series",
            [BaseItemKind.Audio] = "Audio",
            [BaseItemKind.Trailer] = "Trailer",
            [BaseItemKind.BoxSet] = "BoxSet",
            [BaseItemKind.Book] = "Book"
        };
        var itemTypeLookup = new Mock<IItemTypeLookup>();
        itemTypeLookup.SetupGet(itemLookup => itemLookup.BaseItemKindNames).Returns(lookup);

        var context = CreateContextWithItems("Movie", "Movie", "Series", "Audio");
        Assert.Equal(4, context.BaseItems.Count());

        var service = new ItemCountService(
            new SingleContextFactory(context),
            itemTypeLookup.Object,
            queryHelpers.Object);

        var result = service.GetItemCounts(new InternalItemsQuery());

        Assert.Equal(4, result.ItemCount);
        Assert.Equal(2, result.MovieCount);
        Assert.Equal(1, result.SeriesCount);
        Assert.Equal(1, result.SongCount);
        Assert.Equal(0, result.EpisodeCount);
    }

    private static MulletaFlixDbContext CreateContextWithItems(params string[] types)
    {
        var context = CreateContext();
        context.BaseItems.AddRange(types.Select(CreateEntity));
        context.SaveChanges();
        return context;
    }

    private static BaseItemEntity CreateEntity(string type)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = type
        };

    private static MulletaFlixDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var provider = new Mock<IMulletaFlixDatabaseProvider>();
        var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        locking
            .Setup(value => value.OnSaveChanges(It.IsAny<MulletaFlixDbContext>(), It.IsAny<Action>()))
            .Callback<MulletaFlixDbContext, Action>((_, saveChanges) => saveChanges());
        return new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            provider.Object,
            locking.Object);
    }

    private sealed class SingleContextFactory(MulletaFlixDbContext context) : IDbContextFactory<MulletaFlixDbContext>
    {
        public MulletaFlixDbContext CreateDbContext() => context;
    }
}
