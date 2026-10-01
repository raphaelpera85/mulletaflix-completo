using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Emby.Server.Implementations.Plugins;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Plugins;
using MulletaFlix.Extensions.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Plugins;

public sealed class BundledPluginInstallerTests : IDisposable
{
    private const string PluginId = "5e87cc92-571a-4d8d-8d98-d2d4147f9f90";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mulletaflix-bundled-plugin-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void InstallsBundledPluginIntoDataDirectoryBeforeDiscovery()
    {
        var applicationDirectory = Path.Combine(_root, "application");
        var pluginsDirectory = Path.Combine(_root, "data", "plugins");
        CreateBundledPlugin(applicationDirectory);

        BundledPluginInstaller.EnsureFileTransformationInstalled(applicationDirectory, pluginsDirectory, new Version(12, 1, 9), NullLogger.Instance);

        var installed = Path.Combine(pluginsDirectory, "MulletaFlix-FileTransformation_3.0.1.0");
        Assert.Equal("plugin-binary", File.ReadAllText(Path.Combine(installed, "Jellyfin.Plugin.FileTransformation.dll")));
        Assert.True(File.Exists(Path.Combine(installed, "Jellyfin.Plugin.FileTransformation.deps.json")));
        Assert.False(File.Exists(Path.Combine(installed, "symbols.pdb")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(installed, "meta.json")));
        Assert.Equal(PluginId, manifest.RootElement.GetProperty("guid").GetString());
        Assert.Equal("3.0.1.0", manifest.RootElement.GetProperty("version").GetString());
        var loadedManifest = JsonSerializer.Deserialize<PluginManifest>(manifest.RootElement.GetRawText(), JsonDefaults.Options);
        Assert.NotNull(loadedManifest);
        Assert.Equal(PluginStatus.Active, loadedManifest.Status);
        Assert.Equal(new Version(12, 1, 0, 0), Version.Parse(loadedManifest.TargetAbi));
    }

    [Fact]
    public void InstalledBundle_IsDiscoverableByPluginManager()
    {
        var applicationDirectory = Path.Combine(_root, "application");
        var pluginsDirectory = Path.Combine(_root, "data", "plugins");
        CreateBundledPlugin(applicationDirectory);
        var bundledDirectory = Path.Combine(applicationDirectory, "bundled-plugins", "FileTransformation_3.0.1.0");
        File.WriteAllText(Path.Combine(bundledDirectory, "meta.json"), JsonSerializer.Serialize(new
        {
            category = "General",
            guid = PluginId,
            name = "File Transformation",
            targetAbi = "12.1.0.0",
            version = "3.0.1.0",
            status = "Active",
            assemblies = new[] { "Jellyfin.Plugin.FileTransformation.dll" }
        }));

        BundledPluginInstaller.EnsureFileTransformationInstalled(applicationDirectory, pluginsDirectory, new Version(12, 1, 10, 0), NullLogger.Instance);

        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var appHost = new Mock<IServerApplicationHost>();
        appHost.Setup(host => host.Resolve<IHttpClientFactory>()).Returns(services.GetRequiredService<IHttpClientFactory>());
        var manager = new PluginManager(
            NullLogger<PluginManager>.Instance,
            appHost.Object,
            new ServerConfiguration { PluginRepositories = [] },
            pluginsDirectory,
            new Version(12, 1, 10, 0));

        var plugin = Assert.Single(manager.Plugins);
        Assert.Equal("File Transformation", plugin.Name);
        Assert.True(plugin.IsEnabledAndSupported);
        Assert.Equal(
            Path.Combine(pluginsDirectory, "MulletaFlix-FileTransformation_3.0.1.0", "Jellyfin.Plugin.FileTransformation.dll"),
            Assert.Single(plugin.DllFiles));
    }

    [Fact]
    public void DoesNotReplaceAnEqualOrNewerInstalledPlugin()
    {
        var applicationDirectory = Path.Combine(_root, "application");
        var pluginsDirectory = Path.Combine(_root, "data", "plugins");
        CreateBundledPlugin(applicationDirectory);
        var installedDirectory = Path.Combine(pluginsDirectory, "FileTransformation_3.1.0.0");
        Directory.CreateDirectory(installedDirectory);
        File.WriteAllText(Path.Combine(installedDirectory, "meta.json"), CreateManifest("3.1.0.0", "12.1.0.0"));

        BundledPluginInstaller.EnsureFileTransformationInstalled(applicationDirectory, pluginsDirectory, new Version(12, 1, 9), NullLogger.Instance);

        Assert.False(Directory.Exists(Path.Combine(pluginsDirectory, "MulletaFlix-FileTransformation_3.0.1.0")));
        Assert.Equal("3.1.0.0", JsonDocument.Parse(File.ReadAllText(Path.Combine(installedDirectory, "meta.json")))
            .RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void ReplacesNewerButIncompatibleVersionWithBundledCompatibleVersion()
    {
        var applicationDirectory = Path.Combine(_root, "application");
        var pluginsDirectory = Path.Combine(_root, "data", "plugins");
        CreateBundledPlugin(applicationDirectory);
        var installedDirectory = Path.Combine(pluginsDirectory, "FileTransformation_3.2.0.0");
        Directory.CreateDirectory(installedDirectory);
        File.WriteAllText(Path.Combine(installedDirectory, "meta.json"), CreateManifest("3.2.0.0", "12.2.0.0"));

        BundledPluginInstaller.EnsureFileTransformationInstalled(applicationDirectory, pluginsDirectory, new Version(12, 1, 9), NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(pluginsDirectory, "MulletaFlix-FileTransformation_3.0.1.0", "Jellyfin.Plugin.FileTransformation.dll")));
    }

    [Fact]
    public void DoesNothingWhenBundleIsNotPackaged()
    {
        var pluginsDirectory = Path.Combine(_root, "data", "plugins");

        BundledPluginInstaller.EnsureFileTransformationInstalled(Path.Combine(_root, "application"), pluginsDirectory, new Version(12, 1, 9), NullLogger.Instance);

        Assert.False(Directory.Exists(pluginsDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void CreateBundledPlugin(string applicationDirectory)
    {
        var bundle = Path.Combine(applicationDirectory, "bundled-plugins", "FileTransformation_3.0.1.0");
        Directory.CreateDirectory(bundle);
        File.WriteAllText(Path.Combine(bundle, "Jellyfin.Plugin.FileTransformation.dll"), "plugin-binary");
        File.WriteAllText(Path.Combine(bundle, "Jellyfin.Plugin.FileTransformation.deps.json"), "{}");
        File.WriteAllText(Path.Combine(bundle, "symbols.pdb"), "debug symbols");
        File.WriteAllText(Path.Combine(bundle, "meta.json"), CreateManifest("3.0.1.0", "12.1.0.0"));
    }

    private static string CreateManifest(string version, string targetAbi = "12.1.0.0")
    {
        return JsonSerializer.Serialize(new { guid = PluginId, version, targetAbi });
    }
}
