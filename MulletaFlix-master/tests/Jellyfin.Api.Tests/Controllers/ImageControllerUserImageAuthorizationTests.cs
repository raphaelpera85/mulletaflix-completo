using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ProfileImageInfo = MulletaFlix.Database.Implementations.Entities.ImageInfo;
using User = MulletaFlix.Database.Implementations.Entities.User;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ImageControllerUserImageAuthorizationTests
{
    private static readonly string CacheTag = "public-user-image-tag";

    [Fact]
    public async Task PostUserImage_AcquiresSharedProfileImageUpdateLock()
    {
        var user = CreateUser();
        var testDataRoot = Path.Combine(Path.GetTempPath(), "mfx-profile-upload-" + Guid.NewGuid().ToString("N"));
        var mutationOrder = new List<string>();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id))
            .ReturnsAsync(Mock.Of<IDisposable>());
        userManager.Setup(manager => manager.ClearProfileImageAsync(user))
            .Callback(() => mutationOrder.Add("clear"))
            .Returns(Task.CompletedTask);
        userManager.Setup(manager => manager.UpdateUserAsync(user)).Returns(Task.CompletedTask);

        var providerManager = new Mock<IProviderManager>();
        providerManager.Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Stream, string, string>((_, _, _) => mutationOrder.Add("save"))
            .Returns(Task.CompletedTask);
        var appPaths = new Mock<IServerApplicationPaths>();
        appPaths.Setup(paths => paths.UserConfigurationDirectoryPath).Returns(testDataRoot);
        var serverConfigurationManager = new Mock<IServerConfigurationManager>();
        serverConfigurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(appPaths.Object);
        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            providerManager.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageController>.Instance,
            serverConfigurationManager.Object,
            appPaths.Object,
            Mock.Of<INetworkManager>(),
            Mock.Of<IDeviceManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreateAuthenticatedPrincipal(user.Id)
                }
            }
        };
        controller.Request.ContentType = "image/png";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String([1, 2, 3])));

        try
        {
            var result = await controller.PostUserImage(user.Id);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal(["save", "clear"], mutationOrder);
            userManager.Verify(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id), Times.Once);
        }
        finally
        {
            if (Directory.Exists(testDataRoot))
            {
                Directory.Delete(testDataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PostUserImage_WhenSavingReplacementFails_PreservesExistingProfileImage()
    {
        var user = CreateUser();
        var testDataRoot = Path.Combine(Path.GetTempPath(), "mfx-profile-upload-" + Guid.NewGuid().ToString("N"));
        var originalImage = user.ProfileImage;
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager.Setup(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id))
            .ReturnsAsync(Mock.Of<IDisposable>());
        userManager.Setup(manager => manager.ClearProfileImageAsync(user)).Returns(Task.CompletedTask);

        string? failedImagePath = null;
        var providerManager = new Mock<IProviderManager>();
        providerManager
            .Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Stream, string, string>((_, _, path) => failedImagePath = path)
            .ThrowsAsync(new IOException("simulated image write failure"));
        var fileSystem = new Mock<IFileSystem>();
        var appPaths = new Mock<IServerApplicationPaths>();
        appPaths.Setup(paths => paths.UserConfigurationDirectoryPath).Returns(testDataRoot);
        var serverConfigurationManager = new Mock<IServerConfigurationManager>();
        serverConfigurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(appPaths.Object);
        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            providerManager.Object,
            Mock.Of<IImageProcessor>(),
            fileSystem.Object,
            NullLogger<ImageController>.Instance,
            serverConfigurationManager.Object,
            appPaths.Object,
            Mock.Of<INetworkManager>(),
            Mock.Of<IDeviceManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreateAuthenticatedPrincipal(user.Id)
                }
            }
        };
        controller.Request.ContentType = "image/png";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String([1, 2, 3])));

        try
        {
            await Assert.ThrowsAsync<IOException>(() => controller.PostUserImage(user.Id));

            Assert.Same(originalImage, user.ProfileImage);
            userManager.Verify(manager => manager.ClearProfileImageAsync(It.IsAny<User>()), Times.Never);
            Assert.NotNull(failedImagePath);
            fileSystem.Verify(fs => fs.DeleteFile(failedImagePath), Times.Once);
        }
        finally
        {
            if (Directory.Exists(testDataRoot))
            {
                Directory.Delete(testDataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PostUserImage_WhenPersistingReplacementFails_RestoresPreviousProfileImageReference()
    {
        var user = CreateUser();
        var originalImage = user.ProfileImage!;
        var persistedUser = CreateUser();
        persistedUser.Id = user.Id;
        var testDataRoot = Path.Combine(Path.GetTempPath(), "mfx-profile-upload-" + Guid.NewGuid().ToString("N"));
        var lookups = 0;
        var updates = 0;
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id))
            .Returns(() => ++lookups <= 3 ? user : persistedUser);
        userManager.Setup(manager => manager.AcquireProfileImageUpdateLockAsync(user.Id))
            .ReturnsAsync(Mock.Of<IDisposable>());
        userManager.Setup(manager => manager.ClearProfileImageAsync(user))
            .Callback(() =>
            {
                user.ProfileImage = null;
                persistedUser.ProfileImage = null;
            })
            .Returns(Task.CompletedTask);
        userManager.Setup(manager => manager.UpdateUserAsync(It.IsAny<User>()))
            .Returns<User>(updatedUser =>
            {
                if (++updates == 1)
                {
                    return Task.FromException(new IOException("simulated database write failure"));
                }

                persistedUser.ProfileImage = updatedUser.ProfileImage;
                return Task.CompletedTask;
            });

        var providerManager = new Mock<IProviderManager>();
        providerManager.Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var appPaths = new Mock<IServerApplicationPaths>();
        appPaths.Setup(paths => paths.UserConfigurationDirectoryPath).Returns(testDataRoot);
        var serverConfigurationManager = new Mock<IServerConfigurationManager>();
        serverConfigurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(appPaths.Object);
        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            providerManager.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            NullLogger<ImageController>.Instance,
            serverConfigurationManager.Object,
            appPaths.Object,
            Mock.Of<INetworkManager>(),
            Mock.Of<IDeviceManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreateAuthenticatedPrincipal(user.Id)
                }
            }
        };
        controller.Request.ContentType = "image/png";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String([1, 2, 3])));

        try
        {
            await Assert.ThrowsAsync<IOException>(() => controller.PostUserImage(user.Id));

            Assert.NotNull(persistedUser.ProfileImage);
            Assert.Equal(originalImage.Path, persistedUser.ProfileImage.Path);
            Assert.Equal(originalImage.LastModified, persistedUser.ProfileImage.LastModified);
            Assert.Equal(2, updates);
        }
        finally
        {
            if (Directory.Exists(testDataRoot))
            {
                Directory.Delete(testDataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnonymousRequest_AllowsUserInPublicLoginList()
    {
        var user = CreateUser();
        var (controller, _, _) = CreateController(user, localNetwork: true);

        var result = await controller.GetUserImage(user.Id, CacheTag, null);

        Assert.IsNotType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(PermissionKind.IsHidden)]
    [InlineData(PermissionKind.IsDisabled)]
    public async Task AnonymousRequest_HidesUsersExcludedFromPublicLoginList(PermissionKind permission)
    {
        var user = CreateUser();
        user.SetPermission(permission, true);
        var (controller, _, _) = CreateController(user, localNetwork: true);

        var result = await controller.GetUserImage(user.Id, CacheTag, null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AnonymousRemoteRequest_RequiresRemoteAccessPermission()
    {
        var user = CreateUser();
        user.SetPermission(PermissionKind.EnableRemoteAccess, false);
        var (controller, networkManager, _) = CreateController(user, localNetwork: false);

        var result = await controller.GetUserImage(user.Id, CacheTag, null);

        Assert.IsType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticatedUser_CannotFetchHiddenOtherUsersProfileImage()
    {
        var target = CreateUser();
        target.SetPermission(PermissionKind.IsHidden, true);
        var caller = CreateUser();
        var principal = CreateAuthenticatedPrincipal(caller.Id);
        var (controller, _, _) = CreateController(target, principal, caller: caller, localNetwork: true);

        var result = await controller.GetUserImage(target.Id, null, null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AuthenticatedUser_CanFetchOwnHiddenProfileImage()
    {
        var user = CreateUser();
        user.SetPermission(PermissionKind.IsHidden, true);
        var principal = CreateAuthenticatedPrincipal(user.Id);
        var (controller, _, _) = CreateController(user, principal, caller: user, localNetwork: true);

        var result = await controller.GetUserImage(user.Id, null, null);

        Assert.IsNotType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Administrator_CanFetchHiddenUsersProfileImage()
    {
        var target = CreateUser();
        target.SetPermission(PermissionKind.IsHidden, true);
        var administrator = CreateUser();
        administrator.SetPermission(PermissionKind.IsAdministrator, true);
        var principal = CreateAuthenticatedPrincipal(administrator.Id);
        var (controller, _, _) = CreateController(target, principal, caller: administrator, localNetwork: true);

        var result = await controller.GetUserImage(target.Id, null, null);

        Assert.IsNotType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AuthenticatedPrincipalWithoutUserIdOrApiKey_IsDenied()
    {
        var target = CreateUser();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));
        var (controller, _, _) = CreateController(target, principal, localNetwork: true);

        var result = await controller.GetUserImage(target.Id, null, null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApiKeyWithoutUserId_RetainsAdministrativeAccess()
    {
        var target = CreateUser();
        target.SetPermission(PermissionKind.IsHidden, true);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(InternalClaimTypes.IsApiKey, "true")],
            "api-key"));
        var (controller, _, _) = CreateController(target, principal, localNetwork: true);

        var result = await controller.GetUserImage(target.Id, null, null);

        Assert.IsNotType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApiKeyWithUserId_RetainsAdministrativeAccess()
    {
        var target = CreateUser();
        target.SetPermission(PermissionKind.IsHidden, true);
        var caller = CreateUser();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.IsApiKey, "true"),
                new Claim(InternalClaimTypes.UserId, caller.Id.ToString())
            ],
            "api-key"));
        var (controller, _, _) = CreateController(target, principal, caller: caller, localNetwork: true);

        var result = await controller.GetUserImage(target.Id, null, null);

        Assert.IsNotType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UserImageResponse_DisablesSharedCaching()
    {
        var path = Path.Combine(Path.GetTempPath(), "mulletaflix-profile-image-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
        try
        {
            var user = CreateUser();
            user.ProfileImage!.Path = path;
            var (controller, _, _) = CreateController(user, localNetwork: true);

            _ = await controller.GetUserImage(user.Id, CacheTag, null);

            Assert.Contains("no-store", controller.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("public", controller.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AnonymousRequest_RequiresDeviceAccessWhenSetupIsComplete()
    {
        var user = CreateUser();
        var deviceId = "login-device";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(InternalClaimTypes.DeviceId, deviceId)
        ]));
        var (controller, _, deviceManager) = CreateController(user, principal, localNetwork: true);
        deviceManager.Setup(manager => manager.CanAccessDevice(user, deviceId)).Returns(false);

        var result = await controller.GetUserImage(user.Id, CacheTag, null);

        Assert.IsType<NotFoundResult>(result);
        deviceManager.Verify(manager => manager.CanAccessDevice(user, deviceId), Times.Once);
    }

    [Fact]
    public async Task AnonymousRequest_BeforeSetupMatchesPublicUsersAndSkipsDeviceNetworkFilters()
    {
        var user = CreateUser();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(InternalClaimTypes.DeviceId, "setup-device")
        ]));
        var (controller, networkManager, deviceManager) = CreateController(
            user,
            principal,
            startupWizardCompleted: false,
            localNetwork: false);

        var result = await controller.GetUserImage(user.Id, CacheTag, null);

        Assert.IsNotType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
        deviceManager.Verify(manager => manager.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PublicAndLegacyImageActions_DeclareAnonymousContract()
    {
        AssertAnonymous(nameof(ImageController.GetUserImage));
        AssertAnonymous(nameof(ImageController.GetUserImageLegacy));
        AssertAnonymous(nameof(ImageController.GetUserImageByIndexLegacy));
    }

    private static void AssertAnonymous(string methodName)
    {
        var method = typeof(ImageController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static User CreateUser()
    {
        var user = new User("public-user", "test-auth-provider", "test-password-reset-provider")
        {
            Id = Guid.NewGuid(),
            ProfileImage = new ProfileImageInfo("profile.png")
        };
        user.SetPermission(PermissionKind.IsHidden, false);
        user.SetPermission(PermissionKind.IsDisabled, false);
        user.SetPermission(PermissionKind.EnableRemoteAccess, true);
        return user;
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(Guid userId)
        => new(new ClaimsIdentity([new Claim(InternalClaimTypes.UserId, userId.ToString())], "test"));

    private static (ImageController Controller, Mock<INetworkManager> NetworkManager, Mock<IDeviceManager> DeviceManager) CreateController(
        User user,
        ClaimsPrincipal? principal = null,
        bool startupWizardCompleted = true,
        bool localNetwork = true,
        User? caller = null)
    {
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        userManager
            .Setup(manager => manager.AcquireProfileImageUpdateLockAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Mock.Of<IDisposable>());
        if (caller is not null)
        {
            userManager.Setup(manager => manager.GetUserById(caller.Id)).Returns(caller);
        }

        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration)
            .Returns(new ServerConfiguration { IsStartupWizardCompleted = startupWizardCompleted });

        var networkManager = new Mock<INetworkManager>();
        networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(localNetwork);
        var deviceManager = new Mock<IDeviceManager>();
        var imageProcessor = new Mock<IImageProcessor>();
        imageProcessor.SetupGet(processor => processor.SupportedInputFormats).Returns([".png"]);
        imageProcessor.Setup(processor => processor.ProcessImage(It.IsAny<ImageProcessingOptions>()))
            .ReturnsAsync((user.ProfileImage!.Path, "image/png", DateTime.UtcNow));

        var controller = new ImageController(
            userManager.Object,
            Mock.Of<ILibraryManager>(),
            Mock.Of<IProviderManager>(),
            imageProcessor.Object,
            Mock.Of<IFileSystem>(),
            NullLogger<ImageController>.Instance,
            configuration.Object,
            Mock.Of<MediaBrowser.Common.Configuration.IApplicationPaths>(),
            networkManager.Object,
            deviceManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        controller.Request.Headers.IfNoneMatch = $"\"{CacheTag}\"";
        return (controller, networkManager, deviceManager);
    }
}
