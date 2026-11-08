using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class GenresControllerTests
{
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Guid _userId;
    private readonly User _testUser;
    private readonly GenresController _genresController;
    private readonly MusicGenresController _musicGenresController;

    public GenresControllerTests()
    {
        _userManager = new Mock<IUserManager>(MockBehavior.Loose);
        _libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);
        _dtoService = new Mock<IDtoService>(MockBehavior.Loose);
        _userId = Guid.NewGuid();
        _testUser = new User("genreTester", "testAuth", "testReset");

        _userManager.Setup(m => m.GetUserById(_userId)).Returns(_testUser);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
                    new Claim(ClaimTypes.Role, UserRoles.Administrator)
                },
                "Custom"))
        };

        _genresController = new GenresController(
            _userManager.Object,
            _libraryManager.Object,
            _dtoService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        _musicGenresController = new MusicGenresController(
            _libraryManager.Object,
            _userManager.Object,
            _dtoService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GetGenres_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = _genresController.GetGenres(
            startIndex, null, null, null, [], [], [], null, null, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void GetGenres_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = _genresController.GetGenres(
            null, limit, null, null, [], [], [], null, null, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GetGenres_NegativeImageTypeLimit_ReturnsBadRequest(int imageTypeLimit)
    {
        var result = _genresController.GetGenres(
            null, null, null, null, [], [], [], null, imageTypeLimit, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetGenre_EmptyGenreName_ReturnsBadRequest(string? genreName)
    {
        var result = await _genresController.GetGenre(genreName!, _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GetMusicGenres_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = _musicGenresController.GetMusicGenres(
            startIndex, null, null, null, [], [], [], null, null, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void GetMusicGenres_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = _musicGenresController.GetMusicGenres(
            null, limit, null, null, [], [], [], null, null, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void GetMusicGenres_NegativeImageTypeLimit_ReturnsBadRequest(int imageTypeLimit)
    {
        var result = _musicGenresController.GetMusicGenres(
            null, null, null, null, [], [], [], null, imageTypeLimit, [], _userId, null, null, null, [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMusicGenre_EmptyGenreName_ReturnsBadRequest(string? genreName)
    {
        var result = await _musicGenresController.GetMusicGenre(genreName!, _userId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
