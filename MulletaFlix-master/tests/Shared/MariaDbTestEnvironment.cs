// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Net;
using MySqlConnector;
using Xunit;

namespace MulletaFlix.Testing;

/// <summary>Fail-closed connection handling for tests that create or drop MariaDB schemas.</summary>
internal static class MariaDbTestEnvironment
{
    public const string ConnectionStringVariable = "MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING";

    public static bool IsConfigured
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    public static void SkipUnlessConfigured()
        => Assert.SkipUnless(
            IsConfigured,
            $"Set {ConnectionStringVariable} to an isolated loopback MariaDB on a non-default port.");

    public static MySqlConnectionStringBuilder GetAdminConnection()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} is required; refusing to access any default database endpoint.");
        }

        return Validate(configuredConnection);
    }

    public static MySqlConnectionStringBuilder Validate(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("An explicit disposable MariaDB connection is required.");
        }

        MySqlConnectionStringBuilder connection;
        try
        {
            connection = new MySqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("The disposable MariaDB connection string is invalid.", exception);
        }

        var host = connection.Server.Trim();
        var isLoopback = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
        if (!isLoopback || connection.Port == 3306 || host.Contains(',', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MariaDB integration tests require one loopback host on a non-default port; refusing unsafe database access.");
        }

        connection.Database = string.Empty;
        connection.ConnectionTimeout = 5;
        return connection;
    }
}
