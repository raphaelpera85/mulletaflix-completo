using System;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MulletaFlix.Api.Models;
using MulletaFlix.Extensions.Json;
using Xunit;

namespace MulletaFlix.Server.Integration.Tests.Controllers
{
    public sealed class DashboardControllerTests : IClassFixture<MulletaFlixApplicationFactory>
    {
        private readonly MulletaFlixApplicationFactory _factory;
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;
        private static string? _accessToken;

        public DashboardControllerTests(MulletaFlixApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetDashboardConfigurationPage_NonExistingPage_NotFound()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("web/ConfigurationPage?name=ThisPageDoesntExists", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetDashboardConfigurationPage_ExistingPage_CorrectPage()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

            var response = await client.GetAsync("/web/ConfigurationPage?name=TestPlugin", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(MediaTypeNames.Text.Html, response.Content.Headers.ContentType?.MediaType);

            string resourcePath = GetTestPageResourcePath();
            await using Stream resourceStream = typeof(TestPlugin).Assembly.GetManifestResourceStream(resourcePath)!;
            using StreamReader reader = new StreamReader(resourceStream);

            Assert.Equal(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetDashboardConfigurationPage_GetAvatar_HasDashboardPageWrapper()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

            var response = await client.GetAsync("/web/ConfigurationPage?name=GetAvatar", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("data-role=\"page\"", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pluginConfigurationPage", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("configPage", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetAvatarClientScript_ImplementsGalleryAndRandomSelection()
        {
            const string resourceName = "MulletaFlix.Plugin.GetAvatar.Configuration.Web.clientScript.js";
            await using Stream stream = typeof(global::MulletaFlix.Plugin.GetAvatar.Plugin).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("GetAvatar client script resource was not embedded.");
            using StreamReader reader = new StreamReader(stream);
            string script = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Contains("const apiBase = '/GetAvatar'", script, StringComparison.Ordinal);
            Assert.Contains("url('/Avatars')", script, StringComparison.Ordinal);
            Assert.Contains("url('/SetAvatar')", script, StringComparison.Ordinal);
            Assert.Contains("getavatar-random-button", script, StringComparison.Ordinal);
            Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
            Assert.DoesNotContain("/MulletaFlix/Plugins/GetAvatar/UserMappings", script, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetAvatarConfigurationResources_ExposeCatalogAndAutoAssignment()
        {
            var assembly = typeof(global::MulletaFlix.Plugin.GetAvatar.Plugin).Assembly;
            await using Stream htmlStream = assembly.GetManifestResourceStream("MulletaFlix.Plugin.GetAvatar.Configuration.Web.configPage.html")
                ?? throw new InvalidOperationException("GetAvatar configuration page was not embedded.");
            await using Stream scriptStream = assembly.GetManifestResourceStream("MulletaFlix.Plugin.GetAvatar.Configuration.Web.configPage.js")
                ?? throw new InvalidOperationException("GetAvatar configuration script was not embedded.");
            using StreamReader htmlReader = new StreamReader(htmlStream);
            using StreamReader scriptReader = new StreamReader(scriptStream);
            string html = await htmlReader.ReadToEndAsync(TestContext.Current.CancellationToken);
            string script = await scriptReader.ReadToEndAsync(TestContext.Current.CancellationToken);

            Assert.Contains("enableAutoAssign", html, StringComparison.Ordinal);
            Assert.Contains("avatarList", html, StringComparison.Ordinal);
            Assert.Contains("/Settings", script, StringComparison.Ordinal);
            Assert.Contains("/Avatars", script, StringComparison.Ordinal);
            Assert.Contains("/Upload", script, StringComparison.Ordinal);
            Assert.DoesNotContain("ApiKey", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetDashboardConfigurationPage_NebulaFtp_HasDashboardPageWrapper()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

            var response = await client.GetAsync("/web/ConfigurationPage?name=NebulaFTP", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("data-role=\"page\"", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pluginConfigurationPage", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("configPage", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetDashboardConfigurationPage_BrokenPage_NotFound()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/web/ConfigurationPage?name=BrokenPage", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetConfigurationPages_NoParams_AllConfigurationPages()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

            var response = await client.GetAsync("/web/ConfigurationPages", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            _ = await response.Content.ReadFromJsonAsync<ConfigurationPageInfo[]>(_jsonOptions, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task GetConfigurationPages_True_MainMenuConfigurationPages()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

            var response = await client.GetAsync("/web/ConfigurationPages?enableInMainMenu=true", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Encoding.UTF8.BodyName, response.Content.Headers.ContentType?.CharSet);

            var data = await response.Content.ReadFromJsonAsync<ConfigurationPageInfo[]>(_jsonOptions, TestContext.Current.CancellationToken);
            Assert.NotNull(data);
            Assert.True(data.Length >= 1);
            Assert.Contains(data, p => p.DisplayName == "NebulaFTP");
        }

        private static string GetTestPageResourcePath()
        {
            return typeof(TestPlugin).Assembly
                .GetManifestResourceNames()
                .Single(name => name.EndsWith(".TestPage.html", StringComparison.Ordinal));
        }
    }
}
