using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Plugin.GetAvatar;
using MulletaFlix.Plugin.GetAvatar.Configuration;
using MulletaFlix.Plugin.GetAvatar.Controllers;
using MulletaFlix.Plugin.GetAvatar.Services;
using Xunit;
using GetAvatarPlugin = MulletaFlix.Plugin.GetAvatar.Plugin;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection(GetAvatarPluginTestCollection.Name)]
public sealed class OnlinePackServiceTests
{
    [Fact]
    public async Task ImportPacksAsync_RejectsArchiveWithTooManyEntriesBeforeSavingImages()
    {
        await AssertPackRejectedAsync(archive =>
        {
            for (var index = 0; index < 10001; index++)
            {
                archive.CreateEntry($"image-{index}.png");
            }
        });
    }

    [Fact]
    public async Task ImportPacksAsync_RejectsExcessiveExpandedImageDataBeforeSavingImages()
    {
        var imageData = new byte[5 * 1024 * 1024];
        await AssertPackRejectedAsync(archive =>
        {
            for (var index = 0; index < 52; index++)
            {
                using var image = archive.CreateEntry($"image-{index}.png", CompressionLevel.Optimal).Open();
                image.Write(imageData);
            }
        });
    }

    [Fact]
    public async Task ImportPacksAsync_RejectsOversizedArchiveFromContentLength()
    {
        var archiveBytes = CreateArchive(archive => archive.CreateEntry("avatar.png"));
        await WithServiceAsync(
            archiveBytes,
            async (service, avatarDirectory, _) =>
            {
                var result = await service.ImportPacksAsync([CreatePack()]);

                Assert.False(result.PackResults.Single().Success);
                Assert.Empty(Directory.GetFiles(avatarDirectory));
            },
            declaredContentLength: (256L * 1024L * 1024L) + 1);
    }

    [Theory]
    [InlineData("http://127.0.0.1/internal")]
    [InlineData("https://127.0.0.1/internal")]
    [InlineData("https://github.com.attacker.invalid/cedev-1/jellyfin-avatars/releases/download/tag/pack.zip")]
    [InlineData("https://attacker@github.com/cedev-1/jellyfin-avatars/releases/download/tag/pack.zip")]
    [InlineData("https://github.com/other/repository/releases/download/tag/pack.zip")]
    public async Task ImportPacksAsync_RejectsUntrustedDownloadUrlBeforeImport(string downloadUrl)
    {
        var archiveBytes = CreateArchive(archive =>
        {
            using var image = archive.CreateEntry("avatar.png").Open();
            image.Write(new byte[] { 1, 2, 3 });
        });

        await WithServiceAsync(
            archiveBytes,
            async (service, avatarDirectory, _) =>
            {
                var result = await service.ImportPacksAsync([CreatePack(downloadUrl)]);

                Assert.False(result.PackResults.Single().Success);
                Assert.Empty(Directory.GetFiles(avatarDirectory));
            });
    }

    [Fact]
    public async Task ImportPacksAsync_FollowsTrustedGithubAssetRedirect()
    {
        var archiveBytes = CreateArchive(archive =>
        {
            using var image = archive.CreateEntry("avatar.png").Open();
            image.Write(new byte[] { 1, 2, 3 });
        });
        const string redirectUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/example?token=signed";

        await WithServiceAsync(
            archiveBytes,
            async (service, avatarDirectory, factory) =>
            {
                var result = await service.ImportPacksAsync([CreatePack()]);

                Assert.True(result.PackResults.Single().Success);
                Assert.Equal(1, result.ImportedCount);
                Assert.Equal(
                    new[]
                    {
                        "github.com",
                        "release-assets.githubusercontent.com"
                    },
                    factory.RequestedUris.Select(uri => uri.Host));
                Assert.Equal(new[] { "GetAvatar.PackDownload" }, factory.CreatedClientNames);
                Assert.Single(Directory.GetFiles(avatarDirectory));
            },
            redirectUrl: redirectUrl);
    }

    [Fact]
    public async Task ImportPacksAsync_RejectsUntrustedRedirectBeforeConnectingToTarget()
    {
        var archiveBytes = CreateArchive(archive => archive.CreateEntry("avatar.png"));

        await WithServiceAsync(
            archiveBytes,
            async (service, avatarDirectory, factory) =>
            {
                var result = await service.ImportPacksAsync([CreatePack()]);

                Assert.False(result.PackResults.Single().Success);
                Assert.Equal(new[] { "github.com" }, factory.RequestedUris.Select(uri => uri.Host));
                Assert.Empty(Directory.GetFiles(avatarDirectory));
            },
            redirectUrl: "http://127.0.0.1/internal");
    }

    [Fact]
    public async Task ImportPacksAsync_StopsAfterMaximumTrustedRedirects()
    {
        var archiveBytes = CreateArchive(archive => archive.CreateEntry("avatar.png"));
        const string redirectUrl = "https://github.com/cedev-1/jellyfin-avatars/releases/download/test/ignored.zip";

        await WithServiceAsync(
            archiveBytes,
            async (service, avatarDirectory, factory) =>
            {
                var result = await service.ImportPacksAsync([CreatePack()]);

                Assert.False(result.PackResults.Single().Success);
                Assert.Equal(6, factory.RequestedUris.Count);
                Assert.Empty(Directory.GetFiles(avatarDirectory));
            },
            redirectUrl: redirectUrl);
    }

