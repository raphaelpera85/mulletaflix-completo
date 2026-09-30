using System;
using System.Diagnostics;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MulletaFlix.Server.Extensions;

internal static class MulletaFlixOpenTelemetryExtensions
{
    internal readonly record struct OtlpSignalConfiguration(bool TracesEnabled, bool MetricsEnabled);

    internal static OtlpSignalConfiguration ConfigureOpenTelemetry(
        IServiceCollection services,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var signals = ResolveSignals(getEnvironmentVariable);
        if (!signals.TracesEnabled && !signals.MetricsEnabled)
        {
            return signals;
        }

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("MulletaFlix.Server"));

        if (signals.TracesEnabled)
        {
            builder.WithTracing(tracing => tracing
                .AddSource(NebulaHttpStreamServer.ActivitySourceName)
                .AddSource(NebulaUploadEngine.ActivitySourceName)
                .AddSource(NebulaDownloaderEngine.ActivitySourceName)
                .AddSource(NebulaTelegramPool.ActivitySourceName)
                .AddSource(NebulaMongoContext.ActivitySourceName)
                .AddSource(NebulaPlaybackSessionMonitor.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = false;
                    options.EnrichWithHttpRequest = static (activity, _) => RemoveSensitiveRequestTags(activity);
                })
                .AddOtlpExporter());
        }

        if (signals.MetricsEnabled)
        {
            builder.WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(NebulaUploadEngine.MeterName)
                .AddMeter(NebulaDownloaderEngine.MeterName)
                .AddOtlpExporter());
        }

        return signals;
    }

    internal static OtlpSignalConfiguration ResolveSignals(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var sharedEndpoint = HasValue(getEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
        var tracesEndpoint = sharedEndpoint || HasValue(getEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"));
        var metricsEndpoint = sharedEndpoint || HasValue(getEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"));
        return new OtlpSignalConfiguration(tracesEndpoint, metricsEndpoint);
    }

    internal static void RemoveSensitiveRequestTags(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.SetTag("url.path", null);
        activity.SetTag("url.query", null);
        activity.SetTag("url.full", null);
        activity.SetTag("user_agent.original", null);
    }

    private static bool HasValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}
