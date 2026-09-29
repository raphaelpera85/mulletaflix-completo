using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Nebula;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Results;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class ServerHealthControllerTests
{
    private static ServerHealthController CreateController(
        Mock<IBackupService> backupService,
        Mock<INebulaFtpManager> nebulaFtpManager,
        Mock<ISystemManager> systemManager)
    {
        var applicationHost = new Mock<IServerApplicationHost>();
        applicationHost.SetupGet(h => h.FriendlyName).Returns("test-server");
        applicationHost.SetupGet(h => h.ApplicationVersionString).Returns("1.0.0");
        applicationHost.SetupGet(h => h.HasPendingRestart).Returns(false);

        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(l => l.ApplicationStopping).Returns(CancellationToken.None);

        var taskManager = new Mock<ITaskManager>();
        taskManager.SetupGet(t => t.ScheduledTasks).Returns(Array.Empty<IScheduledTaskWorker>());

        var pluginManager = new Mock<IPluginManager>();
        pluginManager.SetupGet(p => p.Plugins).Returns(Array.Empty<LocalPlugin>());

        return new ServerHealthController(
            applicationHost.Object,
            Mock.Of<IServerApplicationPaths>(),
            Mock.Of<IServerConfigurationManager>(),
            applicationLifetime.Object,
            taskManager.Object,
            Mock.Of<ILibraryManager>(),
            pluginManager.Object,
            backupService.Object,
            systemManager.Object,
            nebulaFtpManager.Object);
    }

    private static SystemStorageInfo HealthyStorageInfo()
    {
        FolderStorageInfo Folder(string path) => new()
        {
            Path = path,
            ResolvedPath = path,
            FreeSpace = 900,
            UsedSpace = 100
        };

        return new SystemStorageInfo
        {
            ProgramDataFolder = Folder("program-data"),
            WebFolder = Folder("web"),
            ImageCacheFolder = Folder("image-cache"),
            CacheFolder = Folder("cache"),
            LogFolder = Folder("log"),
            InternalMetadataFolder = Folder("internal-metadata"),
            TranscodingTempFolder = Folder("transcode"),
            Libraries = Array.Empty<LibraryStorageInfo>()
        };
    }

    [Fact]
    public async System.Threading.Tasks.Task GetOperationalAlerts_WithHealthySignals_ReturnsNoAlerts()
    {
        var backupService = new Mock<IBackupService>();
        backupService.Setup(s => s.EnumerateBackups()).ReturnsAsync(
            new[]
            {
                new BackupManifestDto
                {
                    ServerVersion = new Version(1, 0),
                    BackupEngineVersion = new Version(0, 2, 0),
                    DateCreated = DateTimeOffset.UtcNow.AddHours(-1),
                    Path = "backup.zip",
                    Options = new BackupOptionsDto { Database = true }
                }
            });

        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        nebulaFtpManager.Setup(m => m.GetUploadQueueSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaUploadQueueSummaryDto { IsAvailable = false });
        nebulaFtpManager.Setup(m => m.GetSupabaseStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaSupabaseStatusDto { IsConfigured = false, LastRestoreFailed = false });

        var systemManager = new Mock<ISystemManager>();
        systemManager.Setup(s => s.GetSystemStorageInfo()).Returns(HealthyStorageInfo());

        var controller = CreateController(backupService, nebulaFtpManager, systemManager);

        var result = await controller.GetOperationalAlerts(CancellationToken.None);

        var ok = Assert.IsType<OkResult<IReadOnlyList<OperationalAlertDto>>>(result.Result);
        var alerts = Assert.IsAssignableFrom<IReadOnlyList<OperationalAlertDto>>(ok.Value);
        Assert.Empty(alerts);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetOperationalAlerts_WithFailedRestoreAndNoBackups_ReturnsCriticalAlerts()
    {
        var backupService = new Mock<IBackupService>();
        backupService.Setup(s => s.EnumerateBackups()).ReturnsAsync(Array.Empty<BackupManifestDto>());

        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        nebulaFtpManager.Setup(m => m.GetUploadQueueSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaUploadQueueSummaryDto { IsAvailable = false });
        nebulaFtpManager.Setup(m => m.GetSupabaseStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaSupabaseStatusDto
            {
                IsConfigured = true,
                LastRestoreFailed = true,
                LastRestoreStatus = "Falha: HTTP 503"
            });

        var systemManager = new Mock<ISystemManager>();
        systemManager.Setup(s => s.GetSystemStorageInfo()).Returns(HealthyStorageInfo());

        var controller = CreateController(backupService, nebulaFtpManager, systemManager);

        var result = await controller.GetOperationalAlerts(CancellationToken.None);

        var ok = Assert.IsType<OkResult<IReadOnlyList<OperationalAlertDto>>>(result.Result);
        var alerts = Assert.IsAssignableFrom<IReadOnlyList<OperationalAlertDto>>(ok.Value);

        Assert.Contains(alerts, a => a.Kind == OperationalAlertKind.BackupOverdue && a.Severity == OperationalAlertSeverity.Critical);
        Assert.Contains(alerts, a => a.Kind == OperationalAlertKind.RestoreFailed && a.Severity == OperationalAlertSeverity.Critical);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetOperationalAlerts_WhenNebulaThrows_StillReturnsBackupAndDiskAlerts()
    {
        var backupService = new Mock<IBackupService>();
        backupService.Setup(s => s.EnumerateBackups()).ReturnsAsync(Array.Empty<BackupManifestDto>());

        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        nebulaFtpManager.Setup(m => m.GetUploadQueueSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Nebula desabilitado"));
        nebulaFtpManager.Setup(m => m.GetSupabaseStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Nebula desabilitado"));

        var systemManager = new Mock<ISystemManager>();
        systemManager.Setup(s => s.GetSystemStorageInfo()).Returns(HealthyStorageInfo());

        var controller = CreateController(backupService, nebulaFtpManager, systemManager);

        var result = await controller.GetOperationalAlerts(CancellationToken.None);

        var ok = Assert.IsType<OkResult<IReadOnlyList<OperationalAlertDto>>>(result.Result);
        var alerts = Assert.IsAssignableFrom<IReadOnlyList<OperationalAlertDto>>(ok.Value);

        Assert.Contains(alerts, a => a.Kind == OperationalAlertKind.BackupOverdue);
        Assert.DoesNotContain(alerts, a => a.Kind == OperationalAlertKind.RestoreFailed);
    }
}
