using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class CollectionControllerTests
{
    private readonly Mock<ICollectionManager> _collectionManager = new(MockBehavior.Strict);
    private readonly Mock<IDtoService> _dtoService = new(MockBehavior.Strict);

    [Fact]
    public async Task AddToCollection_EmptyCollectionId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddToCollection(Guid.Empty, [Guid.NewGuid()]);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Collection id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task AddToCollection_NullIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddToCollection(Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task AddToCollection_EmptyIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddToCollection(Guid.NewGuid(), Array.Empty<Guid>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveFromCollection_EmptyCollectionId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveFromCollection(Guid.Empty, [Guid.NewGuid()]);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Collection id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveFromCollection_NullIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveFromCollection(Guid.NewGuid(), null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task RemoveFromCollection_EmptyIds_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveFromCollection(Guid.NewGuid(), Array.Empty<Guid>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item ids must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task CreateCollection_EmptyParentId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.CreateCollection("Test", ["id1"], Guid.Empty, false);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Parent id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task AddToCollection_ValidInputs_CallsManagerAndReturnsNoContent()
    {
        var collectionId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid() };
        _collectionManager.Setup(m => m.AddToCollectionAsync(collectionId, ids)).Returns(Task.CompletedTask);
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.AddToCollection(collectionId, ids);

        Assert.IsType<NoContentResult>(result);
        _collectionManager.Verify(m => m.AddToCollectionAsync(collectionId, ids), Times.Once);
    }

    [Fact]
    public async Task RemoveFromCollection_ValidInputs_CallsManagerAndReturnsNoContent()
    {
        var collectionId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid() };
        _collectionManager.Setup(m => m.RemoveFromCollectionAsync(collectionId, ids)).Returns(Task.CompletedTask);
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.RemoveFromCollection(collectionId, ids);

        Assert.IsType<NoContentResult>(result);
        _collectionManager.Verify(m => m.RemoveFromCollectionAsync(collectionId, ids), Times.Once);
    }

    private CollectionController CreateController(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, userId.ToString("D"))],
            "test");

        return new CollectionController(_collectionManager.Object, _dtoService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
