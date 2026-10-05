using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using MulletaFlix.Api.Middleware;
using MulletaFlix.Server.Helpers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Parsing;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public sealed class HostingRequestLogRedactionTests
{
    [Fact]
    public async Task HostingDiagnostics_DoesNotLogCapabilityOrQueryAndHandlerReceivesOriginalRequest()
    {
        const string capability = "capability-secret";
        const string querySecret = "api-key-secret";
        var sink = new CollectingSink();
        using var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByExcluding(StartupHelpers.ShouldExcludeRawHostingRequestLog)
            .WriteTo.Sink(sink)
            .CreateLogger();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddSerilog(serilogLogger, dispose: false);

        await using var application = builder.Build();
        application.UseMiddleware<RequestPathLogRedactionMiddleware>();

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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/LiveTv/LiveRecordings/{capability}/stream", pathSeenByHandler);
        Assert.Equal($"?api_key={querySecret}", querySeenByHandler);

        var renderedLogs = string.Join(
            Environment.NewLine,
            sink.Events.Select(static logEvent => logEvent.RenderMessage(CultureInfo.InvariantCulture)));
        Assert.DoesNotContain(capability, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(querySecret, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveRecordings", renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldExcludeRawHostingRequestLog_ExcludesOnlyHostingDiagnosticsWithPathOrQuery()
    {
        var hostPathEvent = CreateLogEvent(
            "Microsoft.AspNetCore.Hosting.Diagnostics",
            "Request starting {Path}{QueryString}",
            ("Path", "/LiveTv/LiveRecordings/capability-secret/stream"),
            ("QueryString", "?api_key=api-key-secret"));
        var hostEventWithoutRequestData = CreateLogEvent(
            "Microsoft.AspNetCore.Hosting.Diagnostics",
            "Hosting started on {Address}",
            ("Address", "http://localhost"));
        var otherCategoryEvent = CreateLogEvent(
            "MulletaFlix.CustomDiagnostics",
            "Custom operation {Path}",
            ("Path", "/internal/status"));

        Assert.True(StartupHelpers.ShouldExcludeRawHostingRequestLog(hostPathEvent));
        Assert.False(StartupHelpers.ShouldExcludeRawHostingRequestLog(hostEventWithoutRequestData));
        Assert.False(StartupHelpers.ShouldExcludeRawHostingRequestLog(otherCategoryEvent));
    }

    private static LogEvent CreateLogEvent(string sourceContext, string messageTemplate, params (string Name, object Value)[] properties)
    {
        var logProperties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(sourceContext))
        };

        foreach (var (name, value) in properties)
        {
            logProperties.Add(new LogEventProperty(name, new ScalarValue(value)));
        }

        var template = new MessageTemplateParser().Parse(messageTemplate);
        return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, logProperties);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
