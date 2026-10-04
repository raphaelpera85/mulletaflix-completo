using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LibraryControllerAuthorizationTests
{
    [Theory]
    [InlineData("theme-songs", false)]
    [InlineData("theme-songs", true)]
    [InlineData("theme-videos", false)]
    [InlineData("theme-videos", true)]
    [InlineData("theme-media", false)]
    [InlineData("theme-media", true)]
    [InlineData("item-counts", false)]
    [InlineData("item-counts", true)]
    [InlineData("ancestors", false)]
    [InlineData("ancestors", true)]
    [InlineData("similar-items", false)]
    [InlineData("similar-items", true)]
    [InlineData("download", false)]
    [InlineData("download", true)]
    public async Task ReadEndpoints_RejectMissingOrUnresolvedUserBeforeReadingLibrary(
        string endpoint,
        bool includeUserIdClaim)
    {
        var fixture = CreateFixture(includeUserIdClaim, userExists: !includeUserIdClaim);

        var result = await InvokeEndpoint(fixture, endpoint);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(fixture.LibraryManager.Invocations);
        Assert.Empty(fixture.DtoService.Invocations);
        Assert.Empty(fixture.SimilarItemsManager.Invocations);
    }

    [Theory]
    [InlineData("theme-songs")]
    [InlineData("theme-videos")]
    [InlineData("theme-media")]
    [InlineData("item-counts")]
    [InlineData("ancestors")]
    [InlineData("similar-items")]
    [InlineData("download")]
    public async Task Endpoints_ApiKeyWithoutUserRemainSupported(string endpoint)
    {
        var fixture = CreateFixture(includeUserIdClaim: false, userExists: false, isApiKey: true);

        var result = await InvokeEndpoint(fixture, endpoint);

        Assert.NotNull(result);
        Assert.IsNotType<UnauthorizedResult>(result);
        Assert.NotEmpty(fixture.LibraryManager.Invocations);
    }

    private static async Task<IActionResult?> InvokeEndpoint(ControllerFixture fixture, string endpoint)
    {
        return endpoint switch
        {
            "theme-songs" => (await fixture.Controller.GetThemeSongs(fixture.ItemId, null)).Result,
            "theme-videos" => (await fixture.Controller.GetThemeVideos(fixture.ItemId, null)).Result,
            "theme-media" => (await fixture.Controller.GetThemeMedia(fixture.ItemId, null)).Result,
            "item-counts" => GetItemCounts(fixture.Controller),
            "ancestors" => (await fixture.Controller.GetAncestors(fixture.ItemId, null)).Result,
            "similar-items" => (await fixture.Controller.GetSimilarItems(
                fixture.ItemId, [], null, null, [], CancellationToken.None)).Result,
            "download" => await fixture.Controller.GetDownload(fixture.ItemId),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint")
        };
    }

    private static IActionResult? GetItemCounts(LibraryController controller)
    {
        var result = controller.GetItemCounts(null, null);
        return result.Result ?? new OkObjectResult(result.Value);
    }

    private static ControllerFixture CreateFixture(bool includeUserIdClaim, bool userExists, bool isApiKey = false)
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
        var controller = new LibraryController(
            Mock.Of<IProviderManager>(),
            similarItemsManager.Object,
            libraryManager.Object,
            userManager.Object,
            Mock.Of<ICollectionManager>(),
            dtoService.Object,
            Mock.Of<IActivityManager>(),
            Mock.Of<ILocalizationManager>(),
            Mock.Of<ILibraryMonitor>(),
            Mock.Of<ILogger<LibraryController>>(),
            Mock.Of<IServerConfigurationManager>());

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

        return new ControllerFixture(controller, Guid.NewGuid(), libraryManager, dtoService, similarItemsManager);
    }

    private sealed record ControllerFixture(
        LibraryController Controller,
        Guid ItemId,
        Mock<ILibraryManager> LibraryManager,
        Mock<IDtoService> DtoService,
        Mock<ISimilarItemsManager> SimilarItemsManager);
}
