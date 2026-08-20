using System;
using System.Linq;
using System.Security.Claims;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data.Enums;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class SearchControllerTests
{
    [Fact]
    public void GetSearchHints_MapsSearchQueryAndUsesClaimUserId()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        SearchQuery? capturedQuery = null;

        var searchEngine = new Mock<ISearchEngine>();
        searchEngine
            .Setup(engine => engine.GetSearchHints(It.IsAny<SearchQuery>()))
            .Callback<SearchQuery>(query => capturedQuery = query)
            .Returns(new QueryResult<SearchHintInfo>(0, 7, Array.Empty<SearchHintInfo>()));

        var controller = new SearchController(
            searchEngine.Object,
            Mock.Of<ILibraryManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(InternalClaimTypes.UserId, userId.ToString("N"))
                            },
                            "test"))
                }
            }
        };

        var result = controller.GetSearchHints(
            startIndex: 5,
            limit: 25,
            userId: null,
            searchTerm: "  batman  ",
            includeItemTypes: [BaseItemKind.Movie, BaseItemKind.Series],
            excludeItemTypes: [BaseItemKind.Episode],
            mediaTypes: [MediaType.Video],
            parentId: parentId,
            isMovie: true,
            isSeries: false,
            isNews: null,
            isKids: true,
            isSports: false,
            includePeople: false,
            includeMedia: true,
            includeGenres: false,
            includeStudios: true,
            includeArtists: false);

        Assert.NotNull(capturedQuery);
        Assert.Equal(userId, capturedQuery!.UserId);
        Assert.Equal("  batman  ", capturedQuery.SearchTerm);
        Assert.Equal(5, capturedQuery.StartIndex);
        Assert.Equal(25, capturedQuery.Limit);
        Assert.False(capturedQuery.IncludePeople);
        Assert.True(capturedQuery.IncludeMedia);
        Assert.False(capturedQuery.IncludeGenres);
        Assert.True(capturedQuery.IncludeStudios);
        Assert.False(capturedQuery.IncludeArtists);
        Assert.Equal(parentId, capturedQuery.ParentId);
        Assert.True(capturedQuery.IsMovie);
        Assert.False(capturedQuery.IsSeries);
        Assert.Null(capturedQuery.IsNews);
        Assert.True(capturedQuery.IsKids);
        Assert.False(capturedQuery.IsSports);
        Assert.Equal([BaseItemKind.Movie, BaseItemKind.Series], capturedQuery.IncludeItemTypes);
        Assert.Equal([BaseItemKind.Episode], capturedQuery.ExcludeItemTypes);
        Assert.Equal([MediaType.Video], capturedQuery.MediaTypes);

        var okResult = Assert.IsType<ActionResult<SearchHintResult>>(result);
        var value = Assert.IsType<SearchHintResult>(okResult.Value);
        Assert.Empty(value.SearchHints);
        Assert.Equal(7, value.TotalRecordCount);
    }
}
