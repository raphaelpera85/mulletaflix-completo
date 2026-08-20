using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AutoFixture;
using Emby.Server.Implementations.Library;
using Emby.Server.Implementations.Plugins;
using MulletaFlix.Extensions;
using MulletaFlix.Extensions.Json;
using MulletaFlix.Extensions.Json.Converters;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Plugins
{
    public partial class PluginManagerTests
    {
        [Fact]
        public void BootstrapPluginCatalog_DefaultPluginRepositories_DoNotIncludeGetAvatar()
        {
            var catalogType = typeof(PluginManager).Assembly.GetType("Emby.Server.Implementations.Plugins.BootstrapPluginCatalog", throwOnError: true);
            var repositories = (Array?)catalogType!.GetField("DefaultPluginRepositories", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

            Assert.DoesNotContain(repositories!.Cast<object>(), repository =>
            {
                var repositoryType = repository.GetType();
                var name = (string?)repositoryType.GetProperty("Name")?.GetValue(repository);
                var url = (string?)repositoryType.GetProperty("Url")?.GetValue(repository);

                return name?.Contains("GetAvatar", StringComparison.OrdinalIgnoreCase) == true
                    || url?.Contains("GetAvatar", StringComparison.OrdinalIgnoreCase) == true;
            });
        }

        [Fact]
        public void SaveManifest_RoundTrip_Success()
        {
            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), null!, new Version(1, 0));
            var manifest = new PluginManifest()
            {
                Version = "1.0",
                Timestamp = DateTime.UtcNow
            };

            Assert.True(pluginManager.SaveManifest(manifest, _pluginPath));

            var res = pluginManager.LoadManifest(_pluginPath);

            Assert.Equal(manifest.Category, res.Manifest.Category);
            Assert.Equal(manifest.Changelog, res.Manifest.Changelog);
            Assert.Equal(manifest.Description, res.Manifest.Description);
            Assert.Equal(manifest.Id, res.Manifest.Id);
            Assert.Equal(manifest.Name, res.Manifest.Name);
            Assert.Equal(manifest.Overview, res.Manifest.Overview);
            Assert.Equal(manifest.Owner, res.Manifest.Owner);
            Assert.Equal(manifest.TargetAbi, res.Manifest.TargetAbi);
            Assert.Equal(manifest.Timestamp, res.Manifest.Timestamp);
            Assert.Equal(manifest.Version, res.Manifest.Version);
            Assert.Equal(manifest.Status, res.Manifest.Status);
            Assert.Equal(manifest.AutoUpdate, res.Manifest.AutoUpdate);
            Assert.Equal(manifest.ImagePath, res.Manifest.ImagePath);
            Assert.Equal(manifest.Assemblies, res.Manifest.Assemblies);
        }

        /// <summary>
        ///  Tests safe traversal within the plugin directory.
        /// </summary>
        /// <param name="dllFile">The safe path to evaluate.</param>
        [Theory]
        [InlineData("./some.dll")]
        [InlineData("some.dll")]
        [InlineData("sub/path/some.dll")]
        public void Constructor_DiscoversSafePluginAssembly_Status_Active(string dllFile)
        {
            var manifest = new PluginManifest
            {
                Id = Guid.NewGuid(),
                Name = "Safe Assembly",
                Assemblies = new string[] { dllFile }
            };

            var filename = Path.GetFileName(dllFile)!;
            var dllPath = Path.GetDirectoryName(Path.Combine(_pluginPath, dllFile))!;

            Directory.CreateDirectory(dllPath);
            FileHelper.CreateEmpty(Path.Combine(dllPath, filename));
            var metafilePath = Path.Combine(_pluginPath, "meta.json");

            File.WriteAllText(metafilePath, JsonSerializer.Serialize(manifest, _options));

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), _tempPath, new Version(1, 0));

            var res = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(metafilePath), _options);

            var expectedFullPath = Path.Combine(_pluginPath, dllFile).Canonicalize();

            Assert.NotNull(res);
            Assert.NotEmpty(pluginManager.Plugins);
            Assert.Equal(PluginStatus.Active, res!.Status);
            Assert.Equal(expectedFullPath, pluginManager.Plugins[0].DllFiles[0]);
            Assert.StartsWith(_pluginPath, expectedFullPath, StringComparison.InvariantCulture);
        }

        /// <summary>
        ///  Tests unsafe attempts to traverse to higher directories.
        /// </summary>
        /// <remarks>
        ///  Attempts to load directories outside of the plugin should be
        ///  constrained. Path traversal, shell expansion, and double encoding
        ///  can be used to load unintended files.
        ///  See <see href="https://owasp.org/www-community/attacks/Path_Traversal"/> for more.
        /// </remarks>
        /// <param name="unsafePath">The unsafe path to evaluate.</param>
        [Theory]
        [InlineData("/some.dll")] // Root path.
        [InlineData("../some.dll")] // Simple traversal.
        [InlineData("C:\\some.dll")] // Windows root path.
        [InlineData("test.txt")] // Not a DLL
        [InlineData(".././.././../some.dll")] // Traversal with current and parent
        [InlineData(@"..\.\..\.\..\some.dll")] // Windows traversal with current and parent
        [InlineData(@"\\network\resource.dll")] // UNC Path
        [InlineData("https://MulletaFlix.org/some.dll")] // URL
        [InlineData("~/some.dll")] // Tilde poses a shell expansion risk, but is a valid path character.
        public void Constructor_DiscoversUnsafePluginAssembly_Status_Malfunctioned(string unsafePath)
        {
            var manifest = new PluginManifest
            {
                Id = Guid.NewGuid(),
                Name = "Unsafe Assembly",
                Assemblies = new string[] { unsafePath }
            };

            // Only create very specific files. Otherwise the test will be exploiting path traversal.
            var files = new string[]
            {
                "../other.dll",
                "some.dll"
            };

            foreach (var file in files)
            {
                FileHelper.CreateEmpty(Path.Combine(_pluginPath, file));
            }

            var metafilePath = Path.Combine(_pluginPath, "meta.json");

            File.WriteAllText(metafilePath, JsonSerializer.Serialize(manifest, _options));

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), _tempPath, new Version(1, 0));

            var res = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(metafilePath), _options);

            Assert.NotNull(res);
            Assert.Empty(pluginManager.Plugins);
            Assert.Equal(PluginStatus.Malfunctioned, res!.Status);
        }


    }
}
