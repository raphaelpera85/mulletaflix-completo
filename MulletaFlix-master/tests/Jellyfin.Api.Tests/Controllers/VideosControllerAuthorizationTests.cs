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
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class VideosControllerAuthorizationTests
{
    [Fact]
    public async Task GetAdditionalPart_AuthenticatedUserCannotBeResolved_ReturnsUnauthorizedWithoutLibraryLookup()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(userId)).Returns((User?)null);

        var controller = new VideosController(
            libraryManager.Object,
            userManager.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new TransientMediaItemRegistry())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                        "TestAuth"))
                }
            }
        };

        var result = await controller.GetAdditionalPart(itemId, null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(itemId, It.IsAny<User?>()), Times.Never);
        libraryManager.Verify(library => library.GetUserRootFolder(), Times.Never);
        libraryManager.VerifyGet(library => library.RootFolder, Times.Never);
    }

    [Fact]
    public async Task GetAdditionalPart_HiddenAdditionalPart_IsNotReturnedOrLookedUpGlobally()
    {
        var user = new User(
            "video-reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var itemId = Guid.NewGuid();
        var additionalPartId = Guid.NewGuid();
        var video = new Video
        {
            Id = itemId,
            AdditionalParts = ["hidden-part.mkv"]
        };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(itemId, user)).Returns(video);
        libraryManager.Setup(library => library.GetNewItemId("hidden-part.mkv", typeof(Video))).Returns(additionalPartId);
        libraryManager.Setup(library => library.GetItemById<Video>(additionalPartId, user)).Returns((Video?)null);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var controller = new VideosController(
            libraryManager.Object,
            userManager.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new TransientMediaItemRegistry())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
                        "TestAuth"))
                }
            }
        };

        var result = await controller.GetAdditionalPart(itemId, null);

        var queryResult = Assert.IsType<QueryResult<BaseItemDto>>(result.Value);
        Assert.Empty(queryResult.Items);
        libraryManager.Verify(library => library.GetItemById<Video>(additionalPartId, user), Times.Once);
        libraryManager.Verify(library => library.GetItemById<Video>(additionalPartId), Times.Never);
    }

    [Fact]
    public async Task GetAdditionalPart_VisibleAdditionalPart_IsReturnedThroughUserScopedLookup()
    {
        var user = new User(
            "video-reader-visible",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var itemId = Guid.NewGuid();
        var additionalPartId = Guid.NewGuid();
        var video = new Video
        {
            Id = itemId,
            AdditionalParts = ["visible-part.mkv"]
        };
        var additionalPart = new Video { Id = additionalPartId, SortName = "visible-part" };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(itemId, user)).Returns(video);
        libraryManager.Setup(library => library.GetNewItemId("visible-part.mkv", typeof(Video))).Returns(additionalPartId);
        libraryManager.Setup(library => library.GetItemById<Video>(additionalPartId, user)).Returns(additionalPart);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var dto = new BaseItemDto { Id = additionalPartId };
        var dtoService = new Mock<IDtoService>();
        dtoService.Setup(service => service.GetBaseItemDtoAsync(additionalPart, It.IsAny<DtoOptions>(), user, video))
            .ReturnsAsync(dto);

        var controller = new VideosController(
            libraryManager.Object,
            userManager.Object,
            null!,
            dtoService.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new TransientMediaItemRegistry())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
                        "TestAuth"))
                }
            }
        };

        var result = await controller.GetAdditionalPart(itemId, null);

        var queryResult = Assert.IsType<QueryResult<BaseItemDto>>(result.Value);
        Assert.Same(dto, Assert.Single(queryResult.Items));
        libraryManager.Verify(library => library.GetItemById<Video>(additionalPartId, user), Times.Once);
        libraryManager.Verify(library => library.GetItemById<Video>(additionalPartId), Times.Never);
    }
}
