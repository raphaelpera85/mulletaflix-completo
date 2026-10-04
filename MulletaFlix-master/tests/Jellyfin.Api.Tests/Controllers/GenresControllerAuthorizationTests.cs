using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;
using Genre = MediaBrowser.Controller.Entities.Genre;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class GenresControllerAuthorizationTests
{
    [Theory]
    [InlineData("list", false)]
    [InlineData("list", true)]
    [InlineData("genre", false)]
    [InlineData("genre", true)]
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
        Assert.Empty(fixture.DtoService.Invocations);
    }

    [Fact]
    public async Task GetGenre_ApiKeyWithoutUserRemainsSupported()
    {
        var fixture = CreateFixture(isApiKey: true, includeUserIdClaim: false);
        var genre = new Genre();
        var dto = new BaseItemDto();
        fixture.LibraryManager.Setup(library => library.GetGenre("Drama")).Returns(genre);
        fixture.DtoService.Setup(service => service.GetBaseItemDtoAsync(
                genre,
                It.IsAny<DtoOptions>(),
                null,
                null))
            .ReturnsAsync(dto);

        var result = await fixture.Controller.GetGenre("Drama", null);

        Assert.Same(dto, result.Value);
        fixture.LibraryManager.Verify(library => library.GetGenre("Drama"), Times.Once);
        fixture.DtoService.Verify(
            service => service.GetBaseItemDtoAsync(
                genre,
                It.IsAny<DtoOptions>(),
                null,
                null),
            Times.Once);
    }

    private static async Task<IActionResult?> InvokeEndpoint(ControllerFixture fixture, string endpoint)
    {
        if (endpoint == "list")
        {
            return fixture.Controller.GetGenres(
                null,
                null,
                null,
                null,
                [],
                [],
                [],
                null,
                null,
                [],
                null,
                null,
                null,
                null,
                [],
                []).Result;
        }

        return (await fixture.Controller.GetGenre("Drama", null)).Result;
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
        var controller = new GenresController(userManager.Object, libraryManager.Object, dtoService.Object);

        var claims = new List<Claim>();
        if (includeUserIdClaim)
        {
            claims.Add(new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")));
        }

        if (isApiKey)
        {
            claims.Add(new Claim(InternalClaimTypes.IsApiKey, bool.TrueString));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return new ControllerFixture(controller, libraryManager, dtoService);
    }

    private sealed record ControllerFixture(
        GenresController Controller,
        Mock<ILibraryManager> LibraryManager,
        Mock<IDtoService> DtoService);
}
