using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class MediaInfoControllerAuthorizationTests
{
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
            null!);

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

        return new ControllerFixture(controller, user, itemId, libraryManager, mediaSourceManager, transientItems, deviceManager);
    }

    private sealed record ControllerFixture(
        MediaInfoController Controller,
        User User,
        Guid ItemId,
        Mock<ILibraryManager> LibraryManager,
        Mock<IMediaSourceManager> MediaSourceManager,
        TransientMediaItemRegistry TransientItems,
        Mock<IDeviceManager> DeviceManager);

    private sealed class OpenMediaSourceReachedException : Exception
    {
    }
}
