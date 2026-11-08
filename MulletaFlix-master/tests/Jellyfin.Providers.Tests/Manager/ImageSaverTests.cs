using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

[Collection("LibraryManagerTests")]
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

    [Fact]
    public async Task SaveImage_PathWithoutParentDirectory_SavesSuccessfullyWithoutArgumentException()
    {
        var tempFile = "mfx-image-saver-nodir-" + Guid.NewGuid().ToString("N") + ".jpg";
        var expectedBytes = new byte[] { 0xAA, 0xBB, 0xCC };
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());
        var fileSystem = new Mock<IFileSystem>();
        var saver = new ImageSaver(
            configuration.Object,
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            Mock.Of<ILogger>());

        try
        {
            await saver.SaveImage(new MemoryStream(expectedBytes), tempFile);

            Assert.True(File.Exists(tempFile));
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(tempFile, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveImage_RetryAfterPartialRead_WritesCompleteBytes()
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-media-" + Guid.NewGuid().ToString("N"));
        var metadataRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-metadata-" + Guid.NewGuid().ToString("N"));
        var expectedBytes = new byte[] { 1, 2, 3, 4, 5, 6 };
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
        {
            ImageSavingConvention = ImageSavingConvention.Compatible
        });
        configuration.Setup(manager => manager.GetConfiguration("xbmcmetadata"))
            .Returns(new XbmcMetadataOptions());
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(files => files.GetFileInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                Exists = File.Exists(path),
                FullName = path,
                Name = Path.GetFileName(path),
                Extension = Path.GetExtension(path),
                Length = File.Exists(path) ? new FileInfo(path).Length : 0
            });
        var previousFileSystem = BaseItem.FileSystem;
        var previousMediaSourceManager = BaseItem.MediaSourceManager;
        BaseItem.FileSystem = fileSystem.Object;
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(manager => manager.GetPathProtocol(It.IsAny<string>())).Returns(MediaProtocol.File);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;
        var saver = new ImageSaver(
            configuration.Object,
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            Mock.Of<ILogger>());
        var item = new LocalMetadataTestItem(mediaRoot, metadataRoot);

        try
        {
            await saver.SaveImage(
                item,
                new PartialReadStream(expectedBytes),
                "image/jpeg",
                ImageType.Primary,
                null,
                TestContext.Current.CancellationToken);

            var image = item.GetImageInfo(ImageType.Primary, 0);
            Assert.NotNull(image);
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(image.Path, TestContext.Current.CancellationToken));
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
            BaseItem.MediaSourceManager = previousMediaSourceManager;
            if (Directory.Exists(mediaRoot))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }

            if (Directory.Exists(metadataRoot))
            {
                Directory.Delete(metadataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveImage_MultipleCompatibleBackdropPaths_FallsBackToMetadataPath()
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-backdrop-media-" + Guid.NewGuid().ToString("N"));
        var metadataRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-backdrop-metadata-" + Guid.NewGuid().ToString("N"));
        var expectedBytes = new byte[] { 9, 8, 7, 6 };
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
        {
            ImageSavingConvention = ImageSavingConvention.Compatible
        });
        configuration.Setup(manager => manager.GetConfiguration("xbmcmetadata"))
            .Returns(new XbmcMetadataOptions { EnableExtraThumbsDuplication = true });
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(files => files.GetFileInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                Exists = File.Exists(path),
                FullName = path,
                Name = Path.GetFileName(path),
                Extension = Path.GetExtension(path),
                Length = File.Exists(path) ? new FileInfo(path).Length : 0
            });
        var previousFileSystem = BaseItem.FileSystem;
        var previousMediaSourceManager = BaseItem.MediaSourceManager;
        BaseItem.FileSystem = fileSystem.Object;
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(manager => manager.GetPathProtocol(It.IsAny<string>())).Returns(MediaProtocol.File);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;
        var saver = new ImageSaver(
            configuration.Object,
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            Mock.Of<ILogger>());
        var item = new LocalMetadataTestItem(mediaRoot, metadataRoot);
        item.SetImage(new ItemImageInfo { Path = Path.Combine(mediaRoot, "backdrop.jpg"), Type = ImageType.Backdrop }, 0);

        try
        {
            Directory.CreateDirectory(mediaRoot);
            File.WriteAllText(Path.Combine(mediaRoot, "extrafanart"), "blocks directory creation");

            await saver.SaveImage(
                item,
                new MemoryStream(expectedBytes),
                "image/jpeg",
                ImageType.Backdrop,
                null,
                TestContext.Current.CancellationToken);

            var image = item.GetImageInfo(ImageType.Backdrop, 1);
            Assert.NotNull(image);
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(image.Path, TestContext.Current.CancellationToken));
            Assert.Equal(Path.Combine(metadataRoot, "backdrop1.jpg"), image.Path);
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
            BaseItem.MediaSourceManager = previousMediaSourceManager;
            if (Directory.Exists(mediaRoot))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }

            if (Directory.Exists(metadataRoot))
            {
                Directory.Delete(metadataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveImage_NonSeekableSource_IsRewoundBeforeSave()
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-nonseeking-media-" + Guid.NewGuid().ToString("N"));
        var metadataRoot = Path.Combine(Path.GetTempPath(), "mfx-image-saver-nonseeking-metadata-" + Guid.NewGuid().ToString("N"));
        var expectedBytes = new byte[] { 6, 5, 4, 3, 2, 1 };
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
        {
            ImageSavingConvention = ImageSavingConvention.Compatible
        });
        configuration.Setup(manager => manager.GetConfiguration("xbmcmetadata"))
            .Returns(new XbmcMetadataOptions());
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(files => files.GetFileInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                Exists = File.Exists(path),
                FullName = path,
                Name = Path.GetFileName(path),
                Extension = Path.GetExtension(path),
                Length = File.Exists(path) ? new FileInfo(path).Length : 0
            });
        var previousFileSystem = BaseItem.FileSystem;
        var previousMediaSourceManager = BaseItem.MediaSourceManager;
        BaseItem.FileSystem = fileSystem.Object;
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(manager => manager.GetPathProtocol(It.IsAny<string>())).Returns(MediaProtocol.File);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;
        var saver = new ImageSaver(
            configuration.Object,
            Mock.Of<ILibraryMonitor>(),
            fileSystem.Object,
            Mock.Of<ILogger>());
        var item = new LocalMetadataTestItem(mediaRoot, metadataRoot);

        try
        {
            await saver.SaveImage(
                item,
                new NonSeekableReadStream(expectedBytes),
                "image/jpeg",
                ImageType.Primary,
                null,
                TestContext.Current.CancellationToken);

            var image = item.GetImageInfo(ImageType.Primary, 0);
            Assert.NotNull(image);
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(image.Path, TestContext.Current.CancellationToken));
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
            BaseItem.MediaSourceManager = previousMediaSourceManager;
            if (Directory.Exists(mediaRoot))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }

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

    private sealed class LocalMetadataTestItem : BaseItem
    {
        private readonly string _metadataRoot;

        public LocalMetadataTestItem(string mediaRoot, string metadataRoot)
        {
            _metadataRoot = metadataRoot;
            Path = System.IO.Path.Combine(mediaRoot, "movie.mkv");
        }

        public override bool SupportsLocalMetadata => true;

        public override bool IsSaveLocalMetadataEnabled() => true;

        public override string GetInternalMetadataPath() => _metadataRoot;
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool _throwOnNextRead;
        private bool _hasThrown;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_throwOnNextRead && !_hasThrown)
            {
                _hasThrown = true;
                throw new IOException("simulated partial image read");
            }

            var read = base.Read(buffer, offset, Math.Min(2, count));
            _throwOnNextRead = read > 0;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_throwOnNextRead && !_hasThrown)
            {
                _hasThrown = true;
                throw new IOException("simulated partial image read");
            }

            var limited = buffer[..Math.Min(2, buffer.Length)];
            var read = base.Read(limited.Span);
            _throwOnNextRead = read > 0;
            return ValueTask.FromResult(read);
        }
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
