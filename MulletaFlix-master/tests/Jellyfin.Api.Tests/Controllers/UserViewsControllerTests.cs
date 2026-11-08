using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MulletaFlix.Api.Caching;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class UserViewsControllerTests : IDisposable
{
    private readonly MemoryCache _memoryCache;
    private readonly ItemsResponseCache _cache;
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<IUserViewManager> _userViewManager;
    private readonly Mock<IDtoService> _dtoService;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Guid _userId;
    private readonly User _testUser;
    private readonly UserViewsController _controller;

    public UserViewsControllerTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new ItemsResponseCache(_memoryCache);
        _userManager = new Mock<IUserManager>(MockBehavior.Loose);
        _userViewManager = new Mock<IUserViewManager>(MockBehavior.Loose);
        _dtoService = new Mock<IDtoService>(MockBehavior.Loose);
        _libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);

        _userId = Guid.NewGuid();
        _testUser = new User("testUser", "testAuth", "testReset");

        _userManager.Setup(m => m.GetUserById(_userId)).Returns(_testUser);
        _userViewManager.Setup(m => m.GetUserViewsAsync(It.IsAny<UserViewQuery>()))
            .ReturnsAsync(Array.Empty<Folder>());
        _dtoService.Setup(m => m.GetBaseItemDtosAsync(It.IsAny<IReadOnlyList<BaseItem>>(), It.IsAny<DtoOptions>(), It.IsAny<User>()))
            .ReturnsAsync(Array.Empty<BaseItemDto>());

        _controller = new UserViewsController(
            _userManager.Object,
            _userViewManager.Object,
            _dtoService.Object,
            _libraryManager.Object,
            _cache)
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

    [Fact]
    public async Task GetUserViews_NullPresetViews_DoesNotThrowNullReferenceException()
    {
        var result = await _controller.GetUserViews(_userId, null, null!, false);

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task GetUserViews_UserNotFound_ThrowsResourceNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(unknownId)).Returns((User?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _controller.GetUserViews(unknownId, null, null!, false));
    }

    [Fact]
    public void GetGroupingOptions_UserNotFound_ReturnsNotFound()
    {
        var unknownId = Guid.NewGuid();
        _userManager.Setup(m => m.GetUserById(unknownId)).Returns((User?)null);

        var result = _controller.GetGroupingOptions(unknownId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetGroupingOptions_ValidUser_ReturnsOkResult()
    {
        var rootFolder = new Mock<Folder>();
        rootFolder.Setup(f => f.GetChildren(_testUser, true)).Returns(Array.Empty<BaseItem>());
        _libraryManager.Setup(m => m.GetUserRootFolder()).Returns(rootFolder.Object);

        var result = _controller.GetGroupingOptions(_userId);

        Assert.NotNull(result.Result);
        Assert.IsAssignableFrom<ActionResult>(result.Result);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
