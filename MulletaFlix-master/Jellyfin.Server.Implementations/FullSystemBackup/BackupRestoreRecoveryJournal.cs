using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MulletaFlix.Server.Implementations.FullSystemBackup;

/// <summary>
/// Persists the identity of an in-progress restore so startup can safely replay it.
/// </summary>
internal sealed class BackupRestoreRecoveryJournal
{
    internal sealed record BackupRestoreRecoveryJournalEntry(
        int FormatVersion,
        string OperationId,
        string ArchivePath,
        long ArchiveLength,
        long ArchiveLastWriteTimeUtcTicks,
        string ArchiveSha256);

    private const int CurrentFormatVersion = 1;
    private const string JournalDirectoryName = "restore-recovery";
    private const string JournalFileName = "active-restore.json";
    private const string OperationLeaseFileName = "restore-operation.lock";
    private readonly string _directoryPath;
    private readonly string _journalPath;
    private readonly string _programDataPath;

    public BackupRestoreRecoveryJournal(string programDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programDataPath);
        _programDataPath = Path.GetFullPath(programDataPath);
        _directoryPath = Path.Combine(_programDataPath, JournalDirectoryName);
        _journalPath = Path.Combine(_directoryPath, JournalFileName);
    }

    public string JournalPath => _journalPath;

    public bool JournalExists()
    {
        using var directory = TryOpenRecoveryDirectory();
        return directory?.FileExists(JournalFileName) ?? false;
    }

    public FileStream AcquireOperationLease()
    {
        if (OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Restore operation leases require Windows or Linux.");
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Restore operation leases require Windows or Linux.");
        }

        using var directory = OpenRecoveryDirectory(create: true);
        var lease = directory.OpenOrCreateFile(OperationLeaseFileName);
        try
        {
            directory.ValidateTrustedFile(lease);
            lease.Lock(0, 1);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public async Task<BackupRestoreRecoveryJournalEntry?> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var directory = TryOpenRecoveryDirectory();
        if (directory is null)
        {
            return null;
        }

        try
        {
            await using var stream = directory.OpenExistingFile(JournalFileName);
            directory.ValidateTrustedFile(stream);
            var entry = await JsonSerializer.DeserializeAsync<BackupRestoreRecoveryJournalEntry>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry is null
                || entry.FormatVersion != CurrentFormatVersion
                || !Guid.TryParseExact(entry.OperationId, "N", out _)
                || string.IsNullOrWhiteSpace(entry.ArchivePath)
                || entry.ArchiveLength < 0
                || entry.ArchiveLastWriteTimeUtcTicks < 0
                || string.IsNullOrWhiteSpace(entry.ArchiveSha256)
                || entry.ArchiveSha256.Length != 64
                || !entry.ArchiveSha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("The interrupted restore journal is invalid or uses an unsupported format.");
            }

            return entry;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The interrupted restore journal is corrupt; startup recovery cannot continue safely.", exception);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return null;
        }
    }

    public async Task<BackupRestoreRecoveryJournalEntry> CreateAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        var fullArchivePath = Path.GetFullPath(archivePath);
        await using var stream = new FileStream(
            fullArchivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await CreateAsync(fullArchivePath, stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackupRestoreRecoveryJournalEntry> CreateAsync(
        string archivePath,
        Stream archiveStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        if (!archiveStream.CanRead || !archiveStream.CanSeek)
        {
            throw new ArgumentException("The open backup archive stream must be readable and seekable.", nameof(archiveStream));
        }

        var fullArchivePath = Path.GetFullPath(archivePath);
        var archiveInfo = new FileInfo(fullArchivePath);
        if (!archiveInfo.Exists)
        {
            throw new FileNotFoundException("The backup archive required for restore recovery was not found.", fullArchivePath);
        }

        var originalPosition = archiveStream.Position;
        byte[] hash;
        long length;
        try
        {
            length = archiveStream.Length;
            archiveStream.Position = 0;
            hash = await SHA256.HashDataAsync(archiveStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            archiveStream.Position = originalPosition;
        }

        var entry = new BackupRestoreRecoveryJournalEntry(
            CurrentFormatVersion,
            Guid.NewGuid().ToString("N"),
            fullArchivePath,
            length,
            archiveInfo.LastWriteTimeUtc.Ticks,
            Convert.ToHexString(hash));

        await WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task VerifyArchiveAsync(BackupRestoreRecoveryJournalEntry entry, CancellationToken cancellationToken = default)
    {
        var identity = await GetArchiveIdentityAsync(entry.ArchivePath, cancellationToken).ConfigureAwait(false);
        VerifyArchiveIdentity(entry, identity.Length, Convert.FromHexString(identity.Sha256));
    }

    public async Task VerifyArchiveAsync(
        BackupRestoreRecoveryJournalEntry entry,
        Stream archiveStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        if (!archiveStream.CanRead || !archiveStream.CanSeek)
        {
            throw new ArgumentException("The open backup archive stream must be readable and seekable.", nameof(archiveStream));
        }

        var originalPosition = archiveStream.Position;
        byte[] hash;
        long length;
        try
        {
            length = archiveStream.Length;
            archiveStream.Position = 0;
            hash = await SHA256.HashDataAsync(archiveStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            archiveStream.Position = originalPosition;
        }

        VerifyArchiveIdentity(entry, length, hash);
    }

    private static void VerifyArchiveIdentity(
        BackupRestoreRecoveryJournalEntry entry,
        long length,
        byte[] hash)
    {
        if (length != entry.ArchiveLength
            || !CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(entry.ArchiveSha256)))
        {
            throw new InvalidDataException("The backup for the interrupted restore is missing or has changed; startup recovery was not attempted.");
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var directory = TryOpenRecoveryDirectory();
        if (directory is not null)
        {
            directory.DeleteFileIfExists(JournalFileName);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAsync(BackupRestoreRecoveryJournalEntry entry, CancellationToken cancellationToken)
    {
        using var directory = OpenRecoveryDirectory(create: true);
        var temporaryName = $"{JournalFileName}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = directory.CreateNewFile(temporaryName))
            {
                await JsonSerializer.SerializeAsync(stream, entry, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            directory.Rename(temporaryName, directory, JournalFileName, replaceExisting: true);
        }
        finally
        {
            directory.DeleteNonDirectoryEntryIfExists(temporaryName);
        }
    }

    private BackupRestoreDirectory? TryOpenRecoveryDirectory()
    {
        try
        {
            return OpenRecoveryDirectory(create: false);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return null;
        }
    }

    private BackupRestoreDirectory OpenRecoveryDirectory(bool create)
    {
        using var programData = BackupRestoreDirectory.OpenRoot(_programDataPath);
        programData.ValidateTrustedWriteBoundary();

        if (create)
        {
            programData.ValidateTrustedNamespaceParent();
        }

        var directory = create
            ? programData.OpenOrCreateDirectory(JournalDirectoryName)
            : programData.OpenDirectory(JournalDirectoryName);
        try
        {
            directory.ValidateTrustedWriteBoundary();
            return directory;
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }

    private static async Task<(long Length, long LastWriteTimeUtcTicks, string Sha256)> GetArchiveIdentityAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        var before = new FileInfo(archivePath);
        if (!before.Exists)
        {
            throw new FileNotFoundException("The backup archive required for restore recovery was not found.", archivePath);
        }

        var length = before.Length;
        var lastWriteTimeUtcTicks = before.LastWriteTimeUtc.Ticks;
        await using var stream = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        var after = new FileInfo(archivePath);
        after.Refresh();
        if (!after.Exists || after.Length != length || after.LastWriteTimeUtc.Ticks != lastWriteTimeUtcTicks)
        {
            throw new IOException("The backup archive changed while its recovery identity was being checked.");
        }

        return (length, lastWriteTimeUtcTicks, Convert.ToHexString(hash));
    }
}
