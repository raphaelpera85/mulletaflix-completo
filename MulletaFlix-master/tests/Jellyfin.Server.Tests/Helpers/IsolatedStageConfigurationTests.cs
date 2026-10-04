using System;
using MulletaFlix.Server.Helpers;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public class IsolatedStageConfigurationTests
{
    [Theory]
    [InlineData(null, 3306)]
    [InlineData("", 3306)]
    [InlineData("33127", 33127)]
    [InlineData("65535", 65535)]
    public void ResolveDatabasePort_UsesDefaultOrConfiguredPort(string? configuredPort, int expectedPort)
    {
        Assert.Equal(expectedPort, MariaDbProcessManager.ResolveDatabasePort(configuredPort));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    [InlineData("-1")]
    public void ResolveDatabasePort_RejectsInvalidPort(string configuredPort)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MariaDbProcessManager.ResolveDatabasePort(configuredPort));
    }

    [Fact]
    public void GetInstanceMutexName_UsesProductionMutexUnlessExplicitE2eMode()
    {
        Assert.Equal("Global\\MulletaFlix.Server", Program.GetInstanceMutexName(false, 123));
        Assert.Equal("Local\\MulletaFlix.Server.E2E.123", Program.GetInstanceMutexName(true, 123));
    }
}
