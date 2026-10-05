using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jellyfin.Server.Implementations.Database;

/// <summary>
/// Records EF command duration without exporting SQL text or parameter values.
/// </summary>
internal sealed class DatabaseCommandTelemetryInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentDictionary<Guid, CommandMeasurement> _commands = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Start(command, eventData, result.HasResult);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Start(command, eventData, result.HasResult);
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Complete(eventData.CommandId, "success");
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Complete(eventData.CommandId, "success");
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Start(command, eventData, result.HasResult);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Start(command, eventData, result.HasResult);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Complete(eventData.CommandId, "success");
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Complete(eventData.CommandId, "success");
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Start(command, eventData, result.HasResult);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Start(command, eventData, result.HasResult);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Complete(eventData.CommandId, "success");
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Complete(eventData.CommandId, "success");
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Complete(eventData.CommandId, GetFailureResult(eventData.Exception));
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.CommandId, GetFailureResult(eventData.Exception));
        return Task.CompletedTask;
    }

    public override void CommandCanceled(DbCommand command, CommandEndEventData eventData)
    {
        Complete(eventData.CommandId, "cancelled");
    }

    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.CommandId, "cancelled");
        return Task.CompletedTask;
    }

    internal static string ClassifyOperation(string commandText)
    {
        var keyword = ReadFirstKeyword(commandText);
        return keyword switch
        {
            "SELECT" or "SHOW" or "DESCRIBE" or "EXPLAIN" => "read",
            "INSERT" or "UPDATE" or "DELETE" or "REPLACE" => "write",
            "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "RENAME" => "schema",
            "WITH" => "with",
            _ => "other"
        };
    }

    internal static string ClassifyContext(DbContext? context)
    {
        return context switch
        {
            MulletaFlixDbContext => "catalog",
            UsersDbContext => "users",
            MoviesDbContext => "movies",
            SeriesDbContext => "series",
            ChannelsDbContext => "channels",
            BooksDbContext => "books",
            SystemDbContext => "system",
            null => "unknown",
            _ => "other"
        };
    }

    private static string GetFailureResult(Exception exception)
        => exception is OperationCanceledException ? "cancelled" : "failure";

    private static string ReadFirstKeyword(string commandText)
    {
        var remaining = commandText.AsSpan().TrimStart();
        while (!remaining.IsEmpty)
        {
            if (remaining.StartsWith("/*", StringComparison.Ordinal))
            {
                var commentEnd = remaining[2..].IndexOf("*/", StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    return string.Empty;
                }

                remaining = remaining[(commentEnd + 4)..].TrimStart();
                continue;
            }

            if (remaining.StartsWith("--", StringComparison.Ordinal))
            {
                var lineEnd = remaining.IndexOf('\n');
                remaining = lineEnd < 0 ? ReadOnlySpan<char>.Empty : remaining[(lineEnd + 1)..].TrimStart();
                continue;
            }

            break;
        }

        var keywordLength = 0;
        while (keywordLength < remaining.Length && char.IsAsciiLetter(remaining[keywordLength]))
        {
            keywordLength++;
        }

        return keywordLength == 0
            ? string.Empty
            : remaining[..keywordLength].ToString().ToUpperInvariant();
    }

    private void Start(DbCommand command, CommandEventData eventData, bool wasSuppressed)
    {
        if (!wasSuppressed)
        {
            _commands.TryAdd(
                eventData.CommandId,
                new CommandMeasurement(
                    Stopwatch.GetTimestamp(),
                    ClassifyOperation(command.CommandText),
                    ClassifyContext(eventData.Context)));
        }
    }

    private void Complete(Guid commandId, string result)
    {
        if (_commands.TryRemove(commandId, out var measurement))
        {
            DatabaseTelemetryMetrics.RecordCommand(
                Stopwatch.GetElapsedTime(measurement.StartedAt).TotalSeconds,
                measurement.Operation,
                measurement.Context,
                result);
        }
    }

    private sealed record CommandMeasurement(long StartedAt, string Operation, string Context);
}
