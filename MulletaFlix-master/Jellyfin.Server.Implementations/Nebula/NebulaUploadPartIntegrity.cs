using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Content identity for the exact logical chunks persisted as Nebula upload parts.</summary>
internal static class NebulaUploadPartIntegrity
{
    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static async Task<string> ComputeFilePartSha256Async(
        string path,
        int partNumber,
        long sourceSize,
        int chunkSizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (partNumber < 0 || sourceSize <= 0 || chunkSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partNumber));
        }

        var offset = checked((long)partNumber * chunkSizeBytes);
        if (offset >= sourceSize)
        {
            throw new InvalidDataException("A parte Nebula excede o tamanho da origem.");
        }

        var remaining = Math.Min(chunkSizeBytes, sourceSize - offset);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Seek(offset, SeekOrigin.Begin);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("A origem mudou ou foi truncada durante a validação de uma parte Nebula.");
            }

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static bool IsSha256(string? value)
        => value is { Length: 64 } && IsHex(value);

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
