using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using MulletaFlix.Server.Helpers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public sealed class HostingRequestLogRedactionTests
{
    [Fact]
    public async Task HostingDiagnostics_RedactsCapabilityAndQueryWithoutMutatingRequest()
    {
        const string capability = "capability-secret";
        const string querySecret = "api-key-secret";
        var sink = new CollectingSink();
        using var serilogLogger = StartupHelpers.AddHostingRequestLogRedaction(
                new LoggerConfiguration().MinimumLevel.Information())
            .WriteTo.Sink(sink)
            .CreateLogger();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddSerilog(serilogLogger, dispose: false);

        await using var application = builder.Build();
        string? pathSeenByHandler = null;
        string? querySeenByHandler = null;
        application.Run(context =>
        {
            pathSeenByHandler = context.Request.Path.Value;
            querySeenByHandler = context.Request.QueryString.Value;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();
        using var response = await client.GetAsync(
            $"/LiveTv/LiveRecordings/{capability}/stream?api_key={querySecret}",
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/LiveTv/LiveRecordings/{capability}/stream", pathSeenByHandler);
        Assert.Equal($"?api_key={querySecret}", querySeenByHandler);

        var hostingLogs = sink.Events
            .Where(static logEvent => GetSourceContext(logEvent) == "Microsoft.AspNetCore.Hosting.Diagnostics")
            .ToArray();
        Assert.Equal(2, hostingLogs.Length);
        var renderedLogs = string.Join(
            Environment.NewLine,
            hostingLogs.Select(static logEvent => logEvent.RenderMessage(CultureInfo.InvariantCulture)));
        Assert.Contains("/LiveTv/LiveRecordings/[REDACTED]/stream", renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(capability, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(querySecret, renderedLogs, StringComparison.Ordinal);
        Assert.All(hostingLogs, static logEvent =>
        {
            Assert.Equal(
                string.Empty,
                Assert.IsType<ScalarValue>(logEvent.Properties["QueryString"]).Value);
        });
    }

    [Fact]
    public void AddHostingRequestLogRedaction_RedactsOnlyHostingRequestProperties()
    {
        var sink = new CollectingSink();
        using var logger = StartupHelpers.AddHostingRequestLogRedaction(new LoggerConfiguration())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger
            .ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Information(
                "Request {Path}{QueryString}",
                "/LiveTv/LiveRecordings/capability-secret/stream",
                "?api_key=api-key-secret");
        logger
            .ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Information("Hosting started on {Address}", "http://localhost");
        logger
            .ForContext("SourceContext", "MulletaFlix.CustomDiagnostics")
            .Information("Custom operation {Path}", "/internal/status");

        var hostRequestEvent = Assert.Single(sink.Events, static logEvent =>
            GetSourceContext(logEvent) == "Microsoft.AspNetCore.Hosting.Diagnostics"
            && logEvent.Properties.ContainsKey("Path"));
        Assert.Equal(
            "/LiveTv/LiveRecordings/[REDACTED]/stream",
            Assert.IsType<ScalarValue>(hostRequestEvent.Properties["Path"]).Value);
        Assert.Equal(string.Empty, Assert.IsType<ScalarValue>(hostRequestEvent.Properties["QueryString"]).Value);

        var hostStartupEvent = Assert.Single(sink.Events, static logEvent =>
            GetSourceContext(logEvent) == "Microsoft.AspNetCore.Hosting.Diagnostics"
            && logEvent.Properties.ContainsKey("Address"));
        Assert.Equal("http://localhost", Assert.IsType<ScalarValue>(hostStartupEvent.Properties["Address"]).Value);

        var customEvent = Assert.Single(sink.Events, static logEvent =>
            GetSourceContext(logEvent) == "MulletaFlix.CustomDiagnostics");
        Assert.Equal("/internal/status", Assert.IsType<ScalarValue>(customEvent.Properties["Path"]).Value);
    }

    private static string? GetSourceContext(LogEvent logEvent)
        => logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? (sourceContext as ScalarValue)?.Value as string
            : null;

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
