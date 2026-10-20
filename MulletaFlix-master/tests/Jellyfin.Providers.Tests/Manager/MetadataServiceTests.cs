using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using MediaBrowser.Providers.Movies;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager
{
    public class MetadataServiceTests
    {
        [Fact]
        public void HasProviderId_UsesExplicitRecognizedProviderId()
        {
            var lookup = new SeriesInfo();
            lookup.SetProviderId("GoodShort", "goodshort-123");

            Assert.True(MetadataService<Series, SeriesInfo>.HasProviderId(lookup, "goodshort"));
            Assert.False(MetadataService<Series, SeriesInfo>.HasProviderId(lookup, "TheMovieDb"));
        }

        [Fact]
        public void HasProviderId_IgnoresEmptyProviderId()
        {
            var lookup = new SeriesInfo();
            lookup.ProviderIds["GoodShort"] = " ";

            Assert.False(MetadataService<Series, SeriesInfo>.HasProviderId(lookup, "GoodShort"));
        }

        [Fact]
        public async Task SaveRemoteResultImages_SingularType_StopsAfterFirstSuccessfulCandidate()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "primary-one", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("primary-one", ImageType.Primary));
            result.RemoteImages.Add(("primary-two", ImageType.Primary));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            providerManager.Verify(manager => manager.SaveImage(item, "primary-one", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            providerManager.Verify(manager => manager.SaveImage(item, "primary-two", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(new[] { ImageType.Primary }, foundImageTypes);
            Assert.Equal(0, refreshResult.Failures);
        }

        [Fact]
        public async Task SaveRemoteResultImages_SingularType_TriesNextCandidateWhenFirstFails()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "primary-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Image unavailable."));
            providerManager.Setup(manager => manager.SaveImage(item, "primary-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("primary-unavailable", ImageType.Primary));
            result.RemoteImages.Add(("primary-fallback", ImageType.Primary));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            providerManager.Verify(manager => manager.SaveImage(item, "primary-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Exactly(3));
            providerManager.Verify(manager => manager.SaveImage(item, "primary-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { ImageType.Primary }, foundImageTypes);
            Assert.Equal(0, refreshResult.Failures);
        }

        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task SaveRemoteResultImages_TransientHttpStatus_Retries(HttpStatusCode statusCode)
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.SetupSequence(manager => manager.SaveImage(item, "primary-transient", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Transient image failure.", null, statusCode))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("primary-transient", ImageType.Primary));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            providerManager.Verify(manager => manager.SaveImage(item, "primary-transient", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Equal(new[] { ImageType.Primary }, foundImageTypes);
            Assert.Equal(0, refreshResult.Failures);
        }

        [Fact]
        public async Task SaveRemoteResultImages_PermanentHttpStatus_SkipsToNextCandidate()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "primary-not-found", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Image not found.", null, HttpStatusCode.NotFound));
            providerManager.Setup(manager => manager.SaveImage(item, "primary-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("primary-not-found", ImageType.Primary));
            result.RemoteImages.Add(("primary-fallback", ImageType.Primary));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            providerManager.Verify(manager => manager.SaveImage(item, "primary-not-found", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            providerManager.Verify(manager => manager.SaveImage(item, "primary-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { ImageType.Primary }, foundImageTypes);
            Assert.Equal(0, refreshResult.Failures);
        }

        [Fact]
        public async Task SaveRemoteResultImages_FinalSingularImageFailure_IsReported()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "primary-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Image unavailable."));

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("primary-unavailable", ImageType.Primary));
            result.RemoteImages.Add(("primary-also-unavailable", ImageType.Primary));
            providerManager.Setup(manager => manager.SaveImage(item, "primary-also-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Fallback image unavailable."));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            Assert.Empty(foundImageTypes);
            Assert.Equal(1, refreshResult.Failures);
            Assert.Equal("Fallback image unavailable.", refreshResult.ErrorMessage);
        }

        [Fact]
        public async Task SaveRemoteResultImages_MultipleImageType_SavesEveryCandidate()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "backdrop-one", ImageType.Backdrop, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            providerManager.Setup(manager => manager.SaveImage(item, "backdrop-two", ImageType.Backdrop, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("backdrop-one", ImageType.Backdrop));
            result.RemoteImages.Add(("backdrop-two", ImageType.Backdrop));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                refreshResult,
                new HashSet<ImageType>(),
                null,
                null,
                CancellationToken.None);

            providerManager.Verify(manager => manager.SaveImage(item, "backdrop-one", ImageType.Backdrop, null, It.IsAny<CancellationToken>()), Times.Once);
            providerManager.Verify(manager => manager.SaveImage(item, "backdrop-two", ImageType.Backdrop, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(2, foundImageTypes.Count);
            Assert.All(foundImageTypes, imageType => Assert.Equal(ImageType.Backdrop, imageType));
            Assert.Equal(0, refreshResult.Failures);
        }

        [Fact]
        public async Task SaveRemoteResultImages_SingularFailureRecoveredByNextProvider_DoesNotRemainFailed()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "primary-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Image unavailable."));
            providerManager.Setup(manager => manager.SaveImage(item, "primary-recovered", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
            var refreshResult = new RefreshResult();
            var pendingSingleFailures = new Dictionary<ImageType, string>();
            var pendingMultipleFailures = new List<string>();
            var successfullySavedImageTypes = new HashSet<ImageType>();
            var failedProviderResult = new MetadataResult<Movie>();
            failedProviderResult.RemoteImages.Add(("primary-unavailable", ImageType.Primary));
            var successfulProviderResult = new MetadataResult<Movie>();
            successfulProviderResult.RemoteImages.Add(("primary-recovered", ImageType.Primary));

            await service.SaveRemoteResultImages(item, failedProviderResult, options, "ProviderOne", false, refreshResult, successfullySavedImageTypes, pendingSingleFailures, pendingMultipleFailures, CancellationToken.None);
            await service.SaveRemoteResultImages(item, successfulProviderResult, options, "ProviderTwo", false, refreshResult, successfullySavedImageTypes, pendingSingleFailures, pendingMultipleFailures, CancellationToken.None);

            Assert.Empty(pendingSingleFailures);
            Assert.Empty(pendingMultipleFailures);
            Assert.Equal(0, refreshResult.Failures);
        }

        [Fact]
        public async Task SaveRemoteResultImages_IdentifiedProvider_CanReplacePreviouslySavedSingularImage()
        {
            var item = new Movie();
            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "identified-primary", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateMovieMetadataService(providerManager.Object);
            var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>())
            {
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllImages = true
            };
            var result = new MetadataResult<Movie>();
            result.RemoteImages.Add(("identified-primary", ImageType.Primary));
            result.RemoteImages.Add(("identified-primary-duplicate", ImageType.Primary));
            var refreshResult = new RefreshResult();

            var foundImageTypes = await service.SaveRemoteResultImages(
                item,
                result,
                options,
                "IdentifiedProvider",
                true,
                refreshResult,
                new HashSet<ImageType> { ImageType.Primary },
                null,
                null,
                CancellationToken.None);

            Assert.Equal(new[] { ImageType.Primary }, foundImageTypes);
            providerManager.Verify(manager => manager.SaveImage(item, "identified-primary", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            providerManager.Verify(manager => manager.SaveImage(item, "identified-primary-duplicate", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SaveRemoteResultImages_RejectsPartialFailureStateEvenWhenNoImagesExist()
        {
            var service = CreateMovieMetadataService(Mock.Of<IProviderManager>());

            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveRemoteResultImages(
                new Movie(),
                new MetadataResult<Movie>(),
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                "MockProvider",
                false,
                new RefreshResult(),
                new HashSet<ImageType>(),
                new Dictionary<ImageType, string>(),
                null,
                CancellationToken.None));
        }

        [Fact]
        public async Task RefreshWithProviders_LocalSingularImage_FallsBackAndDoesNotRetainRecoveredFailure()
        {
            var item = new Movie();
            var localResult = new MetadataResult<Movie>
            {
                HasMetadata = true,
                Item = new Movie { Name = "Local metadata" }
            };
            localResult.RemoteImages.Add(("local-unavailable", ImageType.Primary));
            localResult.RemoteImages.Add(("local-fallback", ImageType.Primary));

            var localProvider = new Mock<ILocalMetadataProvider<Movie>>(MockBehavior.Strict);
            localProvider.SetupGet(provider => provider.Name).Returns("LocalProvider");
            localProvider.Setup(provider => provider.GetMetadata(It.IsAny<ItemInfo>(), It.IsAny<IDirectoryService>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(localResult);

            var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
            providerManager.Setup(manager => manager.SaveImage(item, "local-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Local image unavailable."));
            providerManager.Setup(manager => manager.SaveImage(item, "local-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new TestableMovieMetadataService(providerManager.Object);
            var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
            var refreshResult = await service.Refresh(item, options, localProvider.Object);

            Assert.Equal(0, refreshResult.Failures);
            providerManager.Verify(manager => manager.SaveImage(item, "local-unavailable", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
            providerManager.Verify(manager => manager.SaveImage(item, "local-fallback", ImageType.Primary, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        private static MovieMetadataService CreateMovieMetadataService(IProviderManager providerManager)
        {
            return new MovieMetadataService(
                Mock.Of<IServerConfigurationManager>(),
                Mock.Of<ILogger<MovieMetadataService>>(),
                providerManager,
                Mock.Of<IFileSystem>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<IExternalDataManager>(),
                Mock.Of<IItemRepository>());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void MergeBaseItemData_MergeMetadataSettings_MergesWhenSet(bool mergeMetadataSettings, bool defaultDate)
        {
            var newLocked = new[] { MetadataField.Genres, MetadataField.Cast };
            var newString = "new";
            var newDate = DateTime.UtcNow;

            var oldLocked = new[] { MetadataField.Genres };
            var oldString = "old";
            var oldDate = DateTime.UnixEpoch;

            var source = new MetadataResult<Movie>
            {
                Item = new Movie
                {
                    LockedFields = newLocked,
                    IsLocked = true,
                    PreferredMetadataCountryCode = newString,
                    PreferredMetadataLanguage = newString,
                    DateCreated = newDate
                }
            };

            if (defaultDate)
            {
                source.Item.DateCreated = default;
            }

            var target = new MetadataResult<Movie>
            {
                Item = new Movie
                {
                    LockedFields = oldLocked,
                    IsLocked = false,
                    PreferredMetadataCountryCode = oldString,
                    PreferredMetadataLanguage = oldString,
                    DateCreated = oldDate
                }
            };

            MetadataService<Movie, MovieInfo>.MergeBaseItemData(source, target, Array.Empty<MetadataField>(), true, mergeMetadataSettings);

            if (mergeMetadataSettings)
            {
                Assert.Equal(newLocked, target.Item.LockedFields);
                Assert.True(target.Item.IsLocked);
                Assert.Equal(newString, target.Item.PreferredMetadataCountryCode);
                Assert.Equal(newString, target.Item.PreferredMetadataLanguage);
                Assert.Equal(defaultDate ? oldDate : newDate, target.Item.DateCreated);
            }
            else
            {
                Assert.Equal(oldLocked, target.Item.LockedFields);
                Assert.False(target.Item.IsLocked);
                Assert.Equal(oldString, target.Item.PreferredMetadataCountryCode);
                Assert.Equal(oldString, target.Item.PreferredMetadataLanguage);
                Assert.Equal(oldDate, target.Item.DateCreated);
            }
        }

        [Theory]
        [InlineData("Name", MetadataField.Name, false)]
        [InlineData("OriginalTitle", null)]
        [InlineData("OfficialRating", MetadataField.OfficialRating)]
        [InlineData("CustomRating")]
        [InlineData("Tagline")]
        [InlineData("Overview", MetadataField.Overview)]
        [InlineData("DisplayOrder", null, false)]
        [InlineData("ForcedSortName", null, false)]
        public void MergeBaseItemData_StringField_ReplacesAppropriately(string propName, MetadataField? lockField = null, bool replacesWithEmpty = true)
        {
            var oldValue = "Old";
            var newValue = "New";

            // Use type Series to hit DisplayOrder
            Assert.False(TestMergeBaseItemData<Series, SeriesInfo>(propName, oldValue, newValue, null, false, out _));
            if (lockField is not null)
            {
                Assert.False(TestMergeBaseItemData<Series, SeriesInfo>(propName, oldValue, newValue, lockField, true, out _));
                Assert.False(TestMergeBaseItemData<Series, SeriesInfo>(propName, null, newValue, lockField, false, out _));
                Assert.False(TestMergeBaseItemData<Series, SeriesInfo>(propName, string.Empty, newValue, lockField, false, out _));
            }

            Assert.True(TestMergeBaseItemData<Series, SeriesInfo>(propName, oldValue, newValue, null, true, out _));
            Assert.True(TestMergeBaseItemData<Series, SeriesInfo>(propName, null, newValue, null, false, out _));
            Assert.True(TestMergeBaseItemData<Series, SeriesInfo>(propName, string.Empty, newValue, null, false, out _));

            var replacedWithEmpty = TestMergeBaseItemData<Series, SeriesInfo>(propName, oldValue, string.Empty, null, true, out _);
            Assert.Equal(replacesWithEmpty, replacedWithEmpty);
        }

        [Theory]
        [InlineData("Genres", MetadataField.Genres)]
        [InlineData("Studios", MetadataField.Studios)]
        [InlineData("Tags", MetadataField.Tags)]
        [InlineData("ProductionLocations", MetadataField.ProductionLocations)]
        [InlineData("AlbumArtists")]
        public void MergeBaseItemData_StringArrayField_ReplacesAppropriately(string propName, MetadataField? lockField = null)
        {
            // Note that arrays are replaced, not merged
            var oldValue = new[] { "Old" };
            var newValue = new[] { "New" };

            // Use type Audio to hit AlbumArtists
            Assert.False(TestMergeBaseItemData<Audio, SongInfo>(propName, oldValue, newValue, null, false, out _));
            if (lockField is not null)
            {
                Assert.False(TestMergeBaseItemData<Audio, SongInfo>(propName, oldValue, newValue, lockField, true, out _));
                Assert.False(TestMergeBaseItemData<Audio, SongInfo>(propName, Array.Empty<string>(), newValue, lockField, false, out _));
            }

            Assert.True(TestMergeBaseItemData<Audio, SongInfo>(propName, oldValue, newValue, null, true, out _));
            Assert.True(TestMergeBaseItemData<Audio, SongInfo>(propName, Array.Empty<string>(), newValue, null, false, out _));

            Assert.True(TestMergeBaseItemData<Audio, SongInfo>(propName, oldValue, Array.Empty<string>(), null, true, out _));
        }

        public static TheoryData<string, object, object> MergeBaseItemData_SimpleField_ReplacesAppropriately_TestData()
            => new()
            {
                { "IndexNumber", 1, 2 },
                { "ParentIndexNumber", 1, 2 },
                { "ProductionYear", 1, 2 },
                { "CommunityRating", 1.0f, 2.0f },
                { "CriticRating", 1.0f, 2.0f },
                { "EndDate", DateTime.UnixEpoch, DateTime.UtcNow },
                { "PremiereDate", DateTime.UnixEpoch, DateTime.UtcNow },
                { "Video3DFormat", Video3DFormat.HalfSideBySide, Video3DFormat.FullSideBySide }
            };

        [Theory]
        [MemberData(nameof(MergeBaseItemData_SimpleField_ReplacesAppropriately_TestData))]
        public void MergeBaseItemData_SimpleField_ReplacesAppropriately(string propName, object oldValue, object newValue)
        {
            // Use type Movie to allow testing of Video3DFormat
            Assert.False(TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, newValue, null, false, out _));

            Assert.True(TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, newValue, null, true, out _));
            Assert.True(TestMergeBaseItemData<Movie, MovieInfo>(propName, null, newValue, null, false, out _));

            // Video3DFormat - null values do NOT replace existing data
            if (string.Equals(propName, "Video3DFormat", StringComparison.Ordinal))
            {
                Assert.False(
                    TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, null, null, true, out _));
            }
            else
            {
                Assert.True(
                    TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, null, null, true, out _));
            }
        }

        [Fact]
        public void MergeBaseItemData_MergeTrailers_ReplacesAppropriately()
        {
            string propName = "RemoteTrailers";
            var oldValue = new[]
            {
                new MediaUrl
                {
                    Name = "Name 1",
                    Url = "URL 1"
                }
            };
            var newValue = new[]
            {
                new MediaUrl
                {
                    Name = "Name 2",
                    Url = "URL 2"
                }
            };

            Assert.False(TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, newValue, null, false, out _));

            Assert.True(TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, newValue, null, true, out _));
            Assert.True(TestMergeBaseItemData<Movie, MovieInfo>(propName, Array.Empty<MediaUrl>(), newValue, null, false, out _));

            Assert.True(TestMergeBaseItemData<Movie, MovieInfo>(propName, oldValue, Array.Empty<MediaUrl>(), null, true, out _));
        }

        [Fact]
        public void MergeBaseItemData_ProviderIds_MergesAppropriately()
        {
            var propName = "ProviderIds";
            var oldValue = new Dictionary<string, string>
            {
                { "provider 1", "id 1" }
            };

            // overwrite provider id
            var overwriteNewValue = new Dictionary<string, string>
            {
                { "provider 1", "id 2" }
            };
            Assert.False(TestMergeBaseItemData<Movie, MovieInfo>(propName, new Dictionary<string, string>(oldValue), overwriteNewValue, null, false, out _));
            TestMergeBaseItemData<Movie, MovieInfo>(propName, new Dictionary<string, string>(oldValue), overwriteNewValue, null, true, out var overwritten);
            Assert.Equal(overwriteNewValue, overwritten);

            // merge without overwriting
            var mergeNewValue = new Dictionary<string, string>
            {
                { "provider 1", "id 2" },
                { "provider 2", "id 3" }
            };
            TestMergeBaseItemData<Movie, MovieInfo>(propName, new Dictionary<string, string>(oldValue), mergeNewValue, null, false, out var merged);
            var actual = (Dictionary<string, string>)merged!;
            Assert.Equal("id 1", actual["provider 1"]);
            Assert.Equal("id 3", actual["provider 2"]);

            // empty source results in no change
            TestMergeBaseItemData<Movie, MovieInfo>(propName, new Dictionary<string, string>(oldValue), new Dictionary<string, string>(), null, true, out var notOverwritten);
            Assert.Equal(oldValue, notOverwritten);
        }

        [Fact]
        public void MergeBaseItemData_MergePeople_MergesAppropriately()
        {
            // PersonInfo in list is changed by merge, create new for every call
            List<PersonInfo> GetOldValue()
                => new()
                {
                    new PersonInfo
                    {
                        Name = "Name 1",
                        ProviderIds = new Dictionary<string, string>
                        {
                            { "Provider 1", "1234" }
                        }
                    }
                };

            // overwrite provider id
            var overwriteNewValue = new List<PersonInfo>
            {
                new()
                {
                    Name = "Name 2"
                }
            };
            Assert.False(TestMergeBaseItemDataPerson(GetOldValue(), overwriteNewValue, null, false, out var result));
            // People not already in target are not merged into it from source
            List<PersonInfo> actual = (List<PersonInfo>)result!;
            Assert.Single(actual);
            Assert.Equal("Name 1", actual[0].Name);

            Assert.True(TestMergeBaseItemDataPerson(GetOldValue(), overwriteNewValue, null, true, out _));
            Assert.True(TestMergeBaseItemDataPerson(new List<PersonInfo>(), overwriteNewValue, null, false, out _));
            Assert.True(TestMergeBaseItemDataPerson(null, overwriteNewValue, null, false, out _));

            Assert.False(TestMergeBaseItemDataPerson(GetOldValue(), overwriteNewValue, MetadataField.Cast, true, out _));

            // providers merge but don't overwrite existing keys
            var mergeNewValue = new List<PersonInfo>
            {
                new()
                {
                    Name = "Name 1",
                    ProviderIds = new Dictionary<string, string>
                    {
                        { "Provider 1", "5678" },
                        { "Provider 2", "5678" }
                    }
                }
            };
            TestMergeBaseItemDataPerson(GetOldValue(), mergeNewValue, null, false, out result);
            actual = (List<PersonInfo>)result!;
            Assert.Single(actual);
            Assert.Equal("Name 1", actual[0].Name);
            Assert.Equal(2, actual[0].ProviderIds.Count);
            Assert.Equal("1234", actual[0].ProviderIds["Provider 1"]);
            Assert.Equal("5678", actual[0].ProviderIds["Provider 2"]);

            // picture adds if missing but won't overwrite (forcing overwrites entire list, not entries in merged PersonInfo)
            var mergePicture1 = new List<PersonInfo>
            {
                new()
                {
                    Name = "Name 1",
                    ImageUrl = "URL 1"
                }
            };
            TestMergeBaseItemDataPerson(GetOldValue(), mergePicture1, null, false, out result);
            actual = (List<PersonInfo>)result!;
            Assert.Single(actual);
            Assert.Equal("Name 1", actual[0].Name);
            Assert.Equal("URL 1", actual[0].ImageUrl);
            var mergePicture2 = new List<PersonInfo>
            {
                new()
                {
                    Name = "Name 1",
                    ImageUrl = "URL 2"
                }
            };
            TestMergeBaseItemDataPerson(mergePicture1, mergePicture2, null, false, out result);
            actual = (List<PersonInfo>)result!;
            Assert.Single(actual);
            Assert.Equal("Name 1", actual[0].Name);
            Assert.Equal("URL 1", actual[0].ImageUrl);

            // empty source can be forced to overwrite a target with data
            Assert.True(TestMergeBaseItemDataPerson(GetOldValue(), new List<PersonInfo>(), null, true, out _));
        }

        private static bool TestMergeBaseItemDataPerson(List<PersonInfo>? oldValue, List<PersonInfo>? newValue, MetadataField? lockField, bool replaceData, out object? actualValue)
        {
            var source = new MetadataResult<Movie>
            {
                Item = new Movie(),
                People = newValue
            };

            var target = new MetadataResult<Movie>
            {
                Item = new Movie(),
                People = oldValue
            };

            var lockedFields = lockField is null ? Array.Empty<MetadataField>() : new[] { (MetadataField)lockField };
            MetadataService<Movie, MovieInfo>.MergeBaseItemData(source, target, lockedFields, replaceData, false);

            actualValue = target.People;
            return newValue?.SequenceEqual((IEnumerable<PersonInfo>)actualValue!) ?? actualValue is null;
        }

        /// <summary>
        /// Makes a call to <see cref="MetadataService{TItemType,TIdType}.MergeBaseItemData"/> with the provided parameters and returns whether the target changed or not.
        ///
        /// Reflection is used to allow testing of all fields using the same logic, rather than relying on copy/pasting test code for each field.
        /// </summary>
        /// <param name="propName">The property to test.</param>
        /// <param name="oldValue">The initial value in the target object.</param>
        /// <param name="newValue">The initial value in the source object.</param>
        /// <param name="lockField">The metadata field that locks this property if the field should be locked, or <c>null</c> to leave unlocked.</param>
        /// <param name="replaceData">Passed through to <see cref="MetadataService{TItemType,TIdType}.MergeBaseItemData"/>.</param>
        /// <param name="actualValue">The resulting value set to the target.</param>
        /// <typeparam name="TItemType">The <see cref="BaseItem"/> type to test on.</typeparam>
        /// <typeparam name="TIdType">The <see cref="BaseItem"/> info type.</typeparam>
        /// <returns><c>true</c> if the property on the target updates to match the source value when<see cref="MetadataService{TItemType,TIdType}.MergeBaseItemData"/> is called.</returns>
        private static bool TestMergeBaseItemData<TItemType, TIdType>(string propName, object? oldValue, object? newValue, MetadataField? lockField, bool replaceData, out object? actualValue)
            where TItemType : BaseItem, IHasLookupInfo<TIdType>, new()
            where TIdType : ItemLookupInfo, new()
        {
            var property = typeof(TItemType).GetProperty(propName)!;

            var source = new MetadataResult<TItemType>
            {
                Item = new TItemType()
            };
            property.SetValue(source.Item, newValue);

            var target = new MetadataResult<TItemType>
            {
                Item = new TItemType()
            };
            property.SetValue(target.Item, oldValue);

            var lockedFields = lockField is null ? Array.Empty<MetadataField>() : new[] { (MetadataField)lockField };
            // generic type doesn't actually matter to call the static method, just has to be filled in
            MetadataService<TItemType, TIdType>.MergeBaseItemData(source, target, lockedFields, replaceData, false);

            actualValue = property.GetValue(target.Item);
            return newValue?.Equals(actualValue) ?? actualValue is null;
        }

        private sealed class TestableMovieMetadataService : MovieMetadataService
        {
            public TestableMovieMetadataService(IProviderManager providerManager)
                : base(
                    Mock.Of<IServerConfigurationManager>(),
                    Mock.Of<ILogger<MovieMetadataService>>(),
                    providerManager,
                    Mock.Of<IFileSystem>(),
                    Mock.Of<ILibraryManager>(),
                    Mock.Of<IExternalDataManager>(),
                    Mock.Of<IItemRepository>())
            {
            }

            public Task<RefreshResult> Refresh(Movie item, MetadataRefreshOptions options, IMetadataProvider provider)
            {
                return RefreshWithProviders(new MetadataResult<Movie> { Item = item }, null!, options, new[] { provider }, ImageProvider, false, CancellationToken.None);
            }
        }
    }
}
