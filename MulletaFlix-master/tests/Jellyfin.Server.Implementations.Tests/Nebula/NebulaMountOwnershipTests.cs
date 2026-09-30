using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMountOwnershipTests
{
    [Fact]
    public void StopOwnedProcess_StopsOnlyTheRegisteredProcess()
    {
        using var foreign = StartTestProcess();
        using var owned = StartTestProcess();
        using var ownedObserver = Process.GetProcessById(owned.Id);
        using var manager = CreateManager();
        SetProcess(manager, owned);
        try
        {
            Assert.True(StopOwnedProcess(manager));
            Assert.Null(GetProcess(manager));
            Assert.True(ownedObserver.HasExited);
            Assert.False(foreign.HasExited);
        }
        finally
        {
            StopTestProcess(foreign);
            StopTestProcess(ownedObserver);
        }
    }

    [Fact]
    public async Task UnmountWithoutOwnedProcess_PreservesOtherProcesses()
    {
        using var foreign = StartTestProcess();
        using var manager = CreateManager();
        try
        {
            Assert.True(await manager.UnmountDriveNAsync(CancellationToken.None));
            Assert.False(foreign.HasExited);
        }
        finally
        {
            StopTestProcess(foreign);
        }
    }

    [Fact]
    public void FailedStop_PreservesTheRegisteredHandleForRetry()
    {
        using var unstarted = new Process();
        var manager = CreateManager();
        SetProcess(manager, unstarted);
        try
        {
            Assert.False(StopOwnedProcess(manager));
            Assert.Same(unstarted, GetProcess(manager));
        }
        finally
        {
            SetProcess(manager, null);
            manager.Dispose();
        }
    }

    [Fact]
    public async Task DisposeAsync_DrainsOwnedProcessOutputBeforeReleasingHandle()
    {
        using var process = StartOutputProcess();
        using var observer = Process.GetProcessById(process.Id);
        var manager = CreateManager();
        SetProcess(manager, process);
        SetField(manager, "_rcloneStdoutTask", DrainOutput(process.StandardOutput));
        SetField(manager, "_rcloneStderrTask", DrainOutput(process.StandardError));

        await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(observer.HasExited);
        Assert.Null(GetProcess(manager));
        Assert.Null(GetField(manager, "_rcloneStdoutTask"));
        Assert.Null(GetField(manager, "_rcloneStderrTask"));
    }

    [Fact]
    public async Task DisposeAsync_ReleasesProcessWhenOutputReaderHasFaultedAndCompleted()
    {
        using var process = StartTestProcess();
        using var observer = Process.GetProcessById(process.Id);
        var manager = CreateManager();
        SetProcess(manager, process);
        SetField(manager, "_rcloneStdoutTask", Task.FromException(new IOException("simulated reader failure")));
        SetField(manager, "_rcloneStderrTask", Task.CompletedTask);

        await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(observer.HasExited);
        Assert.Null(GetProcess(manager));
        Assert.Null(GetField(manager, "_rcloneStdoutTask"));
        Assert.Null(GetField(manager, "_rcloneStderrTask"));
    }

    private static NebulaFtpManager CreateManager() => new(
        Mock.Of<IServerConfigurationManager>(), NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

    private static bool StopOwnedProcess(NebulaFtpManager manager) =>
        (bool)typeof(NebulaFtpManager).GetMethod("StopOwnedRcloneProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null)!;

    private static Process? GetProcess(NebulaFtpManager manager) =>
        (Process?)typeof(NebulaFtpManager).GetField("_rcloneProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager);

    private static void SetProcess(NebulaFtpManager manager, Process? process) =>
        typeof(NebulaFtpManager).GetField("_rcloneProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, process);

    private static object? GetField(NebulaFtpManager manager, string name) =>
        typeof(NebulaFtpManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager);

    private static void SetField(NebulaFtpManager manager, string name, object? value) =>
        typeof(NebulaFtpManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);

    private static Task DrainOutput(StreamReader reader) =>
        (Task)typeof(NebulaFtpManager).GetMethod("DrainMountOutputAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { reader, (Action<string>)(_ => { }) })!;

    private static Process StartOutputProcess()
    {
        var info = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add("Write-Output ready; [Console]::Error.WriteLine('ready-error'); Start-Sleep -Seconds 60");
        }
        else
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("printf 'ready\\n'; printf 'ready-error\\n' >&2; sleep 60");
        }

        return Process.Start(info) ?? throw new InvalidOperationException("Test process did not start.");
    }

    private static Process StartTestProcess()
    {
        var info = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add("Start-Sleep -Seconds 60");
        }
        else
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("sleep 60");
        }

        return Process.Start(info) ?? throw new InvalidOperationException("Test process did not start.");
    }

    private static void StopTestProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(5000));
        }
    }
}
