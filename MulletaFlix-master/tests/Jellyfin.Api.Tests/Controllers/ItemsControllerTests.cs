using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Querying;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Extensions;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ItemsControllerTests : IDisposable
{
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<ILocalizationManager> _localization;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Mock<ISessionManager> _sessionManager;
    private readonly Mock<IUserDataManager> _userDataRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ItemsController _controller;
    private readonly User _user;
    private readonly Guid _userId;

    public ItemsControllerTests()
    {
        _userManager = new Mock<IUserManager>();
        _libraryManager = new Mock<ILibraryManager>();
        _localization = new Mock<ILocalizationManager>();
        _dtoService = new Mock<IDtoService>();
        _sessionManager = new Mock<ISessionManager>();
        _userDataRepository = new Mock<IUserDataManager>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _user = new User(
            "itemTester",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        _user.AddDefaultPermissions();
        _user.AddDefaultPreferences();
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

        _controller = new ItemsController(
            _userManager.Object,
            _libraryManager.Object,
            _localization.Object,
            _dtoService.Object,
            NullLogger<ItemsController>.Instance,
            _sessionManager.Object,
            _userDataRepository.Object,
            _memoryCache);

        var identity = new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.UserId, _userId.ToString("N")),
                new Claim(ClaimTypes.Role, UserRoles.Administrator)
            ],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private Task<ActionResult<QueryResult<BaseItemDto>>> CallGetItems(
        Guid? userId = null,
        int? startIndex = 0,
        int? limit = 10,
        int? imageTypeLimit = null,
        int? minWidth = null,
        int? minHeight = null,
        int? maxWidth = null,
        int? maxHeight = null,
        Guid? parentId = null)
    {
        return _controller.GetItems(
            userId: userId ?? _userId,
            maxOfficialRating: null,
            hasThemeSong: null,
            hasThemeVideo: null,
            hasSubtitles: null,
            hasSpecialFeature: null,
            hasTrailer: null,
            adjacentTo: null,
            indexNumber: null,
            parentIndexNumber: null,
            hasParentalRating: null,
            isHd: null,
            is4K: null,
            locationTypes: [],
            excludeLocationTypes: [],
            isMissing: null,
            isUnaired: null,
            minCommunityRating: null,
            minCriticRating: null,
            minPremiereDate: null,
            minDateLastSaved: null,
            minDateLastSavedForUser: null,
            maxPremiereDate: null,
            hasOverview: null,
            hasImdbId: null,
            hasTmdbId: null,
            hasTvdbId: null,
            isMovie: null,
            isSeries: null,
            isNews: null,
            isKids: null,
            isSports: null,
            excludeItemIds: [],
            startIndex: startIndex,
            limit: limit,
            recursive: null,
            searchTerm: null,
            sortOrder: [],
            parentId: parentId,
            fields: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            filters: [],
            isFavorite: null,
            mediaTypes: [],
            imageTypes: [],
            sortBy: [],
            isPlayed: null,
            genres: [],
            officialRatings: [],
            tags: [],
            years: [],
            enableUserData: null,
            imageTypeLimit: imageTypeLimit,
            enableImageTypes: [],
            person: null,
            personIds: [],
            personTypes: [],
            studios: [],
            artists: [],
            excludeArtistIds: [],
            artistIds: [],
            albumArtistIds: [],
            contributingArtistIds: [],
            albums: [],
            albumIds: [],
            ids: [],
            videoTypes: [],
            minOfficialRating: null,
            isLocked: null,
            isPlaceHolder: null,
            hasOfficialRating: null,
            collapseBoxSetItems: null,
            minWidth: minWidth,
            minHeight: minHeight,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            is3D: null,
            seriesStatus: [],
            nameStartsWithOrGreater: null,
            nameStartsWith: null,
            nameLessThan: null,
            studioIds: [],
            genreIds: [],
            audioLanguages: [],
            subtitleLanguages: [],
            enableTotalRecordCount: true,
            enableImages: true);
    }

    [Fact]
    public async Task GetItems_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await CallGetItems(startIndex: -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetItems_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await CallGetItems(limit: -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetItems_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await CallGetItems(imageTypeLimit: -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetItems_WithNegativeMinWidth_ReturnsBadRequest()
    {
        var result = await CallGetItems(minWidth: -5);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("MinWidth must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetItems_WithEmptyParentId_ReturnsBadRequest()
    {
        var result = await CallGetItems(parentId: Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Parent id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetItems_WhenUserNotFound_ThrowsResourceNotFoundException()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => CallGetItems(userId: nonExistentUser));
    }

    [Fact]
    public async Task GetResumeItems_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetResumeItems(
            userId: _userId,
            startIndex: -1,
            limit: 10,
            searchTerm: null,
            parentId: null,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetResumeItems_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetResumeItems(
            userId: _userId,
            startIndex: 0,
            limit: -1,
            searchTerm: null,
            parentId: null,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetResumeItems_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetResumeItems(
            userId: _userId,
            startIndex: 0,
            limit: 10,
            searchTerm: null,
            parentId: null,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: -1,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetResumeItems_WithEmptyParentId_ReturnsBadRequest()
    {
        var result = await _controller.GetResumeItems(
            userId: _userId,
            startIndex: 0,
            limit: 10,
            searchTerm: null,
            parentId: Guid.Empty,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Parent id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetResumeItems_WhenUserNotFound_ReturnsNotFound()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = await _controller.GetResumeItems(
            userId: nonExistentUser,
            startIndex: 0,
            limit: 10,
            searchTerm: null,
            parentId: null,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetResumeItems_WithValidUser_ReturnsQueryResult()
    {
        var queryResult = new QueryResult<BaseItem>
        {
            Items = Array.Empty<BaseItem>(),
            TotalRecordCount = 0
        };

        _libraryManager
            .Setup(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(queryResult);

        var result = await _controller.GetResumeItems(
            userId: _userId,
            startIndex: 0,
            limit: 10,
            searchTerm: null,
            parentId: null,
            fields: [],
            mediaTypes: [],
            enableUserData: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            excludeItemTypes: [],
            includeItemTypes: [],
            enableTotalRecordCount: true,
            enableImages: true,
            excludeActiveSessions: false);

        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.TotalRecordCount);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public void GetItemUserData_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = _controller.GetItemUserData(_userId, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetItemUserData_WhenUserNotFound_ReturnsNotFound()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = _controller.GetItemUserData(nonExistentUser, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetItemUserData_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, _user)).Returns((BaseItem?)null);

        var result = _controller.GetItemUserData(_userId, itemId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetItemUserData_WithValidItem_ReturnsUserDataDto()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId };
        var expectedDto = new UserItemDataDto { Key = itemId.ToString() };

        _libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, _user)).Returns(item);
        _userDataRepository.Setup(m => m.GetUserDataDto(item, _user)).Returns(expectedDto);

        var result = _controller.GetItemUserData(_userId, itemId);

        Assert.Same(expectedDto, result.Value);
    }

    [Fact]
    public void UpdateItemUserData_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = _controller.UpdateItemUserData(_userId, Guid.Empty, new UpdateUserItemDataDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateItemUserData_WithNullDto_ReturnsBadRequest()
    {
        var result = _controller.UpdateItemUserData(_userId, Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User data is required.", badRequest.Value);
    }

    [Fact]
    public void UpdateItemUserData_WhenUserNotFound_ReturnsNotFound()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = _controller.UpdateItemUserData(nonExistentUser, Guid.NewGuid(), new UpdateUserItemDataDto());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void UpdateItemUserData_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, _user)).Returns((BaseItem?)null);

        var result = _controller.UpdateItemUserData(_userId, itemId, new UpdateUserItemDataDto());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void UpdateItemUserData_WithValidItemAndDto_SavesAndReturnsUserDataDto()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId };
        var updateDto = new UpdateUserItemDataDto { Played = true };
        var expectedDto = new UserItemDataDto { Key = itemId.ToString(), Played = true };

        _libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, _user)).Returns(item);
        _userDataRepository.Setup(m => m.GetUserDataDto(item, _user)).Returns(expectedDto);

        var result = _controller.UpdateItemUserData(_userId, itemId, updateDto);

        Assert.Same(expectedDto, result.Value);
        _userDataRepository.Verify(
            m => m.SaveUserData(_user, item, updateDto, UserDataSaveReason.UpdateUserData),
            Times.Once);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
