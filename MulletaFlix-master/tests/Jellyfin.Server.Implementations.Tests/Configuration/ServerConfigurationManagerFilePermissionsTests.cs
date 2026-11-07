using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Emby.Server.Implementations;
using Emby.Server.Implementations.AppBase;
using Emby.Server.Implementations.Configuration;
using Emby.Server.Implementations.Serialization;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    [Fact]
    public void SaveConfiguration_FailedSerializationPreservesExistingFile()
    {
        var serializer = new Mock<IXmlSerializer>();
        var innerSerializer = new MyXmlSerializer();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((configuration, path) => innerSerializer.SerializeToFile(configuration, path));
        var applicationPaths = new ServerApplicationPaths(
            _tempDataPath,
            Path.Combine(_tempDataPath, "logs"),
            Path.Combine(_tempDataPath, "config"),
            Path.Combine(_tempDataPath, "cache"),
            Path.Combine(_tempDataPath, "web"));
        var manager = new ServerConfigurationManager(applicationPaths, NullLoggerFactory.Instance, serializer.Object);
        var configPath = applicationPaths.SystemConfigurationFilePath;
        var original = File.ReadAllBytes(configPath);

        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new IOException("test serialization failure"));

        Assert.Throws<IOException>(() => manager.SaveConfiguration());
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(configPath)!, "*.tmp"));
    }

    [Fact]
    public void SaveConfiguration_PreservesExistingWindowsAcl()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACLs are only available on Windows.");

        var applicationPaths = new ServerApplicationPaths(
            _tempDataPath,
            Path.Combine(_tempDataPath, "logs"),
            Path.Combine(_tempDataPath, "config"),
            Path.Combine(_tempDataPath, "cache"),
            Path.Combine(_tempDataPath, "web"));
        var manager = new ServerConfigurationManager(applicationPaths, NullLoggerFactory.Instance, new MyXmlSerializer());
        var configPath = applicationPaths.SystemConfigurationFilePath;
        var configFile = new FileInfo(configPath);
        var security = configFile.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Current Windows identity has no SID."),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        configFile.SetAccessControl(security);
        var originalAcl = configFile.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);

        manager.SaveConfiguration();

        var savedAcl = configFile.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        Assert.Equal(originalAcl, savedAcl);
    }

    [Fact]
    public void SaveConfiguration_RestrictsWindowsTemporaryBeforeSerialization()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACLs are only available on Windows.");

        var serializer = new Mock<IXmlSerializer>();
        var innerSerializer = new MyXmlSerializer();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((configuration, path) =>
            {
                var security = new FileInfo(path).GetAccessControl();
                Assert.True(security.AreAccessRulesProtected);
                var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .ToArray();
                var serviceIdentity = WindowsIdentity.GetCurrent().User;
                Assert.NotNull(serviceIdentity);
                Assert.Contains(rules, rule => rule.IdentityReference.Equals(serviceIdentity)
                    && rule.AccessControlType == AccessControlType.Allow);
                Assert.All(rules, rule => Assert.False(rule.IsInherited));

                innerSerializer.SerializeToFile(configuration, path);
            });
        var applicationPaths = new ServerApplicationPaths(
            _tempDataPath,
            Path.Combine(_tempDataPath, "logs"),
            Path.Combine(_tempDataPath, "config"),
            Path.Combine(_tempDataPath, "cache"),
            Path.Combine(_tempDataPath, "web"));
        var manager = new ServerConfigurationManager(applicationPaths, NullLoggerFactory.Instance, serializer.Object);

        manager.SaveConfiguration();

        Assert.True(File.Exists(applicationPaths.SystemConfigurationFilePath));
    }

    [Fact]
    public void RestoreConfigurationBackup_OnlyRestoresWhenDestinationIsMissing()
    {
        var destinationPath = Path.Combine(_tempDataPath, "server.xml");
        var backupPath = Path.Combine(_tempDataPath, "server.xml.backup");
        File.WriteAllText(backupPath, "previous configuration");

        BaseConfigurationManager.RestoreConfigurationBackupIfDestinationMissing(destinationPath, backupPath);

        Assert.Equal("previous configuration", File.ReadAllText(destinationPath));
        Assert.False(File.Exists(backupPath));

        File.WriteAllText(backupPath, "older configuration");
        File.WriteAllText(destinationPath, "new configuration");

        BaseConfigurationManager.RestoreConfigurationBackupIfDestinationMissing(destinationPath, backupPath);

        Assert.Equal("new configuration", File.ReadAllText(destinationPath));
        Assert.Equal("older configuration", File.ReadAllText(backupPath));
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
