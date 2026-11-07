using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Xunit;

namespace MulletaFlix.Common.Tests.Plugins;

public sealed class BasePluginConfigurationPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersistText_PromotesProtectedTemporaryFile(bool destinationExists)
    {
        var root = Path.Combine(Path.GetTempPath(), "protected-text-" + Path.GetRandomFileName());
        var path = Path.Combine(root, "update_state.json");
        const string expected = "{\"DownloadedVersion\":\"12.1.46\",\"InstallState\":\"ReadyToApply\"}";

        if (destinationExists)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "previous state");
        }

        try
        {
            ProtectedConfigurationWriter.PersistText(expected, path, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(expected, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PersistText_CancellationPreservesExistingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "protected-text-cancel-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "update_state.json");
        File.WriteAllText(path, "previous state");
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(() => ProtectedConfigurationWriter.PersistText("new state", path, cancellationToken: cancellation.Token));

            Assert.Equal("previous state", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void PersistText_ReplacementFailurePreservesExistingFileAndCleansTemporary()
    {
        var root = Path.Combine(Path.GetTempPath(), "protected-text-replace-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "update_state.json");
        File.WriteAllText(path, "previous state");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

        try
        {
            Assert.ThrowsAny<UnauthorizedAccessException>(() => ProtectedConfigurationWriter.PersistText("new state", path, restrictExistingWindowsAcl: true, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("previous state", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveConfiguration_PartialSerializationFailurePreservesExistingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-config-" + Path.GetRandomFileName());
        var configDirectory = Path.Combine(root, "configurations");
        Directory.CreateDirectory(configDirectory);
        var applicationPaths = new TestApplicationPaths(root, configDirectory);
        var serializer = new TestXmlSerializer
        {
            Serialize = path =>
            {
                File.WriteAllText(path, "partial");
                throw new IOException("serialization failed");
            }
        };
        var plugin = new TestPlugin(applicationPaths, serializer);
        File.WriteAllText(plugin.ConfigurationFilePath, "previous configuration");

        try
        {
            Assert.Throws<IOException>(() => plugin.SaveConfiguration(new BasePluginConfiguration()));

            Assert.Equal("previous configuration", File.ReadAllText(plugin.ConfigurationFilePath));
            Assert.EndsWith(".tmp", serializer.LastSerializedPath, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(configDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveConfiguration_PromotesProtectedTemporaryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-config-" + Path.GetRandomFileName());
        var configDirectory = Path.Combine(root, "configurations");
        var applicationPaths = new TestApplicationPaths(root, configDirectory);
        var serializer = new TestXmlSerializer
        {
            Serialize = path => File.WriteAllText(path, "saved configuration")
        };
        var plugin = new TestPlugin(applicationPaths, serializer);

        try
        {
            plugin.SaveConfiguration(new BasePluginConfiguration());

            Assert.Equal("saved configuration", File.ReadAllText(plugin.ConfigurationFilePath));
            Assert.EndsWith(".tmp", serializer.LastSerializedPath, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(configDirectory, "*.tmp"));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(plugin.ConfigurationFilePath));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void SaveConfiguration_RestrictsExistingWindowsAcl()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACLs are only available on Windows.");

        var root = Path.Combine(Path.GetTempPath(), "plugin-config-" + Path.GetRandomFileName());
        var configDirectory = Path.Combine(root, "configurations");
        var applicationPaths = new TestApplicationPaths(root, configDirectory);
        var serializer = new TestXmlSerializer { Serialize = path => File.WriteAllText(path, "saved configuration") };
        var plugin = new TestPlugin(applicationPaths, serializer);
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(plugin.ConfigurationFilePath, "previous configuration");

        try
        {
            var file = new FileInfo(plugin.ConfigurationFilePath);
            var security = file.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.Read,
                AccessControlType.Allow));
            file.SetAccessControl(security);

            plugin.SaveConfiguration(new BasePluginConfiguration());

            var rules = file.GetAccessControl().GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .ToArray();
            Assert.DoesNotContain(rules, rule => rule.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.WorldSid, null)));
            Assert.Equal("saved configuration", File.ReadAllText(plugin.ConfigurationFilePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PersistXml_PartialWriteFailurePreservesExistingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "xml-config-" + Path.GetRandomFileName());
        var path = Path.Combine(root, "network.xml");
        Directory.CreateDirectory(root);
        File.WriteAllText(path, "previous configuration");

        try
        {
            Assert.Throws<IOException>(() => ProtectedConfigurationWriter.PersistXml(path, stream =>
            {
                stream.Write("partial"u8);
                throw new IOException("serialization failed");
            }));

            Assert.Equal("previous configuration", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestPlugin : BasePlugin<BasePluginConfiguration>
    {
        public TestPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
        }

        public override string Name => "Configuration persistence test";
    }

    private sealed class TestXmlSerializer : IXmlSerializer
    {
        public Action<string>? Serialize { get; init; }

        public string? LastSerializedPath { get; private set; }

        public object DeserializeFromStream(Type type, Stream stream) => throw new NotImplementedException();

        public void SerializeToStream(object obj, Stream stream) => throw new NotImplementedException();

        public void SerializeToFile(object obj, string file)
        {
            LastSerializedPath = file;
            Serialize!(file);
        }

        public object DeserializeFromFile(Type type, string file) => throw new NotImplementedException();

        public object DeserializeFromBytes(Type type, byte[] buffer) => throw new NotImplementedException();
    }

    private sealed class TestApplicationPaths : IApplicationPaths
    {
        public TestApplicationPaths(string root, string pluginConfigurationsPath)
        {
            PluginConfigurationsPath = pluginConfigurationsPath;
            PluginsPath = Path.Combine(root, "plugins");
        }

        public string ProgramDataPath => string.Empty;

        public string WebPath => string.Empty;

        public string ProgramSystemPath => string.Empty;

        public string DataPath => string.Empty;

        public string ImageCachePath => string.Empty;

        public string PluginsPath { get; }

        public string PluginConfigurationsPath { get; }

        public string LogDirectoryPath => string.Empty;

        public string ConfigurationDirectoryPath => string.Empty;

        public string SystemConfigurationFilePath => string.Empty;

        public string CachePath => string.Empty;

        public string TempDirectory => string.Empty;

        public string VirtualDataPath => string.Empty;

        public string TrickplayPath => string.Empty;

        public string BackupPath => string.Empty;

        public void MakeSanityCheckOrThrow() => throw new NotImplementedException();

        public void CreateAndCheckMarker(string path, string markerName, bool recursive = false) => throw new NotImplementedException();
    }
}
