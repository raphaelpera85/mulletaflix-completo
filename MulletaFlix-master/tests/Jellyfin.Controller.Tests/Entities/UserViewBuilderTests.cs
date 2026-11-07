using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Controller.Tests.Entities;

public sealed class UserViewBuilderTests
{
    [Fact]
    public void GetUserItems_TvNextUp_PropagatesDisabledTotalCount()
    {
        var user = new User("testuser", "auth", "auth");
        NextUpQuery? capturedQuery = null;
        var tvSeriesManager = new Mock<ITVSeriesManager>();
        tvSeriesManager
            .Setup(manager => manager.GetNextUp(It.IsAny<NextUpQuery>(), It.IsAny<BaseItem[]>(), It.IsAny<DtoOptions>()))
            .Callback<NextUpQuery, BaseItem[], DtoOptions>((query, _, _) => capturedQuery = query)
            .Returns(new QueryResult<BaseItem>());

        var builder = new UserViewBuilder(
            Mock.Of<IUserViewManager>(),
            Mock.Of<ILibraryManager>(),
            NullLogger<BaseItem>.Instance,
            Mock.Of<IUserDataManager>(),
            tvSeriesManager.Object);

        builder.GetUserItems(
            new Folder(),
            new Folder(),
            CollectionType.tvnextup,
            new InternalItemsQuery(user) { EnableTotalRecordCount = false });

        Assert.NotNull(capturedQuery);
        Assert.False(capturedQuery.EnableTotalRecordCount);
    }
}
