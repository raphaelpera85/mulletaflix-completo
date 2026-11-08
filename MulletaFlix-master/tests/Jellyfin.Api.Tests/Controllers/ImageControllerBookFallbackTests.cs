using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public class ImageControllerBookFallbackTests
{
    private readonly ImageController _subject;
    private readonly Mock<IProviderManager> _providerManager;

    public ImageControllerBookFallbackTests()
    {
        _providerManager = new Mock<IProviderManager>(MockBehavior.Strict);

        _subject = new ImageController(
            Mock.Of<IUserManager>(),
            Mock.Of<ILibraryManager>(),
            _providerManager.Object,
            Mock.Of<IImageProcessor>(),
            Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<ImageController>>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<MediaBrowser.Common.Net.INetworkManager>(),
            Mock.Of<MediaBrowser.Controller.Devices.IDeviceManager>());
    }

    [Fact]
    public async Task TryRecoverMissingBookPrimaryImageAsync_DownloadsRemoteCover()
    {
        var book = new Book { Name = "20 Mil Leguas Submarinas" };
        book.SetProviderId("OpenLibrary", "OL26415696W");

        var remoteImage = new RemoteImageInfo
        {
            Type = ImageType.Primary,
            Url = "https://covers.openlibrary.org/b/id/123-L.jpg"
        };

        _providerManager
            .Setup(pm => pm.GetAvailableRemoteImages(
                book,
                It.Is<RemoteImageQuery>(query =>
                    query.ImageType == ImageType.Primary &&
                    query.IncludeAllLanguages &&
                    query.IncludeDisabledProviders &&
                    query.ProviderName == string.Empty),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { remoteImage });

        _providerManager
            .Setup(pm => pm.SaveImage(
                book,
                remoteImage.Url,
                ImageType.Primary,
                0,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((item, _, type, _, _) =>
            {
                item.AddImage(new ItemImageInfo
                {
                    Path = remoteImage.Url,
                    Type = type
                });
            })
            .Returns(Task.CompletedTask);

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(book, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;

        try
        {
            var recovered = await _subject.TryRecoverMissingBookPrimaryImageAsync(book, ImageType.Primary, 0, CancellationToken.None);

            Assert.True(recovered);
            Assert.NotNull(book.GetImageInfo(ImageType.Primary, 0));
            Assert.Equal(remoteImage.Url, book.GetImagePath(ImageType.Primary, 0));

            _providerManager.VerifyAll();
            libraryManager.Verify(manager => manager.UpdateItemAsync(book, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }

    [Fact]
    public async Task TryRecoverMissingBookPrimaryImageAsync_PrefersLanguageAndPersistsProvenance()
    {
        var book = new Book { Name = "Vinte Mil Léguas Submarinas", PreferredMetadataLanguage = "pt-BR" };
        book.SetProviderId("OpenLibrary", "OL26415696W");

        var englishImage = new RemoteImageInfo
        {
            Type = ImageType.Primary,
            Url = "https://covers.openlibrary.org/b/id/123-L.jpg",
            ProviderName = "OpenLibrary",
            Language = "en"
        };
        var localizedImage = new RemoteImageInfo
        {
            Type = ImageType.Primary,
            Url = "https://covers.openlibrary.org/b/id/456-L.jpg",
            ProviderName = "OpenLibrary",
            Language = "pt-BR"
        };

        _providerManager
            .Setup(pm => pm.GetAvailableRemoteImages(
                book,
                It.Is<RemoteImageQuery>(query => query.ImageType == ImageType.Primary),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { englishImage, localizedImage });

        _providerManager
            .Setup(pm => pm.SaveImage(
                book,
                localizedImage.Url,
                ImageType.Primary,
                0,
                It.IsAny<CancellationToken>()))
            .Callback<BaseItem, string, ImageType, int?, CancellationToken>((item, _, type, _, _) =>
            {
                item.AddImage(new ItemImageInfo { Path = localizedImage.Url, Type = type });
            })
            .Returns(Task.CompletedTask);

        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(book, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var previousLibraryManager = BaseItem.LibraryManager;
        BaseItem.LibraryManager = libraryManager.Object;

        try
        {
            var recovered = await _subject.TryRecoverMissingBookPrimaryImageAsync(book, ImageType.Primary, 0, CancellationToken.None);

            Assert.True(recovered);
            Assert.Equal(localizedImage.Url, book.GetImagePath(ImageType.Primary, 0));
            Assert.Equal("OpenLibrary", book.GetImageInfo(ImageType.Primary, 0)!.ProviderName);
            Assert.Equal("pt-BR", book.GetImageInfo(ImageType.Primary, 0)!.Language);
            _providerManager.Verify(pm => pm.SaveImage(book, englishImage.Url, ImageType.Primary, 0, It.IsAny<CancellationToken>()), Times.Never);
            _providerManager.Verify(pm => pm.SaveImage(book, localizedImage.Url, ImageType.Primary, 0, It.IsAny<CancellationToken>()), Times.Once);
            libraryManager.Verify(manager => manager.UpdateItemAsync(book, null, ItemUpdateType.ImageUpdate, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }
}
