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

        public static TheoryData<int, int[]?, int[]?, int?[]?, int[]> GetImageProvidersOrderData()
            => new()
            {
                { 3, null, null, null, new[] { 0, 1, 2 } }, // no order options set

                // library options ordering
                { 3, Array.Empty<int>(), null, null, new[] { 0, 1, 2 } }, // no order provided
                { 3, new[] { 1 }, null, null, new[] { 1, 0, 2 } }, // one item in order
                { 3, new[] { 2, 1, 0 }, null, null, new[] { 2, 1, 0 } }, // full reverse order

                // server options ordering
                { 3, null, Array.Empty<int>(), null, new[] { 0, 1, 2 } }, // no order provided
                { 3, null, new[] { 1 }, null, new[] { 1, 0, 2 } }, // one item in order
                { 3, null, new[] { 2, 1, 0 }, null, new[] { 2, 1, 0 } }, // full reverse order

                // IHasOrder ordering
                { 3, null, null, new int?[] { null, 1, null }, new[] { 1, 0, 2 } }, // one item with defined order
                { 3, null, null, new int?[] { 2, 1, 0 }, new[] { 2, 1, 0 } }, // full reverse order

                // multiple orders set
                { 3, new[] { 1 }, new[] { 2, 0, 1 }, null, new[] { 1, 0, 2 } }, // partial library order first, server order ignored
                { 3, new[] { 1 }, null, new int?[] { 2, 0, 1 }, new[] { 1, 2, 0 } }, // library order first, then orderby
                { 3, new[] { 2, 1, 0 }, new[] { 1, 2, 0 }, new int?[] { 2, 0, 1 }, new[] { 2, 1, 0 } }, // library order wins
            };

        [Theory]
        [MemberData(nameof(GetImageProvidersOrderData))]
        public void GetImageProviders_ProviderOrder_MatchesExpected(int providerCount, int[]? libraryOrder, int[]? serverOrder, int?[]? hasOrderOrder, int[] expectedOrder)
        {
            var item = new Movie();

            var nameProvider = new Func<int, string>(i => "Provider" + i);

            var providerList = new List<IImageProvider>();
            for (var i = 0; i < providerCount; i++)
            {
                var order = hasOrderOrder?[i];
                providerList.Add(MockIImageProvider<ILocalImageProvider>(nameProvider(i), item, order: order));
            }

            var libraryOptions = CreateLibraryOptions(item.GetType().Name, imageFetcherOrder: libraryOrder?.Select(nameProvider).ToArray());
            var serverConfiguration = CreateServerConfiguration(item.GetType().Name, imageFetcherOrder: serverOrder?.Select(nameProvider).ToArray());

            using var providerManager = GetProviderManager(serverConfiguration: serverConfiguration, libraryOptions: libraryOptions);
            AddParts(providerManager, imageProviders: providerList);

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>(MockBehavior.Strict));
            var actualProviders = providerManager.GetImageProviders(item, refreshOptions).ToList();

            Assert.Equal(providerList.Count, actualProviders.Count);
            var actualOrder = actualProviders.Select(i => providerList.IndexOf(i)).ToArray();
            Assert.Equal(expectedOrder, actualOrder);
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, false, false)]
        [InlineData(true, true, false)]
        public void GetImageProviders_CanRefreshImagesBasic_WhenSupportsWithoutError(bool supports, bool errorOnSupported, bool expected)
        {
            GetImageProviders_CanRefreshImages_Tester(nameof(IImageProvider), supports, expected, errorOnSupported: errorOnSupported);
        }

        [Theory]
        [InlineData(nameof(ILocalImageProvider), false, true)]
        [InlineData(nameof(ILocalImageProvider), true, true)]
        [InlineData(nameof(IImageProvider), false, false)]
        [InlineData(nameof(IImageProvider), true, true)]
        public void GetImageProviders_CanRefreshImagesLocked_WhenLocalOrFullRefresh(string providerType, bool fullRefresh, bool expected)
        {
            GetImageProviders_CanRefreshImages_Tester(providerType, true, expected, itemLocked: true, fullRefresh: fullRefresh);
        }

        [Theory]
        [InlineData(nameof(ILocalImageProvider), false, true)]
        [InlineData(nameof(IRemoteImageProvider), true, true)]
        [InlineData(nameof(IDynamicImageProvider), true, true)]
        [InlineData(nameof(IRemoteImageProvider), false, false)]
        [InlineData(nameof(IDynamicImageProvider), false, false)]
        public void GetImageProviders_CanRefreshImagesBaseItemEnabled_WhenLocalOrEnabled(string providerType, bool enabled, bool expected)
        {
            GetImageProviders_CanRefreshImages_Tester(providerType, true, expected, baseItemEnabled: enabled);
        }

        private static void GetImageProviders_CanRefreshImages_Tester(
            string providerType,
            bool supports,
            bool expected,
            bool errorOnSupported = false,
            bool itemLocked = false,
            bool fullRefresh = false,
            bool baseItemEnabled = true)
        {
            var item = new Movie
            {
                IsLocked = itemLocked
            };

            var providerName = "provider";
            IImageProvider provider = providerType switch
            {
                "IImageProvider" => MockIImageProvider<IImageProvider>(providerName, item, supports: supports, errorOnSupported: errorOnSupported),
                "ILocalImageProvider" => MockIImageProvider<ILocalImageProvider>(providerName, item, supports: supports, errorOnSupported: errorOnSupported),
                "IRemoteImageProvider" => MockIImageProvider<IRemoteImageProvider>(providerName, item, supports: supports, errorOnSupported: errorOnSupported),
                "IDynamicImageProvider" => MockIImageProvider<IDynamicImageProvider>(providerName, item, supports: supports, errorOnSupported: errorOnSupported),
                _ => throw new ArgumentException("Unexpected provider type")
            };

            var refreshOptions = new ImageRefreshOptions(Mock.Of<IDirectoryService>(MockBehavior.Strict))
            {
                ImageRefreshMode = fullRefresh ? MetadataRefreshMode.FullRefresh : MetadataRefreshMode.Default
            };

            var baseItemManager = new Mock<IBaseItemManager>(MockBehavior.Strict);
            baseItemManager.Setup(i => i.IsImageFetcherEnabled(item, It.IsAny<TypeOptions>(), providerName))
                .Returns(baseItemEnabled);

            using var providerManager = GetProviderManager(baseItemManager: baseItemManager.Object);
            AddParts(providerManager, imageProviders: new[] { provider });

            var actualProviders = providerManager.GetImageProviders(item, refreshOptions).ToArray();

            Assert.Equal(expected ? 1 : 0, actualProviders.Length);
        }

        public static TheoryData<string[], int[]?, int[]?, int[]?, int[]?, int?[]?, int[]> GetMetadataProvidersOrderData()
        {
            var l = nameof(ILocalMetadataProvider);
            var r = nameof(IRemoteMetadataProvider);
            return new()
            {
                { new[] { l, l, r, r }, null, null, null, null, null, new[] { 0, 1, 2, 3 } }, // no order options set

                // library options ordering
                { new[] { l, l, r, r }, Array.Empty<int>(), Array.Empty<int>(), null, null, null, new[] { 0, 1, 2, 3 } }, // no order provided
                // local only
                { new[] { r, l, l, l }, new[] { 2 }, null, null, null, null, new[] { 2, 0, 1, 3 } }, // one item in order
                { new[] { r, l, l, l }, new[] { 3, 2, 1 }, null, null, null, null, new[] { 3, 2, 1, 0 } }, // full reverse order
                // remote only
                { new[] { l, r, r, r }, null, new[] { 2 }, null, null, null, new[] { 2, 0, 1, 3 } }, // one item in order
                { new[] { l, r, r, r }, null, new[] { 3, 2, 1 }, null, null, null, new[] { 3, 2, 1, 0 } }, // full reverse order
                // local and remote, note that results will be interleaved (odd but expected)
                { new[] { l, l, r, r }, new[] { 1 }, new[] { 3 }, null, null, null, new[] { 1, 3, 0, 2 } }, // one item in each order
                { new[] { l, l, l, r, r, r }, new[] { 2, 1, 0 }, new[] { 5, 4, 3 }, null, null, null, new[] { 2, 5, 1, 4, 0, 3 } }, // full reverse order

                // // server options ordering
                { new[] { l, l, r, r }, null, null, Array.Empty<int>(), Array.Empty<int>(), null, new[] { 0, 1, 2, 3 } }, // no order provided
                // local only
                { new[] { r, l, l, l }, null, null, new[] { 2 }, null, null, new[] { 2, 0, 1, 3 } }, // one item in order
                { new[] { r, l, l, l }, null, null, new[] { 3, 2, 1 }, null, null, new[] { 3, 2, 1, 0 } }, // full reverse order
                // remote only
                { new[] { l, r, r, r }, null, null, null, new[] { 2 }, null, new[] { 2, 0, 1, 3 } }, // one item in order
                { new[] { l, r, r, r }, null, null, null, new[] { 3, 2, 1 }, null, new[] { 3, 2, 1, 0 } }, // full reverse order
                // local and remote, note that results will be interleaved (odd but expected)
                { new[] { l, l, r, r }, null, null, new[] { 1 }, new[] { 3 }, null, new[] { 1, 3, 0, 2 } }, // one item in each order
                { new[] { l, l, l, r, r, r }, null, null, new[] { 2, 1, 0 }, new[] { 5, 4, 3 }, null, new[] { 2, 5, 1, 4, 0, 3 } }, // full reverse order

                // IHasOrder ordering (not interleaved, doesn't care about types)
                { new[] { l, l, r, r }, null, null, null, null, new int?[] { 2, null, 1, null }, new[] { 2, 0, 1, 3 } }, // partially defined
                { new[] { l, l, r, r }, null, null, null, null, new int?[] { 3, 2, 1, 0 }, new[] { 3, 2, 1, 0 } }, // full reverse order

                // multiple orders set
                { new[] { l, l, l, r, r, r }, new[] { 1 }, new[] { 4 }, new[] { 2, 1, 0 }, new[] { 5, 4, 3 }, null, new[] { 1, 4, 0, 2, 3, 5 } }, // partial library order first, server order ignored
                { new[] { l, l, l }, new[] { 1 }, null, null, null, new int?[] { 2, 0, 1 }, new[] { 1, 2, 0 } }, // library order first, then orderby
                { new[] { l, l, l, r, r, r }, new[] { 2, 1, 0 }, new[] { 5, 4, 3 }, new[] { 1, 2, 0 }, new[] { 4, 5, 3 }, new int?[] { 5, 4, 1, 6, 3, 2 }, new[] { 2, 5, 4, 1, 0, 3 } }, // library order wins (with orderby between local/remote)
            };
        }

    }
}
