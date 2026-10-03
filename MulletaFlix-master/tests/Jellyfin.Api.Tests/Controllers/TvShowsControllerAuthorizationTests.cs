using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class TvShowsControllerAuthorizationTests
{
    [Fact]
    public async Task GetEpisodes_DoesNotExposeSeasonOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = await GetEpisodes(fixture, seasonId: fixture.SeasonId);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        fixture.LibraryManager.Verify(library => library.GetItemById<BaseItem>(fixture.SeasonId, fixture.User), Times.Once);
        fixture.LibraryManager.Verify(library => library.GetItemById<BaseItem>(fixture.SeasonId), Times.Never);
    }

    [Fact]
    public async Task GetEpisodesBySeasonNumber_DoesNotExposeSeriesOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = await GetEpisodes(fixture, season: 1);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        fixture.LibraryManager.Verify(library => library.GetItemById<Series>(fixture.SeriesId, fixture.User), Times.Once);
        fixture.LibraryManager.Verify(library => library.GetItemById<Series>(fixture.SeriesId), Times.Never);
    }

    [Fact]
    public async Task GetEpisodesWithoutSeason_DoesNotExposeSeriesOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = await GetEpisodes(fixture);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        fixture.LibraryManager.Verify(library => library.GetItemById<Series>(fixture.SeriesId, fixture.User), Times.Once);
        fixture.LibraryManager.Verify(library => library.GetItemById<Series>(fixture.SeriesId), Times.Never);
    }

    private static Task<ActionResult<QueryResult<BaseItemDto>>> GetEpisodes(
        ControllerFixture fixture,
        int? season = null,
        Guid? seasonId = null)
    {
        return fixture.Controller.GetEpisodes(
            fixture.SeriesId,
            null,
            [],
            season,
            seasonId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            null,
            null);
    }

    private static ControllerFixture CreateController()
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();
        var seriesId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var libraryManager = new Mock<ILibraryManager>();
        var dtoService = new Mock<IDtoService>();

        var controller = new TvShowsController(
            userManager.Object,
            libraryManager.Object,
            dtoService.Object,
            Mock.Of<ITVSeriesManager>());
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
            "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new ControllerFixture(controller, user, seriesId, seasonId, libraryManager);
    }

    private sealed record ControllerFixture(
        TvShowsController Controller,
        User User,
        Guid SeriesId,
        Guid SeasonId,
        Mock<ILibraryManager> LibraryManager);
}
