using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ImageControllerDeleteUserImageTests
{
    [Fact]
    public async Task DeleteUserImage_WhenFileDeleteFails_ReturnsServerErrorAndKeepsImageReference()
    {
        var user = CreateUser();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.DeleteFile(user.ProfileImage!.Path)).Throws(new IOException("disk unavailable"));
        var controller = CreateController(userManager, fileSystem, user);

        var result = await controller.DeleteUserImage(user.Id);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, ((ObjectResult)result).StatusCode);
        Assert.NotNull(user.ProfileImage);
        userManager.Verify(manager => manager.ClearProfileImageAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserImage_WhenFileDeleteSucceeds_ClearsImageReference()
    {
        var user = CreateUser();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);
        var fileSystem = new Mock<IFileSystem>();
        var controller = CreateController(userManager, fileSystem, user);

        var result = await controller.DeleteUserImage(user.Id);

        Assert.IsType<NoContentResult>(result);
        fileSystem.Verify(fs => fs.DeleteFile(user.ProfileImage!.Path), Times.Once);
        userManager.Verify(manager => manager.ClearProfileImageAsync(user), Times.Once);
    }

    private static User CreateUser()
        => new("test", "test", "test")
        {
            Id = Guid.NewGuid(),
            ProfileImage = new ImageInfo("test-profile-image.png")
        };

    private static ImageController CreateController(Mock<IUserManager> userManager, Mock<IFileSystem> fileSystem, User user)
    {
        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            fileSystem.Object,
            NullLogger<ImageController>.Instance,
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<MediaBrowser.Common.Configuration.IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, user.Id.ToString())],
                        "test"))
                }
            }
        };
        return controller;
    }
}
