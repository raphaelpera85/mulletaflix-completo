using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Api.Models.LiveTvDtos;
using MulletaFlix.Data;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class LiveTvProgramsAuthorizationTests
{
    [Theory]
    [InlineData("channels", true)]
    [InlineData("channels", false)]
    [InlineData("channel", true)]
    [InlineData("channel", false)]
    [InlineData("recordings", true)]
    [InlineData("recordings", false)]
    [InlineData("recording-folders", true)]
    [InlineData("recording-folders", false)]
    [InlineData("recording", true)]
    [InlineData("recording", false)]
    [InlineData("programs", true)]
    [InlineData("programs", false)]
    [InlineData("programs-post", true)]
    [InlineData("programs-post", false)]
    [InlineData("programs-recommended", true)]
    [InlineData("programs-recommended", false)]
    [InlineData("program", true)]
    [InlineData("program", false)]
    public async Task ReadEndpoints_ReturnUnauthorizedWhenUserIsMissingOrCannotBeResolved(
        string endpoint,
        bool includeUserIdClaim)
    {
        var fixture = CreateFixture(userExists: !includeUserIdClaim, includeUserIdClaim: includeUserIdClaim);

        var result = await InvokeReadEndpoint(fixture, endpoint);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(fixture.LiveTvManager.Invocations);
        Assert.Empty(fixture.LibraryManager.Invocations);
    }

    [Fact]
    public async Task GetLiveTvPrograms_ApiKeyWithoutUser_RemainsSupported()
    {
        var fixture = CreateFixture(isApiKey: true, includeUserIdClaim: false);
        InternalItemsQuery? capturedQuery = null;
        fixture.LiveTvManager
            .Setup(manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<InternalItemsQuery, DtoOptions, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new QueryResult<BaseItemDto>());

        var result = await fixture.Controller.GetLiveTvPrograms(
            [],
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
            [],
            [],
            [],
            [],
            null,
            null,
            [],
            null,
            null,
            null,
            [],
            true);

        Assert.NotNull(result.Value);
        Assert.NotNull(capturedQuery);
        Assert.Null(capturedQuery.User);
    }

    [Theory]
    [InlineData("channels")]
    [InlineData("channel")]
    [InlineData("recordings")]
    [InlineData("recording-folders")]
    [InlineData("recording")]
    [InlineData("programs")]
    [InlineData("programs-post")]
    [InlineData("programs-recommended")]
    [InlineData("program")]
    public async Task ReadEndpoints_PreserveApiKeyAccessWithoutUserId(string endpoint)
    {
        var fixture = CreateFixture(isApiKey: true, includeUserIdClaim: false);

        var result = await InvokeReadEndpoint(fixture, endpoint);

        Assert.False(result is UnauthorizedResult, $"API key was rejected by {endpoint}.");
    }

    [Fact]
    public async Task GetLiveTvPrograms_ReturnsNotFoundForSeriesOutsideUsersLibrary()
    {
        var fixture = CreateFixture();
        var seriesId = Guid.NewGuid();
        fixture.LibraryManager
            .Setup(manager => manager.GetItemById<Series>(seriesId, fixture.User))
            .Returns((Series?)null);

        var result = await GetLiveTvPrograms(fixture, seriesId);

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.LiveTvManager.Verify(
            manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPrograms_ForbidsRequestingAnotherUsersPrograms()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<SecurityException>(() => fixture.Controller.GetPrograms(new GetProgramsDto
        {
            UserId = Guid.NewGuid()
        }));

        fixture.LiveTvManager.Verify(
            manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProgramEndpoints_ReturnUnauthorizedWhenPrincipalUserCannotBeResolved()
    {
        var fixture = CreateFixture(userExists: false);

        var getResult = await GetLiveTvPrograms(fixture, null);
        var postResult = await fixture.Controller.GetPrograms(new GetProgramsDto());

        Assert.IsType<UnauthorizedResult>(getResult.Result);
        Assert.IsType<UnauthorizedResult>(postResult.Result);
        fixture.LiveTvManager.Verify(
            manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPrograms_ReturnsNotFoundForSeriesOutsideUsersLibrary()
    {
        var fixture = CreateFixture();
        var seriesId = Guid.NewGuid();
        fixture.LibraryManager
            .Setup(manager => manager.GetItemById<Series>(seriesId, fixture.User))
            .Returns((Series?)null);

        var result = await fixture.Controller.GetPrograms(new GetProgramsDto
        {
            UserId = fixture.User.Id,
            LibrarySeriesId = seriesId
        });

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.LiveTvManager.Verify(
            manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPrograms_UsesVisibleSeriesAndCurrentUserForQuery()
    {
        var fixture = CreateFixture();
        var seriesId = Guid.NewGuid();
        var series = new Series { Name = "Visible series" };
        InternalItemsQuery? capturedQuery = null;
        fixture.LibraryManager
            .Setup(manager => manager.GetItemById<Series>(seriesId, fixture.User))
            .Returns(series);
        fixture.LiveTvManager
            .Setup(manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<InternalItemsQuery, DtoOptions, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new QueryResult<BaseItemDto>());

        var result = await fixture.Controller.GetPrograms(new GetProgramsDto
        {
            UserId = fixture.User.Id,
            LibrarySeriesId = seriesId
        });

        Assert.NotNull(result.Value);
        Assert.NotNull(capturedQuery);
        Assert.Same(fixture.User, capturedQuery.User);
        Assert.Equal(series.Name, capturedQuery.Name);
        fixture.LibraryManager.Verify(manager => manager.GetItemById<Series>(seriesId, fixture.User), Times.Once);
    }

    private static Task<ActionResult<QueryResult<BaseItemDto>>> GetLiveTvPrograms(
        ControllerFixture fixture,
        Guid? seriesId)
    {
        return fixture.Controller.GetLiveTvPrograms(
            [],
            fixture.User.Id,
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
            [],
            [],
            [],
            [],
            null,
            null,
            [],
            null,
            null,
            seriesId,
            [],
            true);
    }

    private static async Task<IActionResult?> InvokeReadEndpoint(ControllerFixture fixture, string endpoint)
    {
        switch (endpoint)
        {
            case "channels":
                return (await fixture.Controller.GetLiveTvChannels(
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
                    [],
                    [],
                    null,
                    [],
                    null)).Result;
            case "channel":
                return (await fixture.Controller.GetChannel(Guid.NewGuid(), null)).Result;
            case "recordings":
                return (await fixture.Controller.GetRecordings(
                    null, null, null, null, null, null, null, null, null, [], [], null, null, null, null, null, null, true)).Result;
            case "recording-folders":
                return (await fixture.Controller.GetRecordingFolders(null)).Result;
            case "recording":
                return (await fixture.Controller.GetRecording(Guid.NewGuid(), null)).Result;
            case "programs":
                return (await fixture.Controller.GetLiveTvPrograms(
                    [],
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
                    [],
                    [],
                    [],
                    [],
                    null,
                    null,
                    [],
                    null,
                    null,
                    null,
                    [],
                    true)).Result;
            case "programs-post":
                return (await fixture.Controller.GetPrograms(new GetProgramsDto())).Result;
            case "programs-recommended":
                return (await fixture.Controller.GetRecommendedPrograms(
                    null, null, null, null, null, null, null, null, null, null, null, null, [], [], [], null, true)).Result;
            case "program":
                return (await fixture.Controller.GetProgram("program-id", null)).Result;
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint");
        }
    }

    private static ControllerFixture CreateFixture(
        bool userExists = true,
        bool includeUserIdClaim = true,
        bool isApiKey = false)
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var liveTvManager = new Mock<ILiveTvManager>();
        liveTvManager
            .Setup(manager => manager.GetInternalChannels(
                It.IsAny<LiveTvChannelQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(new QueryResult<BaseItem>());
        liveTvManager
            .Setup(manager => manager.GetRecordingsAsync(It.IsAny<RecordingQuery>(), It.IsAny<DtoOptions>()))
            .ReturnsAsync(new QueryResult<BaseItemDto>());
        liveTvManager
            .Setup(manager => manager.GetRecordingFoldersAsync(It.IsAny<User>()))
            .ReturnsAsync(Array.Empty<BaseItem>());
        liveTvManager
            .Setup(manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<BaseItemDto>());
        liveTvManager
            .Setup(manager => manager.GetRecommendedProgramsAsync(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<BaseItemDto>());
        var libraryManager = new Mock<ILibraryManager>();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);
        var dtoService = new Mock<IDtoService>();
        dtoService
            .Setup(service => service.GetBaseItemDtosAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<User>()))
            .ReturnsAsync(Array.Empty<BaseItemDto>());
        var controller = new LiveTvController(
            liveTvManager.Object,
            Mock.Of<IGuideManager>(),
            Mock.Of<ITunerHostManager>(),
            Mock.Of<IListingsManager>(),
            Mock.Of<IRecordingsManager>(),
            userManager.Object,
            libraryManager.Object,
            dtoService.Object,
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<ITranscodeManager>(),
            Mock.Of<ISchedulesDirectService>());
        var claims = new List<Claim>();
        if (includeUserIdClaim)
        {
            claims.Add(new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")));
        }

        if (isApiKey)
        {
            claims.Add(new Claim(InternalClaimTypes.IsApiKey, bool.TrueString));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new ControllerFixture(controller, user, liveTvManager, libraryManager);
    }

    private sealed record ControllerFixture(
        LiveTvController Controller,
        User User,
        Mock<ILiveTvManager> LiveTvManager,
        Mock<ILibraryManager> LibraryManager);
}
