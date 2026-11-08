using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Api.Jobs;
using MulletaFlix.Data;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UserLibraryControllerTests
{
    private readonly Mock<IUserManager> _mockUserManager;
    private readonly Mock<IUserDataManager> _mockUserDataRepository;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IDtoService> _mockDtoService;
    private readonly Mock<IUserViewManager> _mockUserViewManager;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJobQueue> _mockJobQueue;
    private readonly TransientMediaItemRegistry _transientRegistry;
    private readonly Mock<INebulaFtpManager> _mockNebulaFtpManager;
    private readonly User _testUser;
    private readonly UserLibraryController _subject;

    public UserLibraryControllerTests()
    {
        _mockUserManager = new Mock<IUserManager>();
        _mockUserDataRepository = new Mock<IUserDataManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockDtoService = new Mock<IDtoService>();
        _mockUserViewManager = new Mock<IUserViewManager>();
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJobQueue = new Mock<IJobQueue>();
        _transientRegistry = new TransientMediaItemRegistry();
        _mockNebulaFtpManager = new Mock<INebulaFtpManager>();

        _testUser = new User(
            "testuser",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        _testUser.AddDefaultPermissions();
        _testUser.AddDefaultPreferences();

        _mockUserManager.Setup(m => m.GetUserById(_testUser.Id)).Returns(_testUser);

        _subject = new UserLibraryController(
            _mockUserManager.Object,
            _mockUserDataRepository.Object,
            _mockLibraryManager.Object,
            _mockDtoService.Object,
            _mockUserViewManager.Object,
            _mockFileSystem.Object,
            _mockJobQueue.Object,
            _transientRegistry,
            _mockNebulaFtpManager.Object)
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
    public void MarkFavoriteItem_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.MarkFavoriteItem(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void MarkFavoriteItemLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = _subject.MarkFavoriteItemLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void MarkFavoriteItemLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.MarkFavoriteItemLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UnmarkFavoriteItem_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.UnmarkFavoriteItem(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UnmarkFavoriteItemLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = _subject.UnmarkFavoriteItemLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UnmarkFavoriteItemLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.UnmarkFavoriteItemLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void DeleteUserItemRating_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.DeleteUserItemRating(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void DeleteUserItemRatingLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = _subject.DeleteUserItemRatingLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void DeleteUserItemRatingLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.DeleteUserItemRatingLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateUserItemRating_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.UpdateUserItemRating(_testUser.Id, Guid.Empty, true);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateUserItemRatingLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = _subject.UpdateUserItemRatingLegacy(Guid.Empty, Guid.NewGuid(), true);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateUserItemRatingLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = _subject.UpdateUserItemRatingLegacy(_testUser.Id, Guid.Empty, true);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetLocalTrailers_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetLocalTrailers(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetLocalTrailersLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetLocalTrailersLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetLocalTrailersLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetLocalTrailersLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetSpecialFeatures_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetSpecialFeatures(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetSpecialFeaturesLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetSpecialFeaturesLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetSpecialFeaturesLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetSpecialFeaturesLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetIntros_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetIntros(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetIntrosLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetIntrosLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetIntrosLegacy_EmptyItemId_ReturnsBadRequest()
    {
        var result = await _subject.GetIntrosLegacy(_testUser.Id, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetItemLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetItemLegacy(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetRootLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetRootFolderLegacy(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetLatestMedia_NegativeLimit_ReturnsBadRequest()
    {
        var result = await _subject.GetLatestMedia(_testUser.Id, null, Array.Empty<MediaBrowser.Model.Querying.ItemFields>(), Array.Empty<BaseItemKind>(), null, null, null, Array.Empty<MediaBrowser.Model.Entities.ImageType>(), null, -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetLatestMedia_NegativeImageTypeLimit_ReturnsBadRequest()
    {
        var result = await _subject.GetLatestMedia(_testUser.Id, null, Array.Empty<MediaBrowser.Model.Querying.ItemFields>(), Array.Empty<BaseItemKind>(), null, null, -1, Array.Empty<MediaBrowser.Model.Entities.ImageType>(), null, 10);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetLatestMediaLegacy_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _subject.GetLatestMediaLegacy(Guid.Empty, null, Array.Empty<MediaBrowser.Model.Querying.ItemFields>(), Array.Empty<BaseItemKind>(), null, null, null, Array.Empty<MediaBrowser.Model.Entities.ImageType>(), null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }
}
