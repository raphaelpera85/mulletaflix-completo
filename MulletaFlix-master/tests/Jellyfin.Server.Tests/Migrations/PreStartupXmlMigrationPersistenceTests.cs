using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Emby.Server.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Migrations.PreStartupRoutines;
using Xunit;

namespace MulletaFlix.Server.Tests.Migrations;

public sealed class PreStartupXmlMigrationPersistenceTests
{
    [Fact]
    public void MigrateNetworkConfiguration_PreservesCertificatePasswordAndProducesValidXml()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-network-migration-" + Path.GetRandomFileName());
        var configPath = Path.Combine(root, "config");
        Directory.CreateDirectory(configPath);
        var paths = CreateApplicationPaths(root, configPath);
        var filePath = Path.Combine(configPath, "network.xml");
        var oldConfiguration = new MigrateNetworkConfiguration.OldNetworkConfiguration
        {
            CertificatePassword = "certificate-secret",
            VirtualInterfaceNames = "vEthernet*"
        };
        var serializer = new XmlSerializer(
            typeof(MigrateNetworkConfiguration.OldNetworkConfiguration),
            new XmlRootAttribute("NetworkConfiguration"));

        try
        {
            using (var stream = File.Create(filePath))
            {
                serializer.Serialize(stream, oldConfiguration);
            }

            new MigrateNetworkConfiguration(paths, NullLoggerFactory.Instance).Perform();

            var document = XDocument.Load(filePath);
            Assert.Equal("certificate-secret", document.Root?.Element("CertificatePassword")?.Value);
            Assert.Equal("veth", document.Root?.Element("VirtualInterfaceNames")?.Element("string")?.Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RenameEnableGroupingIntoCollections_UsesNewElementName()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-system-migration-" + Path.GetRandomFileName());
        var configPath = Path.Combine(root, "config");
        Directory.CreateDirectory(configPath);
        var paths = CreateApplicationPaths(root, configPath);
        var filePath = Path.Combine(configPath, "system.xml");

        try
        {
            File.WriteAllText(filePath, "<ServerConfiguration><EnableGroupingIntoCollections>true</EnableGroupingIntoCollections></ServerConfiguration>");

            new RenameEnableGroupingIntoCollections(paths, NullLoggerFactory.Instance).Perform();

            var document = XDocument.Load(filePath);
            Assert.Equal("true", document.Root?.Element("EnableGroupingMoviesIntoCollections")?.Value);
            Assert.Null(document.Root?.Element("EnableGroupingIntoCollections"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RenameEnableGroupingIntoCollections_PropagatesInvalidXml()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-invalid-system-migration-" + Path.GetRandomFileName());
        var configPath = Path.Combine(root, "config");
        Directory.CreateDirectory(configPath);
        var paths = CreateApplicationPaths(root, configPath);

        try
        {
            File.WriteAllText(Path.Combine(configPath, "system.xml"), "<ServerConfiguration>");

            Assert.Throws<XmlException>(() => new RenameEnableGroupingIntoCollections(paths, NullLoggerFactory.Instance).Perform());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ServerApplicationPaths CreateApplicationPaths(string root, string configurationDirectory)
    {
        return new ServerApplicationPaths(
            root,
            Path.Combine(root, "logs"),
            configurationDirectory,
            Path.Combine(root, "cache"),
            Path.Combine(root, "web"));
    }
}
