using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Xunit;

namespace Jellyfin.Server.Tests;

public sealed class ProgramStartupRecoveryTests
{
    [Fact]
    public async Task StartupRecoveryGate_UsesProductionBackupRecoveryCallbackBeforeInitialization_WhenNoRestoreIsPending()
    {
        if (OperatingSystem.IsMacOS())
        {
            Assert.Skip("Backup restore recovery requires Windows or Linux.");
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mulletaflix-startup-recovery-tests",
            Guid.NewGuid().ToString("N"));
        var programDataPath = Path.Combine(root, "program-data");
        var restoreRootsPath = Path.Combine(root, "restore-roots");
        try
        {
            CreatePrivateRestoreTestRoot(programDataPath);
            var paths = Mock.Of<IServerApplicationPaths>(applicationPaths =>
                applicationPaths.ProgramDataPath == programDataPath
                && applicationPaths.ConfigurationDirectoryPath == Path.Combine(restoreRootsPath, "config")
                && applicationPaths.DataPath == Path.Combine(restoreRootsPath, "data")
                && applicationPaths.RootFolderPath == Path.Combine(restoreRootsPath, "library")
                && applicationPaths.InternalMetadataPath == Path.Combine(restoreRootsPath, "metadata")
                && applicationPaths.DefaultInternalMetadataPath == Path.Combine(restoreRootsPath, "default-metadata"));
            using var backupService = new BackupService(
                NullLogger<BackupService>.Instance,
                null!,
                null!,
                paths,
                null!,
                Mock.Of<IHostApplicationLifetime>());
            var initialized = false;
            var recoveryCallbackCompleted = false;
            var startupMigrationApplied = false;
            var result = await MulletaFlix.Server.Program.RunAfterBackupRecoveryAsync(
                backupService,
                recoveredPath =>
                {
                    Assert.Null(recoveredPath);
                    recoveryCallbackCompleted = true;
                    return Task.FromResult(true);
                },
                () =>
                {
                    Assert.True(recoveryCallbackCompleted);
                    startupMigrationApplied = true;
                    return Task.CompletedTask;
                },
                () =>
                {
                    Assert.True(recoveryCallbackCompleted);
                    Assert.True(startupMigrationApplied);
                    initialized = true;
                    return Task.CompletedTask;
                });

            Assert.True(result);
            Assert.True(initialized);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("failure", 1, false)]
    [InlineData("rejected", 0, false)]
    [InlineData("success", 0, true)]
    public async Task StartupRecoveryGate_ControlsChildProcessExitAndInitialization(
        string scenario,
        int expectedExitCode,
        bool expectedInitialized)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "MulletaFlix.Server.StartupRecoveryProbe.dll");
        var markerPath = Path.Combine(Path.GetTempPath(), $"mulletaflix-startup-{Guid.NewGuid():N}.marker");
        try
        {
            using var process = StartProbe(probePath, scenario, markerPath);
            var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await WaitForProbeExitAsync(process, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var output = await standardError + await standardOutput;

            Assert.Equal(expectedExitCode, process.ExitCode);
            Assert.Equal(expectedInitialized, File.Exists(markerPath));
            if (expectedExitCode != 0)
            {
                Assert.Contains("recovery failed", output, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    [Fact]
    public async Task StartupRecoveryProbe_KillsChildThatExceedsTimeout()
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "MulletaFlix.Server.StartupRecoveryProbe.dll");
        var markerPath = Path.Combine(Path.GetTempPath(), $"mulletaflix-startup-{Guid.NewGuid():N}.marker");
        try
        {
            using var process = StartProbe(probePath, "hang", markerPath);

            await Assert.ThrowsAsync<TimeoutException>(
                () => WaitForProbeExitAsync(process, TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken));

            Assert.True(process.HasExited);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    private static Process StartProbe(string probePath, string scenario, string markerPath)
        => Process.Start(new ProcessStartInfo("dotnet")
        {
            ArgumentList = { probePath, scenario, markerPath },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Unable to start the startup recovery probe.");

    private static async Task WaitForProbeExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException) when (process.HasExited)
        {
            return;
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The probe exited between HasExited and Kill.
                }
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    private static void CreatePrivateRestoreTestRoot(string root)
    {
        Directory.CreateDirectory(root);

        if (OperatingSystem.IsWindows())
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User
                ?? throw new UnauthorizedAccessException("The restore fixture has no current user SID.");
            var security = new System.Security.AccessControl.DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(user);
            foreach (var sid in new[]
            {
                user,
                new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null),
                new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null)
            })
            {
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    sid,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                    System.Security.AccessControl.PropagationFlags.None,
                    System.Security.AccessControl.AccessControlType.Allow));
            }

            new DirectoryInfo(root).SetAccessControl(security);
        }
        else if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void ShouldWaitForSetupServerAfterStartupFailure_OnlyForUnrelatedEarlyMediaServerFailures(
        bool configurationCompleted,
        bool restoreRecoveryFailed,
        bool isMediaServerMode,
        bool expected)
    {
        Assert.Equal(
            expected,
            MulletaFlix.Server.Program.ShouldWaitForSetupServerAfterStartupFailure(
                configurationCompleted,
                restoreRecoveryFailed,
                isMediaServerMode));
    }
}
