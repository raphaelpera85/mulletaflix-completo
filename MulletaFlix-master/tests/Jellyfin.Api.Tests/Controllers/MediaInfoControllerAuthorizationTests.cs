using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Api.Models.MediaInfoDtos;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class MediaInfoControllerAuthorizationTests
{
    [Fact]
    public async Task CloseLiveStream_ForbidsStreamOutsideRequestUsersSessions()
    {
        var fixture = CreateFixture();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns(Array.Empty<SessionInfoDto>());

        var result = await fixture.Controller.CloseLiveStream("another-users-stream");

        Assert.IsType<ForbidResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CloseLiveStream_AllowsStreamInRequestUsersSession()
    {
        var fixture = CreateFixture();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns([new SessionInfoDto { UserId = fixture.User.Id, PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "owned-stream" } }]);

        var result = await fixture.Controller.CloseLiveStream("owned-stream");

        Assert.IsType<NoContentResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream("owned-stream"), Times.Once);
    }

    [Fact]
    public async Task CloseLiveStream_ForbidsMatchingStreamOnUnownedSession()
    {
        var fixture = CreateFixture();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns([new SessionInfoDto { PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "unowned-stream" } }]);

        var result = await fixture.Controller.CloseLiveStream("unowned-stream");

        Assert.IsType<ForbidResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CloseLiveStream_PreservesApiKeyAccessToVisibleSession()
    {
        var fixture = CreateFixture(isApiKey: true);
        fixture.SessionManager.Setup(manager => manager.GetSessions(Guid.Empty, string.Empty, null, null, true))
            .Returns([new SessionInfoDto { PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "admin-visible-stream" } }]);

        var result = await fixture.Controller.CloseLiveStream("admin-visible-stream");

        Assert.IsType<NoContentResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream("admin-visible-stream"), Times.Once);
    }

    [Fact]
    public async Task CloseLiveStream_PreservesAdministratorAccessToOtherUsersSession()
    {
        var fixture = CreateFixture();
        fixture.User.Permissions.Single(permission => permission.Kind == PermissionKind.IsAdministrator).Value = true;
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns([new SessionInfoDto { UserId = Guid.NewGuid(), PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "admin-visible-stream" } }]);

        var result = await fixture.Controller.CloseLiveStream("admin-visible-stream");

        Assert.IsType<NoContentResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream("admin-visible-stream"), Times.Once);
    }

    [Fact]
    public async Task CloseLiveStream_AllowsStreamSharedWithRequestUser()
    {
        var fixture = CreateFixture();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns([new SessionInfoDto
            {
                UserId = Guid.NewGuid(),
                AdditionalUsers = [new MediaBrowser.Model.Session.SessionUserInfo { UserId = fixture.User.Id }],
                PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "shared-stream" }
            }]);

        var result = await fixture.Controller.CloseLiveStream("shared-stream");

        Assert.IsType<NoContentResult>(result);
        fixture.MediaSourceManager.Verify(manager => manager.CloseLiveStream("shared-stream"), Times.Once);
    }

    [Fact]
    public async Task GetLiveStreamMediaInfo_ForbidsStreamOutsideRequestUsersSessions()
    {
        var fixture = CreateFixture();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns(Array.Empty<SessionInfoDto>());

        var result = await fixture.Controller.GetLiveStreamMediaInfo("another-users-stream", null, null);

        Assert.IsType<ForbidResult>(result.Result);
        fixture.MediaSourceManager.Verify(
            manager => manager.GetLiveStreamMediaInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetLiveStreamMediaInfo_AllowsStreamInRequestUsersSession()
    {
        var fixture = CreateFixture();
        var mediaSource = new MediaSourceInfo();
        fixture.SessionManager.Setup(manager => manager.GetSessions(fixture.User.Id, string.Empty, null, null, false))
            .Returns([new SessionInfoDto { UserId = fixture.User.Id, PlayState = new MediaBrowser.Model.Session.PlayerStateInfo { LiveStreamId = "owned-stream" } }]);
        fixture.MediaSourceManager.Setup(manager => manager.GetLiveStreamMediaInfo("owned-stream", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mediaSource);

        var result = await fixture.Controller.GetLiveStreamMediaInfo("owned-stream", null, null);

        Assert.Same(mediaSource, result.Value);
        fixture.MediaSourceManager.Verify(
            manager => manager.GetLiveStreamMediaInfo("owned-stream", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPlaybackInfo_AuthenticatedUserCannotBeResolved_ReturnsUnauthorized()
    {
        var fixture = CreateFixture(userExists: false);

        var result = await fixture.Controller.GetPlaybackInfo(fixture.ItemId, null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, It.IsAny<User?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPlaybackInfo_AuthenticatedUserIdMissing_ReturnsUnauthorizedBeforeLibraryLookup()
    {
        var fixture = CreateFixture(includeUserIdClaim: false);

        var result = await fixture.Controller.GetPlaybackInfo(fixture.ItemId, null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(fixture.LibraryManager.Invocations);
    }

    [Fact]
    public async Task GetPostedPlaybackInfo_AuthenticatedUserCannotBeResolved_ReturnsUnauthorized()
    {
        var fixture = CreateFixture(userExists: false);

        var result = await fixture.Controller.GetPostedPlaybackInfo(
            fixture.ItemId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new PlaybackInfoDto { DeviceProfile = new DeviceProfile() });

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, It.IsAny<User?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPostedPlaybackInfo_AuthenticatedUserIdMissing_ReturnsUnauthorizedBeforeDeviceOrLibraryLookup()
    {
        var fixture = CreateFixture(includeUserIdClaim: false);

        var result = await fixture.Controller.GetPostedPlaybackInfo(
            fixture.ItemId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            playbackInfoDto: null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(fixture.LibraryManager.Invocations);
        Assert.Empty(fixture.DeviceManager.Invocations);
    }

    [Fact]
    public async Task GetPlaybackInfo_ApiKeyWithoutUser_PreservesUnscopedLookup()
    {
        var fixture = CreateFixture(isApiKey: true, itemIsVisible: false);

        var result = await fixture.Controller.GetPlaybackInfo(fixture.ItemId, null);

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, (User?)null),
            Times.Once);
    }

    [Fact]
    public async Task GetPostedPlaybackInfo_ApiKeyWithoutUser_PreservesUnscopedLookup()
    {
        var fixture = CreateFixture(isApiKey: true, itemIsVisible: false);

        var result = await fixture.Controller.GetPostedPlaybackInfo(
            fixture.ItemId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new PlaybackInfoDto { DeviceProfile = new DeviceProfile() });

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, (User?)null),
            Times.Once);
    }

    [Fact]
    public async Task OpenLiveStream_AuthenticatedUserCannotBeResolved_ReturnsUnauthorizedBeforeOpening()
    {
        var fixture = CreateFixture(userExists: false);

        var result = await OpenLiveStream(fixture.Controller, fixture.ItemId);

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(It.IsAny<LiveStreamRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenLiveStream_AuthenticatedUserIdMissing_ReturnsUnauthorizedBeforeLibraryOrStreamManager()
    {
        var fixture = CreateFixture(includeUserIdClaim: false);

        var result = await OpenLiveStream(fixture.Controller, fixture.ItemId);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(fixture.LibraryManager.Invocations);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(It.IsAny<LiveStreamRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenLiveStream_ItemOutsideUsersLibrary_ReturnsNotFoundBeforeOpening()
    {
        var fixture = CreateFixture(itemIsVisible: false);

        var result = await OpenLiveStream(fixture.Controller, fixture.ItemId);

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, fixture.User),
            Times.Once);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId),
            Times.Never);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(It.IsAny<LiveStreamRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenLiveStream_RegisteredTransientItem_RemainsOpenable()
    {
        var fixture = CreateFixture();
        var item = new Mock<BaseItem>().Object;
        item.Id = fixture.ItemId;
        item.Path = "transient-media.mkv";
        fixture.TransientItems.Register(item);

        await Assert.ThrowsAsync<OpenMediaSourceReachedException>(() => OpenLiveStream(fixture.Controller, fixture.ItemId));

        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, fixture.User),
            Times.Once);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(
                It.Is<LiveStreamRequest>(request => request.ItemId == fixture.ItemId && request.UserId == fixture.User.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenLiveStream_VisibleLibraryItem_RemainsOpenable()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<OpenMediaSourceReachedException>(() => OpenLiveStream(fixture.Controller, fixture.ItemId));

        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, fixture.User),
            Times.Once);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(
                It.Is<LiveStreamRequest>(request => request.ItemId == fixture.ItemId && request.UserId == fixture.User.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenLiveStream_ApiKeyWithoutUser_PreservesUnscopedAccess()
    {
        var fixture = CreateFixture(isApiKey: true);

        await Assert.ThrowsAsync<OpenMediaSourceReachedException>(() => OpenLiveStream(fixture.Controller, fixture.ItemId));

        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ItemId, (User?)null),
            Times.Once);
        fixture.MediaSourceManager.Verify(
            manager => manager.OpenLiveStream(
                It.Is<LiveStreamRequest>(request => request.ItemId == fixture.ItemId && request.UserId == Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Task<ActionResult<LiveStreamResponse>> OpenLiveStream(MediaInfoController controller, Guid itemId)
    {
        return controller.OpenLiveStream(
            "test-open-token",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            itemId,
            null,
            null,
            null,
            null);
    }

    private static ControllerFixture CreateFixture(
        bool userExists = true,
        bool itemIsVisible = true,
        bool isApiKey = false,
        bool includeUserIdClaim = true)
    {
        var user = new User(
            "media-reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var itemId = Guid.NewGuid();
        var visibleItem = new Mock<BaseItem>().Object;
        visibleItem.Id = itemId;
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(itemId, user))
            .Returns(itemIsVisible ? visibleItem : null);
        libraryManager.Setup(library => library.GetItemById<BaseItem>(itemId, (User?)null))
            .Returns(itemIsVisible ? visibleItem : null);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager
            .Setup(manager => manager.OpenLiveStream(It.IsAny<LiveStreamRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OpenMediaSourceReachedException());
        var deviceManager = new Mock<IDeviceManager>();
        var sessionManager = new Mock<ISessionManager>();

        var transientItems = new TransientMediaItemRegistry();
        var mediaInfoHelper = new MediaInfoHelper(
            userManager.Object,
            libraryManager.Object,
            mediaSourceManager.Object,
            null!,
            null!,
            NullLogger<MediaInfoHelper>.Instance,
            null!,
            null!);
        var controller = new MediaInfoController(
            mediaSourceManager.Object,
            deviceManager.Object,
            libraryManager.Object,
            null!,
            NullLogger<MediaInfoController>.Instance,
            mediaInfoHelper,
            userManager.Object,
            transientItems,
            null!,
            sessionManager.Object);

        var claims = isApiKey
            ? new[]
            {
                new Claim(InternalClaimTypes.IsApiKey, bool.TrueString),
                new Claim(ClaimTypes.Name, "api-key")
            }
            : new[]
            {
                new Claim(ClaimTypes.Name, user.Username)
            };
        if (!isApiKey && includeUserIdClaim)
        {
            claims = [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")), .. claims];
        }
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new ControllerFixture(controller, user, itemId, libraryManager, mediaSourceManager, transientItems, deviceManager, sessionManager);
    }

    private sealed record ControllerFixture(
        MediaInfoController Controller,
        User User,
        Guid ItemId,
        Mock<ILibraryManager> LibraryManager,
        Mock<IMediaSourceManager> MediaSourceManager,
        TransientMediaItemRegistry TransientItems,
        Mock<IDeviceManager> DeviceManager,
        Mock<ISessionManager> SessionManager);

    private sealed class OpenMediaSourceReachedException : Exception
    {
    }
}
