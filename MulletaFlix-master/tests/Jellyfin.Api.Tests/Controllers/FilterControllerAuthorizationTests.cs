using System;
using System.Collections.Generic;
using System.Security.Claims;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Globalization;
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

public class FilterControllerAuthorizationTests
{
    [Theory]
    [InlineData("legacy", false)]
    [InlineData("legacy", true)]
    [InlineData("current", false)]
    [InlineData("current", true)]
    public void ReadEndpoints_RejectMissingOrUnresolvedUserBeforeReadingLibrary(string endpoint, bool includeUserIdClaim)
    {
        var fixture = CreateController(
            userExists: !includeUserIdClaim,
            includeUserIdClaim: includeUserIdClaim);

        var result = endpoint == "legacy"
            ? fixture.Controller.GetQueryFiltersLegacy(null, fixture.ParentId, [], []).Result
            : fixture.Controller.GetQueryFilters(
                null,
                fixture.ParentId,
                [],
                null,
                null,
                null,
                null,
                null,
                null,
                null).Result;

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(fixture.LibraryManager.Invocations);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("current")]
    public void ReadEndpoints_ApiKeyWithoutUserRemainsSupported(string endpoint)
    {
        var fixture = CreateController(includeUserIdClaim: false, isApiKey: true);

        if (endpoint == "legacy")
        {
            var result = fixture.Controller.GetQueryFiltersLegacy(null, fixture.ParentId, [], []);
            Assert.IsType<QueryFiltersLegacy>(result.Value);
        }
        else
        {
            var result = fixture.Controller.GetQueryFilters(
                null,
                fixture.ParentId,
                [],
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            Assert.IsType<QueryFilters>(result.Value);
        }

        Assert.Single(fixture.LibraryManager.Invocations);
    }

    [Fact]
    public void GetQueryFiltersLegacy_DoesNotExposeFiltersForParentOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = fixture.Controller.GetQueryFiltersLegacy(fixture.User.Id, fixture.ParentId, [], []);

        Assert.IsType<QueryFiltersLegacy>(result.Value);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ParentId, fixture.User),
            Times.Once);
        fixture.LibraryManager.Verify(
            library => library.GetParentItem(It.IsAny<Guid?>(), It.IsAny<Guid?>()),
            Times.Never);
        fixture.LibraryManager.Verify(
            library => library.GetQueryFiltersLegacy(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    [Fact]
    public void GetQueryFiltersLegacy_RejectsUnknownRequestUser()
    {
        var fixture = CreateController(userExists: false);

        var result = fixture.Controller.GetQueryFiltersLegacy(fixture.User.Id, fixture.ParentId, [], []);

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public void GetQueryFilters_DoesNotExposeFiltersForParentOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = fixture.Controller.GetQueryFilters(
            fixture.User.Id,
            fixture.ParentId,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Empty(result.Value!.Genres);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ParentId, fixture.User),
            Times.Once);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.ParentId),
            Times.Never);
        fixture.LibraryManager.Verify(
            library => library.GetGenres(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    [Fact]
    public void GetQueryFilters_RejectsUnknownRequestUser()
    {
        var fixture = CreateController(userExists: false);

        var result = fixture.Controller.GetQueryFilters(
            fixture.User.Id,
            fixture.ParentId,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    private static ControllerFixture CreateController(
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
        var parentId = Guid.NewGuid();

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(parentId, user)).Returns((BaseItem?)null);

        var controller = new FilterController(
            libraryManager.Object,
            userManager.Object,
            Mock.Of<ILocalizationManager>());
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

        return new ControllerFixture(controller, user, parentId, libraryManager);
    }

    private sealed record ControllerFixture(
        FilterController Controller,
        User User,
        Guid ParentId,
        Mock<ILibraryManager> LibraryManager);
}
