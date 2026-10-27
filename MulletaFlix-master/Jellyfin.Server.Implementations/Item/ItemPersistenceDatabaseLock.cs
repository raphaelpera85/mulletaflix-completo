using System;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace MulletaFlix.Server.Implementations.Item;

internal sealed class ItemPersistenceDatabaseLock : IDisposable, IAsyncDisposable
{
    private const int LockCount = 16;
    private const int TimeoutSeconds = 30;
    private readonly DatabaseFacade _database;
    private readonly ILogger _logger;
    private readonly DbConnection? _connection;
    private readonly string[] _names;
    private bool _disposed;

    private ItemPersistenceDatabaseLock(DatabaseFacade database, ILogger logger, DbConnection? connection, string[] names)
    {
        _database = database;
        _logger = logger;
        _connection = connection;
        _names = names;
    }

    public static ItemPersistenceDatabaseLock Acquire(
        DatabaseFacade database,
        int[] lockIndices,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var orderedLockIndices = OrderLockIndices(lockIndices);
        if (!SupportsAdvisoryLocks(database))
        {
            // shortcut: custom providers only get process-local locking; add provider-specific advisory locks before multi-instance use.
            return new ItemPersistenceDatabaseLock(database, logger, null, []);
        }

        database.OpenConnection();
        var connection = database.GetDbConnection();
        string[] names = [];
        var acquired = 0;
        try
        {
            names = GetLockNames(connection, orderedLockIndices);
            var elapsed = Stopwatch.StartNew();
            while (acquired < names.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remainingSeconds = GetLockTimeoutSeconds(elapsed.Elapsed);
                if (remainingSeconds == 0)
                {
                    throw new TimeoutException($"Timed out acquiring item persistence lock stripe {orderedLockIndices[acquired]}.");
                }

                using var command = CreateCommand(connection, "SELECT GET_LOCK(@lockName, @timeoutSeconds)", names[acquired]);
                command.Parameters.Add(CreateParameter(command, "@timeoutSeconds", Math.Min(1, remainingSeconds)));
                var result = command.ExecuteScalar();
                if (result is null or DBNull)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException($"MariaDB returned NULL while acquiring item persistence lock stripe {orderedLockIndices[acquired]}.");
                }

                if (Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1)
                {
                    acquired++;
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ItemPersistenceDatabaseLock(database, logger, connection, names);
        }
        catch
        {
            Release(database, connection, names, GetPotentiallyAcquiredLockCount(acquired, names.Length), logger);
            throw;
        }
    }

    public static async Task<ItemPersistenceDatabaseLock> AcquireAsync(
        DatabaseFacade database,
        int[] lockIndices,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var orderedLockIndices = OrderLockIndices(lockIndices);
        if (!SupportsAdvisoryLocks(database))
        {
            return new ItemPersistenceDatabaseLock(database, logger, null, []);
        }

        await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = database.GetDbConnection();
        string[] names = [];
        var acquired = 0;
        try
        {
            names = GetLockNames(connection, orderedLockIndices);
            var elapsed = Stopwatch.StartNew();
            for (; acquired < names.Length; acquired++)
            {
                var timeoutSeconds = GetLockTimeoutSeconds(elapsed.Elapsed);
                if (timeoutSeconds == 0)
                {
                    throw new TimeoutException($"Timed out acquiring item persistence lock stripe {orderedLockIndices[acquired]}.");
                }

                await using var command = CreateCommand(connection, "SELECT GET_LOCK(@lockName, @timeoutSeconds)", names[acquired]);
                command.Parameters.Add(CreateParameter(command, "@timeoutSeconds", timeoutSeconds));
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result is null or DBNull)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (!IsLockAcquired(result, orderedLockIndices[acquired]))
                {
                    throw new TimeoutException($"Timed out acquiring item persistence lock stripe {orderedLockIndices[acquired]}.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ItemPersistenceDatabaseLock(database, logger, connection, names);
        }
        catch
        {
            await ReleaseAsync(database, connection, names, GetPotentiallyAcquiredLockCount(acquired, names.Length), logger).ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_connection is not null)
            {
                Release(_database, _connection, _names, _names.Length, _logger);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _connection is null
            ? ValueTask.CompletedTask
            : new ValueTask(ReleaseAsync(_database, _connection, _names, _names.Length, _logger));
    }

    private static bool SupportsAdvisoryLocks(DatabaseFacade database)
        => database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    internal static int GetLockTimeoutSeconds(TimeSpan elapsed)
    {
        var remainingSeconds = TimeoutSeconds - elapsed.TotalSeconds;
        return remainingSeconds <= 0 ? 0 : (int)Math.Ceiling(remainingSeconds);
    }

    internal static int GetPotentiallyAcquiredLockCount(int acquired, int lockCount)
        => Math.Min(acquired + 1, lockCount);

    internal static string[] GetLockNames(DbConnection connection, int[] lockIndices)
    {
        var schemaHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(connection.Database)))[..16];
        return lockIndices
            .Select(index => $"mfx:item:{schemaHash}:{index}")
            .ToArray();
    }

    private static bool IsLockAcquired(object? result, int stripe)
    {
        if (result is null or DBNull)
        {
            throw new InvalidOperationException($"MariaDB returned NULL while acquiring item persistence lock stripe {stripe}.");
        }

        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static int[] OrderLockIndices(int[] lockIndices)
    {
        ArgumentNullException.ThrowIfNull(lockIndices);
        var ordered = lockIndices.Distinct().Order().ToArray();
        if (ordered.Any(index => (uint)index >= LockCount))
        {
            throw new ArgumentOutOfRangeException(nameof(lockIndices));
        }

        return ordered;
    }

    private static DbCommand CreateCommand(DbConnection connection, string sql, string lockName)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(CreateParameter(command, "@lockName", lockName));
        return command;
    }

    private static DbParameter CreateParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    private static void Release(DatabaseFacade database, DbConnection connection, string[] names, int count, ILogger logger)
    {
        var releaseFailed = false;
        try
        {
            for (var i = count - 1; i >= 0; i--)
            {
                using var command = CreateCommand(connection, "SELECT RELEASE_LOCK(@lockName)", names[i]);
                command.ExecuteScalar();
            }
        }
        catch (Exception exception)
        {
            releaseFailed = true;
            logger.LogWarning(exception, "Unable to release item persistence advisory locks; closing the database connection to release them.");
        }
        finally
        {
            if (releaseFailed && connection is MySqlConnection mySqlConnection)
            {
                try
                {
                    MySqlConnection.ClearPool(mySqlConnection);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Unable to clear the database connection pool after an advisory lock release failure.");
                }
            }

            database.CloseConnection();
        }
    }

    private static async Task ReleaseAsync(DatabaseFacade database, DbConnection connection, string[] names, int count, ILogger logger)
    {
        var releaseFailed = false;
        try
        {
            for (var i = count - 1; i >= 0; i--)
            {
                await using var command = CreateCommand(connection, "SELECT RELEASE_LOCK(@lockName)", names[i]);
                await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            releaseFailed = true;
            logger.LogWarning(exception, "Unable to release item persistence advisory locks; closing the database connection to release them.");
        }
        finally
        {
            if (releaseFailed && connection is MySqlConnection mySqlConnection)
            {
                try
                {
                    MySqlConnection.ClearPool(mySqlConnection);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Unable to clear the database connection pool after an advisory lock release failure.");
                }
            }

            await database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}

