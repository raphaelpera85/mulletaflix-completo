using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public class DeleteLogFileTaskTests
{
    [Fact]
    public void ExecuteAsync_DeletesExpiredSerilogAndLegacyLogsButKeepsRecentLogs()
    {
        var expiredSerilogLog = new FileSystemMetadata { FullName = "logs/log_2026-09-01_001.log", Name = "log_2026-09-01_001.log" };
        var expiredLegacyLog = new FileSystemMetadata { FullName = "logs/legacy.log", Name = "legacy.log" };
        var recentSerilogLog = new FileSystemMetadata { FullName = "logs/log_2026-09-28.log", Name = "log_2026-09-28.log" };
        var files = new[] { expiredSerilogLog, expiredLegacyLog, recentSerilogLog };
        var modifiedTimes = new Dictionary<string, DateTime>
        {
            [expiredSerilogLog.FullName] = DateTime.UtcNow.AddDays(-8),
            [expiredLegacyLog.FullName] = DateTime.UtcNow.AddDays(-8),
            [recentSerilogLog.FullName] = DateTime.UtcNow.AddDays(-1)
        };

        var config = new Mock<IConfigurationManager>();
        config.SetupGet(x => x.CommonConfiguration).Returns(new BaseApplicationConfiguration { LogFileRetentionDays = 7 });
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.LogDirectoryPath).Returns("logs");
        config.SetupGet(x => x.CommonApplicationPaths).Returns(applicationPaths.Object);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.GetFiles("logs", true)).Returns(files);
        fileSystem.Setup(x => x.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns<FileSystemMetadata>(file => modifiedTimes[file.FullName]);

        var task = new DeleteLogFileTask(config.Object, fileSystem.Object, Mock.Of<ILocalizationManager>());
        task.ExecuteAsync(new Progress<double>(_ => { }), CancellationToken.None);

        fileSystem.Verify(x => x.DeleteFile(expiredSerilogLog.FullName), Times.Once);
        fileSystem.Verify(x => x.DeleteFile(expiredLegacyLog.FullName), Times.Once);
        fileSystem.Verify(x => x.DeleteFile(recentSerilogLog.FullName), Times.Never);
    }
}
