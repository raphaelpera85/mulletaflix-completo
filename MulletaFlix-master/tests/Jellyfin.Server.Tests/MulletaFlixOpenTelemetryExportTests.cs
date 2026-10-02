using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Server.Implementations.Nebula;
using MulletaFlix.Server.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Jellyfin.Server.Tests;

/// <summary>
/// Valida o pipeline de exportação OTLP de ponta a ponta: o SDK OpenTelemetry real
/// é construído a partir de <see cref="MulletaFlixOpenTelemetryExtensions"/>, spans e métricas
/// são emitidos pelas fontes de produção e um receptor OTLP/HTTP em processo confirma
/// que os bytes chegaram codificados. Isto substitui a inspeção local por
/// <c>ActivityListener</c> como prova de que os spans realmente saem do processo,
/// sem depender de um collector externo instalado na máquina.
/// </summary>
public sealed class MulletaFlixOpenTelemetryExportTests
{
    private const string TracesPath = "/v1/traces";
    private const string MetricsPath = "/v1/metrics";

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsNebulaSpansOverOtlpHttpWithoutSensitivePayload()
    {
        using var receiver = new OtlpTraceReceiver(requireApiKey: true);
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        var previousProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        var previousHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
        var previousTracesHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS");
        var previousMetricsHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS");
        try
        {
            // Apenas o ambiente deste processo de teste é alterado; nenhum servidor
            // externo é iniciado nem reconfigurado.
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", receiver.Endpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", "X-API-Key=unit-test-secret");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", null);

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(
                services,
                Environment.GetEnvironmentVariable);

            Assert.True(signals.TracesEnabled);

            await using var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            // Emite pelas fontes de produção realmente registradas no pipeline.
            EmitSpanFrom(
                NebulaPlaybackSessionMonitor.ActivitySourceName,
                "nebula.playback.session_start",
                ActivityKind.Internal,
                activity =>
                {
                    activity.SetTag("nebula.playback.result", "tracked");
                    activity.SetTag("nebula.playback.active_sessions", 1);
                });

            EmitSpanFrom(
                NebulaMongoContext.ActivitySourceName,
                "mongodb.sync_staging_directory",
                ActivityKind.Client,
                activity =>
                {
                    activity.SetTag("mongodb.result", "success");
                    activity.SetTag("nebula.staging.root_count", 2);
                });

            EmitSpanFrom(
                NebulaTelegramPool.ActivitySourceName,
                "telegram.send_message",
                ActivityKind.Client,
                activity => activity.SetTag("telegram.result", "success"));

            Assert.True(tracerProvider.ForceFlush(10000), "O TracerProvider não conseguiu drenar os spans.");

            var payloads = await receiver.WaitForRequestsAsync(1, TimeSpan.FromSeconds(30));
            Assert.NotEmpty(payloads);

            // O exporter OTLP/HTTP precisa ter falado o protocolo correto no caminho correto.
            Assert.All(payloads, payload =>
            {
                Assert.Equal("POST", payload.Method);
                Assert.Equal(TracesPath, payload.Path);
                Assert.Equal("application/x-protobuf", payload.ContentType);
                Assert.NotEmpty(payload.Body);
                Assert.Equal("unit-test-secret", payload.ApiKey);
                Assert.Equal(HttpStatusCode.OK, payload.StatusCode);
            });

            var combined = string.Concat(payloads.Select(payload => Latin1(payload.Body)));

            // Os nomes de operação chegaram ao receptor, ou seja, saíram do processo.
            Assert.Contains("nebula.playback.session_start", combined, StringComparison.Ordinal);
            Assert.Contains("mongodb.sync_staging_directory", combined, StringComparison.Ordinal);
            Assert.Contains("telegram.send_message", combined, StringComparison.Ordinal);
            Assert.Contains("MulletaFlix.Server", combined, StringComparison.Ordinal);

            // E o payload exportado não carrega nada sensível.
            foreach (var forbidden in new[]
            {
                "Sensitive",
                "mongodb://",
                "chat_id",
                "bot_token",
                "connectionString",
                "playSessionId"
            })
            {
                Assert.DoesNotContain(forbidden, combined, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTracesEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetricsEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", previousProtocol);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", previousHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", previousTracesHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", previousMetricsHeaders);
        }
    }

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsBackgroundJobMetricsWithoutSensitiveData()
    {
        using var receiver = new OtlpTraceReceiver(requireApiKey: true);
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        var previousProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        var previousHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
        var previousTracesHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS");
        var previousMetricsHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS");
        var privatePath = Path.Combine(Path.GetTempPath(), $"backup-private-path-{Guid.NewGuid():N}.zip");
        var dramaBoxCachePath = Path.Combine(Path.GetTempPath(), "mulletaflix-dramabox-otlp-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", receiver.Endpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", "X-API-Key=unit-test-secret");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", null);

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(services, Environment.GetEnvironmentVariable);
            Assert.True(signals.MetricsEnabled);

            await using var provider = services.BuildServiceProvider();
            var meterProvider = provider.GetRequiredService<MeterProvider>();
            var backupService = new BackupService(
                NullLogger<BackupService>.Instance,
                null!,
                null!,
                null!,
                null!,
                null!);

            await Assert.ThrowsAsync<FileNotFoundException>(() => backupService.RestoreBackupAsync(privatePath));
            using var supabaseSync = new NebulaSupabaseSyncService(
                null!,
                NullLogger<NebulaSupabaseSyncService>.Instance);
            var syncResult = await supabaseSync.PerformBackupAsync(
                "https://private-supabase.invalid",
                "sb_publishable_private-test-key",
                progressAction: null,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(syncResult.Success);
            var itemRepository = new Mock<IItemRepository>();
            itemRepository.Setup(repository => repository.GetItemList(It.IsAny<InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            var strmProbe = new StrmProbeScheduledTask(
                itemRepository.Object,
                Mock.Of<IFileSystem>(),
                Mock.Of<MulletaFlix.Api.Jobs.IJobQueue>(),
                NullLogger<StrmProbeScheduledTask>.Instance);
            await strmProbe.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var libraryManager = new Mock<MediaBrowser.Controller.Library.ILibraryManager>();
            libraryManager.Setup(manager => manager.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            var unidentifiedCleanup = new UnidentifiedMediaCleanupTask(
                libraryManager.Object,
                Mock.Of<MediaBrowser.Controller.Providers.IProviderManager>(),
                Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>(),
                NullLogger<UnidentifiedMediaCleanupTask>.Instance,
                Mock.Of<IFileSystem>());
            await unidentifiedCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var segmentLibrary = new Mock<ILibraryManager>();
            segmentLibrary.Setup(manager => manager.GetCount(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(0);
            var segmentExtraction = new MediaSegmentExtractionTask(
                segmentLibrary.Object,
                Mock.Of<ILocalizationManager>(),
                Mock.Of<IMediaSegmentManager>());
            await segmentExtraction.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var syncConfiguration = new Mock<MediaBrowser.Common.Configuration.IConfigurationManager>();
            syncConfiguration.Setup(manager => manager.GetConfiguration("midiastorageonline"))
                .Returns(new MediaBrowser.Providers.Plugins.MidiaStorageOnline.Configuration.PluginConfiguration());
            var mediaSyncTask = new MediaBrowser.Providers.Plugins.MidiaStorageOnline.ScheduledTasks.MidiaStorageOnlineSyncTask(
                Mock.Of<System.Net.Http.IHttpClientFactory>(),
                syncConfiguration.Object,
                Mock.Of<MediaBrowser.Controller.IServerApplicationHost>(),
                Mock.Of<ILibraryMonitor>(),
                Mock.Of<MediaBrowser.Controller.LiveTv.ITunerHostManager>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<MulletaFlix.Database.Implementations.MulletaFlixDbContext>>(),
                NullLogger<MediaBrowser.Providers.Plugins.MidiaStorageOnline.ScheduledTasks.MidiaStorageOnlineSyncTask>.Instance);
            await mediaSyncTask.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var dramaLibrary = new Mock<ILibraryManager>();
            dramaLibrary.Setup(manager => manager.GetCount(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(0);
            dramaLibrary.Setup(manager => manager.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            using var dramaFindsClient = new MediaBrowser.Providers.Plugins.DramaFinds.DramaFindsClient(
                Mock.Of<System.Net.Http.IHttpClientFactory>(),
                NullLogger<MediaBrowser.Providers.Plugins.DramaFinds.DramaFindsClient>.Instance);
            var dramaFindsMatch = new DramaFindsMatchTask(
                dramaFindsClient,
                dramaLibrary.Object,
                Mock.Of<MediaBrowser.Controller.Providers.IProviderManager>(),
                Mock.Of<IFileSystem>(),
                NullLogger<DramaFindsMatchTask>.Instance);
            await dramaFindsMatch.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var goodShortLibrary = new Mock<ILibraryManager>();
            goodShortLibrary.Setup(manager => manager.GetCount(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(0);
            goodShortLibrary.Setup(manager => manager.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            using var goodShortClient = new MediaBrowser.Providers.Plugins.GoodShort.GoodShortClient(
                Mock.Of<System.Net.Http.IHttpClientFactory>(),
                NullLogger<MediaBrowser.Providers.Plugins.GoodShort.GoodShortClient>.Instance);
            var goodShortMatch = new Emby.Server.Implementations.ScheduledTasks.Tasks.GoodShortMatchTask(
                goodShortClient,
                goodShortLibrary.Object,
                Mock.Of<MediaBrowser.Controller.Providers.IProviderManager>(),
                Mock.Of<IFileSystem>(),
                NullLogger<Emby.Server.Implementations.ScheduledTasks.Tasks.GoodShortMatchTask>.Instance);
            await goodShortMatch.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var shortMaxLibrary = new Mock<ILibraryManager>();
            shortMaxLibrary.Setup(manager => manager.GetCount(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(0);
            shortMaxLibrary.Setup(manager => manager.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            using var shortMaxClient = new MediaBrowser.Providers.Plugins.ShortMax.ShortMaxClient(
                Mock.Of<System.Net.Http.IHttpClientFactory>(),
                NullLogger<MediaBrowser.Providers.Plugins.ShortMax.ShortMaxClient>.Instance);
            var shortMaxMatch = new Emby.Server.Implementations.ScheduledTasks.Tasks.ShortMaxMatchTask(
                shortMaxClient,
                shortMaxLibrary.Object,
                Mock.Of<MediaBrowser.Controller.Providers.IProviderManager>(),
                Mock.Of<IFileSystem>(),
                NullLogger<Emby.Server.Implementations.ScheduledTasks.Tasks.ShortMaxMatchTask>.Instance);
            await shortMaxMatch.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            const string dramaBoxPage = "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"pages\":1,\"bookList\":[]}}}</script>";
            using var dramaBoxHttpClient = new HttpClient(new FixedPageHttpMessageHandler(dramaBoxPage));
            var dramaBoxHttpClientFactory = new Mock<IHttpClientFactory>();
            dramaBoxHttpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(dramaBoxHttpClient);
            using var dramaBoxClient = new MediaBrowser.Providers.Plugins.DramaBox.DramaBoxClient(
                dramaBoxHttpClientFactory.Object,
                Mock.Of<MediaBrowser.Controller.IServerApplicationPaths>(paths => paths.CachePath == dramaBoxCachePath),
                NullLogger<MediaBrowser.Providers.Plugins.DramaBox.DramaBoxClient>.Instance);
            var dramaBoxLibrary = new Mock<ILibraryManager>();
            dramaBoxLibrary.Setup(manager => manager.GetCount(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(0);
            dramaBoxLibrary.Setup(manager => manager.GetItemList(It.IsAny<MediaBrowser.Controller.Entities.InternalItemsQuery>()))
                .Returns(Array.Empty<BaseItem>());
            var dramaBoxMatch = new Emby.Server.Implementations.ScheduledTasks.Tasks.DramaBoxMatchTask(
                dramaBoxClient,
                dramaBoxLibrary.Object,
                Mock.Of<MediaBrowser.Controller.Providers.IProviderManager>(),
                Mock.Of<IFileSystem>(),
                NullLogger<Emby.Server.Implementations.ScheduledTasks.Tasks.DramaBoxMatchTask>.Instance);
            await dramaBoxMatch.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var deviceManager = new Mock<MediaBrowser.Controller.Devices.IDeviceManager>();
            deviceManager.Setup(manager => manager.GetDevicesForUser(null))
                .Returns(new MediaBrowser.Model.Querying.QueryResult<MediaBrowser.Model.Dto.DeviceInfoDto>(Array.Empty<MediaBrowser.Model.Dto.DeviceInfoDto>()));
            var sessionManager = new Mock<MediaBrowser.Controller.Session.ISessionManager>();
            sessionManager.SetupGet(manager => manager.Sessions).Returns(Array.Empty<MediaBrowser.Controller.Session.SessionInfo>());
            var deviceCleanup = new Emby.Server.Implementations.ScheduledTasks.DeviceCleanupTask(
                deviceManager.Object,
                sessionManager.Object,
                NullLogger<Emby.Server.Implementations.ScheduledTasks.DeviceCleanupTask>.Instance);
            await deviceCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            const string transcodePath = "test-cache/transcodes";
            var transcodePaths = new Mock<MediaBrowser.Common.Configuration.IApplicationPaths>();
            transcodePaths.SetupGet(paths => paths.CachePath).Returns("test-cache");
            transcodePaths.Setup(paths => paths.CreateAndCheckMarker(transcodePath, "transcode", true));
            var transcodeConfig = new Mock<MediaBrowser.Common.Configuration.IConfigurationManager>();
            transcodeConfig.Setup(config => config.GetConfiguration("encoding"))
                .Returns(new MediaBrowser.Model.Configuration.EncodingOptions { TranscodingTempPath = transcodePath });
            transcodeConfig.SetupGet(config => config.CommonApplicationPaths).Returns(transcodePaths.Object);
            var transcodeFileSystem = new Mock<IFileSystem>();
            transcodeFileSystem.Setup(fileSystem => fileSystem.GetFiles(transcodePath, true))
                .Returns(Array.Empty<MediaBrowser.Model.IO.FileSystemMetadata>());
            transcodeFileSystem.Setup(fileSystem => fileSystem.GetDirectoryPaths(transcodePath))
                .Returns(Array.Empty<string>());
            var transcodeCleanup = new Emby.Server.Implementations.ScheduledTasks.Tasks.DeleteTranscodeFileTask(
                NullLogger<Emby.Server.Implementations.ScheduledTasks.Tasks.DeleteTranscodeFileTask>.Instance,
                transcodeFileSystem.Object,
                transcodeConfig.Object,
                Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>());
            await transcodeCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            const string cachePath = "test-cache";
            const string tempPath = "test-temp";
            var cachePaths = new Mock<MediaBrowser.Common.Configuration.IApplicationPaths>();
            cachePaths.SetupGet(paths => paths.CachePath).Returns(cachePath);
            cachePaths.SetupGet(paths => paths.TempDirectory).Returns(tempPath);
            var cacheFileSystem = new Mock<IFileSystem>();
            cacheFileSystem.Setup(fileSystem => fileSystem.GetFiles(cachePath, true))
                .Returns(Array.Empty<MediaBrowser.Model.IO.FileSystemMetadata>());
            cacheFileSystem.Setup(fileSystem => fileSystem.GetFiles(tempPath, true))
                .Returns(Array.Empty<MediaBrowser.Model.IO.FileSystemMetadata>());
            cacheFileSystem.Setup(fileSystem => fileSystem.GetDirectoryPaths(cachePath))
                .Returns(Array.Empty<string>());
            cacheFileSystem.Setup(fileSystem => fileSystem.GetDirectoryPaths(tempPath))
                .Returns(Array.Empty<string>());
            var cacheCleanup = new Emby.Server.Implementations.ScheduledTasks.Tasks.DeleteCacheFileTask(
                cachePaths.Object,
                NullLogger<Emby.Server.Implementations.ScheduledTasks.Tasks.DeleteCacheFileTask>.Instance,
                cacheFileSystem.Object,
                Mock.Of<MediaBrowser.Model.Globalization.ILocalizationManager>());
            await cacheCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var activityLogOptions = new DbContextOptionsBuilder<MulletaFlix.Database.Implementations.Contexts.UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var activityLogDbContext = new MulletaFlix.Database.Implementations.Contexts.UsersDbContext(
                activityLogOptions,
                NullLogger<MulletaFlix.Database.Implementations.Contexts.UsersDbContext>.Instance);
            var activityLogDbContextFactory = new Mock<IDbContextFactory<MulletaFlix.Database.Implementations.Contexts.UsersDbContext>>();
            activityLogDbContextFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(activityLogDbContext);
            var activityLogCleanup = new Emby.Server.Implementations.ScheduledTasks.ActivityLogCleanupTask(
                activityLogDbContextFactory.Object,
                NullLogger<Emby.Server.Implementations.ScheduledTasks.ActivityLogCleanupTask>.Instance);
            await activityLogCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            var userDataOptions = new DbContextOptionsBuilder<MulletaFlix.Database.Implementations.MulletaFlixDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var userDataProvider = new Mock<MulletaFlix.Database.Implementations.IMulletaFlixDatabaseProvider>();
            userDataProvider.Setup(databaseProvider => databaseProvider.OnModelCreating(It.IsAny<ModelBuilder>()));
            var userDataLocking = new Mock<MulletaFlix.Database.Implementations.Locking.IEntityFrameworkCoreLockingBehavior>();
            var userDataDbContext = new MulletaFlix.Database.Implementations.MulletaFlixDbContext(
                userDataOptions,
                NullLogger<MulletaFlix.Database.Implementations.MulletaFlixDbContext>.Instance,
                userDataProvider.Object,
                userDataLocking.Object);
            userDataDbContext.Database.EnsureCreated();
            var userDataDbFactory = new Mock<IDbContextFactory<MulletaFlix.Database.Implementations.MulletaFlixDbContext>>();
            userDataDbFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(userDataDbContext);
            var userDataCleanup = new CleanupUserDataTask(
                Mock.Of<ILocalizationManager>(),
                userDataDbFactory.Object,
                NullLogger<CleanupUserDataTask>.Instance);
            await userDataCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            const string logDirectory = "test-log-directory";
            var logConfiguration = new Mock<MediaBrowser.Common.Configuration.IConfigurationManager>();
            logConfiguration.SetupGet(configuration => configuration.CommonConfiguration)
                .Returns(new MediaBrowser.Model.Configuration.BaseApplicationConfiguration { LogFileRetentionDays = 3 });
            var logApplicationPaths = new Mock<MediaBrowser.Common.Configuration.IApplicationPaths>();
            logApplicationPaths.SetupGet(paths => paths.LogDirectoryPath).Returns(logDirectory);
            logConfiguration.SetupGet(configuration => configuration.CommonApplicationPaths).Returns(logApplicationPaths.Object);
            var logFileSystem = new Mock<IFileSystem>();
            logFileSystem.Setup(fileSystem => fileSystem.GetFiles(logDirectory, true))
                .Returns(Array.Empty<MediaBrowser.Model.IO.FileSystemMetadata>());
            var logCleanup = new DeleteLogFileTask(
                logConfiguration.Object,
                logFileSystem.Object,
                Mock.Of<ILocalizationManager>());
            await logCleanup.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);
            using var jobQueue = new MulletaFlix.Api.Jobs.MulletaFlixJobQueue(
                Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<MulletaFlix.Database.Implementations.Contexts.SystemDbContext>>(),
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MulletaFlix.Api.Jobs.MulletaFlixJobQueue>.Instance);
            var queuedJob = jobQueue.Enqueue("PrivateKind", "Sensitive title", (_, _) => Task.CompletedTask);
            Assert.True(jobQueue.Cancel(queuedJob.Id));
            await using var nebulaManager = new NebulaFtpManager(
                null!,
                NullLogger<NebulaFtpManager>.Instance,
                NullLoggerFactory.Instance);
            var cleanupMethod = typeof(NebulaFtpManager).GetMethod(
                "RunCleanupCycleAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var cleanupTask = (Task)cleanupMethod.Invoke(
                nebulaManager,
                [new List<string>(), CancellationToken.None])!;
            await cleanupTask;
            Assert.True(meterProvider.ForceFlush(10000), "O MeterProvider não conseguiu drenar as métricas.");

            var payloads = await receiver.WaitForRequestsAsync(1, TimeSpan.FromSeconds(30));
            var metricsPayload = Assert.Single(payloads);
            Assert.Equal("POST", metricsPayload.Method);
            Assert.Equal(MetricsPath, metricsPayload.Path);
            Assert.Equal("application/x-protobuf", metricsPayload.ContentType);
            Assert.Equal("unit-test-secret", metricsPayload.ApiKey);
            Assert.Equal(HttpStatusCode.OK, metricsPayload.StatusCode);

            var encodedPayload = Latin1(metricsPayload.Body);
            Assert.Contains("mulletaflix.backup.operations", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.backup.operation.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.supabase.operations", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.supabase.operation.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.nebula.cleanup.cycles", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.nebula.cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.strm_probe.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.strm_probe.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.strm_probe.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.unidentified_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.unidentified_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.unidentified_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.segment_extraction.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.segment_extraction.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.segment_extraction.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.media_sync.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.media_sync.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.media_sync.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramafinds_match.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramafinds_match.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramafinds_match.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.goodshort_match.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.goodshort_match.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.goodshort_match.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.shortmax_match.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.shortmax_match.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.shortmax_match.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.index.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.library_match.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.catalog.books", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.dramabox_match.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.device_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.device_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.device_cleanup.devices.scanned", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.device_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.transcode_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.transcode_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.transcode_cleanup.files.scanned", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.transcode_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.cache_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.cache_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.cache_cleanup.files.scanned", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.cache_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.activity_log_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.activity_log_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.activity_log_cleanup.entries.expired_candidates", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.activity_log_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.log_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.log_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.log_cleanup.files.scanned", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.log_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.entries.detached_per_run", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.entries.expired_candidates_per_run", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.entries.deleted_per_run", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.user_data_cleanup.active_runs", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.jobs.enqueued", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.jobs.cancelled", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("no_items", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("MulletaFlix.Server", encodedPayload, StringComparison.Ordinal);
            Assert.DoesNotContain(privatePath, encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private-supabase.invalid", encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sb_publishable_private-test-key", encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sensitive title", encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PrivateKind", encodedPayload, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dramaBoxCachePath))
            {
                Directory.Delete(dramaBoxCachePath, recursive: true);
            }

            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTracesEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetricsEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", previousProtocol);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", previousHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", previousTracesHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", previousMetricsHeaders);
        }
    }

    [Fact]
    public async Task ConfigureOpenTelemetry_WithoutEndpointDoesNotExportAnything()
    {
        using var receiver = new OtlpTraceReceiver();
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTraces = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetrics = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(
                services,
                Environment.GetEnvironmentVariable);

            Assert.False(signals.TracesEnabled);
            Assert.False(signals.MetricsEnabled);

            await using var provider = services.BuildServiceProvider();
            Assert.Null(provider.GetService<TracerProvider>());

            // Sem pipeline não há listener registrado, então o próprio
            // StartActivity devolve null: o custo de telemetria é zero.
            using var source = new ActivitySource(NebulaPlaybackSessionMonitor.ActivitySourceName);
            using var activity = source.StartActivity("nebula.playback.session_start", ActivityKind.Internal);
            Assert.Null(activity);

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Empty(receiver.Received);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTraces);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetrics);
        }
    }

    private static void EmitSpanFrom(
        string sourceName,
        string operationName,
        ActivityKind kind,
        Action<Activity> configure)
    {
        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity(operationName, kind);
        Assert.NotNull(activity);
        configure(activity);
    }

    private static string Latin1(byte[] body)
    {
        // Latin-1 nunca falha e preserva os bytes 1:1, permitindo procurar as
        // strings UTF-8 embutidas no protobuf sem precisar decodificá-lo.
        return Encoding.Latin1.GetString(body);
    }

    private sealed class FixedPageHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _page;

        public FixedPageHttpMessageHandler(string page)
        {
            _page = page;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_page)
            });
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string? ContentType, string? ApiKey, HttpStatusCode StatusCode, byte[] Body);

    private sealed class OtlpTraceReceiver : IDisposable
    {
        private const string RequiredApiKey = "unit-test-secret";
        private readonly HttpListener _listener = new();
        private readonly List<CapturedRequest> _received = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private readonly object _gate = new();

        public OtlpTraceReceiver(bool requireApiKey = false)
        {
            Port = GetFreePort();
            RequireApiKey = requireApiKey;
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public int Port { get; }

        public string Endpoint => $"http://127.0.0.1:{Port}";

        private bool RequireApiKey { get; }

        public IReadOnlyList<CapturedRequest> Received
        {
            get
            {
                lock (_gate)
                {
                    return _received.ToList();
                }
            }
        }

        public void Start()
        {
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        public async Task<IReadOnlyList<CapturedRequest>> WaitForRequestsAsync(int count, TimeSpan timeout)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            for (var i = 0; i < count; i++)
            {
                await _signal.WaitAsync(timeoutCts.Token);
            }

            return Received;
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    using var buffer = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(buffer);
                    var captured = new CapturedRequest(
                        context.Request.HttpMethod,
                        context.Request.Url?.AbsolutePath ?? string.Empty,
                        context.Request.ContentType,
                        context.Request.Headers["X-API-Key"],
                        RequireApiKey && !string.Equals(context.Request.Headers["X-API-Key"], RequiredApiKey, StringComparison.Ordinal)
                            ? HttpStatusCode.Unauthorized
                            : HttpStatusCode.OK,
                        buffer.ToArray());

                    lock (_gate)
                    {
                        _received.Add(captured);
                    }

                    // Coletores protegidos rejeitam chave ausente/incorreta.
                    // Em sucesso, resposta OTLP/HTTP vazia é válida.
                    context.Response.StatusCode = (int)captured.StatusCode;
                    context.Response.ContentType = "application/x-protobuf";
                    context.Response.ContentLength64 = 0;
                    context.Response.Close();

                    _signal.Release();
                }
                catch (Exception)
                {
                    try
                    {
                        context.Response.Abort();
                    }
                    catch (Exception)
                    {
                        // O exporter tratará a falha de transporte; o teste não depende disto.
                    }
                }
            }
        }

        private static int GetFreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            ((IDisposable)_listener).Dispose();
            _cts.Dispose();
            _signal.Dispose();
        }
    }
}
