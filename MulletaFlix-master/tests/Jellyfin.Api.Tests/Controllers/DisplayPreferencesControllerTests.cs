using System;
using System.Collections.Generic;
using System.Security.Claims;
using MediaBrowser.Controller;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class DisplayPreferencesControllerTests
{
    private readonly Mock<IDisplayPreferencesManager> _manager = new(MockBehavior.Strict);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDisplayPreferences_NullOrWhitespaceDisplayPreferencesId_ReturnsBadRequest(string? id)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetDisplayPreferences(id!, null, "emby-web");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Display preferences id is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDisplayPreferences_NullOrWhitespaceClient_ReturnsBadRequest(string? client)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.GetDisplayPreferences("home", null, client!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Client is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayPreferences_NullOrWhitespaceDisplayPreferencesId_ReturnsBadRequest(string? id)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.UpdateDisplayPreferences(id!, null, "emby-web", new DisplayPreferencesDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Display preferences id is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayPreferences_NullOrWhitespaceClient_ReturnsBadRequest(string? client)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.UpdateDisplayPreferences("home", null, client!, new DisplayPreferencesDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Client is required.", badRequest.Value);
    }

    [Fact]
    public void UpdateDisplayPreferences_NullPayload_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.UpdateDisplayPreferences("home", null, "emby-web", null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Display preferences are required.", badRequest.Value);
    }

    [Fact]
    public void UpdateDisplayPreferences_MalformedSkipLengths_UsesDefaultGracefullyWithoutCrash()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var client = "emby-web";
        var entity = new DisplayPreferences(userId, itemId, client);
        var itemEntity = new ItemDisplayPreferences(userId, itemId, client);

        _manager.Setup(m => m.GetDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(entity);
        _manager.Setup(m => m.GetItemDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(itemEntity);
        _manager.Setup(m => m.SetCustomItemDisplayPreferences(userId, It.IsAny<Guid>(), client, It.IsAny<Dictionary<string, string?>>()));
        _manager.Setup(m => m.UpdateItemDisplayPreferences(itemEntity));
        _manager.Setup(m => m.UpdateDisplayPreferences(entity));

        var controller = CreateController(userId);
        var dto = new DisplayPreferencesDto
        {
            CustomPrefs =
            {
                ["skipBackLength"] = "not-a-number",
                ["skipForwardLength"] = "invalid-int"
            }
        };

        var result = controller.UpdateDisplayPreferences(itemId.ToString("D"), null, client, dto);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(15000, entity.SkipBackwardLength);
        Assert.Equal(15000, entity.SkipForwardLength);
    }

    [Fact]
    public void UpdateDisplayPreferences_MalformedChromecastVersion_UsesDefaultGracefullyWithoutCrash()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var client = "emby-web";
        var entity = new DisplayPreferences(userId, itemId, client);
        var itemEntity = new ItemDisplayPreferences(userId, itemId, client);

        _manager.Setup(m => m.GetDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(entity);
        _manager.Setup(m => m.GetItemDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(itemEntity);
        _manager.Setup(m => m.SetCustomItemDisplayPreferences(userId, It.IsAny<Guid>(), client, It.IsAny<Dictionary<string, string?>>()));
        _manager.Setup(m => m.UpdateItemDisplayPreferences(itemEntity));
        _manager.Setup(m => m.UpdateDisplayPreferences(entity));

        var controller = CreateController(userId);
        var dto = new DisplayPreferencesDto
        {
            CustomPrefs =
            {
                ["chromecastVersion"] = "totally_invalid_version"
            }
        };

        var result = controller.UpdateDisplayPreferences(itemId.ToString("D"), null, client, dto);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(ChromecastVersion.Stable, entity.ChromecastVersion);
    }

    [Fact]
    public void UpdateDisplayPreferences_MalformedEnableNextVideoInfoOverlay_UsesDefaultGracefullyWithoutCrash()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var client = "emby-web";
        var entity = new DisplayPreferences(userId, itemId, client);
        var itemEntity = new ItemDisplayPreferences(userId, itemId, client);

        _manager.Setup(m => m.GetDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(entity);
        _manager.Setup(m => m.GetItemDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(itemEntity);
        _manager.Setup(m => m.SetCustomItemDisplayPreferences(userId, It.IsAny<Guid>(), client, It.IsAny<Dictionary<string, string?>>()));
        _manager.Setup(m => m.UpdateItemDisplayPreferences(itemEntity));
        _manager.Setup(m => m.UpdateDisplayPreferences(entity));

        var controller = CreateController(userId);
        var dto = new DisplayPreferencesDto
        {
            CustomPrefs =
            {
                ["enableNextVideoInfoOverlay"] = "not-a-bool"
            }
        };

        var result = controller.UpdateDisplayPreferences(itemId.ToString("D"), null, client, dto);

        Assert.IsType<NoContentResult>(result);
        Assert.True(entity.EnableNextVideoInfoOverlay);
    }

    [Fact]
    public void UpdateDisplayPreferences_MalformedHomeSectionKey_IgnoresGracefullyWithoutCrash()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var client = "emby-web";
        var entity = new DisplayPreferences(userId, itemId, client);
        var itemEntity = new ItemDisplayPreferences(userId, itemId, client);

        _manager.Setup(m => m.GetDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(entity);
        _manager.Setup(m => m.GetItemDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(itemEntity);
        _manager.Setup(m => m.SetCustomItemDisplayPreferences(userId, It.IsAny<Guid>(), client, It.IsAny<Dictionary<string, string?>>()));
        _manager.Setup(m => m.UpdateItemDisplayPreferences(itemEntity));
        _manager.Setup(m => m.UpdateDisplayPreferences(entity));

        var controller = CreateController(userId);
        var dto = new DisplayPreferencesDto
        {
            CustomPrefs =
            {
                ["homesection_not_a_number"] = "Resume"
            }
        };

        var result = controller.UpdateDisplayPreferences(itemId.ToString("D"), null, client, dto);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void UpdateDisplayPreferences_NullCustomPrefsDictionary_DoesNotThrowNullReferenceException()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var client = "emby-web";
        var entity = new DisplayPreferences(userId, itemId, client);
        var itemEntity = new ItemDisplayPreferences(userId, itemId, client);

        _manager.Setup(m => m.GetDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(entity);
        _manager.Setup(m => m.GetItemDisplayPreferences(userId, It.IsAny<Guid>(), client)).Returns(itemEntity);
        _manager.Setup(m => m.SetCustomItemDisplayPreferences(userId, It.IsAny<Guid>(), client, It.IsAny<Dictionary<string, string?>>()));
        _manager.Setup(m => m.UpdateItemDisplayPreferences(itemEntity));
        _manager.Setup(m => m.UpdateDisplayPreferences(entity));

        var controller = CreateController(userId);
        var dto = new DisplayPreferencesDto
        {
            CustomPrefs = null!
        };

        var result = controller.UpdateDisplayPreferences(itemId.ToString("D"), null, client, dto);

        Assert.IsType<NoContentResult>(result);
    }

    private DisplayPreferencesController CreateController(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(InternalClaimTypes.UserId, userId.ToString("D"))],
            "test");

        return new DisplayPreferencesController(_manager.Object, NullLogger<DisplayPreferencesController>.Instance)
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
