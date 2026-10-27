using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Data.Dtos;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Entities.Security;
using MulletaFlix.Server.Implementations.Tests.Logging;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.SessionManager;

public class SessionManagerTests
{
    [Theory]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(null, typeof(ArgumentNullException))]
    public async Task GetAuthorizationToken_Should_ThrowException(string? deviceId, Type exceptionType)
    {
        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            Mock.Of<IEventManager>(),
            Mock.Of<IUserDataManager>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IUserManager>(),
            Mock.Of<IMusicManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<IDeviceManager>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IHostApplicationLifetime>());

        await Assert.ThrowsAsync(exceptionType, () => sessionManager.GetAuthorizationToken(
            new User("test", "default", "default"),
            deviceId,
            "app_name",
            "0.0.0",
            "device_name"));
    }

    [Theory]
    [MemberData(nameof(AuthenticateNewSessionInternal_Exception_TestData))]
    public async Task AuthenticateNewSessionInternal_Should_ThrowException(AuthenticationRequest authenticationRequest, Type exceptionType)
    {
        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            Mock.Of<IEventManager>(),
            Mock.Of<IUserDataManager>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IUserManager>(),
            Mock.Of<IMusicManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<IDeviceManager>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IHostApplicationLifetime>());

        await Assert.ThrowsAsync(exceptionType, () => sessionManager.AuthenticateNewSessionInternal(authenticationRequest, false));
    }

    [Fact]
    public async Task AuthenticateNewSessionInternal_Should_KeepSessions_Isolated_ForDifferentUsers_OnSameDevice()
    {
        var userOne = new User("user-one", "default", "default");
        var userTwo = new User("user-two", "default", "default");
        var userManager = new Mock<IUserManager>();
        userManager.Setup(x => x.GetUserByName("user-one")).Returns(userOne);
        userManager.Setup(x => x.GetUserByName("user-two")).Returns(userTwo);
        userManager.Setup(x => x.GetUserDto(It.IsAny<User>(), It.IsAny<string?>())).Returns(new MediaBrowser.Model.Dto.UserDto());

        var deviceManager = new Mock<IDeviceManager>();
        deviceManager.Setup(x => x.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>())).Returns(true);
        deviceManager.Setup(x => x.GetDevices(It.IsAny<DeviceQuery>())).Returns(new QueryResult<Device>
        {
            Items = Array.Empty<Device>(),
            TotalRecordCount = 0
        });
        deviceManager.Setup(x => x.CreateDevice(It.IsAny<Device>())).ReturnsAsync((Device device) => device);
        deviceManager.Setup(x => x.DeleteDevice(It.IsAny<Device>())).Returns(Task.CompletedTask);
        deviceManager.Setup(x => x.GetDeviceOptions(It.IsAny<string>())).Returns((DeviceOptionsDto?)null);

        var eventManager = new Mock<IEventManager>();
        eventManager.Setup(x => x.PublishAsync(It.IsAny<AuthenticationRequestEventArgs>())).Returns(Task.CompletedTask);
        eventManager.Setup(x => x.PublishAsync(It.IsAny<AuthenticationResultEventArgs>())).Returns(Task.CompletedTask);
        var logger = new CapturingLogger<Emby.Server.Implementations.Session.SessionManager>();

        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            logger,
            eventManager.Object,
            Mock.Of<IUserDataManager>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<ILibraryManager>(),
            userManager.Object,
            Mock.Of<IMusicManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(x => x.SystemId == "test-server"),
            deviceManager.Object,
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IHostApplicationLifetime>());

        var requestOne = new AuthenticationRequest
        {
            App = "android-tv",
            AppVersion = "1.0.0",
            DeviceId = "shared-device-id",
            DeviceName = "Android TV",
            Username = "user-one"
        };

        var requestTwo = new AuthenticationRequest
        {
            App = "web-browser",
            AppVersion = "1.0.0",
            DeviceId = "shared-device-id",
            DeviceName = "Mobile Browser",
            Username = "user-two"
        };

        var resultOne = await sessionManager.AuthenticateNewSessionInternal(requestOne, false);
        var resultTwo = await sessionManager.AuthenticateNewSessionInternal(requestTwo, false);

        Assert.NotEqual(resultOne.SessionInfo.Id, resultTwo.SessionInfo.Id);
        Assert.Equal(2, sessionManager.Sessions.Count());
        Assert.Contains(sessionManager.Sessions, x => x.UserId == userOne.Id);
        Assert.Contains(sessionManager.Sessions, x => x.UserId == userTwo.Id);

        await sessionManager.Logout(new Device(userOne.Id, requestOne.App, requestOne.AppVersion, requestOne.DeviceName, requestOne.DeviceId)
        {
            AccessToken = resultOne.AccessToken
        });

        Assert.DoesNotContain(resultOne.AccessToken, string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
        Assert.Single(sessionManager.Sessions);
        Assert.Equal(userTwo.Id, sessionManager.Sessions.Single().UserId);
    }

    [Fact]
    public async Task OnPlaybackProgress_AwaitsAsyncUserDataSave()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId, Name = "Test movie" };
        var user = new User("test-user", "default", "default")
        {
            Id = Guid.NewGuid(),
            LastActivityDate = DateTime.UtcNow
        };

        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var userDataManager = new Mock<IUserDataManager>();
        userDataManager.Setup(manager => manager.GetUserData(user, item)).Returns(new UserItemData { Key = itemId.ToString("N") });
        userDataManager.Setup(manager => manager.UpdatePlayState(item, It.IsAny<UserItemData>(), It.IsAny<long?>())).Returns(false);
        userDataManager.Setup(manager => manager.SaveUserDataAsync(
                user,
                item,
                It.IsAny<UserItemData>(),
                UserDataSaveReason.PlaybackProgress,
                It.IsAny<CancellationToken>()))
            .Callback(() => saveStarted.TrySetResult())
            .Returns(completeSave.Task);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(itemId)).Returns(item);
        var eventManager = new Mock<IEventManager>();
        eventManager.Setup(manager => manager.PublishAsync(It.IsAny<PlaybackProgressEventArgs>())).Returns(Task.CompletedTask);
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new MediaBrowser.Model.Configuration.ServerConfiguration());

        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            eventManager.Object,
            userDataManager.Object,
            configurationManager.Object,
            libraryManager.Object,
            userManager.Object,
            Mock.Of<IMusicManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(host => host.SystemId == "test-server"),
            Mock.Of<IDeviceManager>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IHostApplicationLifetime>());

        var session = await sessionManager.LogSessionActivity("test-app", "1.0", "test-device", "Test Device", "127.0.0.1", user);
        var progressTask = sessionManager.OnPlaybackProgress(new PlaybackProgressInfo
        {
            SessionId = session.Id,
            ItemId = itemId,
            Item = new BaseItemDto { Id = itemId },
            PositionTicks = TimeSpan.FromMinutes(5).Ticks
        });

        await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(progressTask.IsCompleted);

        completeSave.SetResult();
        await progressTask;

        userDataManager.Verify(
            manager => manager.SaveUserDataAsync(
                user,
                item,
                It.IsAny<UserItemData>(),
                UserDataSaveReason.PlaybackProgress,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPlaybackStart_AwaitsAsyncItemDtoEnrichment()
    {
        var item = new Movie { Id = Guid.NewGuid(), Name = "Test movie" };
        var user = new User("test-user", "default", "default") { Id = Guid.NewGuid(), LastActivityDate = DateTime.UtcNow };
        var dtoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeDto = new TaskCompletionSource<BaseItemDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(item.Id)).Returns(item);
        var dtoService = new Mock<IDtoService>();
        dtoService.Setup(service => service.GetBaseItemDtoAsync(
                item,
                It.Is<DtoOptions>(options => options.ContainsField(ItemFields.Trickplay)),
                null,
                null))
            .Callback(() => dtoStarted.TrySetResult())
            .Returns(completeDto.Task);
        dtoService.Setup(service => service.GetBaseItemDto(item, It.IsAny<DtoOptions>(), null, null))
            .Throws(new InvalidOperationException("Synchronous DTO conversion must not be used by session playback."));

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(manager => manager.GetMediaSource(
                item,
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaSourceInfo?)null);
        var userDataManager = new Mock<IUserDataManager>();
        userDataManager.Setup(manager => manager.GetUserData(user, item)).Returns(new UserItemData { Key = item.Id.ToString("N") });
        userDataManager.Setup(manager => manager.SaveUserDataAsync(
                user,
                item,
                It.IsAny<UserItemData>(),
                UserDataSaveReason.PlaybackStart,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var eventManager = new Mock<IEventManager>();
        eventManager.Setup(manager => manager.PublishAsync(It.IsAny<PlaybackStartEventArgs>())).Returns(Task.CompletedTask);
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new MediaBrowser.Model.Configuration.ServerConfiguration());

        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            eventManager.Object,
            userDataManager.Object,
            configurationManager.Object,
            libraryManager.Object,
            userManager.Object,
            Mock.Of<IMusicManager>(),
            dtoService.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(host => host.SystemId == "test-server"),
            Mock.Of<IDeviceManager>(),
            mediaSourceManager.Object,
            Mock.Of<IHostApplicationLifetime>());

        var session = await sessionManager.LogSessionActivity("test-app", "1.0", "test-device", "Test Device", "127.0.0.1", user);
        var startTask = sessionManager.OnPlaybackStart(new PlaybackStartInfo
        {
            SessionId = session.Id,
            ItemId = item.Id
        });

        await dtoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(startTask.IsCompleted);

        completeDto.SetResult(new BaseItemDto { Id = item.Id });
        await startTask;

        dtoService.Verify(
            service => service.GetBaseItemDtoAsync(
                item,
                It.Is<DtoOptions>(options => options.ContainsField(ItemFields.Trickplay)),
                null,
                null),
            Times.Once);
        dtoService.Verify(service => service.GetBaseItemDto(item, It.IsAny<DtoOptions>(), null, null), Times.Never);
    }

    [Fact]
    public async Task ReportNowViewingItemAsync_AwaitsAsyncItemDtoEnrichment()
    {
        var item = new Movie { Id = Guid.NewGuid(), Name = "Test movie" };
        var user = new User("test-user", "default", "default") { Id = Guid.NewGuid(), LastActivityDate = DateTime.UtcNow };
        var dtoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeDto = new TaskCompletionSource<BaseItemDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(item.Id)).Returns(item);
        var dtoService = new Mock<IDtoService>();
        dtoService.Setup(service => service.GetBaseItemDtoAsync(
                item,
                It.Is<DtoOptions>(options => options.ContainsField(ItemFields.Trickplay)),
                null,
                null))
            .Callback(() => dtoStarted.TrySetResult())
            .Returns(completeDto.Task);
        dtoService.Setup(service => service.GetBaseItemDto(item, It.IsAny<DtoOptions>(), null, null))
            .Throws(new InvalidOperationException("Synchronous DTO conversion must not be used by session playback."));
        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(manager => manager.Configuration).Returns(new MediaBrowser.Model.Configuration.ServerConfiguration());

        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            Mock.Of<IEventManager>(),
            Mock.Of<IUserDataManager>(),
            configurationManager.Object,
            libraryManager.Object,
            userManager.Object,
            Mock.Of<IMusicManager>(),
            dtoService.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(host => host.SystemId == "test-server"),
            Mock.Of<IDeviceManager>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IHostApplicationLifetime>());

        var session = await sessionManager.LogSessionActivity("test-app", "1.0", "test-device", "Test Device", "127.0.0.1", user);
        var reportTask = sessionManager.ReportNowViewingItemAsync(session.Id, item.Id.ToString("N"));

        await dtoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(reportTask.IsCompleted);

        completeDto.SetResult(new BaseItemDto { Id = item.Id });
        await reportTask;

        dtoService.Verify(service => service.GetBaseItemDto(item, It.IsAny<DtoOptions>(), null, null), Times.Never);
        Assert.NotNull(session.NowViewingItem);
    }

    public static TheoryData<AuthenticationRequest, Type> AuthenticateNewSessionInternal_Exception_TestData()
    {
        var data = new TheoryData<AuthenticationRequest, Type>
        {
            {
                new AuthenticationRequest { App = string.Empty, DeviceId = "device_id", DeviceName = "device_name", AppVersion = "app_version" },
                typeof(ArgumentException)
            },
            {
                new AuthenticationRequest { App = null, DeviceId = "device_id", DeviceName = "device_name", AppVersion = "app_version" },
                typeof(ArgumentNullException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = string.Empty, DeviceName = "device_name", AppVersion = "app_version" },
                typeof(ArgumentException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = null, DeviceName = "device_name", AppVersion = "app_version" },
                typeof(ArgumentNullException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = "device_id", DeviceName = string.Empty, AppVersion = "app_version" },
                typeof(ArgumentException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = "device_id", DeviceName = null, AppVersion = "app_version" },
                typeof(ArgumentNullException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = "device_id", DeviceName = "device_name", AppVersion = string.Empty },
                typeof(ArgumentException)
            },
            {
                new AuthenticationRequest { App = "app_name", DeviceId = "device_id", DeviceName = "device_name", AppVersion = null },
                typeof(ArgumentNullException)
            }
        };

        return data;
    }
}
