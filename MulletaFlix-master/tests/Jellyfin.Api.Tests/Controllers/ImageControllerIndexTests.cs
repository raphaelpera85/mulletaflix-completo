using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public sealed class ImageControllerIndexTests
{
    [Fact]
    public async Task SetItemImageByIndex_PassesRouteIndexToProviderManager()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);
        libraryManager.Setup(manager => manager.GetLocalAlternateVersionIds(item)).Returns(Array.Empty<Guid>());
        libraryManager.Setup(manager => manager.UpdateItemAsync(item, null, It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.SaveImage(
                item,
                It.IsAny<Stream>(),
                "image/png",
                ImageType.Backdrop,
                2,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            providerManager.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        controller.Request.ContentType = "image/png";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("AQ=="));

        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;
        try
        {
            var result = await controller.SetItemImageByIndex(itemId, ImageType.Backdrop, 2);
            Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }

        providerManager.VerifyAll();
    }

    [Fact]
    public async Task SetItemImageByIndex_PropagatesRequestCancellation()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var observedSaveToken = CancellationToken.None;
        var observedUpdateToken = CancellationToken.None;

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);
        libraryManager.Setup(manager => manager.GetLocalAlternateVersionIds(item)).Returns(Array.Empty<Guid>());
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(item, null, It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Callback<BaseItem, BaseItem?, ItemUpdateType, CancellationToken>((_, _, _, token) => observedUpdateToken = token)
            .Returns(Task.CompletedTask);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager
            .Setup(manager => manager.SaveImage(
                item,
                It.IsAny<Stream>(),
                "image/png",
                ImageType.Backdrop,
                2,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, Stream, string, ImageType, int?, CancellationToken>((_, _, _, _, _, token) => observedSaveToken = token)
            .Returns(Task.CompletedTask);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            providerManager.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestAborted = cancellationSource.Token,
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        controller.Request.ContentType = "image/png";
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("AQ=="));

        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;
        try
        {
            await controller.SetItemImageByIndex(itemId, ImageType.Backdrop, 2);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }

        Assert.Equal(cancellationSource.Token, observedSaveToken);
        Assert.Equal(cancellationSource.Token, observedUpdateToken);
    }

    [Fact]
    public async Task GetItemImageInfos_PropagatesRequestCancellationToUpdateImages()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        item.AddImage(new ItemImageInfo
        {
            Path = "https://images.example.test/poster.jpg",
            Type = ImageType.Primary
        });
        using var cancellationSource = new CancellationTokenSource();
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);
        libraryManager
            .Setup(manager => manager.UpdateImagesAsync(item, It.IsAny<bool>(), cancellationSource.Token))
            .Returns(Task.CompletedTask);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestAborted = cancellationSource.Token,
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        await controller.GetItemImageInfos(itemId);

        libraryManager.Verify(manager => manager.UpdateImagesAsync(item, false, cancellationSource.Token), Times.Once);
    }

    [Fact]
    public async Task GetItemImage_RemoteStubConversionFails_ReturnsNoContentInsteadOfThrowing()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        item.AddImage(new ItemImageInfo
        {
            Path = "https://images.example.test/unavailable-poster.jpg",
            Type = ImageType.Primary
        });

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);
        libraryManager
            .Setup(manager => manager.ConvertImageToLocal(
                item,
                It.IsAny<ItemImageInfo>(),
                0,
                true,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unable to convert any images to local"));

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var result = await controller.GetItemImage(
            itemId,
            ImageType.Primary,
            maxWidth: null,
            maxHeight: null,
            width: null,
            height: null,
            quality: null,
            fillWidth: null,
            fillHeight: null,
            tag: null,
            format: null,
            percentPlayed: null,
            unplayedCount: null,
            blur: null,
            backgroundColor: null,
            foregroundLayer: null,
            imageIndex: 0);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
    }

    [Fact]
    public async Task SetItemImage_ItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns((BaseItem)null!);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.SetItemImage(itemId, ImageType.Primary);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
    }

    [Fact]
    public async Task SetItemImage_InvalidContentType_ReturnsBadRequest()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        controller.Request.ContentType = "text/plain";

        var result = await controller.SetItemImage(itemId, ImageType.Primary);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetItemImageByIndex_ItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns((BaseItem)null!);

        var controller = new ImageController(
            Mock.Of<IUserManager>(),
            libraryManager.Object,
            Mock.Of<IProviderManager>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.SetItemImageByIndex(itemId, ImageType.Backdrop, 0);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
    }
}
