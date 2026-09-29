using System;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaUploadRetryPolicyTests
{
    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(4, 480)]
    [InlineData(12, 3600)]
    [InlineData(100, 3600)]
    public void GetDelay_UsesExponentialBackoffWithOneHourCeiling(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), NebulaUploadRetryPolicy.GetDelay(attempt, 1));
    }

    [Theory]
    [InlineData(1, 0.8, 48)]
    [InlineData(1, 1.2, 72)]
    [InlineData(12, 1.2, 3600)]
    public void GetDelay_AppliesJitterWithoutExceedingCeiling(int attempt, double jitterFactor, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), NebulaUploadRetryPolicy.GetDelay(attempt, jitterFactor));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    public void IsTerminal_StopsAutomaticRetriesAtConfiguredAttemptLimit(int attempt, bool expected)
    {
        Assert.Equal(expected, NebulaUploadRetryPolicy.IsTerminal(attempt));
    }
}
