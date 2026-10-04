using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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
}
