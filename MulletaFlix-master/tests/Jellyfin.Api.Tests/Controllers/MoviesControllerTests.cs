using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
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

public class MoviesControllerTests
{
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Mock<ISimilarItemsManager> _similarItemsManager;
    private readonly MoviesController _controller;
    private readonly User _user;

    public MoviesControllerTests()
    {
        _userManager = new Mock<IUserManager>();
        _dtoService = new Mock<IDtoService>();
        _similarItemsManager = new Mock<ISimilarItemsManager>();

        _user = new User(
            "movieTester",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        _user.AddDefaultPermissions();
        _user.AddDefaultPreferences();

        _userManager.Setup(m => m.GetUserById(_user.Id)).Returns(_user);

        _controller = new MoviesController(
            _userManager.Object,
            _dtoService.Object,
            _similarItemsManager.Object);

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
    public async Task GetMovieRecommendations_WithNegativeCategoryLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetMovieRecommendations(
            userId: _user.Id,
            parentId: null,
            fields: [],
            categoryLimit: -1,
            itemLimit: 5);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("CategoryLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetMovieRecommendations_WithNegativeItemLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetMovieRecommendations(
            userId: _user.Id,
            parentId: null,
            fields: [],
            categoryLimit: 5,
            itemLimit: -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ItemLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetMovieRecommendations_WhenUserNotFoundAndNotApiKey_ReturnsUnauthorized()
    {
        var nonExistentUserId = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(nonExistentUserId)).Returns((User?)null);

        var result = await _controller.GetMovieRecommendations(
            userId: nonExistentUserId,
            parentId: null,
            fields: [],
            categoryLimit: 5,
            itemLimit: 5);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetMovieRecommendations_WithValidParameters_ReturnsRecommendations()
    {
        _similarItemsManager
            .Setup(m => m.GetMovieRecommendationsAsync(
                _user,
                Guid.Empty,
                5,
                8,
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SimilarItemsRecommendation>());

        var result = await _controller.GetMovieRecommendations(
            userId: _user.Id,
            parentId: null,
            fields: [],
            categoryLimit: 5,
            itemLimit: 8);

        var okResult = Assert.IsType<MulletaFlix.Api.Results.OkResult<IEnumerable<RecommendationDto>>>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}
