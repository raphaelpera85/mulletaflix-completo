using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class TvShowsControllerTests
{
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Mock<ITVSeriesManager> _tvSeriesManager;
    private readonly TvShowsController _controller;
    private readonly User _user;
    private readonly Guid _seriesId;
    private readonly Guid _seasonId;

    public TvShowsControllerTests()
    {
        _userManager = new Mock<IUserManager>();
        _libraryManager = new Mock<ILibraryManager>();
        _dtoService = new Mock<IDtoService>();
        _tvSeriesManager = new Mock<ITVSeriesManager>();

        _user = new User(
            "tvTester",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        _user.AddDefaultPermissions();
        _user.AddDefaultPreferences();

        _seriesId = Guid.NewGuid();
        _seasonId = Guid.NewGuid();

        _userManager.Setup(m => m.GetUserById(_user.Id)).Returns(_user);

        _controller = new TvShowsController(
            _userManager.Object,
            _libraryManager.Object,
            _dtoService.Object,
            _tvSeriesManager.Object);

        var identity = new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.UserId, _user.Id.ToString("N")),
                new Claim(ClaimTypes.Role, UserRoles.Administrator)
            ],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetNextUp_WhenUserNotFound_ReturnsNotFound()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = await _controller.GetNextUp(
            userId: nonExistentUser,
            startIndex: null,
            limit: null,
            fields: [],
            seriesId: null,
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetNextUp_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetNextUp(
            userId: _user.Id,
            startIndex: -1,
            limit: 10,
            fields: [],
            seriesId: null,
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetNextUp_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetNextUp(
            userId: _user.Id,
            startIndex: 0,
            limit: -5,
            fields: [],
            seriesId: null,
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetNextUp_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetNextUp(
            userId: _user.Id,
            startIndex: 0,
            limit: 10,
            fields: [],
            seriesId: null,
            parentId: null,
            enableImages: null,
            imageTypeLimit: -1,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetNextUp_WithEmptySeriesId_ReturnsBadRequest()
    {
        var result = await _controller.GetNextUp(
            userId: _user.Id,
            startIndex: 0,
            limit: 10,
            fields: [],
            seriesId: Guid.Empty,
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Series id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetNextUp_WithValidParameters_ReturnsNextUpEpisodes()
    {
        _tvSeriesManager
            .Setup(m => m.GetNextUp(It.IsAny<NextUpQuery>(), It.IsAny<DtoOptions>()))
            .Returns(new QueryResult<BaseItem>(0, 0, new List<BaseItem>()));
        _dtoService
            .Setup(m => m.GetBaseItemDtosAsync(It.IsAny<IReadOnlyList<BaseItem>>(), It.IsAny<DtoOptions>(), _user))
            .ReturnsAsync(new List<BaseItemDto>());

        var result = await _controller.GetNextUp(
            userId: _user.Id,
            startIndex: 0,
            limit: 10,
            fields: [],
            seriesId: null,
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            nextUpDateCutoff: null);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task GetUpcomingEpisodes_WhenUserNotFound_ReturnsUnauthorized()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = await _controller.GetUpcomingEpisodes(
            userId: nonExistentUser,
            startIndex: null,
            limit: null,
            fields: [],
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetUpcomingEpisodes_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetUpcomingEpisodes(
            userId: _user.Id,
            startIndex: -1,
            limit: 10,
            fields: [],
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetUpcomingEpisodes_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetUpcomingEpisodes(
            userId: _user.Id,
            startIndex: 0,
            limit: -1,
            fields: [],
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetUpcomingEpisodes_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetUpcomingEpisodes(
            userId: _user.Id,
            startIndex: 0,
            limit: 10,
            fields: [],
            parentId: null,
            enableImages: null,
            imageTypeLimit: -2,
            enableImageTypes: [],
            enableUserData: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetUpcomingEpisodes_WithValidParameters_ReturnsUpcomingEpisodes()
    {
        _libraryManager
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>());
        _dtoService
            .Setup(m => m.GetBaseItemDtosAsync(It.IsAny<IReadOnlyList<BaseItem>>(), It.IsAny<DtoOptions>(), _user))
            .ReturnsAsync(new List<BaseItemDto>());

        var result = await _controller.GetUpcomingEpisodes(
            userId: _user.Id,
            startIndex: 0,
            limit: 10,
            fields: [],
            parentId: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task GetEpisodes_WithEmptySeriesId_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: Guid.Empty,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Series id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WithEmptySeasonId_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: Guid.Empty,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Season id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WithNegativeSeasonNumber_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: -1,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Season number must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: -1,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WithNegativeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: -10,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: -1,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetEpisodes_WhenUserUnauthorized_ReturnsUnauthorized()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: nonExistentUser,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetEpisodes_WhenSeriesNotFound_ReturnsNotFound()
    {
        _libraryManager.Setup(m => m.GetItemById<Series>(_seriesId, _user)).Returns((Series?)null);

        var result = await _controller.GetEpisodes(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            season: null,
            seasonId: null,
            isMissing: null,
            adjacentTo: null,
            startItemId: null,
            startIndex: null,
            limit: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null,
            sortBy: null);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSeasons_WithEmptySeriesId_ReturnsBadRequest()
    {
        var result = await _controller.GetSeasons(
            seriesId: Guid.Empty,
            userId: _user.Id,
            fields: [],
            isSpecialSeason: null,
            isMissing: null,
            adjacentTo: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Series id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetSeasons_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetSeasons(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            isSpecialSeason: null,
            isMissing: null,
            adjacentTo: null,
            enableImages: null,
            imageTypeLimit: -1,
            enableImageTypes: [],
            enableUserData: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSeasons_WhenUserUnauthorized_ReturnsUnauthorized()
    {
        var nonExistentUser = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUser)).Returns((User?)null);

        var result = await _controller.GetSeasons(
            seriesId: _seriesId,
            userId: nonExistentUser,
            fields: [],
            isSpecialSeason: null,
            isMissing: null,
            adjacentTo: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetSeasons_WhenSeriesNotFound_ReturnsNotFound()
    {
        _libraryManager.Setup(m => m.GetItemById<Series>(_seriesId, _user)).Returns((Series?)null);

        var result = await _controller.GetSeasons(
            seriesId: _seriesId,
            userId: _user.Id,
            fields: [],
            isSpecialSeason: null,
            isMissing: null,
            adjacentTo: null,
            enableImages: null,
            imageTypeLimit: null,
            enableImageTypes: [],
            enableUserData: null);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
