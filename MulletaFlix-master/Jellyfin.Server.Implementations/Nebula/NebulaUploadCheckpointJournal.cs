using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Persists Telegram send intents and acknowledged parts outside Mongo so an
/// ambiguous send can be reconciled before an automatic retry.
/// </summary>
internal sealed class NebulaUploadCheckpointJournal
{
    private const int CurrentFormatVersion = 2;
    private static readonly object s_fileLocksSync = new();
    private static readonly Dictionary<string, JournalLockEntry> s_fileLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly string _directory;

    internal sealed record Snapshot(IReadOnlyList<NebulaFilePart> Parts, IReadOnlySet<int> PendingPartNumbers, IReadOnlyDictionary<int, string> PendingPartSha256, bool RequiresNewGeneration = false);

    public NebulaUploadCheckpointJournal(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    public async Task<IReadOnlyList<NebulaFilePart>> LoadAsync(
        string fileUuid,
        long sourceSize,
        long sourceLastWriteUtcTicks,
        int totalParts,
        int chunkSizeBytes,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(fileUuid, sourceSize, sourceLastWriteUtcTicks, totalParts, chunkSizeBytes, cancellationToken).ConfigureAwait(false);
        return snapshot.Parts;
    }

    public async Task<Snapshot> LoadSnapshotAsync(
        string fileUuid,
        long sourceSize,
        long sourceLastWriteUtcTicks,
        int totalParts,
        int chunkSizeBytes,
        CancellationToken cancellationToken)
    {
        var path = GetPath(fileUuid);
        using var fileLock = await AcquireFileLockAsync(path, cancellationToken).ConfigureAwait(false);
        return await LoadSnapshotCoreAsync(
            fileUuid,
            path,
            sourceSize,
            sourceLastWriteUtcTicks,
            totalParts,
            chunkSizeBytes,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Snapshot> LoadSnapshotCoreAsync(
        string fileUuid,
        string path,
        long sourceSize,
        long sourceLastWriteUtcTicks,
        int totalParts,
        int chunkSizeBytes,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new Snapshot(Array.Empty<NebulaFilePart>(), new HashSet<int>(), new Dictionary<int, string>());
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        UploadCheckpoint? checkpoint;
        try
        {
            checkpoint = await JsonSerializer.DeserializeAsync<UploadCheckpoint>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("O checkpoint local do upload Nebula está corrompido; o envio foi interrompido para evitar reenvio cego.", ex);
        }

        if (checkpoint is null || !string.Equals(checkpoint.FileUuid, fileUuid, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("O checkpoint local do upload Nebula não corresponde ao UUID atual.");
        }

        if (checkpoint.FormatVersion == 1 || checkpoint.SourceSize != sourceSize || checkpoint.SourceLastWriteUtcTicks != sourceLastWriteUtcTicks)
        {
            return new Snapshot(Array.Empty<NebulaFilePart>(), new HashSet<int>(), new Dictionary<int, string>(), RequiresNewGeneration: true);
        }

        if (checkpoint.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException("A versão do checkpoint local do upload Nebula não é suportada.");
        }

        var uniqueParts = new Dictionary<int, NebulaFilePart>();
        var requiresNewGeneration = false;
        foreach (var part in checkpoint.Parts ?? [])
        {
            if (!IsValidPart(part, sourceSize, totalParts, chunkSizeBytes))
            {
                throw new InvalidDataException("O checkpoint local do upload Nebula contém uma parte inválida; o envio foi interrompido para evitar reenvio cego.");
            }

            if (!uniqueParts.TryAdd(part.PartNumber, part))
            {
                throw new InvalidDataException("O checkpoint local do upload Nebula contém números de partes confirmadas duplicados.");
            }

            requiresNewGeneration |= !NebulaUploadPartIntegrity.IsSha256(part.SourceSha256);
        }

        var pendingParts = new HashSet<int>();
        var pendingSha256 = new Dictionary<int, string>();
        var pendingHashes = new Dictionary<int, string>();
        foreach (var item in checkpoint.PendingPartSha256 ?? [])
        {
            if (item is null || item.PartNumber < 0 || item.PartNumber >= totalParts ||
                !NebulaUploadPartIntegrity.IsSha256(item.Sha256) || !pendingHashes.TryAdd(item.PartNumber, item.Sha256))
            {
                throw new InvalidDataException("O checkpoint local do upload Nebula contém hashes de partes pendentes inválidos.");
            }
        }

        foreach (var partNumber in checkpoint.PendingPartNumbers ?? [])
        {
            if (partNumber < 0 || partNumber >= totalParts)
            {
                throw new InvalidDataException("O checkpoint local do upload Nebula contém uma intenção de parte inválida.");
            }

            if (!pendingParts.Add(partNumber))
            {
                throw new InvalidDataException("O checkpoint local do upload Nebula contém números de partes pendentes duplicados.");
            }

            if (!uniqueParts.ContainsKey(partNumber))
            {
                if (pendingHashes.TryGetValue(partNumber, out var sha256) && NebulaUploadPartIntegrity.IsSha256(sha256))
                {
                    pendingSha256[partNumber] = sha256;
                }
                else
                {
                    requiresNewGeneration = true;
                }
            }
        }

        return new Snapshot(uniqueParts.Values.OrderBy(static part => part.PartNumber).ToArray(), pendingParts, pendingSha256, requiresNewGeneration);
    }

    public async Task MarkPartPendingAsync(
        string fileUuid,
        long sourceSize,
        long sourceLastWriteUtcTicks,
        int totalParts,
        int chunkSizeBytes,
        int partNumber,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        if (partNumber < 0 || partNumber >= totalParts || !NebulaUploadPartIntegrity.IsSha256(sourceSha256))
        {
            throw new InvalidDataException("Não é possível registrar uma intenção de parte fora dos limites do upload.");
        }

        var path = GetPath(fileUuid);
        using var fileLock = await AcquireFileLockAsync(path, cancellationToken).ConfigureAwait(false);
        var existing = await LoadSnapshotCoreAsync(fileUuid, path, sourceSize, sourceLastWriteUtcTicks, totalParts, chunkSizeBytes, cancellationToken).ConfigureAwait(false);
        if (existing.Parts.Any(part => part.PartNumber == partNumber))
        {
            return;
        }

        var pending = existing.PendingPartNumbers.ToHashSet();
        pending.Add(partNumber);
        var pendingHashes = existing.PendingPartSha256.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        if (pendingHashes.TryGetValue(partNumber, out var previousHash) && !string.Equals(previousHash, sourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A origem da parte mudou durante uma tentativa de upload Nebula.");
        }

        pendingHashes[partNumber] = sourceSha256.ToLowerInvariant();
        await WriteCheckpointAsync(
            fileUuid,
            new UploadCheckpoint
            {
                FormatVersion = CurrentFormatVersion,
                FileUuid = fileUuid,
                SourceSize = sourceSize,
                SourceLastWriteUtcTicks = sourceLastWriteUtcTicks,
                Parts = existing.Parts.ToList(),
                PendingPartNumbers = pending.Order().ToList(),
                PendingPartSha256 = pendingHashes.OrderBy(static pair => pair.Key).Select(static pair => new PendingPartHash { PartNumber = pair.Key, Sha256 = pair.Value }).ToList()
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePartAsync(
        string fileUuid,
        long sourceSize,
        long sourceLastWriteUtcTicks,
        int totalParts,
        int chunkSizeBytes,
        NebulaFilePart part,
        CancellationToken cancellationToken)
    {
        if (!IsValidPart(part, sourceSize, totalParts, chunkSizeBytes))
        {
            throw new InvalidDataException("Não é possível persistir uma parte inválida no checkpoint local do upload Nebula.");
        }

        var path = GetPath(fileUuid);
        using var fileLock = await AcquireFileLockAsync(path, cancellationToken).ConfigureAwait(false);
        var existing = await LoadSnapshotCoreAsync(fileUuid, path, sourceSize, sourceLastWriteUtcTicks, totalParts, chunkSizeBytes, cancellationToken).ConfigureAwait(false);
        var parts = existing.Parts.ToDictionary(static item => item.PartNumber);
        parts[part.PartNumber] = part;
        var checkpoint = new UploadCheckpoint
        {
            FormatVersion = CurrentFormatVersion,
            FileUuid = fileUuid,
            SourceSize = sourceSize,
            SourceLastWriteUtcTicks = sourceLastWriteUtcTicks,
            Parts = parts.Values.OrderBy(static item => item.PartNumber).ToList(),
            PendingPartNumbers = existing.PendingPartNumbers.Where(number => number != part.PartNumber).Order().ToList(),
            PendingPartSha256 = existing.PendingPartSha256.Where(pair => pair.Key != part.PartNumber).OrderBy(static pair => pair.Key).Select(static pair => new PendingPartHash { PartNumber = pair.Key, Sha256 = pair.Value }).ToList()
        };

        await WriteCheckpointAsync(fileUuid, checkpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteCheckpointAsync(string fileUuid, UploadCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var path = GetPath(fileUuid);
        Directory.CreateDirectory(_directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, checkpoint, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void Delete(string fileUuid)
    {
        var path = GetPath(fileUuid);
        using var fileLock = AcquireFileLock(path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPath(string fileUuid)
    {
        if (!Guid.TryParse(fileUuid, out var parsed))
        {
            throw new InvalidDataException("O identificador do upload Nebula não é um UUID válido.");
        }

        return Path.Combine(_directory, parsed.ToString("N") + ".json");
    }

    private static async Task<FileLockLease> AcquireFileLockAsync(string path, CancellationToken cancellationToken)
    {
        var (key, entry) = AddLockReference(path);
        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new FileLockLease(key, entry);
        }
        catch
        {
            RemoveLockReference(key, entry, releaseSemaphore: false);
            throw;
        }
    }

    private static FileLockLease AcquireFileLock(string path)
    {
        var (key, entry) = AddLockReference(path);
        try
        {
            entry.Semaphore.Wait();
            return new FileLockLease(key, entry);
        }
        catch
        {
            RemoveLockReference(key, entry, releaseSemaphore: false);
            throw;
        }
    }

    private static (string Key, JournalLockEntry Entry) AddLockReference(string path)
    {
        var key = Path.GetFullPath(path);
        lock (s_fileLocksSync)
        {
            if (!s_fileLocks.TryGetValue(key, out var entry))
            {
                entry = new JournalLockEntry();
                s_fileLocks.Add(key, entry);
            }

            entry.ReferenceCount++;
            return (key, entry);
        }
    }

    private static void RemoveLockReference(string key, JournalLockEntry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (s_fileLocksSync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 && s_fileLocks.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                s_fileLocks.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class JournalLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class FileLockLease(string key, JournalLockEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                RemoveLockReference(key, entry, releaseSemaphore: true);
            }
        }
    }

    private static bool IsValidPart(NebulaFilePart part, long sourceSize, int totalParts, int chunkSizeBytes)
    {
        if (part is null || part.PartNumber < 0 || part.PartNumber >= totalParts ||
            string.IsNullOrWhiteSpace(part.TgFileId) || part.TgMessageId <= 0 || part.TgChatId == 0 ||
            part.BotIndex < 0 || !string.Equals(part.Status, "completed", StringComparison.OrdinalIgnoreCase) || !NebulaUploadPartIntegrity.IsSha256(part.SourceSha256))
        {
            return false;
        }

        var offset = (long)part.PartNumber * chunkSizeBytes;
        var expectedSize = Math.Min(chunkSizeBytes, sourceSize - offset);
        return expectedSize > 0 && part.Size == expectedSize;
    }

    private sealed class UploadCheckpoint
    {
        public int FormatVersion { get; set; }

        public string FileUuid { get; set; } = string.Empty;

        public long SourceSize { get; set; }

        public long SourceLastWriteUtcTicks { get; set; }

        public List<NebulaFilePart> Parts { get; set; } = [];

        public List<int> PendingPartNumbers { get; set; } = [];

        public List<PendingPartHash> PendingPartSha256 { get; set; } = [];
    }

    private sealed class PendingPartHash
    {
        public int PartNumber { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }
}
