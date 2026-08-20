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
        private static readonly ILogger<ProviderManager> _logger = new NullLogger<ProviderManager>();


        private static Mock<IMetadataService> MockIMetadataService(bool refreshPrimary, bool canRefresh, int order = 0)
        {
            var service = new Mock<IMetadataService>(MockBehavior.Strict);
            service.Setup(s => s.Order)
                .Returns(order);
            service.Setup(s => s.CanRefreshPrimary(It.IsAny<Type>()))
                .Returns(refreshPrimary);
            service.Setup(s => s.CanRefresh(It.IsAny<BaseItem>()))
                .Returns(canRefresh);
            service.Setup(s => s.RefreshMetadata(It.IsAny<BaseItem>(), It.IsAny<MetadataRefreshOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(ItemUpdateType.MetadataDownload));
            return service;
        }

        private static IImageProvider MockIImageProvider<TProviderType>(string name, BaseItem expectedType, bool supports = true, int? order = null, bool errorOnSupported = false)
            where TProviderType : class, IImageProvider
        {
            Mock<IHasOrder>? hasOrder = null;
            if (order is not null)
            {
                hasOrder = new Mock<IHasOrder>(MockBehavior.Strict);
                hasOrder.Setup(i => i.Order)
                    .Returns((int)order);
            }

            var provider = hasOrder is null
                ? new Mock<TProviderType>(MockBehavior.Strict)
                : hasOrder.As<TProviderType>();
            provider.Setup(p => p.Name)
                .Returns(name);
            if (errorOnSupported)
            {
                provider.Setup(p => p.Supports(It.IsAny<BaseItem>()))
                    .Throws(new ArgumentException("Provider threw exception on Supports(item)"));
            }
            else
            {
                provider.Setup(p => p.Supports(expectedType))
                    .Returns(supports);
            }

            return provider.Object;
        }

        private static IMetadataProvider<TItemType> MockIMetadataProviderMapper<TItemType, TLookupInfoType>(string typeName, string providerName, int? order = null, bool forced = false)
            where TItemType : BaseItem, IHasLookupInfo<TLookupInfoType>
            where TLookupInfoType : ItemLookupInfo, new()
            => typeName switch
            {
                "ILocalMetadataProvider" => MockIMetadataProvider<ILocalMetadataProvider<TItemType>, TItemType>(providerName, order, forced),
                "IRemoteMetadataProvider" => MockIMetadataProvider<IRemoteMetadataProvider<TItemType, TLookupInfoType>, TItemType>(providerName, order, forced),
                "ICustomMetadataProvider" => MockIMetadataProvider<ICustomMetadataProvider<TItemType>, TItemType>(providerName, order, forced),
                _ => MockIMetadataProvider<IMetadataProvider<TItemType>, TItemType>(providerName, order, forced)
            };

        private static IMetadataProvider<TItemType> MockIMetadataProvider<TProviderType, TItemType>(string name, int? order = null, bool forced = false)
            where TProviderType : class, IMetadataProvider<TItemType>
            where TItemType : BaseItem
        {
            Mock<IForcedProvider>? forcedProvider = null;
            if (forced)
            {
                forcedProvider = new Mock<IForcedProvider>();
            }

            Mock<IHasOrder>? hasOrder = null;
            if (order is not null)
            {
                hasOrder = forcedProvider is null ? new Mock<IHasOrder>() : forcedProvider.As<IHasOrder>();
                hasOrder.Setup(i => i.Order)
                    .Returns((int)order);
            }

            var provider = hasOrder is null
                ? new Mock<TProviderType>(MockBehavior.Strict)
                : hasOrder.As<TProviderType>();
            provider.Setup(p => p.Name)
                .Returns(name);

            return provider.Object;
        }

        private static LibraryOptions CreateLibraryOptions(
            string typeName,
            string[]? imageFetcherOrder = null,
            string[]? localMetadataReaderOrder = null,
            string[]? metadataFetcherOrder = null)
        {
            var libraryOptions = new LibraryOptions
            {
                LocalMetadataReaderOrder = localMetadataReaderOrder
            };

            // only create type options if populating it with something
            if (imageFetcherOrder is not null || metadataFetcherOrder is not null)
            {
                imageFetcherOrder ??= Array.Empty<string>();
                metadataFetcherOrder ??= Array.Empty<string>();

                libraryOptions.TypeOptions = new[]
                {
                    new TypeOptions
                    {
                        Type = typeName,
                        ImageFetcherOrder = imageFetcherOrder,
                        MetadataFetcherOrder = metadataFetcherOrder
                    }
                };
            }

            return libraryOptions;
        }

        private static ServerConfiguration CreateServerConfiguration(
            string typeName,
            string[]? imageFetcherOrder = null,
            string[]? localMetadataReaderOrder = null,
            string[]? metadataFetcherOrder = null)
        {
            var serverConfiguration = new ServerConfiguration();

            // only create type options if populating it with something
            if (imageFetcherOrder is not null || localMetadataReaderOrder is not null || metadataFetcherOrder is not null)
            {
                imageFetcherOrder ??= Array.Empty<string>();
                localMetadataReaderOrder ??= Array.Empty<string>();
                metadataFetcherOrder ??= Array.Empty<string>();

                serverConfiguration.MetadataOptions = new[]
                {
                    new MetadataOptions
                    {
                        ItemType = typeName,
                        ImageFetcherOrder = imageFetcherOrder,
                        LocalMetadataReaderOrder = localMetadataReaderOrder,
                        MetadataFetcherOrder = metadataFetcherOrder
                    }
                };
            }

            return serverConfiguration;
        }

        private static ProviderManager GetProviderManager(
            ServerConfiguration? serverConfiguration = null,
            LibraryOptions? libraryOptions = null,
            IBaseItemManager? baseItemManager = null)
        {
            var serverConfigurationManager = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
            serverConfigurationManager.Setup(i => i.Configuration)
                .Returns(serverConfiguration ?? new ServerConfiguration());

            var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
            libraryManager.Setup(i => i.GetLibraryOptions(It.IsAny<BaseItem>()))
                .Returns(libraryOptions ?? new LibraryOptions());

            var providerManager = new ProviderManager(
                Mock.Of<IHttpClientFactory>(),
                Mock.Of<ISubtitleManager>(),
                serverConfigurationManager.Object,
                Mock.Of<ILibraryMonitor>(),
                _logger,
                Mock.Of<IFileSystem>(),
                Mock.Of<IServerApplicationPaths>(),
                libraryManager.Object,
                baseItemManager!,
                Mock.Of<ILyricManager>(),
                Mock.Of<IMemoryCache>(),
                Mock.Of<IMediaSegmentManager>(),
                Mock.Of<ISimilarItemsManager>());

            return providerManager;
        }

        private static void AddParts(
            ProviderManager providerManager,
            IEnumerable<IImageProvider>? imageProviders = null,
            IEnumerable<IMetadataService>? metadataServices = null,
            IEnumerable<IMetadataProvider>? metadataProviders = null,
            IEnumerable<IMetadataSaver>? metadataSavers = null,
            IEnumerable<IExternalId>? externalIds = null,
            IEnumerable<IExternalUrlProvider>? externalUrlProviders = null)
        {
            imageProviders ??= Array.Empty<IImageProvider>();
            metadataServices ??= Array.Empty<IMetadataService>();
            metadataProviders ??= Array.Empty<IMetadataProvider>();
            metadataSavers ??= Array.Empty<IMetadataSaver>();
            externalIds ??= Array.Empty<IExternalId>();
            externalUrlProviders ??= Array.Empty<IExternalUrlProvider>();

            providerManager.AddParts(imageProviders, metadataServices, metadataProviders, metadataSavers, externalIds, externalUrlProviders);
        }

        /// <summary>
        /// Simple <see cref="BaseItem"/> extension to make SupportsLocalMetadata directly settable.
        /// </summary>
        internal class MetadataTestItem : BaseItem, IHasLookupInfo<MetadataTestItemInfo>
        {
            public bool EnableLocalMetadata { get; set; } = true;

            public override bool SupportsLocalMetadata => EnableLocalMetadata;

            public MetadataTestItemInfo GetLookupInfo()
            {
                return GetItemLookupInfo<MetadataTestItemInfo>();
            }
        }

        internal class MetadataTestItemInfo : ItemLookupInfo
        {
        }
    }
}

