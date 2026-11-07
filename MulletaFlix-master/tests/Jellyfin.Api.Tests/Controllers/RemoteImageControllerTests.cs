using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class RemoteImageControllerTests
{
    [Theory]
    [InlineData("pt-br", false)]
    [InlineData("en-us", false)]
    [InlineData("zh-hant", false)]
    [InlineData("pt-br", true)]
    [InlineData("en-us", true)]
    [InlineData("zh-hant", true)]
    public async Task DownloadRemoteImage_PersistsSelectedProviderAndLanguage(string language, bool isSeries)
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string imageUrl = "https://images.example/poster.jpg";
        const string providerName = "TheMovieDb";
        var item = CreateTestItem(itemId, isSeries);
        var image = new RemoteImageInfo
        {
            Url = imageUrl,
            Type = ImageType.Primary,
            ProviderName = providerName,
            Language = language
        };
        var providerManager = new Mock<IProviderManager>();
        providerManager
            .Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.IsAny<RemoteImageQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { image });
        providerManager
            .Setup(manager => manager.SaveImage(
                item,
                imageUrl,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImage(new ItemImageInfo { Path = "poster.jpg", Type = type }, index ?? 0))
            .Returns(Task.CompletedTask);

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, userId)).Returns(item);
        var controller = CreateController(providerManager.Object, libraryManager.Object, userId);

        var result = await controller.DownloadRemoteImage(itemId, ImageType.Primary, imageUrl, providerName);

        Assert.IsType<NoContentResult>(result);
        var savedImage = Assert.IsType<ItemImageInfo>(item.GetImageInfo(ImageType.Primary, 0));
        Assert.Equal(providerName, savedImage.ProviderName);
        Assert.Equal(language, savedImage.Language);
        providerManager.Verify(
            manager => manager.GetAvailableRemoteImages(
                item,
                It.Is<RemoteImageQuery>(query =>
                    query.ProviderName == providerName
                    && query.IncludeAllLanguages
                    && query.IncludeDisabledProviders
                    && query.ImageType == ImageType.Primary),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadRemoteImage_WhenProviderNoLongerReturnsSelectedUrl_StillSavesWithoutInventingProvenance()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string imageUrl = "https://images.example/poster.jpg";
        var item = new TestMovie { Id = itemId };
        var providerManager = new Mock<IProviderManager>();
        providerManager
            .Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.IsAny<RemoteImageQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RemoteImageInfo>());
        providerManager
            .Setup(manager => manager.SaveImage(
                item,
                imageUrl,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImage(new ItemImageInfo { Path = "poster.jpg", Type = type }, index ?? 0))
            .Returns(Task.CompletedTask);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, userId)).Returns(item);
        var controller = CreateController(providerManager.Object, libraryManager.Object, userId);

        var result = await controller.DownloadRemoteImage(itemId, ImageType.Primary, imageUrl, "TheMovieDb");

        Assert.IsType<NoContentResult>(result);
        var savedImage = Assert.IsType<ItemImageInfo>(item.GetImageInfo(ImageType.Primary, 0));
        Assert.Null(savedImage.ProviderName);
        Assert.Null(savedImage.Language);
        providerManager.Verify(
            manager => manager.SaveImage(
                item,
                imageUrl,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadRemoteImage_WithoutProviderName_RemainsCompatible()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string imageUrl = "https://images.example/poster.jpg";
        var item = new TestMovie { Id = itemId };
        var providerManager = new Mock<IProviderManager>();
        providerManager
            .Setup(manager => manager.SaveImage(
                item,
                imageUrl,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImage(new ItemImageInfo { Path = "poster.jpg", Type = type }, index ?? 0))
            .Returns(Task.CompletedTask);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, userId)).Returns(item);
        var controller = CreateController(providerManager.Object, libraryManager.Object, userId);

        var result = await controller.DownloadRemoteImage(itemId, ImageType.Primary, imageUrl);

        Assert.IsType<NoContentResult>(result);
        providerManager.Verify(
            manager => manager.GetAvailableRemoteImages(
                item,
                It.IsAny<RemoteImageQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(item.GetImageInfo(ImageType.Primary, 0)?.ProviderName);
    }

    private static RemoteImageController CreateController(
        IProviderManager providerManager,
        ILibraryManager libraryManager,
        Guid userId)
    {
        var controller = new RemoteImageController(providerManager, Mock.Of<IServerApplicationPaths>(), libraryManager);
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, userId.ToString())],
            "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static BaseItem CreateTestItem(Guid itemId, bool isSeries)
        => isSeries ? new TestSeries { Id = itemId } : new TestMovie { Id = itemId };

    private sealed class TestMovie : Movie
    {
        public override Task UpdateToRepositoryAsync(ItemUpdateType updateReason, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TestSeries : Series
    {
        public override Task UpdateToRepositoryAsync(ItemUpdateType updateReason, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
