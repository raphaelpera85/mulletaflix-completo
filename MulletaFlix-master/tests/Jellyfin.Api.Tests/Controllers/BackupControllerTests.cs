using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.SystemBackupService;
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
