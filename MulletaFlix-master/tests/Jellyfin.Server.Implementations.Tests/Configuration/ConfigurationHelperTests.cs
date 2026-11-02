using System.IO;
using System.Text;
using Emby.Server.Implementations;
using Emby.Server.Implementations.AppBase;
using Emby.Server.Implementations.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Configuration;

public sealed class ConfigurationHelperTests
{
    [Fact]
    public void GetXmlConfiguration_UsesProvidedPersistenceWriterWhenSerializedBytesChange()
    {
        var path = Path.GetTempFileName();
        const string original = "old configuration";
        const string updated = "updated configuration";
        try
        {
            File.WriteAllText(path, original);

            var configuration = new LibraryOptions();
            var serializer = new Mock<IXmlSerializer>();
            serializer
                .Setup(instance => instance.DeserializeFromBytes(typeof(LibraryOptions), It.IsAny<byte[]>()))
                .Returns(configuration);
            serializer
                .Setup(instance => instance.SerializeToStream(configuration, It.IsAny<Stream>()))
                .Callback<object, Stream>((_, stream) => stream.Write(Encoding.UTF8.GetBytes(updated)));

            object? persistedConfiguration = null;
            string? persistedPath = null;
            var result = ConfigurationHelper.GetXmlConfiguration(
                typeof(LibraryOptions),
                path,
                serializer.Object,
                (value, targetPath) =>
                {
                    persistedConfiguration = value;
                    persistedPath = targetPath;
                    File.WriteAllText(targetPath, updated);
                });

            Assert.Same(configuration, result);
            Assert.Same(configuration, persistedConfiguration);
            Assert.Equal(path, persistedPath);
            Assert.Equal(updated, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetXmlConfiguration_DoesNotPersistWhenSerializedBytesAreUnchanged()
    {
        var path = Path.GetTempFileName();
        const string contents = "unchanged configuration";
        try
        {
            File.WriteAllText(path, contents);

            var configuration = new LibraryOptions();
            var serializer = new Mock<IXmlSerializer>();
            serializer
                .Setup(instance => instance.DeserializeFromBytes(typeof(LibraryOptions), It.IsAny<byte[]>()))
                .Returns(configuration);
            serializer
                .Setup(instance => instance.SerializeToStream(configuration, It.IsAny<Stream>()))
                .Callback<object, Stream>((_, stream) => stream.Write(Encoding.UTF8.GetBytes(contents)));

            var persistCalled = false;
            ConfigurationHelper.GetXmlConfiguration(
                typeof(LibraryOptions),
                path,
                serializer.Object,
                (_, _) => persistCalled = true);

            Assert.False(persistCalled);
            Assert.Equal(contents, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetXmlConfiguration_PublicOverloadUsesProtectedPersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "configuration-helper-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.xml");
        File.WriteAllText(path, "old configuration");

        var configuration = new LibraryOptions();
        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.DeserializeFromBytes(typeof(LibraryOptions), It.IsAny<byte[]>()))
            .Returns(configuration);
        serializer
            .Setup(instance => instance.SerializeToStream(configuration, It.IsAny<Stream>()))
            .Callback<object, Stream>((_, stream) => stream.Write(Encoding.UTF8.GetBytes("normalized configuration")));
        var serializedPaths = new System.Collections.Generic.List<string>();
        serializer
            .Setup(instance => instance.SerializeToFile(configuration, It.IsAny<string>()))
            .Callback<object, string>((_, targetPath) =>
            {
                serializedPaths.Add(targetPath);
                File.WriteAllText(targetPath, "normalized configuration");
            });

        try
        {
            Assert.Same(configuration, ConfigurationHelper.GetXmlConfiguration(typeof(LibraryOptions), path, serializer.Object));

            Assert.Contains(serializedPaths, targetPath => targetPath.EndsWith(".tmp", System.StringComparison.Ordinal));
            Assert.Equal("normalized configuration", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ServerConfigurationBootstrap_RewritesThroughProtectedTemporaryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "config-bootstrap-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        var paths = new ServerApplicationPaths(
            root,
            Path.Combine(root, "logs"),
            Path.Combine(root, "config"),
            Path.Combine(root, "cache"),
            Path.Combine(root, "web"));
        Directory.CreateDirectory(paths.ConfigurationDirectoryPath);
        File.WriteAllText(paths.SystemConfigurationFilePath, "legacy configuration");
        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.DeserializeFromBytes(typeof(ServerConfiguration), It.IsAny<byte[]>()))
            .Returns(new ServerConfiguration());
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Callback<object, Stream>((_, stream) => stream.Write(Encoding.UTF8.GetBytes("normalized configuration")));
        var serializedPaths = new System.Collections.Generic.List<string>();
        serializer
            .Setup(instance => instance.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((_, targetPath) =>
            {
                serializedPaths.Add(targetPath);
                File.WriteAllText(targetPath, "normalized configuration");
            });

        try
        {
            var manager = new ServerConfigurationManager(paths, NullLoggerFactory.Instance, serializer.Object);

            Assert.NotNull(manager.Configuration);
            Assert.Contains(serializedPaths, targetPath => targetPath.EndsWith(".tmp", System.StringComparison.Ordinal));
            Assert.Equal("normalized configuration", File.ReadAllText(paths.SystemConfigurationFilePath));
            Assert.Empty(Directory.GetFiles(paths.ConfigurationDirectoryPath, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
