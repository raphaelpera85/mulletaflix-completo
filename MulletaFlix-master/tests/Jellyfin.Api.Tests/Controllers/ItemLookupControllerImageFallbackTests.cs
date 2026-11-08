using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public sealed class ItemLookupControllerImageFallbackTests
{
    [Fact]
    public async Task ApplySearchCriteria_FallbackImagePersistsSearchProviderName()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);
        libraryManager.Setup(manager => manager.UpdateItemAsync(item, null, It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.RefreshFullItem(item, It.IsAny<MetadataRefreshOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        providerManager.Setup(manager => manager.SaveImage(
                item,
                "fallback.jpg",
                ImageType.Primary,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, path, type, _, _) =>
                savedItem.SetImage(new ItemImageInfo { Path = path, Type = type }, 0))
            .Returns(Task.CompletedTask);

        var controller = new ItemLookupController(
            providerManager.Object,
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;
        try
        {
            var result = await controller.ApplySearchCriteria(
                itemId,
                new RemoteSearchResult
                {
                    ImageUrl = "fallback.jpg",
                    SearchProviderName = "OpenLibrary",
                    ProviderIds = new Dictionary<string, string>()
                });

            Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }

        Assert.Equal("OpenLibrary", item.GetImageInfo(ImageType.Primary, 0)!.ProviderName);
        libraryManager.Verify(
            manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplySearchCriteria_PropagatesRequestCancellationToRefresh()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.RefreshFullItem(item, It.IsAny<MetadataRefreshOptions>(), cancellation.Token))
            .Returns(Task.CompletedTask);

        var controller = new ItemLookupController(
            providerManager.Object,
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    RequestAborted = cancellation.Token,
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.ApplySearchCriteria(itemId, new RemoteSearchResult());

        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
        providerManager.Verify(
            manager => manager.RefreshFullItem(item, It.IsAny<MetadataRefreshOptions>(), cancellation.Token),
            Times.Once);
    }

    [Fact]
    public async Task GetMovieRemoteSearchResults_PropagatesRequestCancellationToProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.GetRemoteSearchResults<Movie, MovieInfo>(It.IsAny<RemoteSearchQuery<MovieInfo>>(), cancellation.Token))
            .ReturnsAsync(Array.Empty<RemoteSearchResult>());

        var controller = new ItemLookupController(
            providerManager.Object,
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    RequestAborted = cancellation.Token,
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetMovieRemoteSearchResults(new RemoteSearchQuery<MovieInfo>());

        Assert.NotNull(result.Result);
        providerManager.Verify(
            manager => manager.GetRemoteSearchResults<Movie, MovieInfo>(It.IsAny<RemoteSearchQuery<MovieInfo>>(), cancellation.Token),
            Times.Once);
    }

    [Fact]
    public async Task ApplySearchCriteria_NullSearchResult_ReturnsBadRequest()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);

        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.ApplySearchCriteria(itemId, null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ApplySearchCriteria_NullProviderIds_SetsEmptyDictionary()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(manager => manager.RefreshFullItem(item, It.IsAny<MetadataRefreshOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new ItemLookupController(
            providerManager.Object,
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var searchResult = new RemoteSearchResult { ProviderIds = null! };
        var result = await controller.ApplySearchCriteria(itemId, searchResult);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);
        Assert.NotNull(item.ProviderIds);
    }

    [Fact]
    public async Task GetMovieRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetMovieRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void GetExternalIdInfos_WithEmptyItemId_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = controller.GetExternalIdInfos(Guid.Empty);
        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void GetExternalIdInfos_WhenItemNotFound_ReturnsNotFound()
    {
        var itemId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns((BaseItem?)null);

        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = controller.GetExternalIdInfos(itemId);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetExternalIdInfos_WithValidItem_ReturnsExternalIdInfos()
    {
        var itemId = Guid.NewGuid();
        var item = new Movie { Id = itemId };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(m => m.GetItemById<BaseItem>(itemId, It.IsAny<Guid>())).Returns(item);

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(m => m.GetExternalIdInfos(item)).Returns(Array.Empty<ExternalIdInfo>());

        var controller = new ItemLookupController(
            providerManager.Object,
            Mock.Of<IFileSystem>(),
            libraryManager.Object,
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = controller.GetExternalIdInfos(itemId);
        Assert.NotNull(result.Result);
        var okResult = Assert.IsType<MulletaFlix.Api.Results.OkResult<IEnumerable<ExternalIdInfo>>>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ApplySearchCriteria_WithEmptyItemId_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.ApplySearchCriteria(Guid.Empty, new RemoteSearchResult());
        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task GetTrailerRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetTrailerRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMusicVideoRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetMusicVideoRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSeriesRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetSeriesRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetBoxSetRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetBoxSetRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMusicArtistRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetMusicArtistRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMusicAlbumRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetMusicAlbumRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPersonRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetPersonRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetBookRemoteSearchResults_NullQuery_ReturnsBadRequest()
    {
        var controller = new ItemLookupController(
            Mock.Of<IProviderManager>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<ILogger<ItemLookupController>>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetBookRemoteSearchResults(null!);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }
}
