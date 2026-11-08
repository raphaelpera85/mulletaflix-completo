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
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.PlaylistDtos;
using MulletaFlix.Database.Implementations.Entities;
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

    [Fact]
    public async Task UpdatePlaylist_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.UpdatePlaylist(Guid.Empty, new UpdatePlaylistDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task UpdatePlaylist_NullRequest_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.UpdatePlaylist(Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Update playlist request is required.", badRequest.Value);
    }

    [Fact]
    public void GetPlaylist_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetPlaylist(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetPlaylistUsers_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetPlaylistUsers(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetPlaylistUser_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetPlaylistUser(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetPlaylistUser_EmptyUserId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetPlaylistUser(Guid.NewGuid(), Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task UpdatePlaylistUser_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.UpdatePlaylistUser(Guid.Empty, Guid.NewGuid(), new UpdatePlaylistUserDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task UpdatePlaylistUser_EmptyUserId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.UpdatePlaylistUser(Guid.NewGuid(), Guid.Empty, new UpdatePlaylistUserDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task UpdatePlaylistUser_NullRequest_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.UpdatePlaylistUser(Guid.NewGuid(), Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Update playlist user request is required.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveUserFromPlaylist_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveUserFromPlaylist(Guid.Empty, Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveUserFromPlaylist_EmptyUserId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveUserFromPlaylist(Guid.NewGuid(), Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task AddItemToPlaylist_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddItemToPlaylist(Guid.Empty, [Guid.NewGuid()], null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task AddItemToPlaylist_EmptyIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddItemToPlaylist(Guid.NewGuid(), Array.Empty<Guid>(), null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids must not be empty.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task MoveItem_InvalidPlaylistId_ReturnsBadRequest(string? playlistId)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.MoveItem(playlistId!, "item-1", 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Valid playlist id is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MoveItem_EmptyItemId_ReturnsBadRequest(string? itemId)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.MoveItem(Guid.NewGuid().ToString("D"), itemId!, 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id is required.", badRequest.Value);
    }

    [Fact]
    public async Task MoveItem_NegativeIndex_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.MoveItem(Guid.NewGuid().ToString("D"), "item-1", -1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Index must be greater than or equal to 0.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task RemoveItemFromPlaylist_InvalidPlaylistId_ReturnsBadRequest(string? playlistId)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveItemFromPlaylist(playlistId!, ["item-1"]);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Valid playlist id is required.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveItemFromPlaylist_EmptyEntryIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveItemFromPlaylist(Guid.NewGuid().ToString("D"), Array.Empty<string>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Entry ids must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylistItems_EmptyPlaylistId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylistItems(Guid.Empty, null, null, null, null!, null, null, null, null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Playlist id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylists_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylists(startIndex: -1, limit: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylists_WithNegativeLimit_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylists(startIndex: null, limit: -5);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePlaylist_WithNullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.CreatePlaylist(name, Array.Empty<Guid>(), null, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Playlist name is required.", badRequest.Value);
    }

    [Fact]
    public async Task AddItemToPlaylist_WithNegativePosition_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddItemToPlaylist(Guid.NewGuid(), [Guid.NewGuid()], position: -1, userId: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Position must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylistItems_WithNegativeStartIndex_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylistItems(Guid.NewGuid(), null, startIndex: -1, limit: null, null!, null, null, null, null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("StartIndex must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylistItems_WithNegativeLimit_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylistItems(Guid.NewGuid(), null, startIndex: null, limit: -1, null!, null, null, null, null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Limit must be greater than or equal to 0.", badRequest.Value);
    }

    [Fact]
    public async Task GetPlaylistItems_WithNegativeImageTypeLimit_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetPlaylistItems(Guid.NewGuid(), null, startIndex: null, limit: null, null!, null, null, imageTypeLimit: -1, null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("ImageTypeLimit must be greater than or equal to 0.", badRequest.Value);
    }

    private static PlaylistsController CreateController(Guid userId)
    {
        return CreateController(
            userId,
            Mock.Of<IDtoService>(),
            Mock.Of<IPlaylistManager>(),
            Mock.Of<IUserManager>());
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
