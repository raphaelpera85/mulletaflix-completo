using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

[Collection("BaseItem.FileSystem")]
public sealed class ImageSaverTests
{
    [Fact]
    public async Task SaveImage_WhenSourceFailsDuringCopy_PreservesDestinationAndRemovesTemporaryFile()
    {
        using var temporary = new TemporaryDirectory();
        var destinationPath = Path.Combine(temporary.Path, "poster.jpg");
        var previousImage = "previous image"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, previousImage, TestContext.Current.CancellationToken);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.SetAttributes(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()));
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(value => value.Configuration).Returns(new ServerConfiguration());
        var saver = new ImageSaver(configuration.Object, Mock.Of<ILibraryMonitor>(), fileSystem.Object, NullLogger<ImageSaver>.Instance);

        await Assert.ThrowsAsync<IOException>(() => saver.SaveImage(new FailingReadStream("partial"u8.ToArray()), destinationPath));

        Assert.Equal(previousImage, await File.ReadAllBytesAsync(destinationPath, TestContext.Current.CancellationToken));
        Assert.Equal([destinationPath], Directory.GetFiles(temporary.Path));
    }

    [Fact]
    public async Task SaveImage_WhenReplacingOnUnix_PreservesDestinationPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temporary = new TemporaryDirectory();
        var destinationPath = Path.Combine(temporary.Path, "poster.jpg");
        await File.WriteAllTextAsync(destinationPath, "previous image", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(destinationPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.SetAttributes(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()));
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(value => value.Configuration).Returns(new ServerConfiguration());
        var saver = new ImageSaver(configuration.Object, Mock.Of<ILibraryMonitor>(), fileSystem.Object, NullLogger<ImageSaver>.Instance);

        await saver.SaveImage(new MemoryStream("replacement image"u8.ToArray()), destinationPath);

        Assert.Equal("replacement image", await File.ReadAllTextAsync(destinationPath, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(destinationPath));
        Assert.Single(Directory.GetFiles(temporary.Path));
    }

    [Fact]
    public async Task SaveImage_ReplacesAndDeletesPreviousImageInsideMetadataRoot()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Rooted image deletion is enabled only on Windows.");

        using var temporary = new TemporaryDirectory();
        var metadataRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var itemPath = Directory.CreateDirectory(Path.Combine(metadataRoot, "item")).FullName;
        var oldPath = Path.Combine(itemPath, "old-poster.jpg");
        await File.WriteAllTextAsync(oldPath, "old image", TestContext.Current.CancellationToken);

        var (item, replacementPath) = await SaveReplacement(metadataRoot, itemPath, oldPath);

        Assert.False(File.Exists(oldPath));
        Assert.Equal("new image", await File.ReadAllTextAsync(replacementPath, TestContext.Current.CancellationToken));
        Assert.Equal(replacementPath, item.GetImagePath(ImageType.Primary, 0));
    }

    [Fact]
    public async Task SaveImage_WhenRootedDeletionIsUnsupported_PreservesOldAndNewImages()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "This test covers non-Windows fail-closed behavior.");

        using var temporary = new TemporaryDirectory();
        var metadataRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var itemPath = Directory.CreateDirectory(Path.Combine(metadataRoot, "item")).FullName;
        var oldPath = Path.Combine(itemPath, "old-poster.jpg");
        await File.WriteAllTextAsync(oldPath, "old image", TestContext.Current.CancellationToken);

        var (item, replacementPath) = await SaveReplacement(metadataRoot, itemPath, oldPath);

        Assert.Equal("old image", await File.ReadAllTextAsync(oldPath, TestContext.Current.CancellationToken));
        Assert.Equal("new image", await File.ReadAllTextAsync(replacementPath, TestContext.Current.CancellationToken));
        Assert.Equal(replacementPath, item.GetImagePath(ImageType.Primary, 0));
    }

    [Fact]
    public async Task SaveImage_PreservesPreviousImageInSiblingOfMetadataRoot()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Rooted image deletion is enabled only on Windows.");

        using var temporary = new TemporaryDirectory();
        var metadataRoot = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata")).FullName;
        var itemPath = Directory.CreateDirectory(Path.Combine(metadataRoot, "item")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(temporary.Path, "metadata-backup")).FullName;
        var oldPath = Path.Combine(sibling, "old-poster.jpg");
        await File.WriteAllTextAsync(oldPath, "preserve", TestContext.Current.CancellationToken);

        var (item, replacementPath) = await SaveReplacement(metadataRoot, itemPath, oldPath);

        Assert.Equal("preserve", await File.ReadAllTextAsync(oldPath, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(replacementPath));
        Assert.Equal(replacementPath, item.GetImagePath(ImageType.Primary, 0));
    }

    private static async Task<(TestMovie Item, string ReplacementPath)> SaveReplacement(string metadataRoot, string itemPath, string oldPath)
    {
        var previousFileSystem = BaseItem.FileSystem;
        try
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(value => value.SetAttributes(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()));
            fileSystem.Setup(value => value.GetFileInfo(It.IsAny<string>()))
                .Returns((string path) => new FileSystemMetadata
                {
                    Exists = File.Exists(path),
                    FullName = path,
                    Name = Path.GetFileName(path),
                    Extension = Path.GetExtension(path),
                    LastWriteTimeUtc = File.GetLastWriteTimeUtc(path)
                });
            fileSystem.Setup(value => value.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
                .Returns((FileSystemMetadata file) => file.LastWriteTimeUtc);
            BaseItem.FileSystem = fileSystem.Object;

            var appPaths = new Mock<IServerApplicationPaths>();
            appPaths.SetupGet(value => value.InternalMetadataPath).Returns(metadataRoot);
            var configuration = new Mock<IServerConfigurationManager>();
            configuration.SetupGet(value => value.ApplicationPaths).Returns(appPaths.Object);
            configuration.SetupGet(value => value.Configuration).Returns(new ServerConfiguration());

            var item = new TestMovie(itemPath)
            {
                ImageInfos = [new ItemImageInfo { Type = ImageType.Primary, Path = oldPath }]
            };
            var saver = new ImageSaver(
                configuration.Object,
                Mock.Of<ILibraryMonitor>(),
                fileSystem.Object,
                NullLogger<ImageSaver>.Instance);
            await saver.SaveImage(
                item,
                new MemoryStream("new image"u8.ToArray()),
                "image/jpeg",
                ImageType.Primary,
                0,
                saveLocallyWithMedia: false,
                TestContext.Current.CancellationToken);

            return (item, item.GetImagePath(ImageType.Primary, 0));
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    private sealed class TestMovie(string itemPath) : Movie
    {
        public override bool SupportsLocalMetadata => false;

        public override bool IsSaveLocalMetadataEnabled() => false;

        public override string GetInternalMetadataPath() => itemPath;
    }

    private sealed class FailingReadStream(byte[] initialData) : Stream
    {
        private bool _initialDataRead;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_initialDataRead)
            {
                throw new IOException("Synthetic source failure.");
            }

            _initialDataRead = true;
            Array.Copy(initialData, 0, buffer, offset, Math.Min(count, initialData.Length));
            return Math.Min(count, initialData.Length);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialDataRead)
            {
                return ValueTask.FromException<int>(new IOException("Synthetic source failure."));
            }

            _initialDataRead = true;
            var bytesRead = Math.Min(buffer.Length, initialData.Length);
            initialData.AsMemory(0, bytesRead).CopyTo(buffer);
            return ValueTask.FromResult(bytesRead);
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory().FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
