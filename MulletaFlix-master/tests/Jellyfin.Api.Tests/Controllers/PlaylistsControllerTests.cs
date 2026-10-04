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
using MediaBrowser.Model.Playlists;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class PlaylistsControllerTests
{
    [Fact]
    public async Task GetPlaylists_ReturnsSavedPlaylistsInSortedPaginatedResult()
    {
        var userId = Guid.NewGuid();
        var createdPlaylist = new Playlist { Name = "Saved playlist", SortName = "saved playlist", OwnerUserId = userId };
        var otherPlaylist = new Playlist { Name = "Another playlist", SortName = "another playlist", OwnerUserId = userId };
        IReadOnlyList<BaseItem>? mappedItems = null;

        var playlistManager = new Mock<IPlaylistManager>(MockBehavior.Strict);
        playlistManager.Setup(manager => manager.GetPlaylists(userId))
            .Returns([createdPlaylist, otherPlaylist]);

        var dtoService = new Mock<IDtoService>(MockBehavior.Strict);
        dtoService.Setup(service => service.GetBaseItemDtosAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<User>(),
                null,
                true))
            .Callback<IReadOnlyList<BaseItem>, DtoOptions, User, BaseItem?, bool>((items, _, _, _, _) => mappedItems = items)
            .ReturnsAsync(Array.Empty<BaseItemDto>());

        var userManager = new Mock<IUserManager>(MockBehavior.Strict);
        userManager.Setup(manager => manager.GetUserById(userId)).Returns((User?)null);
        var controller = CreateController(userId, dtoService.Object, playlistManager.Object, userManager.Object);

        var result = await controller.GetPlaylists(startIndex: 1, limit: 1);

        var page = Assert.IsType<QueryResult<BaseItemDto>>(result.Value);
        Assert.Equal(1, page.StartIndex);
        Assert.Equal(2, page.TotalRecordCount);
        Assert.Empty(page.Items);
        Assert.NotNull(mappedItems);
        Assert.Single(mappedItems);
        Assert.Equal("Saved playlist", mappedItems[0].Name);
        playlistManager.VerifyAll();
        userManager.VerifyAll();
        dtoService.VerifyAll();
    }

    [Fact]
    public async Task GetPlaylists_ReturnsEmptyResultWhenUserHasNoPlaylists()
    {
        var userId = Guid.NewGuid();
        var playlistManager = new Mock<IPlaylistManager>(MockBehavior.Strict);
        playlistManager.Setup(manager => manager.GetPlaylists(userId)).Returns([]);

        var dtoService = new Mock<IDtoService>(MockBehavior.Strict);
        dtoService.Setup(service => service.GetBaseItemDtosAsync(
                It.Is<IReadOnlyList<BaseItem>>(items => items.Count == 0),
                It.IsAny<DtoOptions>(),
                It.IsAny<User>(),
                null,
                true))
            .ReturnsAsync(Array.Empty<BaseItemDto>());

        var userManager = new Mock<IUserManager>(MockBehavior.Strict);
        userManager.Setup(manager => manager.GetUserById(userId)).Returns((User?)null);
        var controller = CreateController(userId, dtoService.Object, playlistManager.Object, userManager.Object);

        var result = await controller.GetPlaylists(null, null);

        var page = Assert.IsType<QueryResult<BaseItemDto>>(result.Value);
        Assert.Equal(0, page.TotalRecordCount);
        Assert.Empty(page.Items);
        playlistManager.VerifyAll();
        userManager.VerifyAll();
        dtoService.VerifyAll();
    }

    private static PlaylistsController CreateController(
        Guid userId,
        IDtoService dtoService,
        IPlaylistManager playlistManager,
        IUserManager userManager)
    {
        return new PlaylistsController(dtoService, playlistManager, userManager, Mock.Of<ILibraryManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, userId.ToString("D"))],
                        "test"))
                }
            }
        };
    }
}
