using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.BaseItemManager;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Lyrics;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

// Allow Moq to see internal class
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace MulletaFlix.Providers.Tests.Manager
{
    public partial class ProviderManagerTests
    {

        public static TheoryData<Mock<IMetadataService>[], int> RefreshSingleItemOrderData()
            => new()
            {
                // no order set, uses provided order
                {
                    new[]
                    {
                        MockIMetadataService(true, true),
                        MockIMetadataService(true, true)
                    },
                    0
                },
                // sort order sets priority when all match
                {
                    new[]
                    {
                        MockIMetadataService(true, true, 1),
                        MockIMetadataService(true, true, 0),
                        MockIMetadataService(true, true, 2)
                    },
                    1
                },
                // CanRefreshPrimary prioritized
                {
                    new[]
                    {
                        MockIMetadataService(false, true),
                        MockIMetadataService(true, true),
                    },
                    1
                },
                // falls back to CanRefresh
                {
                    new[]
                    {
                        MockIMetadataService(false, false),
                        MockIMetadataService(false, true)
                    },
                    1
                },
            };

        [Theory]
        [MemberData(nameof(RefreshSingleItemOrderData))]
        public async Task RefreshSingleItem_ServiceOrdering_FollowsPriority(Mock<IMetadataService>[] servicesList, int expectedIndex)
        {
            var item = new Movie();

            using var providerManager = GetProviderManager();
            AddParts(providerManager, metadataServices: servicesList.Select(s => s.Object).ToArray());

            var refreshOptions = new MetadataRefreshOptions(Mock.Of<IDirectoryService>(MockBehavior.Strict));
            var actual = await providerManager.RefreshSingleItem(item, refreshOptions, CancellationToken.None);

            Assert.Equal(ItemUpdateType.MetadataDownload, actual);
            for (var i = 0; i < servicesList.Length; i++)
            {
                var times = i == expectedIndex ? Times.Once() : Times.Never();
                servicesList[i].Verify(mock => mock.RefreshMetadata(It.IsAny<BaseItem>(), It.IsAny<MetadataRefreshOptions>(), It.IsAny<CancellationToken>()), times);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RefreshSingleItem_RefreshMetadata_WhenServiceFound(bool serviceFound)
        {
            var item = new Movie();

            var servicesList = new[] { MockIMetadataService(false, serviceFound) };

            using var providerManager = GetProviderManager();
            AddParts(providerManager, metadataServices: servicesList.Select(s => s.Object).ToArray());

            var refreshOptions = new MetadataRefreshOptions(Mock.Of<IDirectoryService>(MockBehavior.Strict));
            var actual = await providerManager.RefreshSingleItem(item, refreshOptions, CancellationToken.None);

            var expectedResult = serviceFound ? ItemUpdateType.MetadataDownload : ItemUpdateType.None;
            Assert.Equal(expectedResult, actual);
        }

        [Fact]
        public async Task QueueRefresh_ProcessesItemsAddedWhileCurrentBatchIsRunning()
        {
            var firstItem = new Movie { Id = Guid.NewGuid() };
            var secondItem = new Movie { Id = Guid.NewGuid() };
            var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishFirstRefresh = new TaskCompletionSource<ItemUpdateType>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
            libraryManager.Setup(i => i.GetLibraryOptions(It.IsAny<BaseItem>())).Returns(new LibraryOptions());
            libraryManager.Setup(i => i.ClearIgnoreRuleCache());
            libraryManager.Setup(i => i.GetItemById(firstItem.Id)).Returns(firstItem);
            libraryManager.Setup(i => i.GetItemById(secondItem.Id)).Returns(secondItem);

            var metadataService = new Mock<IMetadataService>(MockBehavior.Strict);
            metadataService.Setup(i => i.Order).Returns(0);
            metadataService.Setup(i => i.CanRefreshPrimary(It.IsAny<Type>())).Returns(true);
            metadataService.Setup(i => i.CanRefresh(It.IsAny<BaseItem>())).Returns(true);
            metadataService.Setup(i => i.RefreshMetadata(
                    It.IsAny<BaseItem>(),
                    It.IsAny<MetadataRefreshOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns((BaseItem item, MetadataRefreshOptions _, CancellationToken _) =>
                {
                    if (item.Id == firstItem.Id)
                    {
                        firstRefreshStarted.TrySetResult();
                        return finishFirstRefresh.Task;
                    }

                    secondRefreshStarted.TrySetResult();
                    return Task.FromResult(ItemUpdateType.MetadataDownload);
                });

            using var providerManager = GetProviderManager(libraryManagerMock: libraryManager);
            AddParts(providerManager, metadataServices: new[] { metadataService.Object });
            var previousProviderManager = BaseItem.ProviderManager;
            BaseItem.ProviderManager = providerManager;
            try
            {
                var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
                providerManager.QueueRefresh(firstItem.Id, options, RefreshPriority.Normal);
                await firstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // Let the worker finish draining its first batch while that batch remains blocked.
                await Task.Delay(100, TestContext.Current.CancellationToken);
                providerManager.QueueRefresh(secondItem.Id, options, RefreshPriority.Normal);
                Assert.Contains(secondItem.Id, providerManager.GetRefreshQueue());

                finishFirstRefresh.TrySetResult(ItemUpdateType.MetadataDownload);
                await secondRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                libraryManager.Verify(i => i.GetItemById(firstItem.Id), Times.Once);
                libraryManager.Verify(i => i.GetItemById(secondItem.Id), Times.Once);
            }
            finally
            {
                finishFirstRefresh.TrySetResult(ItemUpdateType.MetadataDownload);
                BaseItem.ProviderManager = previousProviderManager;
            }
        }

        [Fact]
        public async Task RefreshSingleItem_DoesNotRefreshSameItemConcurrently()
        {
            var item = new Movie { Id = Guid.NewGuid() };
            var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishFirstRefresh = new TaskCompletionSource<ItemUpdateType>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshCount = 0;
            var metadataService = new Mock<IMetadataService>(MockBehavior.Strict);
            metadataService.Setup(i => i.Order).Returns(0);
            metadataService.Setup(i => i.CanRefreshPrimary(It.IsAny<Type>())).Returns(true);
            metadataService.Setup(i => i.RefreshMetadata(
                    item,
                    It.IsAny<MetadataRefreshOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns((BaseItem _, MetadataRefreshOptions _, CancellationToken _) =>
                {
                    if (Interlocked.Increment(ref refreshCount) == 1)
                    {
                        firstRefreshStarted.TrySetResult();
                        return finishFirstRefresh.Task;
                    }

                    secondRefreshStarted.TrySetResult();
                    return Task.FromResult(ItemUpdateType.MetadataDownload);
                });

            using var providerManager = GetProviderManager();
            AddParts(providerManager, metadataServices: new[] { metadataService.Object });
            var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
            var firstRefresh = providerManager.RefreshSingleItem(item, options, TestContext.Current.CancellationToken);
            await firstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var secondRefresh = providerManager.RefreshSingleItem(item, options, TestContext.Current.CancellationToken);
            Assert.False(secondRefreshStarted.Task.IsCompleted);

            finishFirstRefresh.TrySetResult(ItemUpdateType.MetadataDownload);
            await Task.WhenAll(firstRefresh, secondRefresh).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(secondRefreshStarted.Task.IsCompleted);
            Assert.Equal(2, refreshCount);
        }

    }
}
