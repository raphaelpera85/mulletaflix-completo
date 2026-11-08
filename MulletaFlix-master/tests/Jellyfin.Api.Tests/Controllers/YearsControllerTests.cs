using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class YearsControllerTests
{
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Guid _userId;
    private readonly User _testUser;
    private readonly YearsController _controller;

    public YearsControllerTests()
    {
        _libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);
        _userManager = new Mock<IUserManager>(MockBehavior.Loose);
        _dtoService = new Mock<IDtoService>(MockBehavior.Loose);
        _userId = Guid.NewGuid();
        _testUser = new User("yearTester", "testAuth", "testReset");

        _userManager.Setup(m => m.GetUserById(_userId)).Returns(_testUser);

        _controller = new YearsController(
            _libraryManager.Object,
            _userManager.Object,
            _dtoService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
                            new Claim(ClaimTypes.Role, UserRoles.Administrator)
                        },
                        "Custom"))
                }
            }
        };
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GetYears_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = _controller.GetYears(
            startIndex, null, [], null, [], [], [], [], [], null, null, [], _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void GetYears_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = _controller.GetYears(
            null, limit, [], null, [], [], [], [], [], null, null, [], _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GetYears_NegativeImageTypeLimit_ReturnsBadRequest(int imageTypeLimit)
    {
        var result = _controller.GetYears(
            null, null, [], null, [], [], [], [], [], null, imageTypeLimit, [], _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1999)]
    public async Task GetYear_NonPositiveYear_ReturnsBadRequest(int year)
    {
        var result = await _controller.GetYear(year, _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetYear_NotFound_ReturnsNotFound()
    {
        _libraryManager.Setup(m => m.GetYear(2025)).Returns((Year?)null);

        var result = await _controller.GetYear(2025, _userId);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
