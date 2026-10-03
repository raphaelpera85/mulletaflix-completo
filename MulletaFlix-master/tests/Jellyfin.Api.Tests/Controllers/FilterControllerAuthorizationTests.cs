using System;
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

    private static ControllerFixture CreateController(bool userExists = true)
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
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, user.Id.ToString("N"))],
            "TestAuth");
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
