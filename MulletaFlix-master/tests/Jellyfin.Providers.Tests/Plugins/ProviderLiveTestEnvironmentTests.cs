// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using Xunit;

namespace Jellyfin.Providers.Tests.Plugins;

public sealed class ProviderLiveTestEnvironmentTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("true", false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void IsEnabled_RequiresExplicitOne(string? value, bool expected)
    {
        Assert.Equal(expected, ProviderLiveTestEnvironment.IsEnabled(value));
    }
}
