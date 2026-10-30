using Xunit;

namespace Jellyfin.Providers.Tests;

public sealed class LiveProviderIntegrationTestEnvironmentTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", false)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    [InlineData(" 1 ", false)]
    public void IsEnabled_RequiresExactOptInValue(string? value, bool expected)
        => Assert.Equal(expected, LiveProviderIntegrationTestEnvironment.IsEnabled(value));
}
