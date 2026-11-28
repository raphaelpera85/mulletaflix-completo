using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UpdateInfoControllerTests
{
    [Fact]
    public void TryNormalizeReleaseVersion_UsesThreePartServerVersion()
    {
        Assert.True(UpdateInfoController.TryNormalizeReleaseVersion("12.1.58.0", out var version));
        Assert.Equal("12.1.58", version);
        Assert.False(UpdateInfoController.TryNormalizeReleaseVersion("12.1", out _));
        Assert.False(UpdateInfoController.TryNormalizeReleaseVersion("not-a-version", out _));
    }

    [Fact]
    public void BuildUpdaterArguments_PassesVerifiedTargetVersion()
    {
        var arguments = UpdateInfoController.BuildUpdaterArguments(
            @"C:\ProgramData\MulletaFlix\updates\apply-update.ps1",
            @"C:\Program Files\MulletaFlix\Server",
            @"C:\ProgramData\MulletaFlix\updates\extracted",
            1234,
            @"C:\ProgramData\MulletaFlix",
            "12.1.58.0");

        Assert.Contains("-TargetVersion \"12.1.58\"", arguments, StringComparison.Ordinal);
        Assert.Contains("-InstallDirectory \"C:\\Program Files\\MulletaFlix\\Server\"", arguments, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => UpdateInfoController.BuildUpdaterArguments("script.ps1", "install", "source", 1, "data", "invalid"));
    }

    [Fact]
    public void EmbeddedUpdaterScript_SynchronizesVersionMetadataInIsolatedRegistryKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string registrySubKey = @"Software\MulletaFlix\UpdateVersionMetadataTests";
        var registryPath = "HKCU:\\" + registrySubKey;
        var tempRoot = Path.Combine(Path.GetTempPath(), "mulleta_upd_version_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(registrySubKey))
            {
                Assert.NotNull(key);
                key!.SetValue("DisplayName", "MulletaFlix Server 12.1.5");
                key.SetValue("DisplayVersion", "12.1.5");
            }

            var scriptPath = Path.Combine(tempRoot, "apply-update.ps1");
            File.WriteAllText(
                scriptPath,
                "$ErrorActionPreference = 'Stop'" + Environment.NewLine
                + UpdateInfoController.EmbeddedUpdaterVersionMetadataFunctions
                + Environment.NewLine
                + $"Set-InstalledVersionMetadata -TargetVersion '12.1.58.0' -RegistryPath '{registryPath}' | Out-Null");
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"Embedded updater script failed: {stdout} {stderr}");

            using var updatedKey = Registry.CurrentUser.OpenSubKey(registrySubKey);
            Assert.NotNull(updatedKey);
            Assert.Equal("12.1.58", updatedKey!.GetValue("DisplayVersion"));
            Assert.Equal("MulletaFlix Server 12.1.58", updatedKey.GetValue("DisplayName"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(registrySubKey, throwOnMissingSubKey: false);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void EmbeddedUpdater_SynchronizesRegistryVersionOnlyAfterSuccessfulCopy()
    {
        var script = UpdateInfoController.EmbeddedUpdaterScript;
        Assert.DoesNotContain("TestOnly", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UninstallRegistryPath", script, StringComparison.Ordinal);
        var serviceStop = script.IndexOf("Stop-Service -Name \"MulletaFlixServer\"", StringComparison.Ordinal);
        var stagedCopy = script.IndexOf("Copy-UpdateToStage -SourceDirectory $UpdateSourceDirectory -StageDirectory $stageDirectory", StringComparison.Ordinal);
        var stagedValidation = script.IndexOf("Assert-UpdateVersion -InstallDirectory $stageDirectory -TargetVersion $TargetVersion", StringComparison.Ordinal);
        var stageAssembly = script.IndexOf("Copy-InstalledFilesToStage -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory", StringComparison.Ordinal);
        var assembledValidation = script.IndexOf("Assert-UpdateVersion -InstallDirectory $assembledDirectory -TargetVersion $TargetVersion", StringComparison.Ordinal);
        var installSwap = script.IndexOf("Move-StageIntoInstall -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory", StringComparison.Ordinal);
        Assert.True(stagedCopy >= 0 && stagedValidation > stagedCopy && serviceStop > stagedValidation && stageAssembly > serviceStop && assembledValidation > stageAssembly && installSwap > assembledValidation);
        Assert.Contains("Move-StageIntoInstall", script, StringComparison.Ordinal);
        Assert.Contains("Restore-PreviousInstall", script, StringComparison.Ordinal);
        Assert.Contains("the installed server remains active", script, StringComparison.Ordinal);
        var targetExitCheck = script.IndexOf("Target server process $ProcessId did not exit within 30 seconds", StringComparison.Ordinal);
        var processStop = script.IndexOf("Stop-InstalledProcesses -InstallDirectory $InstallDirectory", StringComparison.Ordinal);
        var livenessCheck = script.IndexOf("Wait-ForServerLiveness -LivenessUri $livenessUri", StringComparison.Ordinal);
        var backupCleanup = script.IndexOf("Remove-Item -LiteralPath $backupDirectory -Recurse -Force", StringComparison.Ordinal);
        Assert.True(targetExitCheck >= 0 && processStop > targetExitCheck);
        Assert.True(livenessCheck > installSwap && backupCleanup > livenessCheck);
        Assert.DoesNotContain("Get-Process -Name $procName", script, StringComparison.Ordinal);

        Assert.Contains("Start-Service -Name 'MulletaFlixServer'", script, StringComparison.Ordinal);
        Assert.Contains("Start-Process -FilePath $exePath", script, StringComparison.Ordinal);
        Assert.Contains("Start-Process -FilePath $trayPath", script, StringComparison.Ordinal);
        Assert.Contains("Start-Service -Name 'MulletaFlixServer' -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.Contains("$runningService.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Service -Name 'MulletaFlixServer' -ErrorAction SilentlyContinue", script, StringComparison.Ordinal);
        Assert.Contains("$serverProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $InstallDirectory -WindowStyle Hidden -PassThru -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.Contains("if ($serverProcess.HasExited)", script, StringComparison.Ordinal);
        Assert.Contains("$null = Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory 'mulletaflix-windows-tray') -PassThru -ErrorAction Stop", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbeddedUpdaterScript_ParsesInWindowsPowerShell()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "mulleta_upd_parse_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var updaterPath = Path.Combine(tempRoot, "apply-update.ps1");
            var parserPath = Path.Combine(tempRoot, "parse-script.ps1");
            File.WriteAllText(updaterPath, UpdateInfoController.EmbeddedUpdaterScript);
            File.WriteAllText(parserPath, @"
$tokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($args[0], [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -gt 0) {
    $parseErrors | Format-List | Out-String | Write-Error
    exit 1
}
");

            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", parserPath, updaterPath
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"Embedded updater script did not parse: {stdout} {stderr}");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void IsPreparedUpdateCurrent_RejectsStateForDifferentReleaseMetadata()
    {
        const string checksum = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        Assert.True(UpdateInfoController.IsPreparedUpdateCurrent("ReadyToApply", "12.1.35", checksum, "12.1.35", checksum));
        Assert.False(UpdateInfoController.IsPreparedUpdateCurrent("ReadyToApply", "12.1.35", checksum, "12.1.36", checksum));
        Assert.False(UpdateInfoController.IsPreparedUpdateCurrent("ReadyToApply", "12.1.35", checksum, "12.1.35", "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        Assert.False(UpdateInfoController.IsPreparedUpdateCurrent("Downloading", "12.1.35", checksum, "12.1.35", checksum));
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsTrue_WhenFilesMatchExactly()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            var subUpdate = Path.Combine(updateDir, "sub");
            var subInstall = Path.Combine(installDir, "sub");
            Directory.CreateDirectory(subUpdate);
            Directory.CreateDirectory(subInstall);

            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "binary content 12345");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "binary content 12345");

            File.WriteAllText(Path.Combine(subUpdate, "feature.dll"), "nested binary data");
            File.WriteAllText(Path.Combine(subInstall, "feature.dll"), "nested binary data");

            // Arquivo temporário/script no update deve ser ignorado na checagem
            File.WriteAllText(Path.Combine(updateDir, "apply-update.ps1"), "# updater script");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.True(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsFalse_WhenFilesDifferInContent()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "new version binary content");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "old version binary content");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsFalse_WhenFileMissingInInstall()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "same content");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "same content");

            File.WriteAllText(Path.Combine(updateDir, "NewFeature.dll"), "new file");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void TryParseGitHubRelease_SelectsServerReleaseAndIgnoresAppRelease_WhenGivenReleaseList()
    {
        var json = @"[
            {
                ""tag_name"": ""app-v12.0.2"",
                ""body"": ""Android app release notes"",
                ""assets"": [
                    {
                        ""name"": ""mulletaflix-app-v12.0.2.apk"",
                        ""size"": 7000000,
                        ""browser_download_url"": ""https://github.com/.../app.apk""
                    }
                ]
            },
            {
                ""tag_name"": ""v12.0.2"",
                ""body"": ""Server release notes"",
                ""assets"": [
                    {
                        ""name"": ""mulletaflix-update-win-x64.zip"",
                        ""size"": 105000000,
                        ""browser_download_url"": ""https://github.com/.../mulletaflix-update-win-x64.zip"",
                        ""digest"": ""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa""
                    },
                    {
                        ""name"": ""mulletaflix-app-v12.0.2.apk"",
                        ""size"": 7000000,
                        ""browser_download_url"": ""https://github.com/.../app.apk""
                    }
                ]
            }
        ]";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size, out var checksum);

        Assert.True(success);
        Assert.Equal(new Version(12, 0, 2), version);
        Assert.Equal("Server release notes", changelog);
        Assert.Equal("https://github.com/.../mulletaflix-update-win-x64.zip", archiveUrl);
        Assert.Equal(105000000, size);
        Assert.Equal("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", checksum);
    }

    [Fact]
    public void TryParseGitHubRelease_ParsesSingleReleaseObject_WhenValid()
    {
        var json = @"{
            ""tag_name"": ""v12.0.3"",
            ""body"": ""Single release notes"",
            ""assets"": [
                {
                    ""name"": ""update.zip"",
                    ""size"": 5000,
                        ""browser_download_url"": ""https://example.com/update.zip"",
                        ""digest"": ""sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb""
                }
            ]
        }";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size, out var checksum);

        Assert.True(success);
        Assert.Equal(new Version(12, 0, 3), version);
        Assert.Equal("https://example.com/update.zip", archiveUrl);
        Assert.Equal("sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", checksum);
    }

    [Fact]
    public void TryParseGitHubRelease_ReturnsFalse_WhenNoZipAssetPresent()
    {
        var json = @"{
            ""tag_name"": ""v12.0.3"",
            ""body"": ""Release without zip"",
            ""assets"": [
                {
                    ""name"": ""update.exe"",
                    ""size"": 5000,
                    ""browser_download_url"": ""https://example.com/update.exe""
                }
            ]
        }";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParseGitHubRelease_RejectsZipWithoutValidDigest()
    {
        const string json = "{\"tag_name\":\"v12.1.36\",\"assets\":[{\"name\":\"server.zip\",\"size\":10,\"browser_download_url\":\"https://example.com/server.zip\"}]}";

        Assert.False(UpdateInfoController.TryParseGitHubRelease(json, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void TryParseGitHubRelease_RejectsHttpArchiveUrl()
    {
        const string json = "{\"tag_name\":\"v12.1.36\",\"assets\":[{\"name\":\"server.zip\",\"size\":10,\"browser_download_url\":\"http://example.test/server.zip\",\"digest\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}]}";

        Assert.False(UpdateInfoController.TryParseGitHubRelease(json, out _, out _, out _, out _, out _));
    }
}

