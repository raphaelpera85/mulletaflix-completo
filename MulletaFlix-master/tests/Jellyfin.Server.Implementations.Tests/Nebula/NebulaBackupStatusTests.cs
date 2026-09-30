using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaBackupStatusTests
{
    [Fact]
    public void MongoBackupFailure_DoesNotExposePreviousCountsAsTheFailedAttempt()
    {
        var lastSuccess = DateTime.UtcNow.AddHours(-2);
        var config = new NebulaFtpConfiguration { SupabaseLastBackupTime = lastSuccess };
        NebulaFtpManager.ApplyMongoBackupResult(config, new NebulaSupabaseBackupResultDto
        {
            Success = true,
            FilesBackedUp = 42,
            UsersBackedUp = 7
        });
        var failed = new NebulaSupabaseBackupResultDto { Success = false, Timestamp = DateTime.UtcNow };

        NebulaFtpManager.ApplyMongoBackupResult(config, failed);

        Assert.True(config.SupabaseLastBackupFailed);
        Assert.Equal(failed.Timestamp, config.SupabaseLastBackupAttemptTime);
        Assert.Null(config.SupabaseLastBackupProcessedFilesCount);
        Assert.Null(config.SupabaseLastBackupProcessedUsersCount);
        Assert.Equal(lastSuccess, config.SupabaseLastBackupTime);
    }

    [Fact]
    public async Task Status_PreservesUnknownLegacyOutcomesAndZeroProcessedCount()
    {
        var config = new NebulaFtpConfiguration { SupabaseKey = string.Empty };
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(manager => manager.GetConfiguration("nebulaftp")).Returns(config);
        await using var manager = new NebulaFtpManager(configManager.Object, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

        var legacy = await manager.GetSupabaseStatusAsync();
        Assert.Null(legacy.LastBackupFailed);
        Assert.Null(legacy.LastUsersBackupFailed);
        Assert.Null(legacy.LastBackupProcessedFilesCount);

        NebulaFtpManager.ApplyMongoBackupResult(config, new NebulaSupabaseBackupResultDto { Success = true });
        var current = await manager.GetSupabaseStatusAsync();
        Assert.False(current.LastBackupFailed);
        Assert.Equal(0, current.LastBackupProcessedFilesCount);
        Assert.Equal(0, current.LastBackupProcessedUsersCount);
    }

    [Fact]
    public async Task UsersBackupWithUnavailableService_RecordsFailureInsteadOfKeepingPreviousSuccess()
    {
        var config = new NebulaFtpConfiguration { SupabaseLastUsersBackupFailed = false, SupabaseLastUsersBackupCount = 5 };
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(manager => manager.GetConfiguration("nebulaftp")).Returns(config);
        var original = config;
        configManager.Setup(manager => manager.UpdateConfiguration("nebulaftp", It.IsAny<Func<object, object>>()))
            .Returns<string, Func<object, object>>((key, update) =>
            {
                config = (NebulaFtpConfiguration)update(config);
                configManager.Object.SaveConfiguration(key, config);
                return config;
            });
        await using var manager = new NebulaFtpManager(configManager.Object, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

        var result = await manager.BackupUsersToSupabaseAsync();

        Assert.False(result.Success);
        Assert.True(config.SupabaseLastUsersBackupFailed);
        Assert.NotNull(config.SupabaseLastUsersBackupTime);
        Assert.False(original.SupabaseLastUsersBackupFailed);
        configManager.Verify(manager => manager.SaveConfiguration("nebulaftp", config), Times.Once);
    }
}
