using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class NebulaFtpControllerTests
{
    [Fact]
    public async Task GetHealth_ReturnsComponentHealthWithoutTransformingIt()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new NebulaComponentHealthDto
        {
            MongoConfigured = true,
            MongoConnected = true,
            TelegramReady = true,
            TelegramAvailableBots = 2,
            FtpListenerRunning = true,
            HttpListenerRunning = true
        };
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.GetHealth(CancellationToken.None);

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GenerateStrm_ForwardsValidIdempotencyKey()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        manager.Setup(m => m.GenerateStrmAsync("request-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = new NebulaFtpController(manager.Object, configuration.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Request.Headers["X-Idempotency-Key"] = "request-123";

        await controller.GenerateStrm(CancellationToken.None);

        manager.Verify(m => m.GenerateStrmAsync("request-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void UpdateConfig_RejectsInvalidPortWithoutPersisting()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.UpdateConfig(new NebulaFtpConfiguration { ServerPort = 0 });

        Assert.IsType<BadRequestObjectResult>(result);
        configuration.Verify(
            m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public void UpdateConfig_AcceptsValidConfigAndReturnsNoContent()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.Setup(m => m.GetConfiguration("nebulaftp"))
            .Returns(new NebulaFtpConfiguration
            {
                ApiHash = "existing-api-hash",
                HttpStreamToken = "existing-stream-token"
            });
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.UpdateConfig(new NebulaFtpConfiguration
        {
            ServerPort = 2121,
            HttpStreamPort = 2123,
            SupabaseUrl = "https://example.supabase.co"
        });

        Assert.IsType<NoContentResult>(result);
        configuration.Verify(m => m.SaveConfiguration("nebulaftp", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void RotateSecrets_UpdatesOnlyProvidedSecrets()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>();
        var existing = new NebulaFtpConfiguration
        {
            Password = "old-password",
            HttpStreamToken = "old-http-token",
            SupabaseKey = "old-supabase-key",
            ApiHash = "12345678901234567890123456789012"
        };
        configuration.Setup(m => m.GetConfiguration("nebulaftp")).Returns(existing);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.RotateSecrets(new NebulaCredentialRotationRequest { HttpStreamToken = "new-http-token" });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("old-password", existing.Password);
        Assert.Equal("new-http-token", existing.HttpStreamToken);
        Assert.Equal("old-supabase-key", existing.SupabaseKey);
        configuration.Verify(m => m.SaveConfiguration("nebulaftp", existing), Times.Once);
    }

    [Fact]
    public void RotateSecrets_RejectsEmptyRequestWithoutPersisting()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.RotateSecrets(new NebulaCredentialRotationRequest());

        Assert.IsType<BadRequestObjectResult>(result);
        configuration.Verify(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void GetPlaybackCacheStatus_ReturnsStatusFromManager()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new NebulaPlaybackCacheStatusDto
        {
            ConfiguredPath = @"D:\cache",
            EffectivePath = @"D:\cache\nebula-playback",
            TotalSizeBytes = 1048576,
            FormattedSize = "1.0 MB",
            CachedFilesCount = 4,
            ActiveLeasesCount = 1,
            FreeSpaceGb = 100.5,
            TotalSpaceGb = 500.0
        };
        manager.Setup(m => m.GetPlaybackCacheStatus()).Returns(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.GetPlaybackCacheStatus();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task ClearPlaybackCache_CallsManagerAndReturnsOk()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        manager.Setup(m => m.ClearPlaybackCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.ClearPlaybackCache(CancellationToken.None);

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Equal(true, ok.Value);
        manager.Verify(m => m.ClearPlaybackCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePlaybackCachePath_WhenSuccessful_ReturnsUpdatedStatus()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new NebulaPlaybackCacheStatusDto
        {
            ConfiguredPath = @"E:\new-cache",
            EffectivePath = @"E:\new-cache\nebula-playback"
        };
        manager.Setup(m => m.UpdatePlaybackCachePathAsync(@"E:\new-cache", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.GetPlaybackCacheStatus()).Returns(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { CachePath = @"E:\new-cache" },
            CancellationToken.None);

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task UpdatePlaybackCachePath_WhenFailed_ReturnsBadRequest()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        manager.Setup(m => m.UpdatePlaybackCachePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { CachePath = @"Z:\invalid" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
