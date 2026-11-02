using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.MediaEncoding.Hls.Cache;
using MulletaFlix.MediaEncoding.Hls.Extractors;
using MulletaFlix.MediaEncoding.Keyframes;
using MediaBrowser.Controller.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.MediaEncoding.Hls.Tests.Cache;

public class CacheDecoratorTests
{
    [Fact]
    public async Task TryExtractKeyframesAsync_CacheHit_DoesNotUseSyncRepositoryOrExtractor()
    {
        var itemId = Guid.NewGuid();
        var data = new KeyframeData(10, new long[] { 0 });
        var repository = new TestRepository(data);
        var extractor = new TestExtractor(null);
        var decorator = new CacheDecorator(repository, extractor, NullLogger<CacheDecorator>.Instance);

        var actual = await decorator.TryExtractKeyframesAsync(itemId, "file.mkv", CancellationToken.None);

        Assert.Same(data, actual);
        Assert.Equal(itemId, repository.ReadItemId);
        Assert.Equal(0, extractor.SyncCalls);
        Assert.Equal(0, extractor.AsyncCalls);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task TryExtractKeyframesAsync_CacheMiss_ExtractsAndAwaitsAsyncSave()
    {
        var itemId = Guid.NewGuid();
        var data = new KeyframeData(10, new long[] { 0 });
        var repository = new TestRepository(null);
        var extractor = new TestExtractor(data);
        var decorator = new CacheDecorator(repository, extractor, NullLogger<CacheDecorator>.Instance);

        var actual = await decorator.TryExtractKeyframesAsync(itemId, "file.mkv", CancellationToken.None);

        Assert.Same(data, actual);
        Assert.Equal(0, extractor.SyncCalls);
        Assert.Equal(1, extractor.AsyncCalls);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(itemId, repository.SavedItemId);
        Assert.Same(data, repository.SavedData);
    }

    [Fact]
    public async Task TryExtractKeyframesAsync_CanceledToken_StopsBeforeExtraction()
    {
        var repository = new TestRepository(null);
        var extractor = new TestExtractor(new KeyframeData(10, new long[] { 0 }));
        var decorator = new CacheDecorator(repository, extractor, NullLogger<CacheDecorator>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.TryExtractKeyframesAsync(Guid.NewGuid(), "file.mkv", cancellationTokenSource.Token).AsTask());

        Assert.Equal(0, extractor.SyncCalls);
        Assert.Equal(0, extractor.AsyncCalls);
        Assert.Equal(0, repository.Saves);
    }

    private sealed class TestRepository : IKeyframeRepository
    {
        private readonly IReadOnlyList<KeyframeData> _data;

        public TestRepository(KeyframeData? data)
        {
            _data = data is null ? Array.Empty<KeyframeData>() : new[] { data };
        }

        public Guid? ReadItemId { get; private set; }

        public int Saves { get; private set; }

        public Guid SavedItemId { get; private set; }

        public KeyframeData? SavedData { get; private set; }

        public IReadOnlyList<KeyframeData> GetKeyframeData(Guid itemId) => throw new InvalidOperationException("Synchronous repository access is not expected.");

        public Task<IReadOnlyList<KeyframeData>> GetKeyframeDataAsync(Guid itemId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadItemId = itemId;
            return Task.FromResult(_data);
        }

        public Task SaveKeyframeDataAsync(Guid itemId, KeyframeData data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Saves++;
            SavedItemId = itemId;
            SavedData = data;
            return Task.CompletedTask;
        }

        public Task DeleteKeyframeDataAsync(Guid itemId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestExtractor : IKeyframeExtractor
    {
        private readonly KeyframeData? _data;

        public TestExtractor(KeyframeData? data)
        {
            _data = data;
        }

        public int SyncCalls { get; private set; }

        public int AsyncCalls { get; private set; }

        public bool IsMetadataBased => true;

        public bool TryExtractKeyframes(Guid itemId, string filePath, out KeyframeData? keyframeData)
        {
            SyncCalls++;
            keyframeData = _data;
            return _data is not null;
        }

        public ValueTask<KeyframeData?> TryExtractKeyframesAsync(Guid itemId, string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncCalls++;
            return ValueTask.FromResult(_data);
        }
    }
}
