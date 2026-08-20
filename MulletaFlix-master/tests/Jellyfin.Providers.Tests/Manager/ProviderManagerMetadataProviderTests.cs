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


        [Theory]
        [MemberData(nameof(GetMetadataProvidersOrderData))]
        public void GetMetadataProviders_ProviderOrder_MatchesExpected(
            string[] providers,
            int[]? libraryLocalOrder,
            int[]? libraryRemoteOrder,
            int[]? serverLocalOrder,
            int[]? serverRemoteOrder,
            int?[]? hasOrderOrder,
            int[] expectedOrder)
        {
            var item = new MetadataTestItem();

            var nameProvider = new Func<int, string>(i => "Provider" + i);

            var providerList = new List<IMetadataProvider<MetadataTestItem>>();
            for (var i = 0; i < providers.Length; i++)
            {
                var order = hasOrderOrder?[i];
                providerList.Add(MockIMetadataProviderMapper<MetadataTestItem, MetadataTestItemInfo>(providers[i], nameProvider(i), order: order));
            }

            var libraryOptions = CreateLibraryOptions(
                item.GetType().Name,
                localMetadataReaderOrder: libraryLocalOrder?.Select(nameProvider).ToArray(),
                metadataFetcherOrder: libraryRemoteOrder?.Select(nameProvider).ToArray());
            var serverConfiguration = CreateServerConfiguration(
                item.GetType().Name,
                localMetadataReaderOrder: serverLocalOrder?.Select(nameProvider).ToArray(),
                metadataFetcherOrder: serverRemoteOrder?.Select(nameProvider).ToArray());

            var baseItemManager = new Mock<IBaseItemManager>(MockBehavior.Strict);
            baseItemManager.Setup(i => i.IsMetadataFetcherEnabled(item, It.IsAny<TypeOptions>(), It.IsAny<string>()))
                .Returns(true);

            using var providerManager = GetProviderManager(serverConfiguration: serverConfiguration, baseItemManager: baseItemManager.Object);
            AddParts(providerManager, metadataProviders: providerList);

            var actualProviders = providerManager.GetMetadataProviders<MetadataTestItem>(item, libraryOptions).ToList();

            Assert.Equal(providerList.Count, actualProviders.Count);
            var actualOrder = actualProviders.Select(i => providerList.IndexOf(i)).ToArray();
            Assert.Equal(expectedOrder, actualOrder);
        }

        [Theory]
        [InlineData(nameof(IMetadataProvider))]
        [InlineData(nameof(ILocalMetadataProvider))]
        [InlineData(nameof(IRemoteMetadataProvider))]
        [InlineData(nameof(ICustomMetadataProvider))]
        public void GetMetadataProviders_CanRefreshMetadataBasic_ReturnsTrue(string providerType)
        {
            GetMetadataProviders_CanRefreshMetadata_Tester(providerType, true);
        }

        [Theory]
        [InlineData(nameof(ILocalMetadataProvider), false, true)]
        [InlineData(nameof(IRemoteMetadataProvider), false, false)]
        [InlineData(nameof(ICustomMetadataProvider), false, false)]
        [InlineData(nameof(ILocalMetadataProvider), true, true)]
        [InlineData(nameof(ICustomMetadataProvider), true, false)]
        public void GetMetadataProviders_CanRefreshMetadataLocked_WhenLocalOrForced(string providerType, bool forced, bool expected)
        {
            GetMetadataProviders_CanRefreshMetadata_Tester(providerType, expected, itemLocked: true, providerForced: forced);
        }

        [Theory]
        [InlineData(nameof(ILocalMetadataProvider), false, true)]
        [InlineData(nameof(ICustomMetadataProvider), false, true)]
        [InlineData(nameof(IRemoteMetadataProvider), false, false)]
        [InlineData(nameof(IRemoteMetadataProvider), true, true)]
        public void GetMetadataProviders_CanRefreshMetadataBaseItemEnabled_WhenEnabledOrNotRemote(string providerType, bool baseItemEnabled, bool expected)
        {
            GetMetadataProviders_CanRefreshMetadata_Tester(providerType, expected, baseItemEnabled: baseItemEnabled);
        }

        [Theory]
        [InlineData(nameof(IRemoteMetadataProvider), false, true)]
        [InlineData(nameof(ICustomMetadataProvider), false, true)]
        [InlineData(nameof(ILocalMetadataProvider), false, false)]
        [InlineData(nameof(ILocalMetadataProvider), true, true)]
        public void GetMetadataProviders_CanRefreshMetadataSupportsLocal_WhenSupportsOrNotLocal(string providerType, bool supportsLocalMetadata, bool expected)
        {
            GetMetadataProviders_CanRefreshMetadata_Tester(providerType, expected, supportsLocalMetadata: supportsLocalMetadata);
        }

        [Theory]
        [InlineData(nameof(ICustomMetadataProvider), true)]
        [InlineData(nameof(IRemoteMetadataProvider), true)]
        [InlineData(nameof(ILocalMetadataProvider), true)]
        public void GetMetadataProviders_CanRefreshMetadataOwned(string providerType, bool expected)
        {
            GetMetadataProviders_CanRefreshMetadata_Tester(providerType, expected, ownedItem: true);
        }

        private static void GetMetadataProviders_CanRefreshMetadata_Tester(
            string providerType,
            bool expected,
            bool itemLocked = false,
            bool baseItemEnabled = true,
            bool providerForced = false,
            bool supportsLocalMetadata = true,
            bool ownedItem = false)
        {
            var item = new MetadataTestItem
            {
                IsLocked = itemLocked,
                OwnerId = ownedItem ? Guid.NewGuid() : Guid.Empty,
                EnableLocalMetadata = supportsLocalMetadata
            };

            var providerName = "provider";
            var provider = MockIMetadataProviderMapper<MetadataTestItem, MetadataTestItemInfo>(providerType, providerName, forced: providerForced);

            var baseItemManager = new Mock<IBaseItemManager>(MockBehavior.Strict);
            baseItemManager.Setup(i => i.IsMetadataFetcherEnabled(item, It.IsAny<TypeOptions>(), providerName))
                .Returns(baseItemEnabled);

            using var providerManager = GetProviderManager(baseItemManager: baseItemManager.Object);
            AddParts(providerManager, metadataProviders: new[] { provider });

            var actualProviders = providerManager.GetMetadataProviders<MetadataTestItem>(item, new LibraryOptions()).ToArray();

            Assert.Equal(expected ? 1 : 0, actualProviders.Length);
        }

    }
}
