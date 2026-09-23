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

    [Fact]
    public void CanTerminateProcess_RefusesToKillLiveServerFromTestHost()
    {
        // Regression: a matching process *name* used to be enough to authorise termination, so
        // running the server test suite on a machine with a live server killed that server.
        Assert.False(PortBindingRecovery.CanTerminateProcess(
            @"C:\Program Files\dotnet\dotnet.exe",
            null,
            "MulletaFlix"));

        Assert.False(PortBindingRecovery.CanTerminateProcess(
            @"D:\repo\tests\Jellyfin.Server.Tests\bin\Debug\net10.0\testhost.exe",
            null,
            "MulletaFlix"));
    }

    [Fact]
    public void CanTerminateProcess_AllowsServerToReclaimItsOwnOrphans()
    {
        // The legitimate recovery path: a server executable reclaiming an orphaned server whose
        // module path is unreadable (elevation or bitness mismatch).
        Assert.True(PortBindingRecovery.CanTerminateProcess(
            @"C:\Program Files\MulletaFlix\Server\MulletaFlix.exe",
            null,
            "MulletaFlix"));
    }
}
