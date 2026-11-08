using System;
using System.Security.Claims;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public sealed class ItemRefreshControllerTests
{
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IProviderManager> _providerManager;
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly ItemRefreshController _controller;
    private readonly Guid _userId;

    public ItemRefreshControllerTests()
    {
        _libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        _providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        _fileSystem = new Mock<IFileSystem>(MockBehavior.Loose);

        _userId = Guid.NewGuid();
        _controller = new ItemRefreshController(
            _libraryManager.Object,
            _providerManager.Object,
            _fileSystem.Object);

        var identity = new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.UserId, _userId.ToString("N")),
                new Claim(ClaimTypes.Role, UserRoles.Administrator)
            ],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public void RefreshItem_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = _controller.RefreshItem(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void RefreshItem_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns((BaseItem?)null);

        var result = _controller.RefreshItem(itemId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void RefreshItem_WithValidItem_QueuesRefreshAndReturnsNoContent()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId, IsShortcut = false };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);

        MetadataRefreshOptions? capturedOptions = null;
        _providerManager
            .Setup(m => m.QueueRefresh(itemId, It.IsAny<MetadataRefreshOptions>(), RefreshPriority.High))
            .Callback<Guid, MetadataRefreshOptions, RefreshPriority>((_, options, _) => capturedOptions = options);

        var result = _controller.RefreshItem(
            itemId,
            metadataRefreshMode: MetadataRefreshMode.Default,
            imageRefreshMode: MetadataRefreshMode.Default,
            replaceAllMetadata: false,
            replaceAllImages: false,
            regenerateTrickplay: false);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(capturedOptions);
        Assert.Equal(MetadataRefreshMode.Default, capturedOptions.MetadataRefreshMode);
        Assert.Equal(MetadataRefreshMode.Default, capturedOptions.ImageRefreshMode);
        Assert.False(capturedOptions.ReplaceAllMetadata);
        Assert.False(capturedOptions.ReplaceAllImages);
        Assert.False(capturedOptions.ForceSave);
        Assert.False(capturedOptions.EnableRemoteContentProbe);
    }

    [Fact]
    public void RefreshItem_WithFullRefreshModes_SetsForceSaveAndRemovesOldMetadata()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId, IsShortcut = false };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);

        MetadataRefreshOptions? capturedOptions = null;
        _providerManager
            .Setup(m => m.QueueRefresh(itemId, It.IsAny<MetadataRefreshOptions>(), RefreshPriority.High))
            .Callback<Guid, MetadataRefreshOptions, RefreshPriority>((_, options, _) => capturedOptions = options);

        var result = _controller.RefreshItem(
            itemId,
            metadataRefreshMode: MetadataRefreshMode.FullRefresh,
            imageRefreshMode: MetadataRefreshMode.FullRefresh,
            replaceAllMetadata: true,
            replaceAllImages: true,
            regenerateTrickplay: true);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(capturedOptions);
        Assert.Equal(MetadataRefreshMode.FullRefresh, capturedOptions.MetadataRefreshMode);
        Assert.Equal(MetadataRefreshMode.FullRefresh, capturedOptions.ImageRefreshMode);
        Assert.True(capturedOptions.ReplaceAllMetadata);
        Assert.True(capturedOptions.ReplaceAllImages);
        Assert.True(capturedOptions.ForceSave);
        Assert.True(capturedOptions.RemoveOldMetadata);
        Assert.True(capturedOptions.RegenerateTrickplay);
    }

    [Fact]
    public void RefreshItem_WithShortcutItemAndFullRefresh_EnablesRemoteContentProbe()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId, IsShortcut = true };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);

        MetadataRefreshOptions? capturedOptions = null;
        _providerManager
            .Setup(m => m.QueueRefresh(itemId, It.IsAny<MetadataRefreshOptions>(), RefreshPriority.High))
            .Callback<Guid, MetadataRefreshOptions, RefreshPriority>((_, options, _) => capturedOptions = options);

        var result = _controller.RefreshItem(
            itemId,
            metadataRefreshMode: MetadataRefreshMode.FullRefresh);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions.EnableRemoteContentProbe);
    }
}
