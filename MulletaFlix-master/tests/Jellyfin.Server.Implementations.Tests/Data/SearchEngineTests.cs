using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Server.Implementations.Library;
using MulletaFlix.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Search;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Data;

public class SearchEngineTests
{
    [Fact]
    public void GetSearchHints_scopes_cache_by_include_item_types()
    {
        var libraryManager = new Mock<ILibraryManager>();
        var userManager = new Mock<IUserManager>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var engine = new SearchEngine(libraryManager.Object, userManager.Object, cache);

        libraryManager
            .Setup(x => x.GetItemsResult(It.Is<InternalItemsQuery>(q => q.IncludeItemTypes.SequenceEqual(new[] { BaseItemKind.Movie }))))
            .Returns(new QueryResult<BaseItem>(0, 1, new List<BaseItem>
            {
                new Movie { Id = Guid.NewGuid(), Name = "Movie result" }
            }));

        libraryManager
            .Setup(x => x.GetItemsResult(It.Is<InternalItemsQuery>(q => q.IncludeItemTypes.SequenceEqual(new[] { BaseItemKind.Series }))))
            .Returns(new QueryResult<BaseItem>(0, 1, new List<BaseItem>
            {
                new Series { Id = Guid.NewGuid(), Name = "Series result" }
            }));

        var movieQuery = new SearchQuery
        {
            SearchTerm = "robot",
            IncludeItemTypes = [BaseItemKind.Movie]
        };

        var seriesQuery = new SearchQuery
        {
            SearchTerm = "robot",
            IncludeItemTypes = [BaseItemKind.Series]
        };

        var movieResult = engine.GetSearchHints(movieQuery);
        var seriesResult = engine.GetSearchHints(seriesQuery);

        Assert.Equal("Movie result", movieResult.Items.Single().Item.Name);
        Assert.Equal("Series result", seriesResult.Items.Single().Item.Name);
        libraryManager.Verify(x => x.GetItemsResult(It.IsAny<InternalItemsQuery>()), Times.Exactly(2));
    }
}
