using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UpdateInfoControllerTests
{
    [Fact]
    public void ApplyUpdate_WhenUpdaterLaunchFails_RestoresReadyStateAndKeepsPackageAvailable()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "mulletaflix-update-launch-" + Guid.NewGuid().ToString("N"));
        var packageDirectory = Path.Combine(tempDirectory, "extracted");
        Directory.CreateDirectory(packageDirectory);
        SetStaticField("_installState", "ReadyToApply");
        SetStaticField("_installProgress", 100);
        SetStaticField("_installError", null);
        SetStaticField("_extractedUpdatePath", packageDirectory);

        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(value => value.ProgramDataPath).Returns(tempDirectory);
            var controller = new UpdateInfoController(
                Mock.Of<IServerApplicationHost>(),
                paths.Object,
                Mock.Of<ISystemManager>(),
                Mock.Of<IHttpClientFactory>(),
                NullLogger<UpdateInfoController>.Instance,
                static _ => throw new InvalidOperationException("Simulated process launch failure."));

            var result = Assert.IsType<ObjectResult>(controller.ApplyUpdate());
            Assert.Equal(500, result.StatusCode);
            Assert.True(Directory.Exists(packageDirectory));

            var status = controller.GetUpdateStatus().Value!;
            Assert.Equal("ReadyToApply", status.InstallState);
            Assert.Equal(100, status.InstallProgress);
            Assert.Contains("try applying it again", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SetStaticField("_installState", "Idle");
            SetStaticField("_installProgress", 0);
            SetStaticField("_installError", null);
            SetStaticField("_extractedUpdatePath", null);
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteEmbeddedUpdaterScript_MatchesRollbackSafePackagingScript()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), "mulletaflix-updater-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            UpdateInfoController.WriteEmbeddedUpdaterScript(temporaryPath);

            var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
            while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "build-update-package.ps1")))
            {
                repositoryRoot = repositoryRoot.Parent;
            }

            Assert.NotNull(repositoryRoot);
            var canonicalScript = Path.Combine(repositoryRoot!.FullName, "MulletaFlix-packaging-master", "MulletaFlix-ux-custom", "nsis", "apply-update.ps1");
            Assert.Equal(File.ReadAllText(canonicalScript), File.ReadAllText(temporaryPath));
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void SetStaticField(string name, object? value)
    {
        var field = typeof(UpdateInfoController).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        field!.SetValue(null, value);
    }

    [Fact]
    public async Task InstallUpdate_RejectsUnapprovedArchiveHostBeforeCreatingHttpClient()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var controller = new UpdateInfoController(
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<IApplicationPaths>(),
            Mock.Of<ISystemManager>(),
            httpClientFactory.Object,
            NullLogger<UpdateInfoController>.Instance);

        var result = await controller.InstallUpdate("https://example.com/package.zip");

        Assert.IsType<BadRequestObjectResult>(result);
        httpClientFactory.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CreateConfiguredClient_WithoutGitHubToken_ClearsAuthorizationHeader()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "factory-token");
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(client);
        var applicationHost = new Mock<IServerApplicationHost>();
        applicationHost.SetupGet(host => host.ApplicationVersionString).Returns("1.2.3");
        var controller = new UpdateInfoController(
            applicationHost.Object,
            Mock.Of<IApplicationPaths>(),
            Mock.Of<ISystemManager>(),
            httpClientFactory.Object,
            NullLogger<UpdateInfoController>.Instance);

        using var configuredClient = controller.CreateConfiguredClient(includeGitHubToken: false);

        Assert.Null(configuredClient.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/mulletaflix-update-win-x64.zip", true)]
    [InlineData("http://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip", false)]
    [InlineData("https://github.com.attacker.example/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip", false)]
    [InlineData("https://github.com/other/repository/releases/download/v12.1.46/package.zip", false)]
    [InlineData("https://user:password@github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip", false)]
    [InlineData("https://github.com:444/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip", false)]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/../package.zip", false)]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/", false)]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download//package.zip", false)]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip?redirect=https://example.com", false)]
    public void IsAllowedGitHubReleaseArchiveUrl_RestrictsPackagesToCanonicalReleaseAssets(string archiveUrl, bool expected)
    {
        Assert.Equal(expected, UpdateInfoController.IsAllowedGitHubReleaseArchiveUrl(archiveUrl));
    }

    [Theory]
    [InlineData("https://release-assets.githubusercontent.com/asset-id", true)]
    [InlineData("https://objects.githubusercontent.com/asset-id", true)]
    [InlineData("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/v12.1.46/package.zip", true)]
    [InlineData("https://release-assets.githubusercontent.com.attacker.example/asset-id", false)]
    [InlineData("http://release-assets.githubusercontent.com/asset-id", false)]
    [InlineData("https://user:password@release-assets.githubusercontent.com/asset-id", false)]
    [InlineData("https://example.com/asset-id", false)]
    public void IsAllowedGitHubArchiveResponseUri_RestrictsRedirectHosts(string url, bool expected)
    {
        Assert.Equal(expected, UpdateInfoController.IsAllowedGitHubArchiveResponseUri(new Uri(url)));
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsTrue_WhenFilesMatchExactly()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            var subUpdate = Path.Combine(updateDir, "sub");
            var subInstall = Path.Combine(installDir, "sub");
            Directory.CreateDirectory(subUpdate);
            Directory.CreateDirectory(subInstall);

            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "binary content 12345");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "binary content 12345");

            File.WriteAllText(Path.Combine(subUpdate, "feature.dll"), "nested binary data");
            File.WriteAllText(Path.Combine(subInstall, "feature.dll"), "nested binary data");

            // Arquivo temporário/script no update deve ser ignorado na checagem
            File.WriteAllText(Path.Combine(updateDir, "apply-update.ps1"), "# updater script");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.True(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsFalse_WhenFilesDifferInContent()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "new version binary content");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "old version binary content");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void AreUpdateFilesIdentical_ReturnsFalse_WhenFileMissingInInstall()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "mulleta_upd_test_" + Guid.NewGuid().ToString("N"));
        var updateDir = Path.Combine(tempBase, "update");
        var installDir = Path.Combine(tempBase, "install");

        Directory.CreateDirectory(updateDir);
        Directory.CreateDirectory(installDir);

        try
        {
            File.WriteAllText(Path.Combine(updateDir, "MulletaFlix.dll"), "same content");
            File.WriteAllText(Path.Combine(installDir, "MulletaFlix.dll"), "same content");

            File.WriteAllText(Path.Combine(updateDir, "NewFeature.dll"), "new file");

            var result = UpdateInfoController.AreUpdateFilesIdentical(updateDir, installDir, NullLogger.Instance);
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, true);
            }
        }
    }

    [Fact]
    public void TryParseGitHubRelease_SelectsServerReleaseAndIgnoresAppRelease_WhenGivenReleaseList()
    {
        var json = @"[
            {
                ""tag_name"": ""app-v12.0.2"",
                ""body"": ""Android app release notes"",
                ""assets"": [
                    {
                        ""name"": ""mulletaflix-app-v12.0.2.apk"",
                        ""size"": 7000000,
                        ""browser_download_url"": ""https://github.com/.../app.apk""
                    }
                ]
            },
            {
                ""tag_name"": ""v12.0.2"",
                ""body"": ""Server release notes"",
                ""assets"": [
                    {
                        ""name"": ""mulletaflix-update-win-x64.zip"",
                        ""size"": 105000000,
                        ""browser_download_url"": ""https://github.com/.../mulletaflix-update-win-x64.zip""
                    },
                    {
                        ""name"": ""mulletaflix-app-v12.0.2.apk"",
                        ""size"": 7000000,
                        ""browser_download_url"": ""https://github.com/.../app.apk""
                    }
                ]
            }
        ]";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size);

        Assert.True(success);
        Assert.Equal(new Version(12, 0, 2), version);
        Assert.Equal("Server release notes", changelog);
        Assert.Equal("https://github.com/.../mulletaflix-update-win-x64.zip", archiveUrl);
        Assert.Equal(105000000, size);
    }

    [Fact]
    public void TryParseGitHubRelease_SelectsUpdatePackageWhenAnotherZipComesFirst()
    {
        var json = """
            [{
              "tag_name": "v12.1.46",
              "assets": [
                { "name": "source-code.zip", "size": 100, "browser_download_url": "https://example.com/source.zip" },
                { "name": "mulletaflix-update-win-x64.zip", "size": 500, "browser_download_url": "https://example.com/server-update.zip" }
              ]
            }]
            """;

        var success = UpdateInfoController.TryParseGitHubRelease(json, out _, out _, out var archiveUrl, out var size);

        Assert.True(success);
        Assert.Equal("https://example.com/server-update.zip", archiveUrl);
        Assert.Equal(500, size);
    }

    [Fact]
    public void TryParseGitHubRelease_RejectsAmbiguousZipAssetsWithoutNamedUpdatePackage()
    {
        var json = """
            [{
              "tag_name": "v12.1.46",
              "assets": [
                { "name": "source-code.zip", "size": 100, "browser_download_url": "https://example.com/source.zip" },
                { "name": "other-package.zip", "size": 500, "browser_download_url": "https://example.com/other.zip" }
              ]
            }]
            """;

        var success = UpdateInfoController.TryParseGitHubRelease(json, out _, out _, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParseGitHubRelease_IgnoresNumericPrereleaseWithNewerVersion()
    {
        var json = @"[
            {
                ""tag_name"": ""v12.1.45"",
                ""prerelease"": false,
                ""assets"": [{ ""name"": ""update.zip"", ""browser_download_url"": ""https://example.com/stable.zip"" }]
            },
            {
                ""tag_name"": ""v12.1.99"",
                ""prerelease"": true,
                ""assets"": [{ ""name"": ""update.zip"", ""browser_download_url"": ""https://example.com/prerelease.zip"" }]
            }
        ]";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out _, out var archiveUrl, out _);

        Assert.True(success);
        Assert.Equal(new Version(12, 1, 45), version);
        Assert.Equal("https://example.com/stable.zip", archiveUrl);
    }

    [Fact]
    public void TryParseGitHubRelease_ParsesSingleReleaseObject_WhenValid()
    {
        var json = @"{
            ""tag_name"": ""v12.0.3"",
            ""body"": ""Single release notes"",
            ""assets"": [
                {
                    ""name"": ""update.zip"",
                    ""size"": 5000,
                    ""browser_download_url"": ""https://example.com/update.zip""
                }
            ]
        }";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size);

        Assert.True(success);
        Assert.Equal(new Version(12, 0, 3), version);
        Assert.Equal("https://example.com/update.zip", archiveUrl);
    }

    [Fact]
    public void TryParseGitHubRelease_ReturnsFalse_WhenNoZipAssetPresent()
    {
        var json = @"{
            ""tag_name"": ""v12.0.3"",
            ""body"": ""Release without zip"",
            ""assets"": [
                {
                    ""name"": ""update.exe"",
                    ""size"": 5000,
                    ""browser_download_url"": ""https://example.com/update.exe""
                }
            ]
        }";

        var success = UpdateInfoController.TryParseGitHubRelease(json, out var version, out var changelog, out var archiveUrl, out var size);

        Assert.False(success);
    }
}

