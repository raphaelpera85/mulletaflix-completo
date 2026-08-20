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

    }
}
