using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Results;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class BackupControllerTests
{
    [Fact]
    public void StartRestoreBackup_ReturnsRetryAfterWhenAnotherBackupIsRunning()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupPath);

        try
        {
            const string archiveFileName = "restore.zip";
            File.WriteAllText(Path.Combine(backupPath, archiveFileName), string.Empty);
            var backupService = new Mock<IBackupService>();
            backupService
                .Setup(service => service.ScheduleRestoreAndRestartServer(It.IsAny<string>()))
                .Throws(new BackupOperationInProgressException());
            var controller = new BackupController(
                backupService.Object,
                Mock.Of<IApplicationPaths>(paths => paths.BackupPath == backupPath));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            var result = controller.StartRestoreBackup(new BackupRestoreRequestDto { ArchiveFileName = archiveFileName });

            var tooManyRequests = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequests.StatusCode);
            Assert.Equal("1", controller.Response.Headers.RetryAfter);
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    [Fact]
    public async Task CreateBackup_ReturnsRetryAfterWhenAnotherBackupIsRunning()
    {
        var backupService = new Mock<IBackupService>();
        backupService
            .Setup(service => service.CreateBackupAsync(It.IsAny<BackupOptionsDto>()))
            .ThrowsAsync(new BackupOperationInProgressException());
        var controller = new BackupController(backupService.Object, Mock.Of<IApplicationPaths>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.CreateBackup(new BackupOptionsDto());

        var tooManyRequests = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequests.StatusCode);
        Assert.Equal("1", controller.Response.Headers.RetryAfter);
    }

    [Fact]
    public async Task GetBackup_UsesBackupDirectorySanitizedPath()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupPath);

        try
        {
            var expectedPath = Path.Combine(backupPath, "backup.zip");
            var manifest = new BackupManifestDto
            {
                ServerVersion = new Version(1, 0),
                BackupEngineVersion = new Version(0, 2, 0),
                DateCreated = DateTimeOffset.UtcNow,
                Path = expectedPath,
                Options = new BackupOptionsDto()
            };

            File.WriteAllText(expectedPath, string.Empty);

            var backupService = new Mock<IBackupService>();
            backupService
                .Setup(service => service.GetBackupManifest(expectedPath))
                .ReturnsAsync(manifest);

            var controller = new BackupController(
                backupService.Object,
                Mock.Of<IApplicationPaths>(paths => paths.BackupPath == backupPath));

            var result = await controller.GetBackup(Path.Combine("..", "nested", "backup.zip"));

            var ok = Assert.IsType<OkResult<BackupManifestDto>>(result.Result);
            Assert.Same(manifest, ok.Value);
            backupService.Verify(service => service.GetBackupManifest(expectedPath), Times.Once);
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    [Fact]
    public async Task GetBackup_RejectsReparsePointInsideBackupDirectory()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupPath);

        try
        {
            var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
            var linkPath = Path.Combine(backupPath, "linked.zip");
            File.WriteAllText(targetPath, string.Empty);

            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
            }

            var backupService = new Mock<IBackupService>();
            var controller = new BackupController(
                backupService.Object,
                Mock.Of<IApplicationPaths>(paths => paths.BackupPath == backupPath));

            var result = await controller.GetBackup("linked.zip");

            Assert.IsType<BadRequestObjectResult>(result.Result);
            backupService.Verify(service => service.GetBackupManifest(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    [Fact]
    public void StartRestoreBackup_UsesBackupDirectorySanitizedPath()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupPath);

        try
        {
            var backupFileName = "restore.zip";
            var expectedPath = Path.Combine(backupPath, backupFileName);
            File.WriteAllText(expectedPath, string.Empty);

            var backupService = new Mock<IBackupService>();
            var controller = new BackupController(
                backupService.Object,
                Mock.Of<IApplicationPaths>(paths => paths.BackupPath == backupPath));

            var result = controller.StartRestoreBackup(new BackupRestoreRequestDto
            {
                ArchiveFileName = Path.Combine("..", "payload", backupFileName)
            });

            Assert.IsType<NoContentResult>(result);
            backupService.Verify(service => service.ScheduleRestoreAndRestartServer(expectedPath), Times.Once);
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }
}
