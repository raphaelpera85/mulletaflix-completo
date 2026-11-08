using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoMoq;
using Emby.Server.Implementations.Library;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.LocalMetadata.Images;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;
using ServerLibraryManager = Emby.Server.Implementations.Library.LibraryManager;

namespace MulletaFlix.Server.Implementations.Tests.Library;

public sealed class LibraryManagerImageConversionTests
{
    [Fact]
    public async Task ConvertImageToLocal_PreservesImageProvenance()
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { CacheSize = 100 });
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        var previousLibraryManager = BaseItem.LibraryManager;
        var previousFileSystem = BaseItem.FileSystem;
        var item = new BaseItemTest();
        var remoteImage = new ItemImageInfo
        {
            Path = "https://images.example.test/poster.jpg",
            Type = ImageType.Primary,
            ProviderName = "ExampleProvider",
            Language = "pt-BR"
        };
        var localPath = Path.Combine(Path.GetTempPath(), "mfx-converted-poster.jpg");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.GetFileInfo(localPath)).Returns(new FileSystemMetadata
        {
            FullName = localPath,
            Name = Path.GetFileName(localPath),
            Extension = ".jpg",
            Exists = true
        });

        providerManager
            .Setup(manager => manager.SaveImage(item, remoteImage.Path, ImageType.Primary, 0, It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImagePath(type, index ?? 0, fileSystem.Object.GetFileInfo(localPath)))
            .Returns(Task.CompletedTask);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        fixture.Inject(configuration.Object);
        fixture.Inject(new Lazy<IProviderManager>(() => providerManager.Object));
        var subject = fixture.Create<ServerLibraryManager>();
        item.AddImage(remoteImage);
        BaseItem.LibraryManager = libraryManager.Object;
        BaseItem.FileSystem = fileSystem.Object;

        try
        {
            var converted = await subject.ConvertImageToLocal(item, remoteImage, 0, removeOnFailure: false);

            Assert.Equal(localPath, converted.Path);
            Assert.Equal("ExampleProvider", converted.ProviderName);
            Assert.Equal("pt-BR", converted.Language);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public async Task ConvertImageToLocal_PropagatesCancellationTokenToImageSaveAndRepositoryUpdate()
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { CacheSize = 100 });
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        var previousLibraryManager = BaseItem.LibraryManager;
        var previousFileSystem = BaseItem.FileSystem;
        var item = new BaseItemTest();
        var remoteImage = new ItemImageInfo
        {
            Path = "https://images.example.test/poster.jpg",
            Type = ImageType.Primary,
            ProviderName = "ExampleProvider",
            Language = "pt-BR"
        };
        var cancellationToken = new CancellationTokenSource().Token;
        var localPath = Path.Combine(Path.GetTempPath(), "mfx-converted-poster-cancel.jpg");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.GetFileInfo(localPath)).Returns(new FileSystemMetadata
        {
            FullName = localPath,
            Name = Path.GetFileName(localPath),
            Extension = ".jpg",
            Exists = true
        });

        providerManager
            .Setup(manager => manager.SaveImage(item, remoteImage.Path, ImageType.Primary, 0, cancellationToken))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImagePath(type, index ?? 0, fileSystem.Object.GetFileInfo(localPath)))
            .Returns(Task.CompletedTask);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, cancellationToken))
            .Returns(Task.CompletedTask);

        fixture.Inject(configuration.Object);
        fixture.Inject(new Lazy<IProviderManager>(() => providerManager.Object));
        var subject = fixture.Create<ServerLibraryManager>();
        item.AddImage(remoteImage);
        BaseItem.LibraryManager = libraryManager.Object;
        BaseItem.FileSystem = fileSystem.Object;

        try
        {
            await subject.ConvertImageToLocal(item, remoteImage, 0, removeOnFailure: false, cancellationToken);

            providerManager.VerifyAll();
            libraryManager.VerifyAll();
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public async Task ConvertImageToLocal_FirstUrlThrowsServerError_FallsBackToSecondPipeUrl()
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { CacheSize = 100 });
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        var previousLibraryManager = BaseItem.LibraryManager;
        var previousFileSystem = BaseItem.FileSystem;
        var item = new BaseItemTest();
        var remoteImage = new ItemImageInfo
        {
            Path = "https://images.example.test/broken.jpg|https://images.example.test/fallback.jpg",
            Type = ImageType.Primary,
            ProviderName = "ExampleProvider",
            Language = "pt-BR"
        };
        var localPath = Path.Combine(Path.GetTempPath(), "mfx-converted-fallback.jpg");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.GetFileInfo(localPath)).Returns(new FileSystemMetadata
        {
            FullName = localPath,
            Name = Path.GetFileName(localPath),
            Extension = ".jpg",
            Exists = true
        });

        providerManager
            .Setup(manager => manager.SaveImage(item, "https://images.example.test/broken.jpg", ImageType.Primary, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Upstream 502", null, HttpStatusCode.BadGateway));
        providerManager
            .Setup(manager => manager.SaveImage(item, "https://images.example.test/fallback.jpg", ImageType.Primary, 0, It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((savedItem, _, type, index, _) =>
                savedItem.SetImagePath(type, index ?? 0, fileSystem.Object.GetFileInfo(localPath)))
            .Returns(Task.CompletedTask);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(item, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        fixture.Inject(configuration.Object);
        fixture.Inject(new Lazy<IProviderManager>(() => providerManager.Object));
        var subject = fixture.Create<ServerLibraryManager>();
        item.AddImage(remoteImage);
        BaseItem.LibraryManager = libraryManager.Object;
        BaseItem.FileSystem = fileSystem.Object;

        try
        {
            var converted = await subject.ConvertImageToLocal(item, remoteImage, 0, removeOnFailure: true);

            Assert.Equal(localPath, converted.Path);
            Assert.Equal("ExampleProvider", converted.ProviderName);
            Assert.Equal("pt-BR", converted.Language);
            providerManager.VerifyAll();
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public async Task ConvertImageToLocal_TransientAndNotFoundFailures_TriesAllUrlsAndPreservesImageOnTransientFailure()
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { CacheSize = 100 });
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        var previousLibraryManager = BaseItem.LibraryManager;
        var item = new BaseItemTest();
        var remoteImage = new ItemImageInfo
        {
            Path = "https://images.example.test/transient.jpg|https://images.example.test/missing.jpg",
            Type = ImageType.Primary
        };

        providerManager
            .Setup(manager => manager.SaveImage(item, "https://images.example.test/transient.jpg", ImageType.Primary, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Upstream 503", null, HttpStatusCode.ServiceUnavailable));
        providerManager
            .Setup(manager => manager.SaveImage(item, "https://images.example.test/missing.jpg", ImageType.Primary, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound));

        fixture.Inject(configuration.Object);
        fixture.Inject(new Lazy<IProviderManager>(() => providerManager.Object));
        var subject = fixture.Create<ServerLibraryManager>();
        item.AddImage(remoteImage);
        BaseItem.LibraryManager = libraryManager.Object;

        try
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                subject.ConvertImageToLocal(item, remoteImage, 0, removeOnFailure: true));

            Assert.True(item.HasImage(ImageType.Primary));
            providerManager.VerifyAll();
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }

    [Fact]
    public void EpisodeLocalImageProvider_GetImages_FindsThumbInCaseInsensitiveMetadataSubdirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mfx-episode-local-" + Guid.NewGuid().ToString("N"));
        var metadataDir = Path.Combine(tempDir, "Metadata");
        var episodePath = Path.Combine(tempDir, "Show - S01E01.mkv");
        var thumbPath = Path.Combine(metadataDir, "Show - S01E01-thumb.jpg");
        var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);
        directoryService.Setup(ds => ds.GetFiles(tempDir)).Returns(new List<FileSystemMetadata>());
        directoryService.Setup(ds => ds.GetDirectories(tempDir)).Returns(new List<FileSystemMetadata>
        {
            new()
            {
                FullName = metadataDir,
                Name = "Metadata",
                IsDirectory = true
            }
        });
        directoryService.Setup(ds => ds.GetFiles(metadataDir)).Returns(new List<FileSystemMetadata>
        {
            new()
            {
                FullName = thumbPath,
                Name = Path.GetFileName(thumbPath),
                Extension = ".jpg",
                IsDirectory = false
            }
        });

        var episode = new Episode { Path = episodePath };
        var provider = new EpisodeLocalImageProvider();

        var images = provider.GetImages(episode, directoryService.Object).ToList();

        var primary = Assert.Single(images);
        Assert.Equal(ImageType.Primary, primary.Type);
        Assert.Equal(thumbPath, primary.FileInfo.FullName);
    }

    [Fact]
    public void LocalImageProvider_GetImages_OrdersExtensionsCaseInsensitivelyPreferringPngOverUppercaseJpg()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mfx-local-ext-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var previousMediaSourceManager = BaseItem.MediaSourceManager;
        try
        {
            var mediaSourceManager = new Mock<IMediaSourceManager>();
            mediaSourceManager.Setup(m => m.GetPathProtocol(It.IsAny<string>())).Returns(MediaBrowser.Model.MediaInfo.MediaProtocol.File);
            BaseItem.MediaSourceManager = mediaSourceManager.Object;

            var jpgPath = Path.Combine(tempDir, "poster.JPG");
            var pngPath = Path.Combine(tempDir, "poster.png");
            var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);
            directoryService.Setup(ds => ds.GetFileSystemEntries(tempDir)).Returns(new FileSystemMetadata[]
            {
                new()
                {
                    FullName = jpgPath,
                    Name = "poster.JPG",
                    Extension = ".JPG",
                    Length = 1024,
                    IsDirectory = false
                },
                new()
                {
                    FullName = pngPath,
                    Name = "poster.png",
                    Extension = ".png",
                    Length = 1024,
                    IsDirectory = false
                }
            });

            var movie = new Movie
            {
                Path = Path.Combine(tempDir, "Movie.mkv"),
                IsInMixedFolder = false
            };
            var provider = new LocalImageProvider(Mock.Of<IFileSystem>());

            var images = provider.GetImages(movie, directoryService.Object).ToList();

            var primary = Assert.Single(images, i => i.Type == ImageType.Primary);
            Assert.Equal(pngPath, primary.FileInfo.FullName);
        }
        finally
        {
            BaseItem.MediaSourceManager = previousMediaSourceManager;
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private sealed class BaseItemTest : BaseItem
    {
        public override string GetInternalMetadataPath() => System.IO.Path.GetTempPath();
    }
}
