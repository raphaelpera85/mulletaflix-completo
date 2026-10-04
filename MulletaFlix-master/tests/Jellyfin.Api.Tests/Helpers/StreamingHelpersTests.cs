using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Helpers;

public static class StreamingHelpersTests
{
    [Fact]
    public static void CanAccessLiveStream_OwnerIsAuthorizedBeforeSessionPlaybackStateExists()
    {
        var user = CreateUser("stream-owner-immediate");
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(manager => manager.IsLiveStreamOwnedByUser("new-stream", user.Id)).Returns(true);

        var sessionManager = new Mock<ISessionManager>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
            "TestAuth"));

        Assert.True(StreamingHelpers.CanAccessLiveStream(
            principal,
            userManager.Object,
            sessionManager.Object,
            mediaSourceManager.Object,
            "new-stream"));

        sessionManager.Verify(
            manager => manager.GetSessions(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public static async Task GetStreamingState_ForeignLiveStream_IsRejectedBeforeStreamLookup()
    {
        var user = CreateUser("stream-owner-check");
        var itemId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, user)).Returns(new Video { Id = itemId });

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var sessionManager = new Mock<ISessionManager>();
        sessionManager.Setup(manager => manager.GetSessions(user.Id, string.Empty, null, null, false)).Returns([]);

        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.Setup(encoder => encoder.CanEncodeToAudioCodec("mp3")).Returns(true);

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Audio/stream.mp3";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
            "TestAuth"));

        var request = new StreamingRequestDto
        {
            Id = itemId,
            LiveStreamId = "another-users-stream",
            AudioCodec = "mp3"
        };

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => StreamingHelpers.GetStreamingState(
            request,
            httpContext,
            mediaSourceManager.Object,
            userManager.Object,
            sessionManager.Object,
            libraryManager.Object,
            null!,
            mediaEncoder.Object,
            null!,
            null!,
            TranscodingJobType.Progressive,
            CancellationToken.None));

        mediaSourceManager.Verify(
            manager => manager.GetLiveStreamWithDirectStreamProvider(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediaSourceManager.Verify(manager => manager.GetLiveStreamInfo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public static void GetOutputFilePath_IsUniquePerUser()
    {
        var state = CreateState();
        state.MediaPath = @"C:\media\movie.mkv";
        state.UserAgent = "Mozilla/5.0";

        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(x => x.GetConfiguration("encoding")).Returns(new EncodingOptions());

        var appPaths = new Mock<IApplicationPaths>();
        appPaths.SetupGet(x => x.CachePath).Returns(@"C:\transcodes");
        appPaths.Setup(x => x.CreateAndCheckMarker(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        configManager.SetupGet(x => x.CommonApplicationPaths).Returns(appPaths.Object);

        var method = typeof(StreamingHelpers).GetMethod(
            "GetOutputFilePath",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var first = Invoke(method!, state, configManager.Object, Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = Invoke(method!, state, configManager.Object, Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.NotEqual(first, second);
    }

    private static string Invoke(MethodInfo method, StreamState state, IServerConfigurationManager configManager, Guid userId)
        => (string)method.Invoke(null, new object[] { state, ".mp4", configManager, userId, "device-1", "play-1" })!;

    private static StreamState CreateState()
    {
        var mediaSourceManager = new Mock<IMediaSourceManager>().Object;
        var transcodeManager = new Mock<ITranscodeManager>().Object;
        return new StreamState(mediaSourceManager, TranscodingJobType.Hls, transcodeManager);
    }

    private static User CreateUser(string name)
    {
        var user = new User(name, typeof(DefaultAuthenticationProvider).FullName!, typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();
        return user;
    }
}