    [Fact]
    public void PackDownloadClient_DisablesAutomaticRedirects()
    {
        HttpClientHandler? packDownloadHandler = null;
        var services = new ServiceCollection();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new CapturingHttpMessageHandlerBuilderFilter(builder =>
        {
            if (builder.Name == "GetAvatar.PackDownload")
            {
                packDownloadHandler = Assert.IsType<HttpClientHandler>(builder.PrimaryHandler);
            }
        }));
        new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>());

        using var serviceProvider = services.BuildServiceProvider();
        using var client = serviceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("GetAvatar.PackDownload");

        Assert.NotNull(packDownloadHandler);
        Assert.False(packDownloadHandler.AllowAutoRedirect);
    }

    [Fact]
    public async Task ImportPacksAsync_ImportsImageEntriesWithoutUsingArchivePathsOnDisk()
    {
        var imageData = new byte[] { 1, 2, 3, 4 };
        var archiveBytes = CreateArchive(archive =>
        {
            using (var image = archive.CreateEntry("../../outside.png").Open())
            {
                image.Write(imageData);
            }

            using (var image = archive.CreateEntry("nested\\avatar.png").Open())
            {
                image.Write(new byte[] { 5, 6, 7 });
            }
        });

        await WithServiceAsync(archiveBytes, async (service, avatarDirectory, _) =>
        {
            var result = await service.ImportPacksAsync([CreatePack()]);

            Assert.Equal(2, result.ImportedCount);
            Assert.Equal(2, result.TotalImages);
            Assert.True(result.PackResults.Single().Success);
            Assert.Equal(2, Directory.GetFiles(avatarDirectory).Length);
            Assert.Contains(Directory.GetFiles(avatarDirectory), path => File.ReadAllBytes(path).SequenceEqual(imageData));
            Assert.DoesNotContain(Directory.GetDirectories(avatarDirectory), path => Path.GetFileName(path) == "nested");
        });
    }

    private static async Task AssertPackRejectedAsync(Action<ZipArchive> addEntries)
    {
        var archiveBytes = CreateArchive(addEntries);
        await WithServiceAsync(archiveBytes, async (service, avatarDirectory, _) =>
        {
            var result = await service.ImportPacksAsync([CreatePack()]);

            Assert.Equal(0, result.ImportedCount);
            Assert.False(result.PackResults.Single().Success);
            Assert.Empty(Directory.GetFiles(avatarDirectory));
        });
    }

    private static async Task WithServiceAsync(
        byte[] archiveBytes,
        Func<OnlinePackService, string, ArchiveHttpClientFactory, Task> action,
        long? declaredContentLength = null,
        string? redirectUrl = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "GetAvatarOnlinePackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousPlugin = GetAvatarPlugin.Instance;

        try
        {
            var applicationPaths = new Mock<IServerApplicationPaths>();
            applicationPaths.SetupGet(paths => paths.PluginConfigurationsPath).Returns(root);
            applicationPaths.SetupGet(paths => paths.PluginsPath).Returns(root);
            applicationPaths.SetupGet(paths => paths.DataPath).Returns(root);
            applicationPaths.SetupGet(paths => paths.UserConfigurationDirectoryPath).Returns(Path.Combine(root, "users"));

            var serializer = new Mock<IXmlSerializer>();
            serializer
                .Setup(xml => xml.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            _ = new GetAvatarPlugin(applicationPaths.Object, serializer.Object, NullLogger<GetAvatarPlugin>.Instance);

            using var avatarService = new AvatarService(
                Mock.Of<IUserManager>(),
                Mock.Of<IProviderManager>(),
                applicationPaths.Object,
                NullLogger<AvatarService>.Instance,
                Mock.Of<IServerConfigurationManager>());
            var factory = new ArchiveHttpClientFactory(archiveBytes, declaredContentLength, redirectUrl);
            var service = new OnlinePackService(
                factory,
                avatarService,
                NullLogger<OnlinePackService>.Instance);

            await action(service, avatarService.AvatarDirectory, factory);
        }
        finally
        {
            typeof(GetAvatarPlugin).GetProperty(nameof(GetAvatarPlugin.Instance))!.SetValue(null, previousPlugin);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static OnlinePackInfo CreatePack(string downloadUrl = "https://github.com/cedev-1/jellyfin-avatars/releases/download/test/ignored.zip")
    {
        return new OnlinePackInfo
        {
            Id = "test",
            Name = "Test",
            FileName = "ignored.zip",
            DownloadUrl = downloadUrl
        };
    }

    private static byte[] CreateArchive(Action<ZipArchive> addEntries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            addEntries(archive);
        }

        return stream.ToArray();
    }

    private sealed class ArchiveHttpClientFactory(byte[] archiveBytes, long? declaredContentLength, string? redirectUrl) : IHttpClientFactory
    {
        public List<Uri> RequestedUris { get; } = [];

        public List<string> CreatedClientNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            CreatedClientNames.Add(name);
            return new HttpClient(new ArchiveHandler(archiveBytes, declaredContentLength, redirectUrl, RequestedUris));
        }
    }

    private sealed class ArchiveHandler(byte[] archiveBytes, long? declaredContentLength, string? redirectUrl, List<Uri> requestedUris) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requestedUris.Add(request.RequestUri!);
            if (redirectUrl is not null && string.Equals(request.RequestUri!.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri(redirectUrl) }
                });
            }

            var content = new ByteArrayContent(archiveBytes);
            if (declaredContentLength.HasValue)
            {
                content.Headers.ContentLength = declaredContentLength;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }

    private sealed class CapturingHttpMessageHandlerBuilderFilter(Action<HttpMessageHandlerBuilder> capture)
        : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                next(builder);
                capture(builder);
            };
        }
    }
}
