using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Cenários de falha de rede e de mudança de caminho no cache de reprodução.
/// </summary>
/// <remarks>
/// T3.6 pede cobertura de rede lenta/interrompida e mudança de caminho. Os testes
/// existentes cobriam cancelamento, cota, parte truncada e recriação do cache, mas
/// não uma falha de transporte no meio do fetch nem a troca do caminho da mídia —
/// dois casos que decidem se um bloco corrompido pode ser servido como válido.
/// </remarks>
public class NebulaPlaybackCacheNetworkFailureTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nebula-cache-net-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task InterruptedNetwork_DoesNotPersistAPartialChunk()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);

            // A rede cai no meio do bloco: o fetch lança depois de já haver dados
            // parciais em trânsito. Persistir esse bloco truncado como completo
            // faria o leitor seguinte receber mídia corrompida direto do cache.
            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrFetchChunkAsync(
                "media-net",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: 1024,
                fetch: _ => throw new HttpRequestException("conexão encerrada pelo par"),
                cancellationToken: CancellationToken.None));

            // A tentativa seguinte precisa ir de novo à origem, não ler lixo.
            var fetched = 0;
            var payload = new byte[1024];
            payload[0] = 7;
            var result = await cache.GetOrFetchChunkAsync(
                "media-net",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: 1024,
                fetch: _ =>
                {
                    Interlocked.Increment(ref fetched);
                    return Task.FromResult(payload);
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, fetched);
            Assert.Equal(1024, result.Length);
            Assert.Equal(7, result[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ShortResponseFromOrigin_IsRejectedInsteadOfCachedAsComplete()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);

            // Rede lenta truncada: a origem devolve menos bytes do que o
            // declarado. Aceitar isso gravaria um bloco curto como definitivo.
            await Assert.ThrowsAnyAsync<IOException>(() => cache.GetOrFetchChunkAsync(
                "media-short",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: 2048,
                fetch: _ => Task.FromResult(new byte[512]),
                cancellationToken: CancellationToken.None));

            // E a origem é consultada de novo, com o tamanho correto aceito.
            var complete = await cache.GetOrFetchChunkAsync(
                "media-short",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: 2048,
                fetch: _ => Task.FromResult(new byte[2048]),
                cancellationToken: CancellationToken.None);

            Assert.Equal(2048, complete.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MediaPathChange_UsesADistinctCacheEntry()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);

            var firstPayload = new byte[256];
            firstPayload[0] = 1;
            await cache.GetOrFetchChunkAsync(
                "N:/Series/Show/ep1.mkv",
                0,
                0,
                256,
                _ => Task.FromResult(firstPayload),
                CancellationToken.None);

            // A mídia foi movida (reclassificação de categoria, por exemplo). O
            // caminho novo não pode servir o conteúdo cacheado do caminho antigo:
            // reaproveitar a entrada entregaria a mídia errada.
            var secondFetched = 0;
            var secondPayload = new byte[256];
            secondPayload[0] = 2;
            var moved = await cache.GetOrFetchChunkAsync(
                "N:/Animacoes/Show/ep1.mkv",
                0,
                0,
                256,
                _ =>
                {
                    Interlocked.Increment(ref secondFetched);
                    return Task.FromResult(secondPayload);
                },
                CancellationToken.None);

            Assert.Equal(1, secondFetched);
            Assert.Equal(2, moved[0]);

            // O caminho original continua servindo do cache, sem nova origem.
            var originalFetched = 0;
            var original = await cache.GetOrFetchChunkAsync(
                "N:/Series/Show/ep1.mkv",
                0,
                0,
                256,
                _ =>
                {
                    Interlocked.Increment(ref originalFetched);
                    return Task.FromResult(new byte[256]);
                },
                CancellationToken.None);

            Assert.Equal(0, originalFetched);
            Assert.Equal(1, original[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RepeatedNetworkFailures_DoNotLeakInflightSlots()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);

            // Uma falha que não liberasse o registro de operação em voo travaria
            // o bloco para sempre: o leitor seguinte esperaria uma operação que
            // já morreu.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrFetchChunkAsync(
                    "media-flaky",
                    0,
                    0,
                    128,
                    _ => throw new HttpRequestException("falha transitória"),
                    CancellationToken.None));
            }

            var recovered = await cache.GetOrFetchChunkAsync(
                "media-flaky",
                0,
                0,
                128,
                _ => Task.FromResult(new byte[128]),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(128, recovered.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CachePersistenceFailure_ReturnsFetchedBytesWithoutCachingPartialData()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            const string mediaKey = "blocked-cache-directory";

            // Substituir a raiz por arquivo simula cache indisponível sem depender
            // de ACL, root ou disco físico cheio. A identidade da mídia é hash, por
            // isso bloquear a raiz cobre qualquer subdiretório derivado da chave.
            Directory.Delete(cache.CachePath, recursive: true);
            await File.WriteAllTextAsync(cache.CachePath, "not a directory");

            var payload = new byte[1024];
            payload[0] = 7;
            var firstFetches = 0;
            var firstResult = await cache.GetOrFetchChunkAsync(
                mediaKey,
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: payload.Length,
                fetch: _ =>
                {
                    Interlocked.Increment(ref firstFetches);
                    return Task.FromResult(payload);
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, firstFetches);
            Assert.Equal(payload, firstResult);
            Assert.Equal(1, cache.CacheErrors);
            Assert.Equal(0, cache.GetCacheSizeBytes());
            Assert.True(File.Exists(cache.CachePath));

            // Sem chunk persistido, a requisição seguinte consulta a origem de novo.
            var secondPayload = new byte[1024];
            secondPayload[0] = 9;
            var secondFetches = 0;
            var secondResult = await cache.GetOrFetchChunkAsync(
                mediaKey,
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: secondPayload.Length,
                fetch: _ =>
                {
                    Interlocked.Increment(ref secondFetches);
                    return Task.FromResult(secondPayload);
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, secondFetches);
            Assert.Equal((byte)9, secondResult[0]);
            Assert.Equal(2, cache.CacheErrors);
            Assert.Equal(0, cache.GetCacheSizeBytes());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InterruptedCacheWrite_RemovesPartialFileAndAllowsRetry()
    {
        var root = CreateTempDirectory();
        try
        {
            var failFirstWrite = 1;
            using var cache = new NebulaPlaybackCache(
                root,
                NullLogger<NebulaPlaybackCache>.Instance,
                entryLifetime: null,
                maxCacheBytes: NebulaPlaybackCache.DefaultMaxCacheBytes,
                minimumFreeSpaceBytes: 0,
                writeChunkToDiskAsync: async (path, data, cancellationToken) =>
                {
                    if (Interlocked.Exchange(ref failFirstWrite, 0) == 1)
                    {
                        await File.WriteAllBytesAsync(path, data[..(data.Length / 2)], cancellationToken);
                        throw new IOException("simulated interruption after partial write");
                    }

                    await File.WriteAllBytesAsync(path, data, cancellationToken);
                });

            var firstPayload = new byte[1024];
            firstPayload[0] = 3;
            var firstFetches = 0;
            var firstResult = await cache.GetOrFetchChunkAsync(
                "partial-write-media",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: firstPayload.Length,
                fetch: _ =>
                {
                    Interlocked.Increment(ref firstFetches);
                    return Task.FromResult(firstPayload);
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(firstPayload, firstResult);
            Assert.Equal(1, firstFetches);
            Assert.Equal(1, cache.CacheErrors);
            Assert.Equal(0, cache.GetCacheSizeBytes());
            Assert.Empty(Directory.GetFiles(cache.CachePath, "*.partial", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(cache.CachePath, "*.bin", SearchOption.AllDirectories));

            var retryPayload = new byte[1024];
            retryPayload[0] = 8;
            var retryFetches = 0;
            var retryResult = await cache.GetOrFetchChunkAsync(
                "partial-write-media",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: retryPayload.Length,
                fetch: _ =>
                {
                    Interlocked.Increment(ref retryFetches);
                    return Task.FromResult(retryPayload);
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, retryFetches);
            Assert.Equal(retryPayload, retryResult);
            Assert.Equal(retryPayload.Length, cache.GetCacheSizeBytes());
            Assert.Empty(Directory.GetFiles(cache.CachePath, "*.partial", SearchOption.AllDirectories));

            var unexpectedFetches = 0;
            var cachedResult = await cache.GetOrFetchChunkAsync(
                "partial-write-media",
                partIndex: 0,
                chunkIndex: 0,
                expectedLength: retryPayload.Length,
                fetch: _ =>
                {
                    Interlocked.Increment(ref unexpectedFetches);
                    return Task.FromResult(Array.Empty<byte>());
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, unexpectedFetches);
            Assert.Equal(retryPayload, cachedResult);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
