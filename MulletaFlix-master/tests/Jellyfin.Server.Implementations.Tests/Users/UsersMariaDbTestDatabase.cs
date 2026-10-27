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
    private const string TestUserHost = "127.0.0.1";
    private readonly string _adminConnectionString;
    private readonly string _applicationUser;
    private readonly string _applicationPassword;
    private readonly string _applicationConnectionString;

    public UsersMariaDbTestDatabase()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();

        var adminConnection = MariaDbTestEnvironment.GetAdminConnection();
        _adminConnectionString = adminConnection.ConnectionString;
        DatabaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        _applicationUser = "mfxu_" + Guid.NewGuid().ToString("N");
        _applicationPassword = Guid.NewGuid().ToString("N");

        using var connection = new MySqlConnection(_adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        try
        {
            command.CommandText = $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            command.ExecuteNonQuery();
            command.CommandText = $"CREATE USER '{_applicationUser}'@'{TestUserHost}' IDENTIFIED BY '{_applicationPassword}';";
            command.ExecuteNonQuery();
            command.CommandText = $"GRANT ALL PRIVILEGES ON `{DatabaseName}`.* TO '{_applicationUser}'@'{TestUserHost}';";
            command.ExecuteNonQuery();
        }
        catch
        {
            command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
            command.ExecuteNonQuery();
            command.CommandText = $"DROP USER IF EXISTS '{_applicationUser}'@'{TestUserHost}';";
            command.ExecuteNonQuery();
            throw;
        }

        var databaseConnection = new MySqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = DatabaseName,
            UserID = _applicationUser,
            Password = _applicationPassword,
        };
        _applicationConnectionString = databaseConnection.ConnectionString;

        Options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseMySql(_applicationConnectionString, new MariaDbServerVersion(new Version(11, 4, 2)))
            .Options;
    }

    public string DatabaseName { get; }

    internal string ApplicationConnectionString => _applicationConnectionString;

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
        command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`; DROP USER IF EXISTS '{_applicationUser}'@'{TestUserHost}';";
        command.ExecuteNonQuery();
    }
}
