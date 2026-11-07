using System;
using System.IO;
using System.Text;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers
{
    public class SystemControllerTests
    {
        [Fact]
        public void GetLogFile_FileDoesNotExist_ReturnsNotFound()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem
                .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns([new() { Name = "file1.txt" }, new() { Name = "file2.txt" }]);

            var controller = new SystemController(
                Mock.Of<ILogger<SystemController>>(),
                Mock.Of<IServerApplicationHost>(),
                Mock.Of<IServerApplicationPaths>(),
                mockFileSystem.Object,
                Mock.Of<INetworkManager>(),
                Mock.Of<ISystemManager>());

            var result = controller.GetLogFile("DOES_NOT_EXIST.txt");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void GetLogFile_TailBytes_ReturnsOnlyRequestedTail()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "first line\nsecond line\nlast line", Encoding.UTF8);
                var mockFileSystem = new Mock<IFileSystem>();
                mockFileSystem
                    .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns([new() { Name = Path.GetFileName(path), FullName = path, LastWriteTimeUtc = DateTime.UtcNow }]);

                var controller = new SystemController(
                    Mock.Of<ILogger<SystemController>>(),
                    Mock.Of<IServerApplicationHost>(),
                    Mock.Of<IServerApplicationPaths>(),
                    mockFileSystem.Object,
                    Mock.Of<INetworkManager>(),
                    Mock.Of<ISystemManager>());

                var result = Assert.IsType<FileContentResult>(controller.GetLogFile(Path.GetFileName(path), 9));

                Assert.Equal("last line", Encoding.UTF8.GetString(result.FileContents));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void GetLogFile_TailBytes_IsCappedAtOneMiB()
        {
            var path = Path.GetTempFileName();
            try
            {
                using (var stream = File.Create(path))
                {
                    stream.SetLength(1_048_577);
                    stream.Seek(-1, SeekOrigin.End);
                    stream.WriteByte(65);
                }

                var mockFileSystem = new Mock<IFileSystem>();
                mockFileSystem
                    .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns([new() { Name = Path.GetFileName(path), FullName = path, LastWriteTimeUtc = DateTime.UtcNow }]);

                var controller = new SystemController(
                    Mock.Of<ILogger<SystemController>>(),
                    Mock.Of<IServerApplicationHost>(),
                    Mock.Of<IServerApplicationPaths>(),
                    mockFileSystem.Object,
                    Mock.Of<INetworkManager>(),
                    Mock.Of<ISystemManager>());

                var result = Assert.IsType<FileContentResult>(controller.GetLogFile(Path.GetFileName(path), int.MaxValue));

                Assert.Equal(1_048_576, result.FileContents.Length);
                Assert.Equal(65, result.FileContents[^1]);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
