using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using MediaBrowser.Controller;
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
        var (user, userConfigurationRoot) = CreateUserWithProfileImage();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.DeleteFile(user.ProfileImage!.Path)).Throws(new IOException("disk unavailable"));
        var controller = CreateController(userManager, fileSystem, user, userConfigurationRoot);

        try
        {
            var result = await controller.DeleteUserImage(user.Id);

            Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, ((ObjectResult)result).StatusCode);
            Assert.NotNull(user.ProfileImage);
            userManager.Verify(manager => manager.ClearProfileImageAsync(It.IsAny<User>()), Times.Never);
        }
        finally
        {
            Directory.Delete(userConfigurationRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteUserImage_WhenFileDeleteSucceeds_ClearsImageReference()
    {
        var (user, userConfigurationRoot) = CreateUserWithProfileImage();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);
        var fileSystem = new Mock<IFileSystem>();
        var controller = CreateController(userManager, fileSystem, user, userConfigurationRoot);

        try
        {
            var result = await controller.DeleteUserImage(user.Id);

            Assert.IsType<NoContentResult>(result);
            fileSystem.Verify(fs => fs.DeleteFile(user.ProfileImage!.Path), Times.Once);
            userManager.Verify(manager => manager.ClearProfileImageAsync(user), Times.Once);
            userManager.Verify(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id), Times.Once);
        }
        finally
        {
            Directory.Delete(userConfigurationRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteUserImage_WhenProfilePathIsOutsideUserDirectory_DoesNotDeleteFileOrClearReference()
    {
        var user = CreateUser();
        var userConfigurationRoot = Path.Combine(Path.GetTempPath(), "mfx-user-image-delete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(userConfigurationRoot, user.Username));
        var outsidePath = Path.Combine(Path.GetTempPath(), "mfx-outside-profile-" + Guid.NewGuid().ToString("N") + ".png");
        user.ProfileImage = new ImageInfo(outsidePath);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);
        var fileSystem = new Mock<IFileSystem>();
        var controller = CreateController(userManager, fileSystem, user, userConfigurationRoot);

        try
        {
            var result = await controller.DeleteUserImage(user.Id);

            Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, ((ObjectResult)result).StatusCode);
            Assert.Equal(outsidePath, user.ProfileImage.Path);
            fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
            userManager.Verify(manager => manager.ClearProfileImageAsync(It.IsAny<User>()), Times.Never);
        }
        finally
        {
            Directory.Delete(userConfigurationRoot, recursive: true);
        }
    }

    private static User CreateUser()
        => new("test", "test", "test")
        {
            Id = Guid.NewGuid()
        };

    private static (User User, string UserConfigurationRoot) CreateUserWithProfileImage()
    {
        var user = CreateUser();
        var root = Path.Combine(Path.GetTempPath(), "mfx-user-image-delete-" + Guid.NewGuid().ToString("N"));
        var userDirectory = Path.Combine(root, user.Username);
        Directory.CreateDirectory(userDirectory);
        user.ProfileImage = new ImageInfo(Path.Combine(userDirectory, "profile.png"));
        return (user, root);
    }

    private static ImageController CreateController(Mock<IUserManager> userManager, Mock<IFileSystem> fileSystem, User user, string userConfigurationRoot)
    {
        userManager
            .Setup(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id))
            .ReturnsAsync(Mock.Of<IDisposable>());

        var applicationPaths = Mock.Of<IServerApplicationPaths>(paths => paths.UserConfigurationDirectoryPath == userConfigurationRoot);
        var configurationManager = Mock.Of<IServerConfigurationManager>(manager => manager.ApplicationPaths == applicationPaths);
        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            fileSystem.Object,
            NullLogger<ImageController>.Instance,
            configurationManager,
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
