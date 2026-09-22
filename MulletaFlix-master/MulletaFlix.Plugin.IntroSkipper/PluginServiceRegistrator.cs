// SPDX-FileCopyrightText: 2024-2026 rlauuzo
// SPDX-FileCopyrightText: 2024-2026 AbandonedCart
// SPDX-FileCopyrightText: 2024-2026 Kilian von Pflugk
// SPDX-License-Identifier: GPL-3.0-only

using IntroSkipper.Db;
using IntroSkipper.FFmpeg;
using IntroSkipper.Filters;
using IntroSkipper.Manager;
using IntroSkipper.Providers;
using IntroSkipper.ScheduledTasks;
using IntroSkipper.SegmentChanges;
using IntroSkipper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.DbConfiguration;

namespace IntroSkipper
{
    /// <summary>
    /// Register Intro Skipper services.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Database layer. IntroSkipper shares the server's MariaDB provider. It must
            // not create a private file or a second local database lifecycle.
            serviceCollection.AddDbContextFactory<IntroSkipperDbContext>((serviceProvider, options) =>
            {
                var provider = serviceProvider.GetRequiredService<IMulletaFlixDatabaseProvider>();
                var databaseConfiguration = serviceProvider.GetRequiredService<IServerConfigurationManager>()
                    .GetConfiguration<DatabaseConfigurationOptions>("database");
                provider.Initialise(options, WithIntroSkipperDatabase(databaseConfiguration));
            });
            serviceCollection.AddDbContextFactory<DetectionCacheDbContext>((serviceProvider, options) =>
            {
                var provider = serviceProvider.GetRequiredService<IMulletaFlixDatabaseProvider>();
                var databaseConfiguration = serviceProvider.GetRequiredService<IServerConfigurationManager>()
                    .GetConfiguration<DatabaseConfigurationOptions>("database");
                provider.Initialise(options, WithIntroSkipperDatabase(databaseConfiguration));
            });
            // The facades own database initialization via their internal retryable
            // gates; every consumer goes through a facade.
            serviceCollection.AddSingleton<IIntroSkipperDatabase, IntroSkipperDatabase>();
            serviceCollection.AddSingleton<IDetectionCacheDatabase, DetectionCacheDatabase>();

            // Registered before Entrypoint so migrations are warmed as the first hosted
            // service; the facades' internal gate still guarantees ordering for any
            // request that arrives earlier.
            serviceCollection.AddHostedService<IntroSkipperDatabaseInitializer>();

            // The only thing that runs the analyzer: the scheduled task, the watcher and
            // the dashboard scan enqueue requests here. Registered ahead of the watcher so
            // hosted services stop in reverse order: the watcher unsubscribes first, then
            // the queue cancels the pass in flight.
            serviceCollection.AddSingleton<AnalysisScheduler>();
            serviceCollection.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<AnalysisScheduler>());
            // The hosted library-event listener that feeds the queue.
            serviceCollection.AddSingleton<Entrypoint>();
            serviceCollection.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<Entrypoint>());
            // Stateless resolution of library items into seasons, shared by every pass,
            // the watcher and the dashboard.
            serviceCollection.AddSingleton<SeasonResolver>();
            // Holds no per-run state, so one instance serves every pass.
            serviceCollection.AddSingleton<BaseItemAnalyzerTask>();
            serviceCollection.AddSingleton<DetectionCacheService>();
            serviceCollection.AddSingleton<IFFmpegService, FFmpegService>();
            // Shared plugin-to-Jellyfin segment conversion plus the direct writer into
            // Jellyfin's MediaSegments table; the provider stays registered so
            // Jellyfin-initiated runs converge to the same data.
            serviceCollection.AddSingleton<SegmentDtoFactory>();
            serviceCollection.AddSingleton<IJellyfinSegmentStore, JellyfinSegmentStore>();
            // Every plugin write into Jellyfin's MediaSegments table goes through the
            // mirror: per-item locked syncs and validated targeted deletes, driven by
            // the projection worker.
            serviceCollection.AddSingleton<MediaSegmentMirror>();
            serviceCollection.AddSingleton<IMediaSegmentProvider, SegmentProvider>();
            // The mutation stripes serialize all interactive mutations per item —
            // apply and projection alike — which only works when every request
            // shares the singleton.
            serviceCollection.AddSingleton<SegmentMutationLocks>();
            // Live view of the mirroring flag plus its toggle event; hosted so it can
            // subscribe to plugin configuration changes.
            serviceCollection.AddSingleton<MediaSegmentMirrorPolicy>();
            serviceCollection.AddSingleton<IMediaSegmentMirrorPolicy>(serviceProvider => serviceProvider.GetRequiredService<MediaSegmentMirrorPolicy>());
            serviceCollection.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<MediaSegmentMirrorPolicy>());
            // Durable segment changes: the coordinator commits intents through the
            // facade and retries journaled projection work; hosted for the retry loop.
            // TryAdd: the service collection is Jellyfin's shared server-wide
            // container, so an unconditional registration would claim (or cede to a
            // later plugin) the global TimeProvider slot for every consumer.
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddSingleton<ISegmentProjectionAdapter, JellyfinSegmentProjectionAdapter>();
            serviceCollection.AddSingleton(serviceProvider => new SegmentChange(
                serviceProvider.GetRequiredService<IIntroSkipperDatabase>(),
                serviceProvider.GetRequiredService<ISegmentProjectionAdapter>(),
                serviceProvider.GetRequiredService<IMediaSegmentMirrorPolicy>(),
                serviceProvider.GetRequiredService<SegmentMutationLocks>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                serviceProvider.GetRequiredService<ILogger<SegmentChange>>()));
            serviceCollection.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<SegmentChange>());
            serviceCollection.AddSingleton<ISegmentEraser, SegmentEraser>();
            serviceCollection.AddSingleton<MediaSegmentsFirstEpisodeFilter>();
            serviceCollection.Configure<MvcOptions>(options =>
            {
                options.Conventions.Add(new MediaSegmentsFilterConvention());
            });
        }

        private static DatabaseConfigurationOptions WithIntroSkipperDatabase(DatabaseConfigurationOptions source)
        {
            var sourceOptions = source.CustomProviderOptions
                ?? throw new InvalidOperationException("MariaDB configuration is missing provider options.");
            if (!source.DatabaseType.Equals("MulletaFlix-MySQL", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("IntroSkipper requires the MariaDB provider (MulletaFlix-MySQL).");
            }
            var options = new CustomDatabaseOptions
            {
                PluginName = sourceOptions.PluginName,
                PluginAssembly = sourceOptions.PluginAssembly,
                ConnectionString = sourceOptions.ConnectionString,
                Options = [.. sourceOptions.Options.Where(option => !option.Key.Equals("database", StringComparison.OrdinalIgnoreCase)), new CustomDatabaseOption { Key = "database", Value = "mulletaflix_introskipper" }]
            };
            return new DatabaseConfigurationOptions
            {
                DatabaseType = source.DatabaseType,
                LockingBehavior = source.LockingBehavior,
                CustomProviderOptions = options
            };
        }
    }
}
