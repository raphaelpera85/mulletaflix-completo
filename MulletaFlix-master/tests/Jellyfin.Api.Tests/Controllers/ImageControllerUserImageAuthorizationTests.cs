using System;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.IO;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Enums;
using MediaBrowser.Common.Net;
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
