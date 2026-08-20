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
        public async Task PopulateManifest_ExistingMetafilePlugin_PopulatesMissingFields()
        {
            var packageInfo = GenerateTestPackage();

            // Partial plugin without a name, but matching version and package ID
            var partial = new PluginManifest
            {
                Id = packageInfo.Id,
                AutoUpdate = false, // Turn off AutoUpdate
                Status = PluginStatus.Restart,
                Version = new Version(1, 0, 0).ToString(),
                Assemblies = new[] { "MulletaFlix.Test.dll" }
            };

            var expectedManifest = new PluginManifest
            {
                Id = partial.Id,
                Name = packageInfo.Name,
                AutoUpdate = partial.AutoUpdate,
                Status = PluginStatus.Active,
                Owner = packageInfo.Owner,
                Assemblies = partial.Assemblies,
                Category = packageInfo.Category,
                Description = packageInfo.Description,
                Overview = packageInfo.Overview,
                TargetAbi = packageInfo.Versions[0].TargetAbi!,
                // Timestamp is preserved from local manifest if not default; skip exact match
                Changelog = packageInfo.Versions[0].Changelog!,
                Version = new Version(1, 0).ToString(),
                ImagePath = string.Empty
            };

            var metafilePath = Path.Combine(_pluginPath, "meta.json");
            await File.WriteAllTextAsync(metafilePath, JsonSerializer.Serialize(partial, _options), TestContext.Current.CancellationToken);

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), _tempPath, new Version(1, 0));

            await pluginManager.PopulateManifest(packageInfo, new Version(1, 0), _pluginPath, PluginStatus.Active);

            var resultBytes = await File.ReadAllBytesAsync(metafilePath, TestContext.Current.CancellationToken);
            var result = JsonSerializer.Deserialize<PluginManifest>(resultBytes, _options);

            Assert.NotNull(result);
            Assert.Equal(expectedManifest.Category, result.Category);
            Assert.Equal(expectedManifest.Changelog, result.Changelog);
            Assert.Equal(expectedManifest.Description, result.Description);
            Assert.Equal(expectedManifest.Id, result.Id);
            Assert.Equal(expectedManifest.Name, result.Name);
            Assert.Equal(expectedManifest.Overview, result.Overview);
            Assert.Equal(expectedManifest.Owner, result.Owner);
            Assert.Equal(expectedManifest.TargetAbi, result.TargetAbi);
            Assert.Equal(expectedManifest.Version, result.Version);
            Assert.Equal(expectedManifest.Status, result.Status);
            Assert.Equal(expectedManifest.AutoUpdate, result.AutoUpdate);
            Assert.Equal(expectedManifest.ImagePath, result.ImagePath);
            Assert.Equal(expectedManifest.Assemblies, result.Assemblies);
        }

        [Fact]
        public async Task PopulateManifest_NoMetafile_PreservesManifest()
        {
            var packageInfo = GenerateTestPackage();
            var expectedManifest = new PluginManifest
            {
                Id = packageInfo.Id,
                Name = packageInfo.Name,
                AutoUpdate = true,
                Status = PluginStatus.Active,
                Owner = packageInfo.Owner,
                Assemblies = Array.Empty<string>(),
                Category = packageInfo.Category,
                Description = packageInfo.Description,
                Overview = packageInfo.Overview,
                TargetAbi = packageInfo.Versions[0].TargetAbi!,
                Timestamp = DateTimeOffset.Parse(packageInfo.Versions[0].Timestamp!, CultureInfo.InvariantCulture).UtcDateTime,
                Changelog = packageInfo.Versions[0].Changelog!,
                Version = packageInfo.Versions[0].Version,
                ImagePath = string.Empty
            };

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), null!, new Version(1, 0));

            await pluginManager.PopulateManifest(packageInfo, new Version(1, 0), _pluginPath, PluginStatus.Active);

            var metafilePath = Path.Combine(_pluginPath, "meta.json");
            var resultBytes = await File.ReadAllBytesAsync(metafilePath, TestContext.Current.CancellationToken);
            var result = JsonSerializer.Deserialize<PluginManifest>(resultBytes, _options);

            Assert.NotNull(result);
            Assert.Equivalent(expectedManifest, result);
        }

        [Fact]
        public async Task PopulateManifest_ExistingMetafileMismatchedIds_Status_Malfunctioned()
        {
            var packageInfo = GenerateTestPackage();

            // Partial plugin without a name, but matching version and package ID
            var partial = new PluginManifest
            {
                Id = Guid.NewGuid(),
                Version = new Version(1, 0, 0).ToString()
            };

            var metafilePath = Path.Combine(_pluginPath, "meta.json");
            await File.WriteAllTextAsync(metafilePath, JsonSerializer.Serialize(partial, _options), TestContext.Current.CancellationToken);

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), _tempPath, new Version(1, 0));

            await pluginManager.PopulateManifest(packageInfo, new Version(1, 0), _pluginPath, PluginStatus.Active);

            var resultBytes = await File.ReadAllBytesAsync(metafilePath, TestContext.Current.CancellationToken);
            var result = JsonSerializer.Deserialize<PluginManifest>(resultBytes, _options);

            Assert.NotNull(result);
            Assert.Equal(packageInfo.Name, result.Name);
            Assert.Equal(PluginStatus.Malfunctioned, result.Status);
        }

        [Fact]
        public async Task PopulateManifest_ExistingMetafileMismatchedVersions_Updates_Version()
        {
            var packageInfo = GenerateTestPackage();

            var partial = new PluginManifest
            {
                Id = packageInfo.Id,
                Version = new Version(2, 0, 0).ToString()
            };

            var metafilePath = Path.Combine(_pluginPath, "meta.json");
            await File.WriteAllTextAsync(metafilePath, JsonSerializer.Serialize(partial, _options), TestContext.Current.CancellationToken);

            var serviceProvider = CreateTestServiceProvider();
            var appHost = CreateTestAppHost(serviceProvider);
            var pluginManager = new PluginManager(new NullLogger<PluginManager>(), appHost, CreateTestConfig(), _tempPath, new Version(1, 0));

            await pluginManager.PopulateManifest(packageInfo, new Version(1, 0), _pluginPath, PluginStatus.Active);

            var resultBytes = await File.ReadAllBytesAsync(metafilePath, TestContext.Current.CancellationToken);
            var result = JsonSerializer.Deserialize<PluginManifest>(resultBytes, _options);

            Assert.NotNull(result);
            Assert.Equal(packageInfo.Name, result.Name);
            Assert.Equal(PluginStatus.Active, result.Status);
            Assert.Equal(packageInfo.Versions[0].Version, result.Version);
        }

        private PackageInfo GenerateTestPackage()
        {
            var fixture = new Fixture();
            fixture.Customize<PackageInfo>(c => c.Without(x => x.Versions).Without(x => x.ImageUrl));
            fixture.Customize<VersionInfo>(c => c.Without(x => x.Version).Without(x => x.Timestamp));

            var versionInfo = fixture.Create<VersionInfo>();
            versionInfo.Version = new Version(1, 0).ToString();
            versionInfo.Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var packageInfo = fixture.Create<PackageInfo>();
            packageInfo.Versions = new[] { versionInfo };

            return packageInfo;
        }

        private JsonSerializerOptions GetTestSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonDefaults.Options)
            {
                WriteIndented = true
            };

            for (var i = 0; i < options.Converters.Count; i++)
            {
                // Remove the Guid converter for parity with plugin manager.
                if (options.Converters[i] is JsonGuidConverter converter)
                {
                    options.Converters.Remove(converter);
                }
            }

            return options;
        }


    }
}
