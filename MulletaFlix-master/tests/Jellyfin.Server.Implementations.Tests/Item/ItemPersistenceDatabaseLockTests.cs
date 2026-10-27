using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Implementations.Item;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Item;

public class ItemPersistenceDatabaseLockTests
{
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1.1, 29)]
    [InlineData(29.1, 1)]
    [InlineData(30, 0)]
    [InlineData(31, 0)]
    public void GetLockTimeoutSeconds_UsesOneSharedWaitBudget(double elapsedSeconds, int expectedSeconds)
    {
        var result = ItemPersistenceDatabaseLock.GetLockTimeoutSeconds(TimeSpan.FromSeconds(elapsedSeconds));

        Assert.Equal(expectedSeconds, result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(29.1, 1)]
    [InlineData(30, 0)]
    public void SynchronousLockPolling_UsesAtMostOneSecondPerWait(double elapsedSeconds, int expectedSeconds)
    {
        var remainingSeconds = ItemPersistenceDatabaseLock.GetLockTimeoutSeconds(TimeSpan.FromSeconds(elapsedSeconds));
        var waitSeconds = Math.Min(1, remainingSeconds);

        Assert.Equal(expectedSeconds, waitSeconds);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 2, 2)]
    [InlineData(0, 0, 0)]
    public void GetPotentiallyAcquiredLockCount_IncludesAmbiguousCurrentLock(int acquired, int lockCount, int expectedCount)
    {
        var result = ItemPersistenceDatabaseLock.GetPotentiallyAcquiredLockCount(acquired, lockCount);

        Assert.Equal(expectedCount, result);
    }

    [Fact]
    public async Task AcquireAsync_ThrowsWhenCancellationWasRequestedForUnsupportedProvider()
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new DbContext(options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ItemPersistenceDatabaseLock.AcquireAsync(context.Database, [0], NullLogger.Instance, cancellation.Token));
    }

    [Fact]
    public void Acquire_ReleasesLockWhoseAcquisitionResponseWasLost()
    {
        using var connection = new LostGetLockResponseConnection();
        using var context = CreateMySqlContext(connection);

        Assert.Contains("MySql", context.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() =>
            ItemPersistenceDatabaseLock.Acquire(context.Database, [0], NullLogger.Instance, CancellationToken.None));

        Assert.Equal(new[] { connection.LastLockName }, connection.ReleasedLockNames);
        Assert.Empty(connection.AcquiredLockNames);
    }

    [Fact]
    public async Task AcquireAsync_ReleasesLockWhoseAcquisitionResponseWasLost()
    {
        await using var connection = new LostGetLockResponseConnection();
        await using var context = CreateMySqlContext(connection);

        Assert.Contains("MySql", context.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ItemPersistenceDatabaseLock.AcquireAsync(context.Database, [0], NullLogger.Instance, CancellationToken.None));

        Assert.Equal(new[] { connection.LastLockName }, connection.ReleasedLockNames);
        Assert.Empty(connection.AcquiredLockNames);
    }

    private static DbContext CreateMySqlContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseMySql("Server=localhost;Database=item_lock_tests", new MariaDbServerVersion(new Version(11, 4, 2)))
            .Options;
        var context = new DbContext(options);
        context.Database.SetDbConnection(connection);
        return context;
    }

    private sealed class LostGetLockResponseConnection : DbConnection
    {
        private readonly HashSet<string> _acquiredLockNames = [];
        private readonly List<string> _releasedLockNames = [];
        private ConnectionState _state;

        public IReadOnlyCollection<string> AcquiredLockNames => _acquiredLockNames;

        public IReadOnlyList<string> ReleasedLockNames => _releasedLockNames;

        public string? LastLockName { get; private set; }

        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "item_lock_tests";

        public override string DataSource => "isolated-fake";

        public override string ServerVersion => "11.4.2";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
            => throw new NotSupportedException();

        public override void Close()
            => _state = ConnectionState.Closed;

        public override void Open()
            => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => new FakeCommand(this);

        private object? Execute(string commandText, DbParameterCollection parameters)
        {
            var lockName = Convert.ToString(
                ((DbParameter)parameters["@lockName"]!).Value,
                System.Globalization.CultureInfo.InvariantCulture)!;
            if (commandText.Contains("GET_LOCK", StringComparison.Ordinal))
            {
                LastLockName = lockName;
                _acquiredLockNames.Add(lockName);
                throw new InvalidOperationException("Simulated lost GET_LOCK response after acquisition.");
            }

            if (commandText.Contains("RELEASE_LOCK", StringComparison.Ordinal))
            {
                _releasedLockNames.Add(lockName);
                _acquiredLockNames.Remove(lockName);
                return 1;
            }

            throw new NotSupportedException($"Unexpected command: {commandText}");
        }

        private sealed class FakeCommand(LostGetLockResponseConnection connection) : DbCommand
        {
            private readonly FakeParameterCollection _parameters = new();

            public override string CommandText { get; set; } = string.Empty;

            public override int CommandTimeout { get; set; }

            public override CommandType CommandType { get; set; } = CommandType.Text;

            public override bool DesignTimeVisible { get; set; }

            public override UpdateRowSource UpdatedRowSource { get; set; }

            protected override DbConnection DbConnection { get; set; } = connection;

            protected override DbParameterCollection DbParameterCollection => _parameters;

            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery()
                => throw new NotSupportedException();

            public override object? ExecuteScalar()
                => connection.Execute(CommandText, _parameters);

            public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
            {
                try
                {
                    return Task.FromResult(connection.Execute(CommandText, _parameters));
                }
                catch (Exception exception)
                {
                    return Task.FromException<object?>(exception);
                }
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter()
                => new FakeParameter();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
                => throw new NotSupportedException();
        }

        private sealed class FakeParameter : DbParameter
        {
            public override DbType DbType { get; set; }

            public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

            public override bool IsNullable { get; set; }

            public override string ParameterName { get; set; } = string.Empty;

            public override string SourceColumn { get; set; } = string.Empty;

            public override object? Value { get; set; }

            public override bool SourceColumnNullMapping { get; set; }

            public override int Size { get; set; }

            public override DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;

            public override void ResetDbType()
            {
            }
        }

        private sealed class FakeParameterCollection : DbParameterCollection
        {
            private readonly List<DbParameter> _parameters = [];

            public override int Count => _parameters.Count;

            public override object SyncRoot => ((System.Collections.ICollection)_parameters).SyncRoot;

            public override int Add(object value)
            {
                _parameters.Add((DbParameter)value);
                return _parameters.Count - 1;
            }

            public override void AddRange(Array values)
            {
                foreach (var value in values)
                {
                    Add(value!);
                }
            }

            public override void Clear()
                => _parameters.Clear();

            public override bool Contains(object value)
                => _parameters.Contains((DbParameter)value);

            public override bool Contains(string value)
                => IndexOf(value) >= 0;

            public override void CopyTo(Array array, int index)
                => ((System.Collections.ICollection)_parameters).CopyTo(array, index);

            public override System.Collections.IEnumerator GetEnumerator()
                => _parameters.GetEnumerator();

            public override int IndexOf(object value)
                => _parameters.IndexOf((DbParameter)value);

            public override int IndexOf(string parameterName)
                => _parameters.FindIndex(parameter => parameter.ParameterName == parameterName);

            public override void Insert(int index, object value)
                => _parameters.Insert(index, (DbParameter)value);

            public override void Remove(object value)
                => _parameters.Remove((DbParameter)value);

            public override void RemoveAt(int index)
                => _parameters.RemoveAt(index);

            public override void RemoveAt(string parameterName)
                => RemoveAt(IndexOf(parameterName));

            protected override DbParameter GetParameter(int index)
                => _parameters[index];

            protected override DbParameter GetParameter(string parameterName)
                => _parameters[IndexOf(parameterName)];

            protected override void SetParameter(int index, DbParameter value)
                => _parameters[index] = value;

            protected override void SetParameter(string parameterName, DbParameter value)
            {
                var index = IndexOf(parameterName);
                if (index < 0)
                {
                    Add(value);
                }
                else
                {
                    SetParameter(index, value);
                }
            }
        }
    }
}

