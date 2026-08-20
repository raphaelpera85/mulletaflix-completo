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
        private static readonly string _testPathRoot = Path.Combine(Path.GetTempPath(), "MulletaFlix-test-data");

        private string _tempPath = string.Empty;

        private string _pluginPath = string.Empty;

        private JsonSerializerOptions _options;

        public PluginManagerTests()
        {
            (_tempPath, _pluginPath) = GetTestPaths("plugin-" + Path.GetRandomFileName());

            Directory.CreateDirectory(_pluginPath);

            _options = GetTestSerializerOptions();
        }

        private static ServerConfiguration CreateTestConfig()
        {
            return new ServerConfiguration
            {
                PluginRepositories = Array.Empty<RepositoryInfo>()
            };
        }

        private static IServiceProvider CreateTestServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddHttpClient();
            return services.BuildServiceProvider();
        }

        private static IServerApplicationHost CreateTestAppHost(IServiceProvider serviceProvider)
        {
            var mock = new Mock<IServerApplicationHost>();
            mock.Setup(h => h.Resolve<IHttpClientFactory>()).Returns(serviceProvider.GetRequiredService<IHttpClientFactory>());
            return mock.Object;
        }

        private (string TempPath, string PluginPath) GetTestPaths(string pluginFolderName)
        {
            var tempPath = Path.Combine(_testPathRoot, "plugin-manager" + Path.GetRandomFileName());
            var pluginPath = Path.Combine(tempPath, pluginFolderName);

            return (tempPath, pluginPath);
        }

    }
}
