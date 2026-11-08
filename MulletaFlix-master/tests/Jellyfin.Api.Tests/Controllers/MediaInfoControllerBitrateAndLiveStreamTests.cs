using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
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

public sealed class MediaInfoControllerBitrateAndLiveStreamTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(1024)]
    [InlineData(65536)]
    public void GetBitrateTestBytes_ValidSize_ReturnsFileContentWithExactSize(int size)
    {
        var controller = CreateController();

        var result = controller.GetBitrateTestBytes(size);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal(size, fileResult.FileContents.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetBitrateTestBytes_ZeroOrNegativeSize_ReturnsBadRequest(int size)
    {
        var controller = CreateController();

        var result = controller.GetBitrateTestBytes(size);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task OpenLiveStream_MissingBothOpenTokenAndItemId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.OpenLiveStream(
            openToken: null,
            userId: null,
            playSessionId: null,
            maxStreamingBitrate: null,
            startTimeTicks: null,
            audioStreamIndex: null,
            subtitleStreamIndex: null,
            maxAudioChannels: null,
            itemId: null,
            openLiveStreamDto: null,
            enableDirectPlay: null,
            enableDirectStream: null,
            alwaysBurnInSubtitleWhenTranscoding: null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static MediaInfoController CreateController()
    {
        var mediaSourceManager = new Mock<IMediaSourceManager>(MockBehavior.Loose);
        var deviceManager = new Mock<IDeviceManager>(MockBehavior.Loose);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);
        var userManager = new Mock<IUserManager>(MockBehavior.Loose);
        var sessionManager = new Mock<ISessionManager>(MockBehavior.Loose);

        var user = new User(
            "media-reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        userManager.Setup(m => m.GetUserById(user.Id)).Returns(user);

        var transientRegistry = new TransientMediaItemRegistry();

        var mediaInfoHelper = new MediaInfoHelper(
            userManager.Object,
            libraryManager.Object,
            mediaSourceManager.Object,
            null!,
            null!,
            NullLogger<MediaInfoHelper>.Instance,
            null!,
            null!);

        var claims = new[]
        {
            new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")),
            new Claim(ClaimTypes.Name, user.Username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");

        var controller = new MediaInfoController(
            mediaSourceManager.Object,
            deviceManager.Object,
            libraryManager.Object,
            null!,
            NullLogger<MediaInfoController>.Instance,
            mediaInfoHelper,
            userManager.Object,
            transientRegistry,
            null!,
            sessionManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        return controller;
    }
}
