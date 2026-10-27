// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using System;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins;

internal static class ProviderLiveTestEnvironment
{
    internal const string EnableVariable = "MULLETAFLIX_RUN_LIVE_PROVIDER_TESTS";

    internal static bool IsEnabled(string? value)
        => string.Equals(value, "1", StringComparison.Ordinal);

    internal static void SkipUnlessEnabled()
        => Assert.SkipUnless(
            IsEnabled(Environment.GetEnvironmentVariable(EnableVariable)),
            $"Set {EnableVariable}=1 to allow provider tests to call live external services.");
}
