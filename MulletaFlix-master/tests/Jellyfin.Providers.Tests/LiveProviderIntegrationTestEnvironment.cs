using System;
using Xunit;

namespace Jellyfin.Providers.Tests;

/// <summary>Explicit opt-in gate for tests that call live third-party provider sites.</summary>
internal static class LiveProviderIntegrationTestEnvironment
{
    public const string Variable = "MULLETAFLIX_RUN_LIVE_PROVIDER_TESTS";

    public static bool IsEnabled(string? value)
        => string.Equals(value, "1", StringComparison.Ordinal);

    public static void SkipUnlessEnabled()
        => Assert.SkipUnless(
            IsEnabled(Environment.GetEnvironmentVariable(Variable)),
            $"Set {Variable}=1 to run tests against live third-party services.");
}
