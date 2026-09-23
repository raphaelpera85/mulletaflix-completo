using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Emby.Server.Implementations.EntryPoints;
using Emby.Server.Implementations.Localization;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using MulletaFlix.Api.Middleware;
using MulletaFlix.Api.Jobs;
using MulletaFlix.Database.Implementations;
using MulletaFlix.LiveTv.Extensions;
using MulletaFlix.LiveTv.Recordings;
using MulletaFlix.MediaEncoding.Hls.Extensions;
using MulletaFlix.Networking;
using MulletaFlix.Networking.HappyEyeballs;
using MulletaFlix.Server.Extensions;
using MulletaFlix.Server.Health;
using MulletaFlix.Server.Implementations.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Extensions;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using MediaBrowser.XbmcMetadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Prometheus;
using Jellyfin.Server.Implementations.Nebula;

namespace MulletaFlix.Server
{
    /// <summary>
    /// Startup configuration for the Kestrel webhost.
    /// </summary>
    public class Startup
    {
        private readonly CoreAppHost _serverApplicationHost;
        private readonly IConfiguration _configuration;
        private readonly IServerConfigurationManager _serverConfigurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
        /// </summary>
        /// <param name="appHost">The server application host.</param>
        /// <param name="configuration">The used Configuration.</param>
        public Startup(CoreAppHost appHost, IConfiguration configuration)
        {
            _serverApplicationHost = appHost;
            _configuration = configuration;
            _serverConfigurationManager = appHost.ConfigurationManager;
        }

        /// <summary>
        /// Configures the service collection for the webhost.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            // EnableForHttps is off by default in ASP.NET Core, which meant deployments that
            // terminate TLS locally served every JSON API response and static asset uncompressed.
            // MulletaFlix is commonly reached over a local HTTPS bind, so opt in explicitly and
            // keep Brotli preferred over gzip. Responses that carry secrets are the BREACH concern;
            // here that is limited to auth endpoints, whose payloads are tiny, while the win is on
            // the much larger library and metadata JSON.
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();

