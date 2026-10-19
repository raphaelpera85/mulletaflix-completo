using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Data;
using Moq;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Plugin.GetAvatar.Configuration;
using MulletaFlix.Plugin.GetAvatar.Controllers;
using MulletaFlix.Plugin.GetAvatar.Services;
using Xunit;
using GetAvatarPlugin = MulletaFlix.Plugin.GetAvatar.Plugin;
using User = MulletaFlix.Database.Implementations.Entities.User;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection(GetAvatarPluginTestCollection.Name)]
public sealed class AvatarControllerUserAvatarAuthorizationTests
{
    [Fact]
    public async Task SaveAvatarAsync_WhenConfigurationPersistenceFails_RollsBackFileAndCatalogEntry()
    {
        using var context = new AvatarTestContext();
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService();

        await Assert.ThrowsAsync<IOException>(() => service.SaveAvatarAsync("new-avatar.png", [4, 5, 6]));

        Assert.Single(GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Equal("test-avatar", GetAvatarPlugin.Config.AvailableAvatars![0].Id);
        Assert.Equal(["test-avatar.png"], Directory.GetFiles(context.AvatarDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public void AnonymousLocalRequest_CanReadPublicUserAvatar_AndDisablesCaching()
    {
        using var context = new AvatarTestContext();
        var (controller, networkManager, _) = context.CreateController(context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        var image = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Once);
        image.FileStream.Dispose();
    }

    [Theory]
    [InlineData(PermissionKind.IsHidden)]
    [InlineData(PermissionKind.IsDisabled)]
    public void AnonymousRequest_CannotReadHiddenOrDisabledUserAvatar(PermissionKind permission)
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(permission, true);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AnonymousRemoteRequest_RequiresRemoteAccessPermission()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.EnableRemoteAccess, false);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, localNetwork: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Once);
    }

    [Fact]
    public void AnonymousRequest_RequiresTargetDeviceAccess()
    {
        using var context = new AvatarTestContext();
        var principal = CreatePrincipal(deviceId: "blocked-device");
        var (controller, networkManager, deviceManager) = context.CreateController(
            context.TargetUser,
            principal,
            deviceAccess: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        deviceManager.Verify(manager => manager.CanAccessDevice(context.TargetUser, "blocked-device"), Times.Once);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AuthenticatedOwner_CanReadOwnHiddenAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var principal = CreatePrincipal(userId: context.TargetUser.Id);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, principal, caller: context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void Administrator_CanReadHiddenUserAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var administrator = CreateUser();
        administrator.SetPermission(PermissionKind.IsAdministrator, true);
        var principal = CreatePrincipal(userId: administrator.Id);
        var (controller, networkManager, _) = context.CreateController(
            context.TargetUser,
            principal,
            caller: administrator);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void ApiKey_CanReadHiddenUserAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var principal = CreatePrincipal(apiKey: true);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, principal);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AuthenticatedOtherUser_CannotReadHiddenProfileAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var caller = CreateUser();
        var principal = CreatePrincipal(userId: caller.Id);
        var (controller, _, _) = context.CreateController(context.TargetUser, principal, caller: caller);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void BeforeSetup_PublicTargetRemainsVisibleWithoutNetworkOrDeviceChecks()
    {
        using var context = new AvatarTestContext();
        var principal = CreatePrincipal(deviceId: "setup-device");
        var (controller, networkManager, deviceManager) = context.CreateController(
            context.TargetUser,
            principal,
            startupWizardCompleted: false,
            localNetwork: false,
            deviceAccess: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
        deviceManager.Verify(manager => manager.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void MissingTargetUser_IsNotFound()
    {
        using var context = new AvatarTestContext();
        var (controller, _, _) = context.CreateController(context.TargetUser, includeTarget: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid? userId = null, bool apiKey = false, string? deviceId = null)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim("MulletaFlix-UserId", userId.Value.ToString()));
        }

        if (apiKey)
        {
            claims.Add(new Claim("MulletaFlix-IsApiKey", "true"));
        }

        if (deviceId is not null)
        {
            claims.Add(new Claim("MulletaFlix-DeviceId", deviceId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, userId.HasValue || apiKey ? "test" : null));
    }

    private static User CreateUser()
    {
        var user = new User("avatar-test-user", "test-auth-provider", "test-password-reset-provider")
        {
            Id = Guid.NewGuid()
        };
        user.SetPermission(PermissionKind.IsHidden, false);
        user.SetPermission(PermissionKind.IsDisabled, false);
        user.SetPermission(PermissionKind.EnableRemoteAccess, true);
        return user;
    }

    private sealed class AvatarTestContext : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "GetAvatarUserAuthorization", Guid.NewGuid().ToString("N"));
        private readonly Mock<IServerApplicationPaths> _applicationPaths = new();
        private readonly Mock<IServerConfigurationManager> _configurationManager = new();
        private readonly Mock<IXmlSerializer> _serializer = new();
        private GetAvatarPlugin? _previousPlugin;

        public AvatarTestContext()
        {
            Directory.CreateDirectory(_root);
            _applicationPaths.SetupGet(paths => paths.PluginConfigurationsPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.PluginsPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.DataPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.UserConfigurationDirectoryPath).Returns(Path.Combine(_root, "users"));
            _configurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(_applicationPaths.Object);
            _configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { IsStartupWizardCompleted = true });

            var avatarFile = Path.Combine(_root, "GetAvatar", "avatars", "test-avatar.png");
            Directory.CreateDirectory(Path.GetDirectoryName(avatarFile)!);
            File.WriteAllBytes(avatarFile, [1, 2, 3]);

            var previousPlugin = GetAvatarPlugin.Instance;
            _serializer
                .Setup(xml => xml.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            _ = new GetAvatarPlugin(_applicationPaths.Object, _serializer.Object, NullLogger<GetAvatarPlugin>.Instance);
            _previousPlugin = previousPlugin;
            GetAvatarPlugin.Config.AvailableAvatars = [new AvatarInfo { Id = "test-avatar", FileName = "test-avatar.png" }];

            TargetUser = CreateUser();
            GetAvatarPlugin.Config.UserAvatars = [new UserAvatarMapping { UserId = TargetUser.Id.ToString(), AvatarId = "test-avatar" }];
        }

        public User TargetUser { get; }

        public Mock<IXmlSerializer> Serializer => _serializer;

        public string AvatarDirectory => Path.Combine(_root, "GetAvatar", "avatars");

        public AvatarService CreateAvatarService()
        {
            return new AvatarService(
                Mock.Of<IUserManager>(),
                Mock.Of<IProviderManager>(),
                _applicationPaths.Object,
                NullLogger<AvatarService>.Instance,
                _configurationManager.Object);
        }

        public (AvatarController Controller, Mock<INetworkManager> NetworkManager, Mock<IDeviceManager> DeviceManager) CreateController(
            User target,
            ClaimsPrincipal? principal = null,
            User? caller = null,
            bool startupWizardCompleted = true,
            bool localNetwork = true,
            bool deviceAccess = true,
            bool includeTarget = true)
        {
            _configurationManager.SetupGet(manager => manager.Configuration)
                .Returns(new ServerConfiguration { IsStartupWizardCompleted = startupWizardCompleted });

            var userManager = new Mock<IUserManager>();
            if (includeTarget)
            {
                userManager.Setup(manager => manager.GetUserById(target.Id)).Returns(target);
            }

            if (caller is not null)
            {
                userManager.Setup(manager => manager.GetUserById(caller.Id)).Returns(caller);
            }

            var networkManager = new Mock<INetworkManager>();
            networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(localNetwork);
            var deviceManager = new Mock<IDeviceManager>();
            deviceManager.Setup(manager => manager.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>())).Returns(deviceAccess);
            var avatarService = new AvatarService(
                userManager.Object,
                Mock.Of<IProviderManager>(),
                _applicationPaths.Object,
                NullLogger<AvatarService>.Instance,
                _configurationManager.Object);

            var controller = new AvatarController(
                avatarService,
                null!,
                userManager.Object,
                _configurationManager.Object,
                networkManager.Object,
                deviceManager.Object,
                NullLogger<AvatarController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
                    }
                }
            };
            controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.2");
            return (controller, networkManager, deviceManager);
        }

        public void Dispose()
        {
            var property = typeof(GetAvatarPlugin).GetProperty(nameof(GetAvatarPlugin.Instance));
            property!.SetValue(null, _previousPlugin);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
