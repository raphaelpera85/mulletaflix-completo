using System;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace MulletaFlix.Server.Implementations.Tests.Users;

/// <summary>Creates one isolated MariaDB schema per test fixture.</summary>
internal sealed class UsersMariaDbTestDatabase : IDisposable
{
    private const string DatabasePrefix = "mfx_users_test_";
    private readonly string _adminConnectionString;

    public UsersMariaDbTestDatabase()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();

        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        _adminConnectionString = adminConnection.ConnectionString;
        DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");

        var databaseConnection = new MySqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = DatabaseName
        };

        Options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseMySql(databaseConnection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
            .Options;

        using var connection = new MySqlConnection(_adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        command.ExecuteNonQuery();
    }

    public string DatabaseName { get; }

    public DbContextOptions<UsersDbContext> Options { get; }

    public UsersDbContext CreateDbContext()
        => new(Options, NullLogger<UsersDbContext>.Instance);

    public void Dispose()
    {
        if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to drop a database outside the test schema prefix.");
        }

        using var connection = new MySqlConnection(_adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
        command.ExecuteNonQuery();
    }
}
