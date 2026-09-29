using System;
using System.IO;
using Emby.Server.Implementations;
using Emby.Server.Implementations.Configuration;
using Emby.Server.Implementations.Serialization;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Configuration;

/// <summary>
/// Verifies that writing the server configuration file restricts its permissions to the
/// owner on Unix. Config files (including plugin-specific ones such as NebulaFtpConfiguration)
/// can hold plaintext secrets, and default OS permissions on most Linux distributions leave
/// them world-readable to any local account.
/// </summary>
public sealed class ServerConfigurationManagerFilePermissionsTests : IDisposable
{
    private readonly string _tempDataPath;

    public ServerConfigurationManagerFilePermissionsTests()
    {
        _tempDataPath = Path.Combine(Path.GetTempPath(), "mulletaflix-config-perm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDataPath);
    }

    [Fact]
    public void SaveConfiguration_RestrictsFilePermissionsToOwnerOnUnix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file mode bits do not apply on Windows; NTFS ACLs already restrict access by default.");

        var applicationPaths = new ServerApplicationPaths(
            _tempDataPath,
            Path.Combine(_tempDataPath, "logs"),
            Path.Combine(_tempDataPath, "config"),
            Path.Combine(_tempDataPath, "cache"),
            Path.Combine(_tempDataPath, "web"));

        var manager = new ServerConfigurationManager(applicationPaths, NullLoggerFactory.Instance, new MyXmlSerializer());

        manager.SaveConfiguration();

        var configPath = applicationPaths.SystemConfigurationFilePath;
        Assert.True(File.Exists(configPath));

        var mode = File.GetUnixFileMode(configPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public void SaveConfiguration_DoesNotThrowOnAnyPlatform()
    {
        // Regardless of OS, saving configuration (and thus attempting the permission restriction)
        // must never throw — a failed chmod should be logged, not crash the save.
        var applicationPaths = new ServerApplicationPaths(
            _tempDataPath,
            Path.Combine(_tempDataPath, "logs"),
            Path.Combine(_tempDataPath, "config"),
            Path.Combine(_tempDataPath, "cache"),
            Path.Combine(_tempDataPath, "web"));

        var manager = new ServerConfigurationManager(applicationPaths, NullLoggerFactory.Instance, new MyXmlSerializer());

        var exception = Record.Exception(() => manager.SaveConfiguration());

        Assert.Null(exception);
        Assert.True(File.Exists(applicationPaths.SystemConfigurationFilePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDataPath))
            {
                Directory.Delete(_tempDataPath, true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup; leftover temp dirs don't affect other tests.
        }
    }
}
