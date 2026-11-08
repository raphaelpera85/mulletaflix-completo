using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.LibraryDtos;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class LibraryControllerTests
{
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IUserManager> _mockUserManager;
    private readonly Mock<ICollectionManager> _mockCollectionManager;
    private readonly Mock<IDtoService> _mockDtoService;
    private readonly Mock<ILibraryMonitor> _mockLibraryMonitor;
    private readonly User _testUser;
    private readonly LibraryController _subject;

    public LibraryControllerTests()
    {
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockUserManager = new Mock<IUserManager>();
        _mockCollectionManager = new Mock<ICollectionManager>();
        _mockDtoService = new Mock<IDtoService>();
        _mockLibraryMonitor = new Mock<ILibraryMonitor>();

        _testUser = new User(
            "testuser",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        _testUser.AddDefaultPermissions();
        _testUser.AddDefaultPreferences();

        _mockUserManager.Setup(m => m.GetUserById(_testUser.Id)).Returns(_testUser);

        _subject = new LibraryController(
            Mock.Of<IProviderManager>(),
            Mock.Of<ISimilarItemsManager>(),
            _mockLibraryManager.Object,
            _mockUserManager.Object,
            _mockCollectionManager.Object,
            _mockDtoService.Object,
            Mock.Of<IActivityManager>(),
            Mock.Of<ILocalizationManager>(),
            _mockLibraryMonitor.Object,
            NullLogger<LibraryController>.Instance,
            Mock.Of<IServerConfigurationManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(InternalClaimTypes.UserId, _testUser.Id.ToString("N"))
                            },
                            "TestAuth"))
                }
            }
        };
    }

    [Fact]
    public void GetFile_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.GetFile(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetThemeSongs_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetThemeSongs(Guid.Empty, _testUser.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetThemeVideos_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetThemeVideos(Guid.Empty, _testUser.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetThemeMedia_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetThemeMedia(Guid.Empty, _testUser.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetAncestors_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetAncestors(Guid.Empty, _testUser.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteItem_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.DeleteItem(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteItems_NullIds_ReturnsBadRequest()
    {
        var result = await _subject.DeleteItems(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids are required.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteItems_EmptyIds_ReturnsBadRequest()
    {
        var result = await _subject.DeleteItems(Array.Empty<Guid>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids are required.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteItems_ContainsEmptyGuid_ReturnsBadRequest()
    {
        var result = await _subject.DeleteItems(new[] { Guid.NewGuid(), Guid.Empty });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PostUpdatedSeries_NullOrWhitespaceTvdbId_ReturnsBadRequest(string? tvdbId)
    {
        var result = _subject.PostUpdatedSeries(tvdbId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("TvdbId is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void PostUpdatedMovies_BothTmdbAndImdbNullOrWhitespace_ReturnsBadRequest(string? tmdbId, string? imdbId)
    {
        var result = _subject.PostUpdatedMovies(tmdbId, imdbId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Either tmdbId or imdbId is required.", badRequest.Value);
    }

    [Fact]
    public void PostUpdatedMedia_NullDto_ReturnsBadRequest()
    {
        var result = _subject.PostUpdatedMedia(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Media update info is required.", badRequest.Value);
    }

    [Fact]
    public void PostUpdatedMedia_NullUpdates_ReturnsBadRequest()
    {
        var result = _subject.PostUpdatedMedia(new MediaUpdateInfoDto { Updates = null! });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Updates collection is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PostUpdatedMedia_NullOrWhitespacePath_ReturnsBadRequest(string? path)
    {
        var result = _subject.PostUpdatedMedia(new MediaUpdateInfoDto
        {
            Updates = new[] { new MediaUpdateInfoPathDto { Path = path! } }
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Update path is required.", badRequest.Value);
    }

    [Fact]
    public async Task GetDownload_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetDownload(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetItemCollections_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetItemCollections(Guid.Empty, _testUser.Id, null, null, Array.Empty<ItemFields>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetItemCollections_NegativeStartIndex_ReturnsBadRequest()
    {
        var result = await _subject.GetItemCollections(Guid.NewGuid(), _testUser.Id, -1, null, Array.Empty<ItemFields>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetItemCollections_NegativeLimit_ReturnsBadRequest()
    {
        var result = await _subject.GetItemCollections(Guid.NewGuid(), _testUser.Id, null, -1, Array.Empty<ItemFields>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetSimilarItems_NegativeLimit_ReturnsBadRequest()
    {
        var result = await _subject.GetSimilarItems(Guid.NewGuid(), Array.Empty<Guid>(), _testUser.Id, -1, Array.Empty<ItemFields>(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }
}
