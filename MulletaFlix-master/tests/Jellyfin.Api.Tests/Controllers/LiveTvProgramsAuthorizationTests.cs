using System;
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

    private static ControllerFixture CreateFixture(bool userExists = true)
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var liveTvManager = new Mock<ILiveTvManager>();
        liveTvManager
            .Setup(manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<BaseItemDto>());
        var libraryManager = new Mock<ILibraryManager>();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);
        var controller = new LiveTvController(
            liveTvManager.Object,
            Mock.Of<IGuideManager>(),
            Mock.Of<ITunerHostManager>(),
            Mock.Of<IListingsManager>(),
            Mock.Of<IRecordingsManager>(),
            userManager.Object,
            libraryManager.Object,
            Mock.Of<IDtoService>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<ITranscodeManager>(),
            Mock.Of<ISchedulesDirectService>());
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
            "TestAuth");
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
