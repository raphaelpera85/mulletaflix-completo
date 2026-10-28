using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UpdateInfoControllerTests
{
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

