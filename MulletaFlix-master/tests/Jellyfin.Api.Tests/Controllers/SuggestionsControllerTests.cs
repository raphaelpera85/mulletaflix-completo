using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MulletaFlix.Api.Caching;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class SuggestionsControllerTests : IDisposable
{
    private readonly Mock<IDtoService> _dtoService;
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ItemsResponseCache _itemsResponseCache;
    private readonly SuggestionsController _controller;
    private readonly User _user;
    private readonly Guid _userId;

    public SuggestionsControllerTests()
    {
        _dtoService = new Mock<IDtoService>();
        _userManager = new Mock<IUserManager>();
        _libraryManager = new Mock<ILibraryManager>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _itemsResponseCache = new ItemsResponseCache(_memoryCache);

        _user = new User(
            "suggestionTester",
            "testAuthProvider",
            "testPasswordResetProvider");
        _userId = _user.Id;

        _userManager.Setup(m => m.GetUserById(_userId)).Returns(_user);

        _dtoService
            .Setup(m => m.GetBaseItemDtosAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<User?>(),
                It.IsAny<BaseItem?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<BaseItemDto>());

        _controller = new SuggestionsController(
            _dtoService.Object,
            _userManager.Object,
            _libraryManager.Object,
            _itemsResponseCache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(InternalClaimTypes.UserId, _userId.ToString("D")),
                            new Claim(ClaimTypes.Role, UserRoles.Administrator)
                        },
                        "Custom"))
                }
            }
        };
    }

    [Fact]
    public async Task GetSuggestions_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetSuggestions(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: -1,
            limit: 10,
            enableTotalRecordCount: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSuggestions_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetSuggestions(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: -1,
            enableTotalRecordCount: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSuggestions_WithAnonymousUser_CallsLibraryManagerAndReturnsQueryResult()
    {
        var queryResult = new QueryResult<BaseItem>
        {
            Items = Array.Empty<BaseItem>(),
            TotalRecordCount = 0
        };

        _libraryManager
            .Setup(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(queryResult);

        var result = await _controller.GetSuggestions(
            userId: null,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: 20,
            enableTotalRecordCount: true);

        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.TotalRecordCount);
        Assert.Empty(result.Value.Items);
        _libraryManager.Verify(m => m.GetItemsResult(It.Is<InternalItemsQuery>(q => q.User == null)), Times.Once);
    }

    [Fact]
    public async Task GetSuggestions_WithAuthenticatedUser_CallsLibraryManagerAndReturnsQueryResult()
    {
        var queryResult = new QueryResult<BaseItem>
        {
            Items = Array.Empty<BaseItem>(),
            TotalRecordCount = 0
        };

        _libraryManager
            .Setup(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(queryResult);

        var result = await _controller.GetSuggestions(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: 20,
            enableTotalRecordCount: true);

        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.TotalRecordCount);
        Assert.Empty(result.Value.Items);
        _libraryManager.Verify(m => m.GetItemsResult(It.Is<InternalItemsQuery>(q => q.User == _user)), Times.Once);
    }

    [Fact]
    public async Task GetSuggestionsLegacy_WithEmptyUserId_ReturnsBadRequest()
    {
        var result = await _controller.GetSuggestionsLegacy(
            userId: Guid.Empty,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: 10,
            enableTotalRecordCount: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetSuggestionsLegacy_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetSuggestionsLegacy(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: -5,
            limit: 10,
            enableTotalRecordCount: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSuggestionsLegacy_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetSuggestionsLegacy(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: -5,
            enableTotalRecordCount: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSuggestionsLegacy_WithValidUserId_DelegatesToGetSuggestions()
    {
        var queryResult = new QueryResult<BaseItem>
        {
            Items = Array.Empty<BaseItem>(),
            TotalRecordCount = 0
        };

        _libraryManager
            .Setup(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(queryResult);

        var result = await _controller.GetSuggestionsLegacy(
            userId: _userId,
            mediaType: Array.Empty<MediaType>(),
            type: Array.Empty<BaseItemKind>(),
            startIndex: 0,
            limit: 15,
            enableTotalRecordCount: false);

        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.TotalRecordCount);
        _libraryManager.Verify(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()), Times.Once);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
