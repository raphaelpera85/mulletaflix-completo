using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using Moq;
using Xunit;

namespace MulletaFlix.Controller.Tests.Entities;

public class BaseItemTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("1", "0000000001")]
    [InlineData("t", "t")]
    [InlineData("test", "test")]
    [InlineData("test1", "test0000000001")]
    [InlineData("1test 2", "0000000001test 0000000002")]
    public void BaseItem_ModifySortChunks_Valid(string input, string expected)
        => Assert.Equal(expected, BaseItem.ModifySortChunks(input));

    [Theory]
    [InlineData("/Movies/Ted/Ted.mp4", "/Movies/Ted/Ted - Unrated Edition.mp4", "Ted", "Unrated Edition")]
    [InlineData("/Movies/Deadpool 2 (2018)/Deadpool 2 (2018).mkv", "/Movies/Deadpool 2 (2018)/Deadpool 2 (2018) - Super Duper Cut.mkv", "Deadpool 2 (2018)", "Super Duper Cut")]
    public void GetMediaSourceName_Valid(string primaryPath, string altPath, string name, string altName)
    {
        var video = new Video()
        {
            Path = primaryPath
        };

        var videoAlt = new Video()
        {
            Path = altPath,
        };

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(x => x.GetPathProtocol(It.IsAny<string>()))
                .Returns((string x) => MediaProtocol.File);
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(x => x.GetLocalAlternateVersionIds(It.IsAny<Video>()))
                .Returns([Guid.Empty]);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;
        BaseItem.LibraryManager = libraryManager.Object;

        Assert.Equal(name, video.GetMediaSourceName(video));
        Assert.Equal(altName, video.GetMediaSourceName(videoAlt));
    }

    [Fact]
    public void ClearPlaybackPosition_ResetsPositionTicks_WhenResumable()
    {
        var item = new Video();
        var user = new MulletaFlix.Database.Implementations.Entities.User("test", "test", "test");
        var userDataManager = new Mock<IUserDataManager>();
        var userData = new UserItemData
        {
            Key = "test_key",
            PlaybackPositionTicks = 1234567,
            Played = false,
            PlayCount = 2
        };
        userDataManager.Setup(m => m.GetUserData(user, item)).Returns(userData);
        BaseItem.UserDataManager = userDataManager.Object;

        item.ClearPlaybackPosition(user);

        Assert.Equal(0, userData.PlaybackPositionTicks);
        Assert.False(userData.Played);
        Assert.Equal(2, userData.PlayCount);
        userDataManager.Verify(m => m.SaveUserData(user, item, userData, MediaBrowser.Model.Entities.UserDataSaveReason.UpdateUserData, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Book_EnablesLocalMetadataForLocalBookFiles()
    {
        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(x => x.GetPathProtocol(It.IsAny<string>())).Returns(MediaProtocol.File);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;
        var book = new Book { Path = Path.GetFullPath(Path.Combine("library", "fantasy", "Cityscape.epub")) };

        Assert.True(book.SupportsLocalMetadata);
        Assert.True(book.IsSaveLocalMetadataEnabled());
    }

    [Fact]
    public void GetPreferredMetadataLanguage_PreservesExplicitRegionalLanguage()
    {
        var item = new Movie
        {
            PreferredMetadataLanguage = "pt-PT",
            PreferredMetadataCountryCode = "BR"
        };

        Assert.Equal("pt-PT", item.GetPreferredMetadataLanguage());
    }

    [Fact]
    public async Task DeleteImageAsync_WhenRepositoryUpdateFails_LeavesFileAndRestoresImageReference()
    {
        var previousFileSystem = BaseItem.FileSystem;
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var item = new RepositoryUpdatingVideo { FailRepositoryUpdate = true };
        var image = new ItemImageInfo { Path = "C:\\media\\poster.jpg", Type = ImageType.Primary };
        item.AddImage(image);
        BaseItem.FileSystem = fileSystem.Object;

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => item.DeleteImageAsync(ImageType.Primary, 0));

            Assert.Same(image, item.GetImageInfo(ImageType.Primary, 0));
            fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public async Task DeleteImageAsync_WhenRepositoryUpdateSucceeds_PreservesLocalFileThatMayBeShared()
    {
        var previousFileSystem = BaseItem.FileSystem;
        var item = new RepositoryUpdatingVideo();
        var otherItem = new RepositoryUpdatingVideo();
        const string imagePath = "C:\\media\\poster.jpg";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        item.AddImage(new ItemImageInfo { Path = imagePath, Type = ImageType.Primary });
        otherItem.AddImage(new ItemImageInfo { Path = imagePath, Type = ImageType.Primary });
        BaseItem.FileSystem = fileSystem.Object;

        try
        {
            await item.DeleteImageAsync(ImageType.Primary, 0);

            Assert.Null(item.GetImageInfo(ImageType.Primary, 0));
            Assert.Equal(imagePath, otherItem.GetImageInfo(ImageType.Primary, 0)?.Path);
            fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    private sealed class RepositoryUpdatingVideo : Video
    {
        public bool FailRepositoryUpdate { get; init; }

        public override Task UpdateToRepositoryAsync(ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            if (FailRepositoryUpdate)
            {
                return Task.FromException(new InvalidOperationException("Persistence failed."));
            }

            return Task.CompletedTask;
        }
    }
}
