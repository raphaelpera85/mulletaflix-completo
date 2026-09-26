using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public sealed class NebulaPlaybackCacheTests
{
    [Fact]
    public async Task FetchesEachChunkOnceAndServesItFromDisk()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var lease = cache.Acquire("media-1");
            var calls = 0;

            var first = await cache.GetOrFetchChunkAsync(
                "media-1",
                0,
                0,
                4,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new byte[] { 1, 2, 3, 4 });
                },
                CancellationToken.None);
            var second = await cache.GetOrFetchChunkAsync(
                "media-1",
                0,
                0,
                4,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(new byte[] { 9 });
                },
                CancellationToken.None);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, first);
            Assert.Equal(first, second);
            Assert.Equal(1, calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupDoesNotRemoveAnActivePlayback()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            using var lease = cache.Acquire("media-2");
            await cache.GetOrFetchChunkAsync("media-2", 0, 0, 1, _ => Task.FromResult(new byte[] { 7 }), CancellationToken.None);

            cache.CleanupExpiredEntries(DateTime.UtcNow.AddHours(2));

            Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupDoesNotRemoveInactivePlaybackBeforeFiveMinutes()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-3", 0, 0, 1, _ => Task.FromResult(new byte[] { 8 }), CancellationToken.None);

            // Simula passagem de apenas 3 minutos (abaixo dos 5 minutos configurados)
            cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(3));

            Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupRemovesInactivePlaybackAfterFiveMinutes()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-4", 0, 0, 1, _ => Task.FromResult(new byte[] { 8 }), CancellationToken.None);

            // Simula passagem de 6 minutos (acima dos 5 minutos configurados)
            cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(6));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupDeletesExpiredEntriesAcrossMultipleDirectoriesEvenIfOneIsLocked()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cache = new NebulaPlaybackCache(root, NullLogger<NebulaPlaybackCache>.Instance);
            await cache.GetOrFetchChunkAsync("media-locked", 0, 0, 1, _ => Task.FromResult(new byte[] { 1 }), CancellationToken.None);
            await cache.GetOrFetchChunkAsync("media-free", 0, 0, 1, _ => Task.FromResult(new byte[] { 2 }), CancellationToken.None);

            var files = Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories);
            var lockedFile = files.First(f => f.Contains("000000-00000000.bin", StringComparison.Ordinal));

            // Abre o arquivo com FileShare.None para bloquear exclusão deste arquivo específico
            using (var fileLock = File.Open(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Limpeza executada após 6 minutos
                cache.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(6));
            }

            // O diretório livre DEVE ter sido removido com sucesso mesmo com o outro bloqueado
            var remainingFiles = Directory.GetFiles(Path.Combine(root, "nebula-playback"), "*.bin", SearchOption.AllDirectories);
            Assert.Contains(remainingFiles, f => f == lockedFile);
            Assert.DoesNotContain(remainingFiles, f => f != lockedFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nebula-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
