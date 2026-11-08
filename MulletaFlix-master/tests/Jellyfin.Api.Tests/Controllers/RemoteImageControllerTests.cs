using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public sealed class RemoteImageControllerTests
{
    [Fact]
    public async Task DownloadRemoteImage_PersistsSelectedRemoteImageProvenance()
    {
        var itemId = Guid.NewGuid();
        var item = new Book { Id = itemId };
        var image = new RemoteImageInfo
        {
            Url = "https://images.example.test/poster.jpg",
            Type = ImageType.Primary,
            ProviderName = "ExampleProvider",
            Language = "pt-BR"
        };
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager
            .Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.Is<RemoteImageQuery>(query =>
                    query.ImageType == ImageType.Primary &&
                    query.IncludeAllLanguages &&
                    query.IncludeDisabledProviders),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { image });
        providerManager
            .Setup(manager => manager.SaveImage(
                item,
                image.Url,
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, _, _) =>
                savedItem.AddImage(new ItemImageInfo { Type = type, Path = image.Url }))
            .Returns(Task.CompletedTask);

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>()))
            .Returns(item);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new RemoteImageController(
            providerManager.Object,
            Mock.Of<IServerApplicationPaths>(),
            libraryManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;

        try
        {
            var result = await controller.DownloadRemoteImage(itemId, ImageType.Primary, image.Url);

            Assert.IsType<NoContentResult>(result);
            var savedImage = item.GetImageInfo(ImageType.Primary, 0);
            Assert.NotNull(savedImage);
            Assert.Equal(image.ProviderName, savedImage.ProviderName);
            Assert.Equal(image.Language, savedImage.Language);
            libraryManager.Verify(
                manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }

    [Fact]
    public async Task DownloadRemoteImage_PropagatesRequestCancellationToProviderLookup()
    {
        var itemId = Guid.NewGuid();
        var item = new Book { Id = itemId };
        using var cancellationSource = new CancellationTokenSource();
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var observedToken = default(CancellationToken);
        providerManager
            .Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.IsAny<RemoteImageQuery>(),
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, RemoteImageQuery, CancellationToken>((_, _, token) => observedToken = token)
            .ThrowsAsync(new OperationCanceledException());

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>()))
            .Returns(item);
        var controller = new RemoteImageController(
            providerManager.Object,
            Mock.Of<IServerApplicationPaths>(),
            libraryManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestAborted = cancellationSource.Token,
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.DownloadRemoteImage(itemId, ImageType.Primary, "https://images.example.test/poster.jpg"));

        Assert.Equal(cancellationSource.Token, observedToken);
        providerManager.Verify(
            manager => manager.SaveImage(
                It.IsAny<BaseItem>(),
                It.IsAny<string>(),
                It.IsAny<ImageType>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRemoteImages_PropagatesRequestCancellationToProviderLookup()
    {
        var itemId = Guid.NewGuid();
        var item = new Book { Id = itemId };
        using var cancellationSource = new CancellationTokenSource();
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var observedToken = default(CancellationToken);
        providerManager
            .Setup(manager => manager.GetAvailableRemoteImages(
                item,
                It.IsAny<RemoteImageQuery>(),
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, RemoteImageQuery, CancellationToken>((_, _, token) => observedToken = token)
            .ThrowsAsync(new OperationCanceledException());

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>()))
            .Returns(item);
        var controller = new RemoteImageController(
            providerManager.Object,
            Mock.Of<IServerApplicationPaths>(),
            libraryManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestAborted = cancellationSource.Token,
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.GetRemoteImages(itemId, ImageType.Primary, null, null, null));

        Assert.Equal(cancellationSource.Token, observedToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadRemoteImage_EmptyImageUrl_ReturnsBadRequest(string? imageUrl)
    {
        var itemId = Guid.NewGuid();
        var item = new Book { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>()))
            .Returns(item);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var controller = new RemoteImageController(
            providerManager.Object,
            Mock.Of<IServerApplicationPaths>(),
            libraryManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.DownloadRemoteImage(itemId, ImageType.Primary, imageUrl);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
