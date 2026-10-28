using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaUploadPartIntegrityTests
{
    [Fact]
    public void ComputeSha256_MatchesTheBytesSentToTelegram()
    {
        var bytes = Encoding.UTF8.GetBytes("same-length payload");

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), NebulaUploadPartIntegrity.ComputeSha256(bytes));
    }

    [Fact]
    public async Task ComputeFilePartSha256Async_DetectsChangedBytesWithSameSizeAndTimestamp()
    {
        var path = Path.Combine(Path.GetTempPath(), "NebulaPartHash-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("abcdefgh"));
            var timestamp = File.GetLastWriteTimeUtc(path);
            var original = await NebulaUploadPartIntegrity.ComputeFilePartSha256Async(path, 0, 8, 4, CancellationToken.None);

            await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("ABCDEFGH"));
            File.SetLastWriteTimeUtc(path, timestamp);
            var changed = await NebulaUploadPartIntegrity.ComputeFilePartSha256Async(path, 0, 8, 4, CancellationToken.None);

            Assert.NotEqual(original, changed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeFilePartSha256Async_UsesPartOffsetsAndShortFinalPart()
    {
        var path = Path.Combine(Path.GetTempPath(), "NebulaPartHash-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("abcdefghij"));

            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes("efgh"))),
                await NebulaUploadPartIntegrity.ComputeFilePartSha256Async(path, 1, 10, 4, CancellationToken.None));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes("ij"))),
                await NebulaUploadPartIntegrity.ComputeFilePartSha256Async(path, 2, 10, 4, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
