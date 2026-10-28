using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.SystemBackupService;

/// <summary>
/// Lets an independently stored plugin database participate in full-system backups.
/// </summary>
public interface IBackupDatabaseParticipant
{
    /// <summary>Stable archive identity, unique across the server.</summary>
    string Name { get; }

    /// <summary>Writes this participant's durable state below its archive prefix.</summary>
    Task WriteBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken);

    /// <summary>Validates the participant data before restore mutates server state.</summary>
    Task ValidateBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken);

    /// <summary>Restores participant data idempotently; startup recovery may replay this call.</summary>
    Task RestoreBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken);
}
