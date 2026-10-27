using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public class ImageSaverTests
{
    [Fact]
    public void GetUniqueReplacementPath_PreservesDirectoryAndExtensionWithoutReusingCurrentPath()
    {
        const string path = "C:\\metadata\\item\\poster.jpg";

        var replacement = ImageSaver.GetUniqueReplacementPath(path);

        Assert.NotEqual(path, replacement);
        Assert.Equal(Path.GetDirectoryName(path), Path.GetDirectoryName(replacement));
        Assert.Equal(Path.GetExtension(path), Path.GetExtension(replacement));
        Assert.StartsWith(Path.GetFileNameWithoutExtension(path) + "-", Path.GetFileNameWithoutExtension(replacement), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveImage_WritesImageBytesAndUpdatesItemPath()
    {
        var metadataRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-" + Guid.NewGuid().ToString("N"));
        var expectedBytes = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(files => files.GetFileInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                Exists = File.Exists(path),
                FullName = path,
                Name = Path.GetFileName(path),
                Extension = Path.GetExtension(path),
                Length = new FileInfo(path).Length,
            });
        var previousFileSystem = BaseItem.FileSystem;
        BaseItem.FileSystem = fileSystem.Object;
        var saver = new ImageSaver(
            configuration.Object,
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            Mock.Of<ILogger>());
        var item = new TestItem(metadataRoot);

        try
        {
            await saver.SaveImage(
                item,
                new MemoryStream(expectedBytes),
                "image/jpeg",
                ImageType.Primary,
                null,
                TestContext.Current.CancellationToken);

            var image = item.GetImageInfo(ImageType.Primary, 0);
            Assert.NotNull(image);
            Assert.True(File.Exists(image.Path));
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(image.Path, TestContext.Current.CancellationToken));
            Assert.Equal(Path.Combine(metadataRoot, "poster.jpg"), image.Path);
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
            if (Directory.Exists(metadataRoot))
            {
                Directory.Delete(metadataRoot, recursive: true);
            }
        }
    }

    private sealed class TestItem(string metadataRoot) : BaseItem
    {
        public override bool SupportsLocalMetadata => false;

        public override string GetInternalMetadataPath() => metadataRoot;
    }
}
