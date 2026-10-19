using System;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMountRuntimeHealthTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void HasLiveFtpRuntime_UsesOnlyLiveHostOrProcess(bool ftpHostRunning, bool pythonProcessRunning, bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.HasLiveFtpRuntime(ftpHostRunning, pythonProcessRunning));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void ShouldRetryMountAfterHelperExit_PausesOnCleanOrOwnedMountExit(int exitCode, bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.ShouldRetryMountAfterHelperExit(exitCode));
    }
}
