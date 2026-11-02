using System;
using System.Collections.Generic;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Item;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public sealed class BaseItemRepositoryTagQueryTranslationTests
{
    [Fact]
    public void TranslateQuery_InheritedTagArrays_ProduceMariaDbSql()
    {
        var provider = new Mock<IMulletaFlixDatabaseProvider>();
        provider.Setup(x => x.OnModelCreating(It.IsAny<ModelBuilder>()));
        provider.Setup(x => x.ConfigureConventions(It.IsAny<ModelConfigurationBuilder>()));
        var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseMySql("Server=localhost;Database=mulletaflix", new MariaDbServerVersion(new Version(11, 4, 2)))
            .Options;
        using var context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            provider.Object,
            locking.Object);

        var itemTypes = new Mock<IItemTypeLookup>();
        itemTypes.Setup(x => x.BaseItemKindNames).Returns(new Dictionary<BaseItemKind, string>());
        var repository = new BaseItemRepository(
            Mock.Of<IDbContextFactory<MulletaFlixDbContext>>(),
            Mock.Of<IServerApplicationHost>(),
            itemTypes.Object,
            Mock.Of<IServerConfigurationManager>(),
            NullLogger<BaseItemRepository>.Instance);

        var excluded = repository.TranslateQuery(
            context.BaseItems,
            context,
            new InternalItemsQuery { ExcludeInheritedTags = ["blocked"] });
        var included = repository.TranslateQuery(
            context.BaseItems,
            context,
            new InternalItemsQuery { IncludeInheritedTags = ["allowed"] });

        Assert.Contains("ItemValues", excluded.ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ItemValues", included.ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }
}
