using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MulletaFlix.Server.Extensions;
using Xunit;

namespace MulletaFlix.Server.Tests.Observability;

public sealed class MulletaFlixOpenTelemetryExtensionsTests
{
    [Fact]
    public void ConfigureOpenTelemetry_DoesNotRegisterExportersWithoutEndpoint()
    {
        var services = new ServiceCollection();

        var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(services, _ => null);

        Assert.False(signals.TracesEnabled);
        Assert.False(signals.MetricsEnabled);
        Assert.Empty(services);
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersProviderWhenSharedEndpointExists()
    {
        var services = new ServiceCollection();

        var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(
            services,
            name => name == "OTEL_EXPORTER_OTLP_ENDPOINT" ? "http://collector:4317" : null);

        Assert.True(signals.TracesEnabled);
        Assert.True(signals.MetricsEnabled);
        Assert.NotEmpty(services);
    }

    [Fact]
    public void ResolveSignals_DisablesExportWhenNoCollectorEndpointExists()
    {
        var signals = MulletaFlixOpenTelemetryExtensions.ResolveSignals(_ => null);

        Assert.False(signals.TracesEnabled);
        Assert.False(signals.MetricsEnabled);
    }

    [Fact]
    public void ResolveSignals_SharedEndpointEnablesTracesAndMetrics()
    {
        var signals = MulletaFlixOpenTelemetryExtensions.ResolveSignals(name =>
            name == "OTEL_EXPORTER_OTLP_ENDPOINT" ? "http://collector:4317" : null);

        Assert.True(signals.TracesEnabled);
        Assert.True(signals.MetricsEnabled);
    }

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", true, false)]
    [InlineData("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", false, true)]
    public void ResolveSignals_EnablesOnlySignalWithConfiguredEndpoint(
        string endpointName,
        bool expectedTraces,
        bool expectedMetrics)
    {
        var signals = MulletaFlixOpenTelemetryExtensions.ResolveSignals(name =>
            name == endpointName ? "http://collector:4318" : null);

        Assert.Equal(expectedTraces, signals.TracesEnabled);
        Assert.Equal(expectedMetrics, signals.MetricsEnabled);
    }

    [Fact]
    public void RemoveSensitiveRequestTags_RemovesPathsQueriesAndUserAgent()
    {
        using var activity = new Activity("request")
            .SetTag("url.path", "/Users/private-id/Items/secret-media")
            .SetTag("url.query", "api_key=secret")
            .SetTag("url.full", "https://server/private?token=secret")
            .SetTag("user_agent.original", "private-client");

        MulletaFlixOpenTelemetryExtensions.RemoveSensitiveRequestTags(activity);

        Assert.Null(activity.GetTagItem("url.path"));
        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("user_agent.original"));
    }
}
