using System;
using Serilog.Core;
using Serilog.Events;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Redacts sensitive request data from ASP.NET hosting diagnostic events without mutating the request.
/// </summary>
public sealed class HostingRequestLogRedactionEnricher : ILogEventEnricher
{
    private const string HostingDiagnosticsSource = "Microsoft.AspNetCore.Hosting.Diagnostics";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            || sourceContext is not ScalarValue { Value: string source }
            || !string.Equals(source, HostingDiagnosticsSource, StringComparison.Ordinal))
        {
            return;
        }

        if (logEvent.Properties.TryGetValue("Path", out var pathProperty))
        {
            var path = pathProperty is ScalarValue { Value: { } value } ? value.ToString() : string.Empty;
            var safePath = RequestPathLogRedactor.RedactSensitiveSegments(path);
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Path", safePath));
        }

        if (logEvent.Properties.ContainsKey("QueryString"))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("QueryString", string.Empty));
        }
    }
}
