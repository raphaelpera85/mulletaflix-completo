using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
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

public sealed class InstantMixControllerAuthorizationTests
{
    [Theory]
    [InlineData("song", true)]
    [InlineData("song", false)]
    [InlineData("album", true)]
    [InlineData("album", false)]
    [InlineData("playlist", true)]
    [InlineData("playlist", false)]
    [InlineData("genre-name", true)]
    [InlineData("genre-name", false)]
    [InlineData("artist", true)]
    [InlineData("artist", false)]
    [InlineData("item", true)]
    [InlineData("item", false)]
    [InlineData("artist-legacy", true)]
    [InlineData("artist-legacy", false)]
    [InlineData("genre-id", true)]
    [InlineData("genre-id", false)]
    public async Task ReadEndpoints_ReturnUnauthorizedWhenUserIsMissingOrCannotBeResolved(
        string endpoint,
        bool includeUserIdClaim)
    {
        var fixture = CreateFixture(userExists: !includeUserIdClaim, includeUserIdClaim: includeUserIdClaim);

        var result = await InvokeEndpoint(fixture, endpoint);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(fixture.LibraryManager.Invocations);
        Assert.Empty(fixture.MusicManager.Invocations);
        Assert.Empty(fixture.DtoService.Invocations);
    }

    [Fact]
    public async Task GetInstantMixFromGenre_ApiKeyWithoutUserRemainsSupported()
    {
        var fixture = CreateFixture(isApiKey: true, includeUserIdClaim: false);
        fixture.MusicManager
            .Setup(manager => manager.GetInstantMixFromGenres(
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<DtoOptions>()))
            .Returns(Array.Empty<BaseItem>());
        fixture.DtoService
            .Setup(service => service.GetBaseItemDtosAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<DtoOptions>(),
                null,
                null,
                false))
            .ReturnsAsync(Array.Empty<BaseItemDto>());

#pragma warning disable CS0618
        var result = await fixture.Controller.GetInstantMixFromMusicGenreByName(
            "Drama", null, null, [], null, null, null, []);
#pragma warning restore CS0618

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
        fixture.MusicManager.Verify(
            manager => manager.GetInstantMixFromGenres(
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<DtoOptions>()),
            Times.Once);
    }

    private static async Task<IActionResult?> InvokeEndpoint(ControllerFixture fixture, string endpoint)
    {
        var itemId = Guid.NewGuid();
#pragma warning disable CS0618
        return endpoint switch
        {
            "song" => (await fixture.Controller.GetInstantMixFromSong(itemId, null, null, [], null, null, null, [])).Result,
            "album" => (await fixture.Controller.GetInstantMixFromAlbum(itemId, null, null, [], null, null, null, [])).Result,
            "playlist" => (await fixture.Controller.GetInstantMixFromPlaylist(itemId, null, null, [], null, null, null, [])).Result,
            "genre-name" => (await fixture.Controller.GetInstantMixFromMusicGenreByName("Drama", null, null, [], null, null, null, [])).Result,
            "artist" => (await fixture.Controller.GetInstantMixFromArtists(itemId, null, null, [], null, null, null, [])).Result,
            "item" => (await fixture.Controller.GetInstantMixFromItem(itemId, null, null, [], null, null, null, [])).Result,
            "artist-legacy" => (await fixture.Controller.GetInstantMixFromArtists2(itemId, null, null, [], null, null, null, [])).Result,
            "genre-id" => (await fixture.Controller.GetInstantMixFromMusicGenreById(itemId, null, null, [], null, null, null, [])).Result,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint")
        };
#pragma warning restore CS0618
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

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);
        var dtoService = new Mock<IDtoService>();
        var libraryManager = new Mock<ILibraryManager>();
        var musicManager = new Mock<IMusicManager>();
        var controller = new InstantMixController(
            userManager.Object,
            dtoService.Object,
            musicManager.Object,
            libraryManager.Object);

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

        return new ControllerFixture(controller, libraryManager, musicManager, dtoService);
    }

    private sealed record ControllerFixture(
        InstantMixController Controller,
        Mock<ILibraryManager> LibraryManager,
        Mock<IMusicManager> MusicManager,
        Mock<IDtoService> DtoService);
}
