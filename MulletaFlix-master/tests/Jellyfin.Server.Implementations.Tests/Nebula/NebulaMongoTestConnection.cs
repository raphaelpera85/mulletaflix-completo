using System;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Requires an explicit, local-only MongoDB test connection before integration tests can write data.
/// </summary>
internal static class NebulaMongoTestConnection
{
    private const string EnvironmentVariableName = "MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING";
    internal const string IsolatedConnectionString = "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=1500&directConnection=true";

    public static bool TryGet(out string connectionString, out string reason)
    {
        var candidate = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!IsAllowedTarget(candidate, out reason))
        {
            connectionString = string.Empty;
            return false;
        }

        // Do not pass caller-supplied topology options to the driver. In
        // particular, directConnection prevents replica-set discovery from
        // routing test writes or cleanup to other advertised hosts.
        connectionString = IsolatedConnectionString;
        return true;
    }

    internal static bool IsAllowedTarget(string? candidate, out string reason)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            reason = $"Defina {EnvironmentVariableName} para habilitar o MongoDB descartável local.";
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "mongodb", StringComparison.Ordinal)
            || !string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
            || uri.Port != 27099
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Authority.Contains(",", StringComparison.Ordinal))
        {
            reason = $"{EnvironmentVariableName} deve apontar apenas para mongodb://127.0.0.1:27099, sem credenciais.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
