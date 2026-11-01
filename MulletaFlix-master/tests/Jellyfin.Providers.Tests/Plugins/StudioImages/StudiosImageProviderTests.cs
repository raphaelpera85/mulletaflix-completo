using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Plugins.StudioImages;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.StudioImages;

public sealed class StudiosImageProviderTests
{
    [Fact]
    public async Task EnsureList_WhenDownloadFails_PreservesPreviousCacheAndRemovesTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = Path.Combine(temporaryDirectory.Path, "remotestudiothumbs.txt");
        await File.WriteAllTextAsync(path, "previous list", TestContext.Current.CancellationToken);
        using var client = new HttpClient(new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new PartialFailureStream())
        }));
        var provider = CreateProvider(client);

        await Assert.ThrowsAsync<IOException>(() => provider.EnsureList("https://example.test/thumbs.txt", path, CreateFileSystem(path), TestContext.Current.CancellationToken));

        Assert.Equal("previous list", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, ".remotestudiothumbs.txt.*.tmp"));
    }

    [Fact]
    public async Task EnsureList_WhenInitialDownloadFails_DoesNotCreatePartialCacheOrLeaveTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = Path.Combine(temporaryDirectory.Path, "remotestudiothumbs.txt");
        using var client = new HttpClient(new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new PartialFailureStream())
        }));
        var provider = CreateProvider(client);

        await Assert.ThrowsAsync<IOException>(() => provider.EnsureList("https://example.test/thumbs.txt", path, CreateFileSystem(path, exists: false), TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, ".remotestudiothumbs.txt.*.tmp"));
    }

    [Fact]
    public async Task EnsureList_WhenDownloadCompletes_ReplacesPreviousCacheAndRemovesTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = Path.Combine(temporaryDirectory.Path, "remotestudiothumbs.txt");
        await File.WriteAllTextAsync(path, "previous list", TestContext.Current.CancellationToken);
        using var client = new HttpClient(new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("new list")
        }));
        var provider = CreateProvider(client);

        await provider.EnsureList("https://example.test/thumbs.txt", path, CreateFileSystem(path), TestContext.Current.CancellationToken);

        Assert.Equal("new list", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, ".remotestudiothumbs.txt.*.tmp"));
    }

    private static StudiosImageProvider CreateProvider(HttpClient client)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(NamedClient.Default)).Returns(client);
        return new StudiosImageProvider(Mock.Of<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(), httpClientFactory.Object, Mock.Of<IFileSystem>());
    }

    private static IFileSystem CreateFileSystem(string path, bool exists = true)
    {
        var metadata = new FileSystemMetadata { Exists = exists };
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.GetFileInfo(path)).Returns(metadata);
        fileSystem.Setup(value => value.GetLastWriteTimeUtc(metadata)).Returns(DateTime.UtcNow.AddDays(-2));
        return fileSystem.Object;
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> createResponse) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(createResponse());
    }

    private sealed class PartialFailureStream : MemoryStream
    {
        private bool _read;

        public PartialFailureStream()
            : base("partial response"u8.ToArray())
        {
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_read)
            {
                return ValueTask.FromException<int>(new IOException("Simulated interrupted response."));
            }

            _read = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory().FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
