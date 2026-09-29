using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Results;
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
    public async Task GetDatabaseHealth_ReturnsNamedMariaDbCheckWithoutDetails()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var entry = new HealthReportEntry(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            "Banco de dados respondeu ao health check.",
            TimeSpan.Zero,
            null,
            new Dictionary<string, object>());
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["MulletaFlixDbContext"] = entry },
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            TimeSpan.Zero);
        var healthCheckService = new Mock<HealthCheckService>();
        healthCheckService
            .Setup(service => service.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.GetDatabaseHealth(healthCheckService.Object, CancellationToken.None);

        var response = Assert.IsType<NebulaDatabaseHealthDto>(Assert.IsType<OkResult<NebulaDatabaseHealthDto>>(result.Result).Value);
        Assert.True(response.Available);
        Assert.True(response.Healthy);
        Assert.Equal("Banco de dados respondeu ao health check.", response.Status);
    }

    [Fact]
    public async Task GetUploadQueueSummary_ReturnsLightweightSummaryFromManager()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new NebulaUploadQueueSummaryDto
        {
            IsAvailable = true,
            WindowStartUtc = DateTime.UnixEpoch.AddHours(-1),
            PendingCount = 17,
            RetryCount = 3,
            OldestPendingName = "Episode.mkv",
            OldestPendingAtUtc = DateTime.UnixEpoch,
            CompletedCountLastHour = 6,
            UploadedBytesLastHour = 1024,
            RecentFailureCount = 2,
            FailuresByStage = new List<NebulaUploadFailureStageCountDto>
            {
                new() { Stage = NebulaUploadFailureStages.TelegramTransfer, Count = 2 }
            }
        };
        manager.Setup(m => m.GetUploadQueueSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.GetUploadQueueSummary(CancellationToken.None);

        var response = Assert.IsType<NebulaUploadQueueSummaryDto>(Assert.IsType<OkResult<NebulaUploadQueueSummaryDto>>(result.Result).Value);
        Assert.Same(expected, response);
        Assert.Equal(6, response.CompletedCountLastHour);
        Assert.Equal(1024, response.UploadedBytesLastHour);
        Assert.Equal(2, response.RecentFailureCount);
        Assert.Equal(NebulaUploadFailureStages.TelegramTransfer, Assert.Single(response.FailuresByStage).Stage);
        manager.Verify(m => m.GetUploadQueueSummaryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTerminalFailedUploads_ReturnsManagerItems()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new[] { new NebulaFailedUploadDto { Id = "507f1f77bcf86cd799439011", Name = "Episode.mkv", SourceAvailable = true } };
        manager.Setup(m => m.GetTerminalFailedUploadsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected.ToList());
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.GetTerminalFailedUploads(CancellationToken.None);

        var response = Assert.IsType<NebulaFailedUploadDto[]>(Assert.IsType<OkResult<NebulaFailedUploadDto[]>>(result.Result).Value);
        Assert.Same(expected[0], Assert.Single(response));
        manager.Verify(m => m.GetTerminalFailedUploadsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryTerminalFailedUpload_ReturnsBadRequestWhenManagerRejectsSource()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        manager.Setup(m => m.RetryTerminalFailedUploadAsync("507f1f77bcf86cd799439011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaFailedUploadRetryResultDto { Message = "Arquivo de origem ausente." });
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.RetryTerminalFailedUpload("507f1f77bcf86cd799439011", CancellationToken.None);

        var response = Assert.IsType<NebulaFailedUploadRetryResultDto>(Assert.IsType<BadRequestObjectResult>(result.Result).Value);
        Assert.Equal("Arquivo de origem ausente.", response.Message);
    }

    [Fact]
    public async Task GetCancellableUploads_ReturnsManagerItems()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var expected = new List<NebulaCancellableUploadDto>
        {
            new() { Id = "507f1f77bcf86cd799439011", Name = "Episode.mkv", Status = "uploading", TotalBytes = 100, UploadedBytes = 50 }
        };
        manager.Setup(m => m.GetCancellableUploadsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.GetCancellableUploads(CancellationToken.None);

        var response = Assert.IsType<NebulaCancellableUploadDto[]>(Assert.IsType<OkResult<NebulaCancellableUploadDto[]>>(result.Result).Value);
        Assert.Same(expected[0], Assert.Single(response));
    }

    [Fact]
    public async Task CancelUpload_PersistsCallingUserIdThroughManager()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        manager.Setup(m => m.CancelUploadAsync("507f1f77bcf86cd799439011", userId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaUploadCancellationResultDto { Success = true, CancellationPending = true });
        var controller = new NebulaFtpController(manager.Object, configuration.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(InternalClaimTypes.UserId, userId.ToString("D"))], "test"))
                }
            }
        };

        var result = await controller.CancelUpload("507f1f77bcf86cd799439011", CancellationToken.None);

        var response = Assert.IsType<NebulaUploadCancellationResultDto>(Assert.IsType<OkResult<NebulaUploadCancellationResultDto>>(result.Result).Value);
        Assert.True(response.CancellationPending);
        manager.VerifyAll();
    }

    [Fact]
    public async Task CancelUpload_ReturnsBadRequestWhenUploadCannotBeCancelled()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        manager.Setup(m => m.CancelUploadAsync("bad-id", userId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaUploadCancellationResultDto { Message = "Identificador inválido." });
        var controller = new NebulaFtpController(manager.Object, configuration.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(InternalClaimTypes.UserId, userId.ToString("D"))], "test"))
                }
            }
        };

        var result = await controller.CancelUpload("bad-id", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
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
    public void GetConfig_RedactsAllSecretFieldsFromResponse()
    {
        // This is the read path an admin's browser actually calls to populate the Nebula settings
        // form; it must never echo back a secret the server already knows, even though the write
        // path (UpdateConfig/PreserveExistingSecretValues) is what re-applies them on save.
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.Setup(m => m.GetConfiguration("nebulaftp"))
            .Returns(new NebulaFtpConfiguration
            {
                Password = "ftp-password",
                HttpStreamToken = "stream-token",
                MongoDbConnectionString = "mongodb://user:pass@host/db",
                ApiHash = "12345678901234567890123456789012",
                SupabaseKey = "supabase-service-role-key",
                BotTokens = "111:AAA,222:BBB",
                // Non-secret fields should still round-trip unchanged.
                ServerHost = "nebula.example.com",
                ServerPort = 2121,
                Username = "nebula-ftp-user"
            });
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = Assert.IsType<ActionResult<NebulaFtpConfiguration>>(controller.GetConfig());
        var config = Assert.IsType<NebulaFtpConfiguration>(Assert.IsType<OkResult<NebulaFtpConfiguration>>(result.Result).Value);

        Assert.Equal(string.Empty, config.Password);
        Assert.Equal(string.Empty, config.HttpStreamToken);
        Assert.Equal(string.Empty, config.MongoDbConnectionString);
        Assert.Equal(string.Empty, config.ApiHash);
        Assert.Equal(string.Empty, config.SupabaseKey);
        Assert.Equal(string.Empty, config.BotTokens);

        Assert.Equal("nebula.example.com", config.ServerHost);
        Assert.Equal(2121, config.ServerPort);
        Assert.Equal("nebula-ftp-user", config.Username);
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
                HttpStreamToken = "existing-stream-token",
                PlaybackCachePath = @"E:\NebulaCache",
                PlaybackCacheMaxSizeGb = 120,
                PlaybackCacheMinimumFreeSpaceGb = 8
            });
        NebulaFtpConfiguration? saved = null;
        configuration.Setup(m => m.SaveConfiguration("nebulaftp", It.IsAny<object>()))
            .Callback<string, object>((_, value) => saved = Assert.IsType<NebulaFtpConfiguration>(value));
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.UpdateConfig(new NebulaFtpConfiguration
        {
            ServerPort = 2121,
            HttpStreamPort = 2123,
            SupabaseUrl = "https://example.supabase.co"
        });

        Assert.IsType<NoContentResult>(result);
        configuration.Verify(m => m.SaveConfiguration("nebulaftp", It.IsAny<object>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal(@"E:\NebulaCache", saved.PlaybackCachePath);
        Assert.Equal(120, saved.PlaybackCacheMaxSizeGb);
        Assert.Equal(8, saved.PlaybackCacheMinimumFreeSpaceGb);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(50, -1)]
    [InlineData(4097, 0)]
    [InlineData(50, 1025)]
    public void UpdateConfig_RejectsInvalidPlaybackCacheLimits(int maxSizeGb, int minimumFreeSpaceGb)
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.UpdateConfig(new NebulaFtpConfiguration
        {
            PlaybackCacheMaxSizeGb = maxSizeGb,
            PlaybackCacheMinimumFreeSpaceGb = minimumFreeSpaceGb
        });

        Assert.IsType<BadRequestObjectResult>(result);
        configuration.Verify(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
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
            TelegramFetchCount = 12,
            TelegramFetchFailures = 2,
            TelegramFetchCancellations = 3,
            AverageTelegramFetchLatencyMs = 125.5,
            CacheCleanupRuns = 8,
            CacheCleanupFailures = 1,
            CacheCleanupSkipped = 2,
            LastCacheCleanupDurationMs = 44.2,
            LastCacheCleanupUtc = new DateTime(2026, 9, 28, 12, 30, 0, DateTimeKind.Utc),
            FreeSpaceGb = 100.5,
            TotalSpaceGb = 500.0
        };
        manager.Setup(m => m.GetPlaybackCacheStatus()).Returns(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = controller.GetPlaybackCacheStatus();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        var json = JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web);
        Assert.Contains("\"telegramFetchCancellations\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"cacheCleanupFailures\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"cacheCleanupSkipped\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"lastCacheCleanupDurationMs\":44.2", json, StringComparison.Ordinal);
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
        configuration.Setup(m => m.GetConfiguration("nebulaftp")).Returns(new NebulaFtpConfiguration());
        var expected = new NebulaPlaybackCacheStatusDto
        {
            ConfiguredPath = @"E:\new-cache",
            EffectivePath = @"E:\new-cache\nebula-playback"
        };
        manager.Setup(m => m.UpdatePlaybackCachePathAsync(@"E:\new-cache", 120, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.GetPlaybackCacheStatus()).Returns(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { CachePath = @"E:\new-cache", MaxCacheSizeGb = 120, MinimumFreeSpaceGb = 8 },
            CancellationToken.None);

        var ok = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task UpdatePlaybackCachePath_WhenPathIsOmitted_PreservesConfiguredPath()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(m => m.GetConfiguration("nebulaftp"))
            .Returns(new NebulaFtpConfiguration { PlaybackCachePath = @"E:\existing-cache" });
        manager.Setup(m => m.UpdatePlaybackCachePathAsync(@"E:\existing-cache", 90, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var expected = new NebulaPlaybackCacheStatusDto();
        manager.Setup(m => m.GetPlaybackCacheStatus()).Returns(expected);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { MaxCacheSizeGb = 90, MinimumFreeSpaceGb = 4 },
            CancellationToken.None);

        var ok = Assert.IsType<OkResult<NebulaPlaybackCacheStatusDto>>(result.Result);
        Assert.Same(expected, ok.Value);
        manager.Verify(m => m.UpdatePlaybackCachePathAsync(@"E:\existing-cache", 90, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePlaybackCachePath_RejectsInvalidLimits()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { CachePath = @"E:\cache", MaxCacheSizeGb = 0, MinimumFreeSpaceGb = -1 },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdatePlaybackCachePath_WhenFailed_ReturnsBadRequest()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(m => m.GetConfiguration("nebulaftp")).Returns(new NebulaFtpConfiguration());
        manager.Setup(m => m.UpdatePlaybackCachePathAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = new NebulaFtpController(manager.Object, configuration.Object);

        var result = await controller.UpdatePlaybackCachePath(
            new NebulaUpdatePlaybackCachePathRequest { CachePath = @"Z:\invalid" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
