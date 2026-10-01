using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
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
        Mock<ISystemManager> systemManager,
        Mock<IMediaEncoder>? mediaEncoder = null,
        Mock<IServerConfigurationManager>? configurationManager = null,
        Mock<ITranscodeManager>? transcodeManager = null)
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

        var configManager = configurationManager ?? new Mock<IServerConfigurationManager>();
        if (configurationManager == null)
        {
            configManager.Setup(c => c.GetConfiguration("encoding")).Returns(new EncodingOptions());
            configManager.SetupGet(c => c.CommonConfiguration).Returns(new BaseApplicationConfiguration());
        }

        return new ServerHealthController(
            applicationHost.Object,
            Mock.Of<IServerApplicationPaths>(),
            configManager.Object,
            applicationLifetime.Object,
            taskManager.Object,
            Mock.Of<ILibraryManager>(),
            pluginManager.Object,
            backupService.Object,
            systemManager.Object,
            nebulaFtpManager.Object,
            mediaEncoder?.Object,
            transcodeManager?.Object);
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

    [Fact]
    public async System.Threading.Tasks.Task GetEncodingHealth_WhenMediaEncoderConfigured_ReturnsValidDiagnostics()
    {
        var backupService = new Mock<IBackupService>();
        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        var systemManager = new Mock<ISystemManager>();

        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.SetupGet(e => e.EncoderPath).Returns("fake-ffmpeg");
        mediaEncoder.SetupGet(e => e.ProbePath).Returns("fake-ffprobe");
        mediaEncoder.SetupGet(e => e.EncoderVersion).Returns(new Version(7, 0, 1));
        mediaEncoder.Setup(e => e.SupportsHwaccel("d3d11va")).Returns(true);
        mediaEncoder.Setup(e => e.SupportsHwaccel("qsv")).Returns(true);
        mediaEncoder.Setup(e => e.SupportsEncoder("libx264")).Returns(true);
        mediaEncoder.Setup(e => e.SupportsEncoder("h264_qsv")).Returns(true);
        mediaEncoder.Setup(e => e.SupportsDecoder("h264")).Returns(true);
        mediaEncoder.Setup(e => e.SupportsFilter("scale")).Returns(true);

        var transcodeManager = new Mock<ITranscodeManager>();
        transcodeManager.SetupGet(t => t.ActiveTranscodingJobsCount).Returns(2);

        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding"))
            .Returns(new EncodingOptions { MaxConcurrentTranscodingJobs = 4 });

        var controller = CreateController(backupService, nebulaFtpManager, systemManager, mediaEncoder, configManager, transcodeManager);

        var result = await controller.GetEncodingHealth(CancellationToken.None, test: false);

        var ok = Assert.IsType<OkResult<EncodingHealthDto>>(result.Result);
        var dto = Assert.IsType<EncodingHealthDto>(ok.Value);
        Assert.NotNull(dto);
        Assert.Equal("fake-ffmpeg", dto.EncoderPath);
        Assert.Equal("fake-ffprobe", dto.ProbePath);
        Assert.Equal("7.0.1", dto.Version);
        Assert.Equal(2, dto.ActiveTranscodingJobsCount);
        Assert.Equal(4, dto.MaxConcurrentTranscodingJobs);
        Assert.Contains("d3d11va", dto.SupportedHwAccelerations);
        Assert.Contains("qsv", dto.SupportedHwAccelerations);
        Assert.Contains("libx264", dto.SupportedEncoders);
        Assert.Contains("h264_qsv", dto.SupportedEncoders);
        Assert.Contains("h264", dto.SupportedDecoders);
        Assert.Contains("scale", dto.SupportedFilters);
        Assert.NotNull(dto.HostCapabilities);
        Assert.NotEmpty(dto.HostCapabilities.OperatingSystem);
        Assert.NotEmpty(dto.HostCapabilities.Architecture);
        Assert.True(dto.HostCapabilities.SupportsQuickSync);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetEncodingHealth_WhenMediaEncoderMissing_ReturnsCriticalStatus()
    {
        var backupService = new Mock<IBackupService>();
        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        var systemManager = new Mock<ISystemManager>();

        var controller = CreateController(backupService, nebulaFtpManager, systemManager, mediaEncoder: null);

        var result = await controller.GetEncodingHealth(CancellationToken.None, test: false);

        var ok = Assert.IsType<OkResult<EncodingHealthDto>>(result.Result);
        var dto = Assert.IsType<EncodingHealthDto>(ok.Value);
        Assert.NotNull(dto);
        Assert.False(dto.IsAvailable);
        Assert.False(dto.CanExecute);
        Assert.Equal(HealthStatus.Critical, dto.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetEncodingHealth_WhenConfiguredHwNotSupported_ReturnsWarningStatus()
    {
        var backupService = new Mock<IBackupService>();
        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        var systemManager = new Mock<ISystemManager>();

        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.SetupGet(e => e.EncoderPath).Returns("fake-ffmpeg");
        mediaEncoder.SetupGet(e => e.EncoderVersion).Returns(new Version(7, 0, 1));
        // Supports only software, nvenc is false
        mediaEncoder.Setup(e => e.SupportsHwaccel("nvenc")).Returns(false);

        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding"))
            .Returns(new EncodingOptions { HardwareAccelerationType = HardwareAccelerationType.nvenc });

        var controller = CreateController(backupService, nebulaFtpManager, systemManager, mediaEncoder, configManager);

        var result = await controller.GetEncodingHealth(CancellationToken.None, test: false);

        var ok = Assert.IsType<OkResult<EncodingHealthDto>>(result.Result);
        var dto = Assert.IsType<EncodingHealthDto>(ok.Value);
        Assert.NotNull(dto);
        Assert.Equal("nvenc", dto.ConfiguredHwAcceleration);
        Assert.Equal(HealthStatus.Warning, dto.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetHealthSummary_IncludesEncodingHealth()
    {
        var backupService = new Mock<IBackupService>();
        backupService.Setup(s => s.EnumerateBackups()).ReturnsAsync(Array.Empty<BackupManifestDto>());

        var nebulaFtpManager = new Mock<INebulaFtpManager>();
        var systemManager = new Mock<ISystemManager>();
        systemManager.Setup(s => s.GetSystemStorageInfo()).Returns(HealthyStorageInfo());

        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.SetupGet(e => e.EncoderPath).Returns("fake-ffmpeg");
        mediaEncoder.SetupGet(e => e.EncoderVersion).Returns(new Version(7, 0));

        var controller = CreateController(backupService, nebulaFtpManager, systemManager, mediaEncoder);

        var result = await controller.GetHealthSummary();

        var ok = Assert.IsType<OkResult<ServerHealthSummaryDto>>(result.Result);
        var summary = Assert.IsType<ServerHealthSummaryDto>(ok.Value);
        Assert.NotNull(summary);
        Assert.NotNull(summary.Encoding);
        Assert.Equal("fake-ffmpeg", summary.Encoding.EncoderPath);
    }
}