                // The ASP.NET Core defaults cover JSON/XML/CSS/HTML but NOT the HLS playlist MIME
                // types. An m3u8 repeats the full request query string on every segment line, so a
                // 1200-segment playlist is several hundred KB of highly repetitive text that was
                // being sent uncompressed. Adding the playlist and WebVTT types compresses them by
                // roughly 5-10x. Segment and range responses are deliberately left out: they already
                // carry Content-Range (which the middleware skips) and are not compressible.
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                [
                    "application/x-mpegurl",
                    "application/vnd.apple.mpegurl",
                    "audio/x-mpegurl",
                    "text/vtt"
                ]);
            });
            services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
            services.AddHttpContextAccessor();
            services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = _serverApplicationHost.HttpsPort;
            });

            services.AddMulletaFlixApi(_serverApplicationHost.GetApiPluginAssemblies(), _serverConfigurationManager.GetNetworkConfiguration());
            services.AddMulletaFlixDbContext(_serverApplicationHost.ConfigurationManager, _configuration);
            services.AddMulletaFlixApiSwagger();

            // configure custom legacy authentication
            services.AddCustomAuthentication();

            services.AddMulletaFlixApiAuthorization();

            var productHeader = new ProductInfoHeaderValue(
                _serverApplicationHost.Name.Replace(' ', '-'),
                _serverApplicationHost.ApplicationVersionString);
            var acceptJsonHeader = new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json, 1.0);
            var acceptXmlHeader = new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Xml, 0.9);
            var acceptAnyHeader = new MediaTypeWithQualityHeaderValue("*/*", 0.8);
            Func<IServiceProvider, HttpMessageHandler> eyeballsHttpClientHandlerDelegate = (_) => new SocketsHttpHandler()
            {
                AutomaticDecompression = DecompressionMethods.All,
                RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8,
                ConnectCallback = HttpClientExtension.OnConnect
            };

            Func<IServiceProvider, HttpMessageHandler> defaultHttpClientHandlerDelegate = (_) => new SocketsHttpHandler()
            {
                AutomaticDecompression = DecompressionMethods.All,
                RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8
            };

            services.AddHttpClient(NamedClient.Default, c =>
                {
                    c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptJsonHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptXmlHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptAnyHeader);
                })
                .ConfigurePrimaryHttpMessageHandler(eyeballsHttpClientHandlerDelegate)
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHttpClient(NamedClient.MusicBrainz, c =>
                {
                    c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"({_serverApplicationHost.ApplicationUserAgentAddress})"));
                    c.DefaultRequestHeaders.Accept.Add(acceptXmlHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptAnyHeader);
                })
                .ConfigurePrimaryHttpMessageHandler(eyeballsHttpClientHandlerDelegate)
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHttpClient(NamedClient.DirectIp, c =>
                {
                    c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptJsonHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptXmlHeader);
                    c.DefaultRequestHeaders.Accept.Add(acceptAnyHeader);
                })
                .ConfigurePrimaryHttpMessageHandler(defaultHttpClientHandlerDelegate)
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = false;
            });

            services.AddHealthChecks()
                .AddCheck<MulletaFlixDatabaseHealthCheck>(
                    nameof(MulletaFlixDbContext),
                    tags: new[] { "ready", "database" })
                .AddCheck<NebulaHealthCheck>("nebula", tags: new[] { "ready", "nebula" });

            services.AddHlsPlaylistGenerator();
            services.AddLiveTvServices();
            services.AddSingleton<MulletaFlixJobQueue>();
            services.AddSingleton<IJobQueue>(serviceProvider => serviceProvider.GetRequiredService<MulletaFlixJobQueue>());

            services.AddSingleton<MulletaFlix.Api.Caching.ItemsResponseCache>();

            // Book Reader services
            services.AddSingleton<MediaBrowser.Controller.Books.IBookConversionService, MulletaFlix.Server.Implementations.Books.BookConversionService>();
            services.AddSingleton<MediaBrowser.Controller.Books.IBookToEpubConverter, MulletaFlix.Server.Implementations.Books.CalibreBookConverter>();

            var serverUICulture = _serverConfigurationManager.Configuration.UICulture;
            if (string.IsNullOrEmpty(serverUICulture))
            {
                serverUICulture = "en-US";
            }

            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(serverUICulture);

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedUICultures = LocalizationManager.GetSupportedUICultures();
                options.SupportedCultures = supportedUICultures;
                options.SupportedUICultures = supportedUICultures;
                options.DefaultRequestCulture = new RequestCulture(serverUICulture);
                options.ApplyCurrentCultureToResponseHeaders = true;
                options.FallBackToParentCultures = true;
                options.FallBackToParentUICultures = true;
            });

            services.AddHostedService<RecordingsHost>();
            services.AddHostedService<AutoDiscoveryHost>();
            services.AddHostedService<PortMappingHost>();
            services.AddHostedService<NfoUserDataSaver>();
            services.AddHostedService<LibraryChangedNotifier>();
            services.AddHostedService<UserDataChangeNotifier>();
            services.AddHostedService<RecordingNotifier>();
            services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<MulletaFlixJobQueue>());
            services.AddHostedService<NebulaHostedService>();
            // Register the concrete exporter as well as the hosted-service view so
            // the STRM downloader can await metadata preparation before downloading.
            services.AddSingleton<NebulaMetadataExportService>();
            services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<NebulaMetadataExportService>());
            services.AddSingleton<NotificationsLibraryNotifier>();
            services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<NotificationsLibraryNotifier>());
        }

        /// <summary>
        /// Configures the app builder for the webhost.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The webhost environment.</param>
        /// <param name="appConfig">The application config.</param>
        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IConfiguration appConfig)
        {
            app.UseBaseUrlRedirection();

            // Wrap rest of configuration so everything only listens on BaseUrl.
            var config = _serverConfigurationManager.GetNetworkConfiguration();
            app.Map(config.BaseUrl, mainApp =>
            {
                if (env.IsDevelopment())
                {
                    mainApp.UseDeveloperExceptionPage();
                }

                mainApp.UseForwardedHeaders();
                mainApp.UseMiddleware<CorrelationIdMiddleware>();
                mainApp.UseMiddleware<ExceptionMiddleware>();

                mainApp.UseMiddleware<SecurityHeadersMiddleware>();
                mainApp.UseMiddleware<ResponseTimeMiddleware>();

                if (config.RequireHttps && _serverApplicationHost.ListenWithHttps)
                {
                    mainApp.UseHsts();
                }

                mainApp.UseWebSockets();

                mainApp.UseResponseCompression();

                mainApp.UseCors();

                mainApp.UseRequestLocalization();

                if (config.RequireHttps && _serverApplicationHost.ListenWithHttps)
                {
                    mainApp.UseHttpsRedirection();
                }

                if (appConfig.HostWebClient())
                {
                    // A fresh install or an isolated integration host may not
                    // have a web directory yet. PhysicalFileProvider requires
                    // the root to exist, so create it before registering the
                    // static-file middleware instead of failing host startup.
                    Directory.CreateDirectory(_serverConfigurationManager.ApplicationPaths.WebPath);

                    var extensionProvider = new FileExtensionContentTypeProvider();

                    // subtitles octopus requires .data, .mem files.
                    extensionProvider.Mappings.Add(".data", MediaTypeNames.Application.Octet);
                    extensionProvider.Mappings.Add(".mem", MediaTypeNames.Application.Octet);
                    mainApp.UseDefaultFiles(new DefaultFilesOptions
                    {
                        FileProvider = new PhysicalFileProvider(_serverConfigurationManager.ApplicationPaths.WebPath),
                        RequestPath = "/web"
                    });
                    mainApp.Use(async (context, next) =>
                    {
                        if (context.Request.Path.StartsWithSegments("/web/assets", out var remainingPath)
                            && string.Equals(Path.GetExtension(remainingPath.Value), ".js", StringComparison.OrdinalIgnoreCase))
                        {
                            var relativeAssetPath = remainingPath.Value?.TrimStart('/', '\\') ?? string.Empty;
                            var assetPath = Path.GetFullPath(Path.Combine(_serverConfigurationManager.ApplicationPaths.WebPath, "assets", relativeAssetPath));
                            var assetsRoot = Path.GetFullPath(Path.Combine(_serverConfigurationManager.ApplicationPaths.WebPath, "assets"));

                            if (assetPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)
                                && !File.Exists(assetPath))
                            {
                                context.Response.ContentType = "application/javascript; charset=utf-8";
                                context.Response.Headers.CacheControl = new StringValues("no-store, no-cache, must-revalidate");
                                await context.Response.WriteAsync("globalThis.location && globalThis.location.reload(); export default {};").ConfigureAwait(false);
                                return;
                            }
                        }

                        await next().ConfigureAwait(false);
                    });
                    mainApp.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(_serverConfigurationManager.ApplicationPaths.WebPath),
                        RequestPath = "/web",
                        ContentTypeProvider = extensionProvider,
                        OnPrepareResponse = (context) =>
                        {
                            var extension = Path.GetExtension(context.File.Name);
                            if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(Path.GetFileName(context.File.Name), "manifest.json", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Context.Response.Headers.CacheControl = new StringValues("no-store, no-cache, must-revalidate");
                                context.Context.Response.Headers.Pragma = new StringValues("no-cache");
                                context.Context.Response.Headers.Expires = new StringValues("0");
                            }
                            else if (string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(extension, ".wasm", StringComparison.OrdinalIgnoreCase))
                            {
                                // Files under /web/assets carry a content hash in their name, so they
                                // are immutable by construction. The previous max-age=3600 forced every
                                // client to revalidate dozens of assets every hour; that is pure request
                                // volume against a network-bound server. Non-hashed paths keep the
                                // shorter TTL because their names can be reused across builds.
                                var isContentHashed = context.Context.Request.Path.StartsWithSegments("/web/assets", StringComparison.Ordinal);
                                context.Context.Response.Headers.CacheControl = new StringValues(
                                    isContentHashed
                                        ? "public, max-age=31536000, immutable"
                                        : "public, max-age=3600");
                            }
                        }
                    });

                    mainApp.UseRobotsRedirection();
                }

                mainApp.UseStaticFiles();
                mainApp.UseAuthentication();

                // Registered after authentication so it can see the authenticated identity and
                // exempt authenticated clients from the anonymous bucket. When it ran before
                // UseAuthentication, every request looked anonymous (30 req / 10 s), throttling
                // legitimate authenticated clients and amplifying thread-pool churn.
                mainApp.UseMiddleware<RateLimitMiddleware>();

                mainApp.UseMulletaFlixApiSwagger(_serverConfigurationManager);
                mainApp.UseQueryStringDecoding();
                mainApp.UseRouting();
                mainApp.UseAuthorization();

                mainApp.UseIPBasedAccessValidation();
                mainApp.UseWebSocketHandler();
                mainApp.UseServerStartupMessage();

                // Metrics stay enabled in the stage so the sprint gate can validate observability.
                mainApp.UseHttpMetrics();

                mainApp.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapMetrics();

                    endpoints.MapHealthChecks("/health");
                    endpoints.MapHealthChecks(
                        "/ready",
                        new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
                });
            });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            var transientErrors = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>();

            var retry = transientErrors.WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
            var circuitBreaker = transientErrors.CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));

            return Policy.WrapAsync(retry, circuitBreaker);
        }
    }
}
