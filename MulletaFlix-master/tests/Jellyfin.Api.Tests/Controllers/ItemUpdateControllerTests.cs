using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MulletaFlix.Data.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public sealed class ItemUpdateControllerTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IProviderManager> _providerManager;
    private readonly Mock<ILocalizationManager> _localizationManager;
    private readonly Mock<IServerConfigurationManager> _serverConfigurationManager;
    private readonly ItemUpdateController _controller;
    private readonly Guid _userId;

    public ItemUpdateControllerTests()
    {
        _fileSystem = new Mock<IFileSystem>(MockBehavior.Loose);
        _libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        _providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        _localizationManager = new Mock<ILocalizationManager>(MockBehavior.Strict);
        _serverConfigurationManager = new Mock<IServerConfigurationManager>(MockBehavior.Strict);

        _userId = Guid.NewGuid();
        _controller = new ItemUpdateController(
            _fileSystem.Object,
            _libraryManager.Object,
            _providerManager.Object,
            _localizationManager.Object,
            _serverConfigurationManager.Object);

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
    public async Task UpdateItem_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItem(Guid.Empty, new BaseItemDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task UpdateItem_WithNullRequest_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItem(Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item data is required.", badRequest.Value);
    }

    [Fact]
    public async Task UpdateItem_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns((BaseItem?)null);

        var result = await _controller.UpdateItem(itemId, new BaseItemDto());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateItem_WithValidMovieItem_UpdatesItemAndReturnsNoContent()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId, Name = "Original Name" };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);
        _libraryManager
            .Setup(m => m.UpdateItemAsync(item, null, ItemUpdateType.MetadataEdit, CancellationToken.None))
            .Returns(Task.CompletedTask);
        _libraryManager
            .Setup(m => m.GetLocalAlternateVersionIds(item))
            .Returns(new List<Guid>());

        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = _libraryManager.Object;
        try
        {
            var request = new BaseItemDto
            {
                Name = "Updated Name",
                Genres = ["Action", "Sci-Fi"],
                Tags = ["Tag1"]
            };

            var result = await _controller.UpdateItem(itemId, request);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Updated Name", item.Name);
            Assert.Equal(2, item.Genres.Length);
            Assert.Contains("Action", item.Genres);
            Assert.Contains("Sci-Fi", item.Genres);
            Assert.Single(item.Tags);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }

        _libraryManager.Verify(
            m => m.UpdateItemAsync(item, null, ItemUpdateType.MetadataEdit, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task UpdateItem_WithSeriesDisplayOrderChanged_QueuesFullRefresh()
    {
        var itemId = Guid.NewGuid();
        var series = new Series { Id = itemId, DisplayOrder = "aired" };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(series);
        _libraryManager
            .Setup(m => m.UpdateItemAsync(series, null, ItemUpdateType.MetadataEdit, CancellationToken.None))
            .Returns(Task.CompletedTask);

        MetadataRefreshOptions? capturedOptions = null;
        _providerManager
            .Setup(m => m.QueueRefresh(itemId, It.IsAny<MetadataRefreshOptions>(), RefreshPriority.High))
            .Callback<Guid, MetadataRefreshOptions, RefreshPriority>((_, options, _) => capturedOptions = options);

        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = _libraryManager.Object;
        try
        {
            var request = new BaseItemDto
            {
                Name = "Series Name",
                DisplayOrder = "dvd"
            };

            var result = await _controller.UpdateItem(itemId, request);

            Assert.IsType<NoContentResult>(result);
            Assert.NotNull(capturedOptions);
            Assert.Equal(MetadataRefreshMode.FullRefresh, capturedOptions.MetadataRefreshMode);
            Assert.Equal(MetadataRefreshMode.FullRefresh, capturedOptions.ImageRefreshMode);
            Assert.True(capturedOptions.ReplaceAllMetadata);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }

    [Fact]
    public void GetMetadataEditorInfo_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = _controller.GetMetadataEditorInfo(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetMetadataEditorInfo_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns((BaseItem?)null);

        var result = _controller.GetMetadataEditorInfo(itemId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetMetadataEditorInfo_WithValidItem_ReturnsMetadataEditorInfo()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);
        _libraryManager
            .Setup(m => m.GetInheritedContentType(item))
            .Returns((CollectionType?)null);
        _libraryManager
            .Setup(m => m.GetConfiguredContentType(item))
            .Returns((CollectionType?)null);

        _localizationManager
            .Setup(m => m.GetParentalRatings())
            .Returns(new List<ParentalRating>());
        _localizationManager
            .Setup(m => m.GetCountries())
            .Returns(Array.Empty<CountryInfo>());
        _localizationManager
            .Setup(m => m.GetCultures())
            .Returns(Array.Empty<CultureDto>());
        _localizationManager
            .Setup(m => m.GetLocalizedString(It.IsAny<string>()))
            .Returns<string>(s => s);

        _providerManager
            .Setup(m => m.GetExternalIdInfos(item))
            .Returns(Array.Empty<ExternalIdInfo>());

        var result = _controller.GetMetadataEditorInfo(itemId);

        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.ParentalRatingOptions);
        Assert.NotNull(result.Value.ExternalIdInfos);
        Assert.NotNull(result.Value.Countries);
        Assert.NotNull(result.Value.Cultures);
    }

    [Fact]
    public void UpdateItemContentType_WithEmptyItemId_ReturnsBadRequest()
    {
        var result = _controller.UpdateItemContentType(Guid.Empty, "movies");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateItemContentType_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns((BaseItem?)null);

        var result = _controller.UpdateItemContentType(itemId, "movies");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void UpdateItemContentType_WithValidItem_UpdatesConfigurationAndReturnsNoContent()
    {
        var itemId = Guid.NewGuid();
        var item = new Folder { Id = itemId, Path = "C:\\Media\\Movies" };

        _libraryManager
            .Setup(m => m.GetItemById<BaseItem>(itemId, _userId))
            .Returns(item);

        var config = new ServerConfiguration
        {
            ContentTypes =
            [
                new NameValuePair { Name = "C:\\Media\\Other", Value = "tvshows" }
            ]
        };

        _serverConfigurationManager
            .SetupGet(m => m.Configuration)
            .Returns(config);
        _serverConfigurationManager
            .Setup(m => m.SaveConfiguration());

        var result = _controller.UpdateItemContentType(itemId, "movies");

        Assert.IsType<NoContentResult>(result);
        _serverConfigurationManager.Verify(m => m.SaveConfiguration(), Times.Once);
        Assert.Contains(config.ContentTypes, c => c.Name == item.ContainingFolderPath && c.Value == "movies");
    }
}
