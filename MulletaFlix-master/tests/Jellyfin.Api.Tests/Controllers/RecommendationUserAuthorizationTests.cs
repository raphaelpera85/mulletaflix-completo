using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class RecommendationUserAuthorizationTests
{
    [Theory]
    [InlineData("movie-recommendations", false)]
    [InlineData("movie-recommendations", true)]
    [InlineData("upcoming", false)]
    [InlineData("upcoming", true)]
    [InlineData("episodes", false)]
    [InlineData("episodes", true)]
    [InlineData("seasons", false)]
    [InlineData("seasons", true)]
    public async Task ReadEndpoints_RejectMissingOrUnresolvedUserBeforeReadingLibrary(
        string endpoint,
        bool includeUserIdClaim)
    {
        var fixture = CreateFixture(
            includeUserIdClaim: includeUserIdClaim,
            userExists: !includeUserIdClaim);

        var result = await InvokeEndpoint(fixture, endpoint);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(fixture.LibraryManager.Invocations);
        Assert.Empty(fixture.SimilarItemsManager.Invocations);
        Assert.Empty(fixture.TvSeriesManager.Invocations);
        Assert.Empty(fixture.DtoService.Invocations);
    }

    [Fact]
    public async Task GetEpisodes_ApiKeyWithoutUserRemainsSupported()
    {
        var fixture = CreateFixture(isApiKey: true, includeUserIdClaim: false);

        var result = await fixture.TvShowsController.GetEpisodes(
            fixture.SeriesId, null, [], null, null, null, null, null, null, null, null, null, [], null, null);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<Series>(fixture.SeriesId, null),
            Times.Once);
    }

    private static async Task<IActionResult?> InvokeEndpoint(ControllerFixture fixture, string endpoint)
    {
        return endpoint switch
        {
            "movie-recommendations" => (await fixture.MoviesController.GetMovieRecommendations(
                null, null, [], cancellationToken: default)).Result,
            "upcoming" => (await fixture.TvShowsController.GetUpcomingEpisodes(
                null, null, null, [], null, null, null, [], null)).Result,
            "episodes" => (await fixture.TvShowsController.GetEpisodes(
                fixture.SeriesId, null, [], null, null, null, null, null, null, null, null, null, [], null, null)).Result,
            "seasons" => (await fixture.TvShowsController.GetSeasons(
                fixture.SeriesId, null, [], null, null, null, null, null, [], null)).Result,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint")
        };
    }

    private static ControllerFixture CreateFixture(
        bool includeUserIdClaim = true,
        bool userExists = true,
        bool isApiKey = false)
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);
        var libraryManager = new Mock<ILibraryManager>();
        var dtoService = new Mock<IDtoService>();
        var similarItemsManager = new Mock<ISimilarItemsManager>();
        var tvSeriesManager = new Mock<ITVSeriesManager>();
        var moviesController = new MoviesController(userManager.Object, dtoService.Object, similarItemsManager.Object);
        var tvShowsController = new TvShowsController(
            userManager.Object,
            libraryManager.Object,
            dtoService.Object,
            tvSeriesManager.Object);

        var claims = new List<Claim>();
        if (includeUserIdClaim)
        {
            claims.Add(new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")));
        }

        if (isApiKey)
        {
            claims.Add(new Claim(InternalClaimTypes.IsApiKey, bool.TrueString));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var httpContext = new DefaultHttpContext { User = principal };
        moviesController.ControllerContext = new ControllerContext { HttpContext = httpContext };
        tvShowsController.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return new ControllerFixture(
            moviesController,
            tvShowsController,
            Guid.NewGuid(),
            libraryManager,
            dtoService,
            similarItemsManager,
            tvSeriesManager);
    }

    private sealed record ControllerFixture(
        MoviesController MoviesController,
        TvShowsController TvShowsController,
        Guid SeriesId,
        Mock<ILibraryManager> LibraryManager,
        Mock<IDtoService> DtoService,
        Mock<ISimilarItemsManager> SimilarItemsManager,
        Mock<ITVSeriesManager> TvSeriesManager);
}
