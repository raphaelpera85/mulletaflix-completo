using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jellyfin.Server.Implementations.Database;

/// <summary>
/// Measures EF connection opening, including pooled connection acquisition latency.
/// </summary>
internal sealed class DatabaseConnectionTelemetryInterceptor : DbConnectionInterceptor
{
    private readonly ConcurrentDictionary<Guid, ConnectionMeasurement> _connections = new();

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Start(eventData, result.IsSuppressed);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Start(eventData, result.IsSuppressed);
        return ValueTask.FromResult(result);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Complete(eventData.ConnectionId, "success");
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.ConnectionId, "success");
        return Task.CompletedTask;
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        Complete(eventData.ConnectionId, GetFailureResult(eventData.Exception));
    }

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.ConnectionId, GetFailureResult(eventData.Exception));
        return Task.CompletedTask;
    }

    private void Start(ConnectionEventData eventData, bool wasSuppressed)
    {
        if (!wasSuppressed)
        {
            _connections.TryAdd(
                eventData.ConnectionId,
                new ConnectionMeasurement(Stopwatch.GetTimestamp(), DatabaseCommandTelemetryInterceptor.ClassifyContext(eventData.Context)));
        }
    }

    private void Complete(Guid connectionId, string result)
    {
        if (_connections.TryRemove(connectionId, out var measurement))
        {
            DatabaseTelemetryMetrics.RecordConnectionOpen(
                Stopwatch.GetElapsedTime(measurement.StartedAt).TotalSeconds,
                measurement.Context,
                result);
        }
    }

    private static string GetFailureResult(Exception exception)
        => exception is OperationCanceledException ? "cancelled" : "failure";

    private sealed record ConnectionMeasurement(long StartedAt, string Context);
}
