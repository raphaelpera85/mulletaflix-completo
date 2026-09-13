using MulletaFlix.Server.Helpers;
using Xunit;

namespace MulletaFlix.Server.Tests;

public sealed class PortBindingRecoveryTests
{
    [Fact]
    public void CanTerminateProcess_RequiresMatchingExecutable()
    {
        Assert.True(PortBindingRecovery.CanTerminateProcess(
            @"C:\Program Files\MulletaFlix\Server\MulletaFlix.exe",
            @"c:\program files\mulletaflix\server\mulletaflix.exe"));
        Assert.False(PortBindingRecovery.CanTerminateProcess(
            @"C:\Program Files\MulletaFlix\Server\MulletaFlix.exe",
            @"C:\Windows\System32\svchost.exe"));
    }

    [Fact]
    public void CanTerminateProcess_RefusesUnknownPaths()
    {
        Assert.False(PortBindingRecovery.CanTerminateProcess(null, @"C:\Program Files\MulletaFlix\Server\MulletaFlix.exe"));
        Assert.False(PortBindingRecovery.CanTerminateProcess(@"C:\Program Files\MulletaFlix\Server\MulletaFlix.exe", null));
        Assert.False(PortBindingRecovery.CanTerminateProcess(string.Empty, string.Empty));
    }
}
