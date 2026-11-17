using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.MediaEncoding;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.MediaEncoding;

public class StrmPrebufferManagerTests
{
    [Fact]
    public async Task CopyToAsync_Should_StartPlayback_WhenInitialStrmBytesAreReady()
    {
        var tempStrm = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.strm");
        await File.WriteAllTextAsync(tempStrm, "https://example.test/video.mp4", TestContext.Current.CancellationToken);
        var source = new DelayedTailStream(Encoding.UTF8.GetBytes("prefix"));

        try
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(source)
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new StringContent("-tail", Encoding.UTF8, "video/mp4")
                });

            var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object, disposeHandler: false));

            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.Setup(x => x.GetConfiguration("branding")).Returns(new BrandingOptions
            {
                PrebufferEnabled = false,
                IntroEnabled = false,
                PrebufferSizeMb = 1
            });

            var applicationHost = new Mock<IServerApplicationHost>();
            applicationHost.Setup(x => x.GetSmartApiUrl("localhost")).Returns("http://localhost");

            using var manager = new StrmPrebufferManager(
                httpClientFactory.Object,
                configurationManager.Object,
                applicationHost.Object,
                NullLogger<StrmPrebufferManager>.Instance);
            var item = new Video { Id = Guid.NewGuid(), Path = tempStrm };

            await manager.PrepareAsync(item);
            await source.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            await using var output = new MemoryStream();
            using var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var copyTask = manager.CopyToAsync(item.Id, output, playbackCts.Token);
            try
            {
                var completed = await Task.WhenAny(copyTask, Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
                Assert.Same(copyTask, completed);
                await copyTask;
                Assert.Equal("prefix-tail", Encoding.UTF8.GetString(output.ToArray()));
            }
            finally
            {
                source.ReleaseTail();
                playbackCts.Cancel();
                try { await copyTask; } catch (OperationCanceledException) { }
            }
        }
        finally
        {
            source.ReleaseTail();
            File.Delete(tempStrm);
        }
    }

    [Fact]
    public async Task CopyToAsync_Should_KeepPrebufferAvailable_ForRepeatedReads()
    {
        var tempStrm = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.strm");
        await File.WriteAllTextAsync(tempStrm, "https://example.test/video.mp4", TestContext.Current.CancellationToken);

        try
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("abcdef", System.Text.Encoding.UTF8, "video/mp4")
                });

            var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object, disposeHandler: false));

            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.Setup(x => x.GetConfiguration("branding")).Returns(new BrandingOptions
            {
                PrebufferEnabled = true,
                PrebufferSizeMb = 1
            });

            var applicationHost = new Mock<IServerApplicationHost>();
            applicationHost.Setup(x => x.GetSmartApiUrl("localhost")).Returns("http://localhost");

            var manager = new StrmPrebufferManager(
                httpClientFactory.Object,
                configurationManager.Object,
                applicationHost.Object,
                NullLogger<StrmPrebufferManager>.Instance);

            var item = new Video
            {
                Id = Guid.NewGuid(),
                Path = tempStrm
            };

            await manager.PrepareAsync(item);

            Assert.True(manager.TryGetProxyUrl(item.Id, out var proxyUrl));
            Assert.Equal("http://localhost/Videos/" + item.Id.ToString("N") + "/Prebuffer", proxyUrl);

            await using (var first = new MemoryStream())
            {
                await manager.CopyToAsync(item.Id, first, TestContext.Current.CancellationToken);
                Assert.Equal("abcdef", System.Text.Encoding.UTF8.GetString(first.ToArray()));
            }

            await using (var second = new MemoryStream())
            {
                await manager.CopyToAsync(item.Id, second, TestContext.Current.CancellationToken);
                Assert.Equal("abcdef", System.Text.Encoding.UTF8.GetString(second.ToArray()));
            }
        }
        finally
        {
            File.Delete(tempStrm);
        }
    }

    private sealed class DelayedTailStream : Stream
    {
        private readonly byte[] _prefix;
        private bool _prefixRead;
        private readonly TaskCompletionSource<bool> _releaseTail = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DelayedTailStream(byte[] prefix) => _prefix = prefix;

        public TaskCompletionSource<bool> FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public void ReleaseTail() => _releaseTail.TrySetResult(true);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_prefixRead)
            {
                _prefixRead = true;
                _prefix.CopyTo(buffer);
                FirstRead.TrySetResult(true);
                return _prefix.Length;
            }

            await _releaseTail.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
