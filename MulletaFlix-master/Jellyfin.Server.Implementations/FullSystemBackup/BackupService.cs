using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Server.Implementations.StorageHelpers;

namespace MulletaFlix.Server.Implementations.FullSystemBackup;

/// <summary>
/// Contains methods for creating and restoring backups.
/// </summary>
public class BackupService : IBackupService, IDisposable
{
    private sealed class CountingReadStream : Stream
    {
        private readonly Stream _inner;

        public CountingReadStream(Stream inner)
        {
            _inner = inner;
        }

        public long BytesRead { get; private set; }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            BytesRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            BytesRead += read;
            return read;
        }
    }

    private sealed record DomainDatabaseContext(string Name, DbContext Context);

    private sealed record ForeignKeyValidationPlan(
        Type DependentType,
        string DependentName,
        string DependentPath,
        Type PrincipalType,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> DependentProperties,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> PrincipalProperties,
        string RelationshipName,
        string PrincipalSetKey);

    private sealed class DomainDatabaseConnectionScope : IDisposable
    {
        private readonly List<(DbContext Context, DbConnection OriginalConnection)> _connections = [];

        public DomainDatabaseConnectionScope(IEnumerable<DomainDatabaseContext> contexts, DbConnection sharedConnection)
        {
            try
            {
                foreach (var domainContext in contexts)
                {
                    var connection = domainContext.Context.Database.GetDbConnection();
                    if (ReferenceEquals(connection, sharedConnection))
                    {
                        continue;
                    }

                    if (connection.State != System.Data.ConnectionState.Closed
                        || !string.Equals(connection.DataSource, sharedConnection.DataSource, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(connection.Database, sharedConnection.Database, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Domain database context '{domainContext.Name}' does not target the same closed database connection as the primary context.");
                    }

                    domainContext.Context.Database.SetDbConnection(sharedConnection, contextOwnsConnection: false);
                    _connections.Add((domainContext.Context, connection));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (var index = _connections.Count - 1; index >= 0; index--)
            {
                var (context, originalConnection) = _connections[index];
                context.Database.UseTransaction((DbTransaction?)null);
                context.Database.SetDbConnection(originalConnection, contextOwnsConnection: false);
            }

            _connections.Clear();
        }
    }

    private static readonly string[] SupportedDomainDatabaseContexts = ["Movies", "Series", "Channels", "Books"];
    private const long BackupGiB = 1024L * 1024 * 1024;
    private const long MaxBackupArchiveBytes = 257 * BackupGiB;
    private const long MaxBackupCentralDirectoryBytes = 32 * 1024 * 1024;
    private const int MaxBackupManifestBytes = 16 * 1024 * 1024;
    private const long MaxBackupExpandedBytes = 256 * BackupGiB;
    private const long MaxBackupEntryBytes = 64 * BackupGiB;
    private const int MaxBackupArchiveEntries = 100_000;
    private const double MaxBackupCompressionRatio = 1_000;
    public const string MeterName = "MulletaFlix.Server.Backup";
    private static readonly Meter BackupMeter = new(MeterName);
    private static readonly Counter<long> BackupOperationCounter = BackupMeter.CreateCounter<long>(
        "mulletaflix.backup.operations",
        unit: "{operation}",
        description: "Completed or rejected full-system backup operations.");
    private static readonly Histogram<double> BackupOperationDuration = BackupMeter.CreateHistogram<double>(
        "mulletaflix.backup.operation.duration",
        unit: "s",
        description: "Duration of full-system backup operations.");
    private static readonly UpDownCounter<long> ActiveBackupOperations = BackupMeter.CreateUpDownCounter<long>(
        "mulletaflix.backup.active_operations",
        unit: "{operation}",
        description: "Currently admitted full-system backup operations.");

    private const string ManifestEntryName = "manifest.json";
    private const string BackupHistoryFileName = "BackupExecutionHistory.json";
    private const int MaxBackupHistoryEntries = 50;
    private readonly ILogger<BackupService> _logger;
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly IDbContextFactory<MoviesDbContext>? _moviesDbProvider;
    private readonly IDbContextFactory<SeriesDbContext>? _seriesDbProvider;
    private readonly IDbContextFactory<ChannelsDbContext>? _channelsDbProvider;
    private readonly IDbContextFactory<BooksDbContext>? _booksDbProvider;
    private readonly IBackupDatabaseParticipant[] _backupDatabaseParticipants;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IServerApplicationPaths _applicationPaths;
    private readonly IMulletaFlixDatabaseProvider _MulletaFlixDatabaseProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Func<Func<Task>, Task>? _relationalCommitForTesting;
    private readonly Action<string>? _restoreBackupSidecarCreatedForTesting;
    private readonly SemaphoreSlim _backupHistoryLock = new(1, 1);
    private int _backupOperationInProgress;
    private static readonly JsonSerializerOptions _serializerSettings = new JsonSerializerOptions(JsonSerializerDefaults.General)
    {
        AllowTrailingCommas = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private readonly Version _backupEngineVersion = new Version(0, 2, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="dbProvider">A Database Factory.</param>
    /// <param name="applicationHost">The Application host.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="MulletaFlixDatabaseProvider">The MulletaFlix database Provider in use.</param>
    /// <param name="applicationLifetime">The SystemManager.</param>
    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime)
        : this(logger, dbProvider, applicationHost, applicationPaths, MulletaFlixDatabaseProvider, applicationLifetime, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes the backup service with all relational contexts used by the server.
    /// </summary>
    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime,
        IDbContextFactory<MoviesDbContext>? moviesDbProvider,
        IDbContextFactory<SeriesDbContext>? seriesDbProvider,
        IDbContextFactory<ChannelsDbContext>? channelsDbProvider,
        IDbContextFactory<BooksDbContext>? booksDbProvider)
        : this(
            logger,
            dbProvider,
            applicationHost,
            applicationPaths,
            MulletaFlixDatabaseProvider,
            applicationLifetime,
            moviesDbProvider,
            seriesDbProvider,
            channelsDbProvider,
            booksDbProvider,
            null)
    {
    }

    /// <summary>Initializes the backup service with registered plugin database participants.</summary>
    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime,
        IDbContextFactory<MoviesDbContext>? moviesDbProvider,
        IDbContextFactory<SeriesDbContext>? seriesDbProvider,
        IDbContextFactory<ChannelsDbContext>? channelsDbProvider,
        IDbContextFactory<BooksDbContext>? booksDbProvider,
        IEnumerable<IBackupDatabaseParticipant> backupDatabaseParticipants)
        : this(
            logger,
            dbProvider,
            applicationHost,
            applicationPaths,
            MulletaFlixDatabaseProvider,
            applicationLifetime,
            moviesDbProvider,
            seriesDbProvider,
            channelsDbProvider,
            booksDbProvider,
            null,
            null,
            backupDatabaseParticipants)
    {
    }

    internal BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime,
        IDbContextFactory<MoviesDbContext>? moviesDbProvider,
        IDbContextFactory<SeriesDbContext>? seriesDbProvider,
        IDbContextFactory<ChannelsDbContext>? channelsDbProvider,
        IDbContextFactory<BooksDbContext>? booksDbProvider,
        Func<Func<Task>, Task>? relationalCommitForTesting,
        Action<string>? restoreBackupSidecarCreatedForTesting = null,
        IEnumerable<IBackupDatabaseParticipant>? backupDatabaseParticipants = null)
    {
        _logger = logger;
        _dbProvider = dbProvider;
        _moviesDbProvider = moviesDbProvider;
        _seriesDbProvider = seriesDbProvider;
        _channelsDbProvider = channelsDbProvider;
        _booksDbProvider = booksDbProvider;
        _backupDatabaseParticipants = (backupDatabaseParticipants ?? []).OrderBy(participant => participant.Name, StringComparer.Ordinal).ToArray();
        if (_backupDatabaseParticipants.Select(participant => participant.Name).Distinct(StringComparer.Ordinal).Count() != _backupDatabaseParticipants.Length)
        {
            throw new InvalidOperationException("Backup database participant names must be unique.");
        }
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _MulletaFlixDatabaseProvider = MulletaFlixDatabaseProvider;
        _hostApplicationLifetime = applicationLifetime;
        _relationalCommitForTesting = relationalCommitForTesting;
        _restoreBackupSidecarCreatedForTesting = restoreBackupSidecarCreatedForTesting;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private async Task<DomainDatabaseContext[]> CreateDomainDatabaseContextsAsync(CancellationToken cancellationToken)
    {
        var factories = new object?[] { _moviesDbProvider, _seriesDbProvider, _channelsDbProvider, _booksDbProvider };
        if (factories.All(factory => factory is null))
        {
            return [];
        }

        if (factories.Any(factory => factory is null))
        {
            throw new InvalidOperationException("All four media-domain database factories must be registered for a complete backup.");
        }

        var contexts = new List<DomainDatabaseContext>(SupportedDomainDatabaseContexts.Length);
        try
        {
            contexts.Add(new("Movies", await _moviesDbProvider!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false)));
            contexts.Add(new("Series", await _seriesDbProvider!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false)));
            contexts.Add(new("Channels", await _channelsDbProvider!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false)));
            contexts.Add(new("Books", await _booksDbProvider!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false)));
            return contexts.ToArray();
        }
        catch
        {
            await DisposeDomainDatabaseContextsAsync(contexts).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask DisposeDomainDatabaseContextsAsync(IEnumerable<DomainDatabaseContext> contexts)
    {
        foreach (var domainContext in contexts)
        {
            await domainContext.Context.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backupHistoryLock.Dispose();
        }
    }

    /// <inheritdoc/>
    public void ScheduleRestoreAndRestartServer(string archivePath)
    {
        ThrowIfRestorePlatformUnsupported();

        const string operation = "schedule_restore";
        var startedAt = Stopwatch.GetTimestamp();
        if (!TryBeginOperation(operation))
        {
            throw new BackupOperationInProgressException();
        }

        try
        {
            _applicationHost.RestoreBackupPath = archivePath;
            _applicationHost.ShouldRestart = true;
            _applicationHost.NotifyPendingRestart();
            RecordOperation(operation, "scheduled", startedAt);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    _hostApplicationLifetime.StopApplication();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during delayed application stop after restore");
                    Environment.Exit(1);
                }
            });
        }
        catch
        {
            CompleteOperation(operation, "failure", startedAt);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task RestoreBackupAsync(string archivePath)
        => RestoreBackupInternalAsync(archivePath, null, "restore");

    /// <summary>
    /// Replays a restore interrupted by a previous process before the server starts accepting requests.
    /// </summary>
    public async Task<string?> RecoverInterruptedRestoreAsync()
    {
        var journal = new BackupRestoreRecoveryJournal(_applicationPaths.ProgramDataPath);
        if (!IsRestorePlatformSupported())
        {
            if (journal.JournalExists())
            {
                throw new PlatformNotSupportedException("An interrupted restore is pending, but restore recovery requires Windows or Linux.");
            }

            return null;
        }

        EnsureRecoveryJournalIsOutsideRestoreRoots(journal.JournalPath);
        using var operationLease = journal.AcquireOperationLease();
        var pendingRestore = await journal.ReadAsync().ConfigureAwait(false);
        if (pendingRestore is null)
        {
            return null;
        }

        await journal.VerifyArchiveAsync(pendingRestore).ConfigureAwait(false);
        _logger.LogCritical(
            "An interrupted full-system restore was found. Replaying backup operation {OperationId} before server initialization.",
            pendingRestore.OperationId);
        await RestoreBackupInternalAsync(pendingRestore.ArchivePath, pendingRestore, "restore_recovery", operationLease).ConfigureAwait(false);
        return pendingRestore.ArchivePath;
    }

    private async Task RestoreBackupInternalAsync(
        string archivePath,
        BackupRestoreRecoveryJournal.BackupRestoreRecoveryJournalEntry? pendingRestore,
        string operation,
        FileStream? operationLease = null)
    {
        ThrowIfRestorePlatformUnsupported();

        var startedAt = Stopwatch.GetTimestamp();
        if (!TryBeginOperation(operation))
        {
            throw new BackupOperationInProgressException();
        }

        var result = "failure";
        FileStream? ownedOperationLease = null;
        try
        {
            var journal = new BackupRestoreRecoveryJournal(_applicationPaths.ProgramDataPath);
            if (operationLease is null)
            {
                try
                {
                    EnsureRecoveryJournalIsOutsideRestoreRoots(journal.JournalPath);
                    ownedOperationLease = journal.AcquireOperationLease();
                }
                catch (Win32Exception exception) when (pendingRestore is null && (exception.NativeErrorCode is 2 or 3) && !File.Exists(archivePath))
                {
                    throw new FileNotFoundException($"Requested backup file '{archivePath}' does not exist.");
                }
            }
            else
            {
                EnsureRecoveryJournalIsOutsideRestoreRoots(journal.JournalPath);
            }

            if (pendingRestore is null && await journal.ReadAsync().ConfigureAwait(false) is not null)
            {
                throw new InvalidOperationException("A previous full-system restore is pending startup recovery; no new restore can be started.");
            }

            await RestoreBackupCoreAsync(archivePath, pendingRestore, journal).ConfigureAwait(false);
            result = "success";
        }
        finally
        {
            ownedOperationLease?.Dispose();
            CompleteOperation(operation, result, startedAt);
        }
    }

    private async Task RestoreBackupCoreAsync(
        string archivePath,
        BackupRestoreRecoveryJournal.BackupRestoreRecoveryJournalEntry? pendingRestore,
        BackupRestoreRecoveryJournal journal)
    {
        _logger.LogWarning("Begin restoring system to {BackupArchive}", archivePath); // Info isn't cutting it
        var fileStream = OpenTrustedRestoreArchive(archivePath);
        await using (fileStream.ConfigureAwait(false))
        {
            await ValidateArchiveIntegrityAsync(fileStream).ConfigureAwait(false);
            StorageHelper.TestCommonPathsForStorageCapacityReadOnly(_applicationPaths, _logger);
            fileStream.Position = 0;
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, false);
            var zipArchiveEntry = zipArchive.GetEntry(ManifestEntryName);

            if (zipArchiveEntry is null)
            {
                throw new NotSupportedException($"The loaded archive '{archivePath}' does not appear to be a MulletaFlix backup as its missing the '{ManifestEntryName}'.");
            }

            BackupManifest? manifest;
            var manifestStream = await zipArchiveEntry.OpenAsync().ConfigureAwait(false);
            await using (manifestStream.ConfigureAwait(false))
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, _serializerSettings).ConfigureAwait(false);
            }

            if (manifest!.ServerVersion > _applicationHost.ApplicationVersion) // newer versions of MulletaFlix should be able to load older versions as we have migrations.
            {
                throw new NotSupportedException($"The loaded archive '{archivePath}' is made for a newer version of MulletaFlix ({manifest.ServerVersion}) and cannot be loaded in this version.");
            }

            if (!TestBackupVersionCompatibility(manifest.BackupEngineVersion))
            {
                throw new NotSupportedException($"The loaded archive '{archivePath}' is made for a newer version of MulletaFlix ({manifest.ServerVersion}) and cannot be loaded in this version.");
            }

            ValidateArchiveEntryPaths(zipArchive, manifest);

            if (!manifest.Options.Database && manifest.DatabaseParticipants is { Length: > 0 })
            {
                throw new InvalidDataException("The backup declares database participants while database backup is disabled.");
            }

            if (manifest.Options.Database)
            {
                if (manifest.DatabaseParticipants is not null)
                {
                    if (manifest.DatabaseParticipants.Distinct(StringComparer.Ordinal).Count() != manifest.DatabaseParticipants.Length)
                    {
                        throw new InvalidDataException("The backup declares duplicate database participants.");
                    }

                    foreach (var participantName in manifest.DatabaseParticipants)
                    {
                        var participant = _backupDatabaseParticipants.SingleOrDefault(candidate => candidate.Name == participantName)
                            ?? throw new NotSupportedException($"Backup database participant '{participantName}' is not available. Enable or install the corresponding plugin before restoring this backup.");
                        await participant.ValidateBackupAsync(zipArchive, GetParticipantEntryPrefix(participantName), CancellationToken.None).ConfigureAwait(false);
                    }
                }

                if (manifest.DomainDatabaseContexts is not null && _MulletaFlixDatabaseProvider is not IMulletaFlixDomainDatabaseProvider)
                {
                    throw new NotSupportedException("The configured database provider does not support restoring media-domain database contexts.");
                }

                await using var validationContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                ValidateDatabaseBackupEntries(manifest, zipArchive, validationContext.Model);
                await ValidateDatabaseBackupContentAsync(zipArchive, validationContext.Model).ConfigureAwait(false);

                if (manifest.DomainDatabaseContexts is not null)
                {
                    var domainContexts = await CreateDomainDatabaseContextsAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        using var connectionScope = new DomainDatabaseConnectionScope(domainContexts, validationContext.Database.GetDbConnection());
                        ValidateDomainDatabaseBackupEntries(manifest, zipArchive, domainContexts);
                        await ValidateDomainDatabaseBackupContentAsync(zipArchive, domainContexts).ConfigureAwait(false);
                    }
                    finally
                    {
                        await DisposeDomainDatabaseContextsAsync(domainContexts).ConfigureAwait(false);
                    }

                }
            }

            EnsureRecoveryJournalIsOutsideRestoreRoots(journal.JournalPath);
            if (pendingRestore is null)
            {
                pendingRestore = await journal.CreateAsync(archivePath, fileStream).ConfigureAwait(false);
                await journal.VerifyArchiveAsync(pendingRestore, fileStream).ConfigureAwait(false);
                await journal.VerifyArchiveAsync(pendingRestore).ConfigureAwait(false);
            }
            else
            {
                await journal.VerifyArchiveAsync(pendingRestore, fileStream).ConfigureAwait(false);
            }

            var fileRestore = new FileSystemRestoreTransaction(_logger, pendingRestore.OperationId, _restoreBackupSidecarCreatedForTesting);
            var relationalCommitAttempted = false;
            try
            {

                async Task CopyDirectoryAsync(string source, string target, string[]? exclude = null)
                {
                    var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    var fullSourcePath = NormalizePathSeparator(Path.GetFullPath(source) + Path.DirectorySeparatorChar);
                    var normalizedTargetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
                    var fullTargetRoot = normalizedTargetRoot + Path.DirectorySeparatorChar;
                    var excludePaths = exclude?.Select(e => $"{source}/{e}/").ToArray();
                    foreach (var item in zipArchive.Entries)
                    {
                        var sourcePath = NormalizePathSeparator(Path.GetFullPath(item.FullName));
                        var targetPath = Path.GetFullPath(Path.Combine(target, Path.GetRelativePath(source, item.FullName)));

                        if (excludePaths is not null && excludePaths.Any(e => item.FullName.StartsWith(e, pathComparison)))
                        {
                            continue;
                        }

                        if (!sourcePath.StartsWith(fullSourcePath, pathComparison)
                            || !targetPath.StartsWith(fullTargetRoot, pathComparison)
                            || Path.EndsInDirectorySeparator(item.FullName))
                        {
                            continue;
                        }

                        _logger.LogInformation("Restore and override {File}", targetPath);
                        await fileRestore.ApplyEntryAsync(item, targetPath, normalizedTargetRoot).ConfigureAwait(false);
                    }
                }

                await CopyDirectoryAsync("Config", _applicationPaths.ConfigurationDirectoryPath).ConfigureAwait(false);
                await CopyDirectoryAsync("Data", _applicationPaths.DataPath, exclude: ["metadata", "metadata-default"]).ConfigureAwait(false);
                await CopyDirectoryAsync("Root", _applicationPaths.RootFolderPath).ConfigureAwait(false);
                await CopyDirectoryAsync("Data/metadata", _applicationPaths.InternalMetadataPath).ConfigureAwait(false);
                await CopyDirectoryAsync("Data/metadata-default", _applicationPaths.DefaultInternalMetadataPath).ConfigureAwait(false);

                if (manifest.Options.Database)
                {
                    _logger.LogInformation("Begin restoring Database");
                    var domainContexts = manifest.DomainDatabaseContexts is not null
                        ? await CreateDomainDatabaseContextsAsync(CancellationToken.None).ConfigureAwait(false)
                        : [];

                    try
                    {
                        var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                        await using (dbContext.ConfigureAwait(false))
                        {
                            using var domainConnectionScope = new DomainDatabaseConnectionScope(domainContexts, dbContext.Database.GetDbConnection());
                            // restore migration history manually
                            var historyEntry = zipArchive.GetEntry(NormalizePathSeparator(Path.Combine("Database", $"{nameof(HistoryRow)}.json")));
                            if (historyEntry is null)
                            {
                                _logger.LogInformation("No backup of the history table in archive. This is required for MulletaFlix operation");
                                throw new InvalidOperationException("Cannot restore backup that has no History data.");
                            }

                            HistoryRow[] historyEntries;
                            var historyArchive = await historyEntry.OpenAsync().ConfigureAwait(false);
                            await using (historyArchive.ConfigureAwait(false))
                            {
                                historyEntries = await JsonSerializer.DeserializeAsync<HistoryRow[]>(historyArchive).ConfigureAwait(false) ??
                                    throw new InvalidOperationException("Cannot restore backup that has no History data.");
                            }

                            var historyRepository = dbContext.GetService<IHistoryRepository>();
                            await historyRepository.CreateIfNotExistsAsync().ConfigureAwait(false);

                            await using var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
                            // Validate and sanitize migration IDs before executing SQL to prevent injection.
                            foreach (var item in await historyRepository.GetAppliedMigrationsAsync(CancellationToken.None).ConfigureAwait(false))
                            {
                                var sanitizedId = SanitizeMigrationId(item.MigrationId);
                                var insertScript = historyRepository.GetDeleteScript(sanitizedId);
                                await dbContext.Database.ExecuteSqlRawAsync(insertScript).ConfigureAwait(false);
                            }

                            foreach (var item in historyEntries)
                            {
                                var sanitizedItem = new HistoryRow(SanitizeMigrationId(item.MigrationId), item.ProductVersion);
                                var insertScript = historyRepository.GetInsertScript(sanitizedItem);
                                await dbContext.Database.ExecuteSqlRawAsync(insertScript).ConfigureAwait(false);
                            }

                            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                            var entityTypes = typeof(MulletaFlixDbContext).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                .Where(e => e.PropertyType.IsAssignableTo(typeof(IQueryable)))
                                .Select(e =>
                                {
                                    var entityType = e.PropertyType.GetGenericArguments()[0];
                                    var tableName = dbContext.Model.FindEntityType(entityType)!.GetSchemaQualifiedTableName()!;
                                    return (Type: e, Name: tableName, Set: e.GetValue(dbContext) as IQueryable);
                                })
                                .ToArray();

                            var tableNames = entityTypes.Select(f => dbContext.Model.FindEntityType(f.Type.PropertyType.GetGenericArguments()[0])!.GetSchemaQualifiedTableName()!);
                            _logger.LogInformation("Begin purging database");
                            await _MulletaFlixDatabaseProvider.PurgeDatabase(dbContext, tableNames).ConfigureAwait(false);
                            _logger.LogInformation("Database Purged");

                            foreach (var entityType in entityTypes)
                            {
                                _logger.LogInformation("Read backup of {Table}", entityType.Name);

                                var zipEntry = zipArchive.GetEntry(NormalizePathSeparator(Path.Combine("Database", $"{entityType.Name}.json")));
                                if (zipEntry is null)
                                {
                                    _logger.LogInformation("No backup of expected table {Table} is present in backup, continuing anyway", entityType.Name);
                                    continue;
                                }

                                var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
                                await using (zipEntryStream.ConfigureAwait(false))
                                {
                                    _logger.LogInformation("Restore backup of {Table}", entityType.Name);
                                    var records = 0;
                                    await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
                                    {
                                        var entity = item.Deserialize(entityType.Type.PropertyType.GetGenericArguments()[0]);
                                        if (entity is null)
                                        {
                                            throw new InvalidOperationException($"Cannot deserialize an entity for table '{entityType.Name}'.");
                                        }

                                        try
                                        {
                                            dbContext.Add(entity);
                                            records++;
                                        }
                                        catch (Exception exception)
                                        {
                                            throw new InvalidDataException(
                                                $"Cannot restore a record for database table '{entityType.Name}'.",
                                                exception);
                                        }
                                    }

                                    _logger.LogInformation("Prepared to restore {Number} entries for {Table}", records, entityType.Name);
                                }
                            }

                            _logger.LogInformation("Try restore Database");
                            await dbContext.SaveChangesAsync().ConfigureAwait(false);

                            foreach (var domainContext in domainContexts)
                            {
                                await RestoreDomainDatabaseContextAsync(zipArchive, domainContext, transaction.GetDbTransaction()).ConfigureAwait(false);
                            }

                            relationalCommitAttempted = true;
                            if (_relationalCommitForTesting is not null)
                            {
                                await _relationalCommitForTesting(
                                    () => transaction.CommitAsync()).ConfigureAwait(false);
                            }
                            else
                            {
                                await transaction.CommitAsync().ConfigureAwait(false);
                            }

                            _logger.LogInformation("Restored primary and media-domain databases");
                        }
                    }
                    finally
                    {
                        await DisposeDomainDatabaseContextsAsync(domainContexts).ConfigureAwait(false);
                    }

                    // Plugin stores can use independent connections. Startup replay reruns
                    // this replacement if the process stops between its commit and file commit.
                    foreach (var participantName in manifest.DatabaseParticipants ?? [])
                    {
                        var participant = _backupDatabaseParticipants.Single(candidate => candidate.Name == participantName);
                        await participant.RestoreBackupAsync(zipArchive, GetParticipantEntryPrefix(participantName), CancellationToken.None).ConfigureAwait(false);
                    }
                }

                try
                {
                    fileRestore.Commit();
                }
                catch
                {
                    fileRestore.PreserveForRecovery();
                    if (!relationalCommitAttempted)
                    {
                        RequestRestartAfterInterruptedRestore();
                    }

                    throw;
                }

                await journal.DeleteAsync().ConfigureAwait(false);

                _logger.LogInformation("Restored MulletaFlix system from {Date}", manifest.DateCreated);
            }
            catch
            {
                if (relationalCommitAttempted)
                {
                    // CommitAsync may have succeeded even if its acknowledgement was lost.
                    // Preserve the restored files and journal so startup replay can converge
                    // the database and filesystem before the server accepts traffic.
                    fileRestore.PreserveForRecovery();
                    RequestRestartAfterInterruptedRestore();
                }
                else
                {
                    fileRestore.Dispose();
                    if (fileRestore.RollbackSucceeded)
                    {
                        await journal.DeleteAsync().ConfigureAwait(false);
                    }
                }

                throw;
            }
            finally
            {
                fileRestore.Dispose();
            }
        }
    }

    private static FileStream OpenTrustedRestoreArchive(string archivePath)
    {
        var fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Requested backup file '{archivePath}' does not exist.");
        }

        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("Restore archive must be inside a trusted directory.");
        using var parent = BackupRestoreDirectory.OpenRoot(parentPath);
        parent.ValidateTrustedNamespaceParent();
        var archive = parent.OpenExistingFile(Path.GetFileName(fullPath), FileAccess.Read, FileShare.Read);
        try
        {
            parent.ValidateTrustedFile(archive);
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private void RequestRestartAfterInterruptedRestore()
    {
        try
        {
            _applicationHost.ShouldRestart = true;
            _applicationHost.NotifyPendingRestart();
            _logger.LogCritical("Restore outcome is uncertain after relational commit; stopping the server for journal replay on startup.");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    _hostApplicationLifetime.StopApplication();
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Could not stop the server after an interrupted restore; terminating to prevent serving inconsistent state.");
                    Environment.Exit(1);
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Could not schedule server restart after an interrupted restore; terminating to prevent serving inconsistent state.");
            Environment.Exit(1);
        }
    }

    private void EnsureRecoveryJournalIsOutsideRestoreRoots(string journalPath)
    {
        var fullJournalPath = Path.GetFullPath(journalPath);
        var journalDirectoryPath = Path.GetDirectoryName(fullJournalPath)
            ?? throw new InvalidOperationException("The restore recovery journal must have a parent directory.");
        var restoreRoots = new[]
        {
            _applicationPaths.ConfigurationDirectoryPath,
            _applicationPaths.DataPath,
            _applicationPaths.RootFolderPath,
            _applicationPaths.InternalMetadataPath,
            _applicationPaths.DefaultInternalMetadataPath
        };

        if (restoreRoots.Any(root => IsPathWithinRoot(fullJournalPath, root)))
        {
            throw new InvalidOperationException("The configured program-data path places the restore recovery journal inside a directory that the restore replaces.");
        }

        using var programData = BackupRestoreDirectory.OpenRoot(_applicationPaths.ProgramDataPath);
        foreach (var rootPath in restoreRoots)
        {
            using var root = TryOpenRestoreRoot(rootPath);
            if (root is not null && root.IsSameOrAncestorOf(programData))
            {
                throw new InvalidOperationException("The configured program-data path physically places the restore recovery journal inside a directory that the restore replaces.");
            }
        }

        using var journalDirectory = TryOpenRestoreRoot(journalDirectoryPath);
        if (journalDirectory is null)
        {
            return;
        }

        if (!programData.IsOnSameMountAs(journalDirectory))
        {
            throw new InvalidOperationException("The restore recovery journal directory must not be a separate mount from program data.");
        }

        foreach (var rootPath in restoreRoots)
        {
            using var root = TryOpenRestoreRoot(rootPath);
            if (root is not null && root.IsSameOrAncestorOf(journalDirectory))
            {
                throw new InvalidOperationException("The configured program-data path physically aliases a directory that the restore replaces.");
            }
        }
    }

    private static BackupRestoreDirectory? TryOpenRestoreRoot(string path)
    {
        try
        {
            return BackupRestoreDirectory.OpenRoot(path);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return null;
        }
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", comparison)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, comparison)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison);
    }

    private sealed class FileSystemRestoreTransaction : IDisposable
    {
        private sealed class RestoredFile
        {
            public required string TargetPath { get; init; }

            public required BackupRestoreDirectory Directory { get; init; }

            public required string TargetName { get; init; }

            public required string StagingName { get; init; }

            public required string BackupName { get; init; }

            public required string OriginalAbsentName { get; init; }

            public bool OriginalMoved { get; set; }

            public bool OriginalWasAbsent { get; set; }

            public bool ReplacementInstalled { get; set; }
        }

        private sealed record CreatedDirectory(string Path, string Name, BackupRestoreDirectory Parent, BackupRestoreDirectory Directory);

        private readonly ILogger _logger;
        private readonly string _operationId;
        private readonly Action<string>? _backupSidecarCreatedForTesting;
        private readonly List<RestoredFile> _files = [];
        private readonly List<CreatedDirectory> _createdDirectories = [];
        private readonly Dictionary<string, BackupRestoreDirectory> _openDirectories = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly Dictionary<string, BackupRestoreDirectory> _restoreRoots = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly List<BackupRestoreDirectory> _directoryOrder = [];
        private readonly HashSet<string> _trackedTargets = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private bool _committed;
        private bool _disposed;
        private bool _preserveForRecovery;

        public FileSystemRestoreTransaction(ILogger logger, string operationId, Action<string>? backupSidecarCreatedForTesting)
        {
            _logger = logger;
            _operationId = operationId;
            _backupSidecarCreatedForTesting = backupSidecarCreatedForTesting;
        }

        public bool RollbackSucceeded { get; private set; } = true;

        public async Task ApplyEntryAsync(ZipArchiveEntry archiveEntry, string targetPath, string targetRoot)
        {
            EnsurePathWithinConfiguredRoot(targetPath, targetRoot);
            var fullTargetPath = Path.GetFullPath(targetPath);
            if (!_trackedTargets.Add(fullTargetPath))
            {
                throw new InvalidDataException("The backup archive contains multiple entries for the same restore destination.");
            }

            var targetDirectoryPath = Path.GetDirectoryName(fullTargetPath)!;
            var targetName = Path.GetFileName(fullTargetPath);
            BackupRestoreDirectory directory;
            try
            {
                directory = OpenTargetDirectory(targetDirectoryPath, targetRoot);
            }
            catch (IOException exception) when (exception.Message.Contains("reparse point", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("symbolic-link", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Backup restore destination contains a reparse point or symbolic link.", exception);
            }

            var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullTargetPath)));
            var sidecarPrefix = $".mfx-restore-{_operationId}-{targetHash}";
            var restoredFile = new RestoredFile
            {
                TargetPath = fullTargetPath,
                Directory = directory,
                TargetName = targetName,
                StagingName = $"{sidecarPrefix}.tmp",
                BackupName = $"{sidecarPrefix}.bak",
                OriginalAbsentName = $"{sidecarPrefix}.absent"
            };
            _files.Add(restoredFile);

            if (directory.FileExists(restoredFile.BackupName) && directory.FileExists(restoredFile.OriginalAbsentName))
            {
                throw new InvalidDataException("The restore recovery sidecars disagree about the target file's original state.");
            }

            if (directory.FileExists(restoredFile.BackupName))
            {
                using (var backup = directory.OpenExistingFile(restoredFile.BackupName))
                {
                    directory.ValidateTrustedFile(backup);
                }

                restoredFile.OriginalMoved = true;
            }
            else if (directory.FileExists(restoredFile.OriginalAbsentName))
            {
                restoredFile.OriginalWasAbsent = true;
            }
            else if (directory.FileExists(restoredFile.TargetName))
            {
                using (var original = directory.OpenExistingFile(restoredFile.TargetName))
                {
                    directory.ValidateTrustedFile(original);
                }

                directory.Rename(restoredFile.TargetName, directory, restoredFile.BackupName);
                restoredFile.OriginalMoved = true;
                _backupSidecarCreatedForTesting?.Invoke(Path.Combine(targetDirectoryPath, restoredFile.BackupName));
            }
            else
            {
                CreateOriginalAbsentMarker(directory, restoredFile.OriginalAbsentName);
                restoredFile.OriginalWasAbsent = true;
            }

            using (var source = archiveEntry.Open())
            {
                directory.DeleteNonDirectoryEntryIfExists(restoredFile.StagingName);
                await using (var destination = directory.CreateNewFile(restoredFile.StagingName))
                {
                    await source.CopyToAsync(destination).ConfigureAwait(false);
                    await destination.FlushAsync().ConfigureAwait(false);
                    destination.Flush(flushToDisk: true);
                }
            }

            if (directory.FileExists(restoredFile.TargetName))
            {
                directory.DeleteFile(restoredFile.TargetName);
            }

            directory.Rename(restoredFile.StagingName, directory, restoredFile.TargetName, replaceExisting: true);
            restoredFile.ReplacementInstalled = true;
        }

        public void Commit()
        {
            if (!CleanupTemporaryFiles(removeBackups: true))
            {
                PreserveForRecovery();
                throw new IOException("Restore sidecar cleanup failed; recovery journal must be replayed on startup.");
            }

            _committed = true;
            _createdDirectories.Clear();
        }

        public void PreserveForRecovery()
        {
            _preserveForRecovery = true;
            RollbackSucceeded = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_committed && !_preserveForRecovery)
            {
                RollbackSucceeded = Rollback();
                RollbackSucceeded &= CleanupTemporaryFiles(removeBackups: false);
                RollbackSucceeded &= RemoveCreatedDirectories();
            }

            for (var index = _directoryOrder.Count - 1; index >= 0; index--)
            {
                _directoryOrder[index].Dispose();
            }
        }

        private static void CreateOriginalAbsentMarker(BackupRestoreDirectory directory, string markerName)
        {
            using var marker = directory.CreateNewFile(markerName);
            marker.Flush(flushToDisk: true);
        }

        private BackupRestoreDirectory OpenTargetDirectory(string path, string targetRoot)
        {
            var fullRoot = Path.GetFullPath(targetRoot);
            if (!_restoreRoots.TryGetValue(fullRoot, out var rootAuthority))
            {
                rootAuthority = OpenTargetDirectoryCore(fullRoot, expectedMount: null, expectedRootPath: null);
                _restoreRoots.Add(fullRoot, rootAuthority);
            }

            return OpenTargetDirectoryCore(path, rootAuthority, fullRoot);
        }

        private BackupRestoreDirectory OpenTargetDirectoryCore(string path, BackupRestoreDirectory? expectedMount, string? expectedRootPath)
        {
            var fullPath = Path.GetFullPath(path);
            if (expectedMount is null && _openDirectories.TryGetValue(fullPath, out var existing))
            {
                EnsureSameRestoreMount(expectedMount, existing);
                existing.ValidateTrustedWriteBoundary();
                return existing;
            }

            BackupRestoreDirectory currentHandle;
            string currentPath;
            if (expectedMount is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(expectedRootPath);
                EnsurePathWithinConfiguredRoot(fullPath, expectedRootPath);
                currentHandle = expectedMount;
                currentPath = Path.GetFullPath(expectedRootPath);
            }
            else
            {
                var ancestorPath = fullPath;
                while (!Directory.Exists(ancestorPath))
                {
                    var parentPath = Path.GetDirectoryName(ancestorPath);
                    if (string.IsNullOrEmpty(parentPath) || string.Equals(parentPath, ancestorPath, StringComparison.Ordinal))
                    {
                        throw new IOException("Could not find an existing directory to open as the restore destination authority.");
                    }

                    ancestorPath = parentPath;
                    if (_openDirectories.TryGetValue(Path.GetFullPath(ancestorPath), out existing))
                    {
                        break;
                    }
                }

                var fullAncestorPath = Path.GetFullPath(ancestorPath);
                if (!_openDirectories.TryGetValue(fullAncestorPath, out currentHandle!))
                {
                    currentHandle = BackupRestoreDirectory.OpenRoot(fullAncestorPath);
                    try
                    {
                        EnsureSameRestoreMount(expectedMount, currentHandle);
                    }
                    catch
                    {
                        currentHandle.Dispose();
                        throw;
                    }

                    _openDirectories.Add(fullAncestorPath, currentHandle);
                    _directoryOrder.Add(currentHandle);
                }
                else
                {
                    EnsureSameRestoreMount(expectedMount, currentHandle);
                }

                currentPath = fullAncestorPath;
            }

            var relativePath = Path.GetRelativePath(currentPath, fullPath);
            if (!string.Equals(relativePath, ".", StringComparison.Ordinal))
            {
                foreach (var component in relativePath.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var childPath = Path.GetFullPath(Path.Combine(currentPath, component));
                    if (_openDirectories.TryGetValue(childPath, out var childHandle))
                    {
                        if (expectedMount is not null)
                        {
                            using var openedChild = currentHandle.OpenDirectory(component);
                            EnsureSameRestoreMount(expectedMount, openedChild);
                            if (!childHandle.IsSameDirectoryAs(openedChild))
                            {
                                throw new IOException("Restore destination directory changed after it was opened beneath its configured root.");
                            }
                        }

                        currentHandle = childHandle;
                        currentPath = childPath;
                        continue;
                    }

                    var parentHandle = currentHandle;
                    parentHandle.ValidateTrustedNamespaceParent();
                    childHandle = parentHandle.OpenOrCreateDirectory(component, out var created);
                    try
                    {
                        EnsureSameRestoreMount(expectedMount, childHandle);
                    }
                    catch
                    {
                        childHandle.Dispose();
                        throw;
                    }

                    _openDirectories.Add(childPath, childHandle);
                    _directoryOrder.Add(childHandle);
                    if (created)
                    {
                        _createdDirectories.Add(new CreatedDirectory(childPath, component, parentHandle, childHandle));
                    }

                    currentHandle = childHandle;
                    currentPath = childPath;
                }
            }

            currentHandle.ValidateTrustedWriteBoundary();
            return currentHandle;
        }

        private static void EnsureSameRestoreMount(BackupRestoreDirectory? expectedMount, BackupRestoreDirectory candidate)
        {
            if (expectedMount is not null && !expectedMount.IsOnSameMountAs(candidate))
            {
                throw new InvalidDataException("Restore destination crosses a mount boundary inside its configured root.");
            }
        }

        private bool Rollback()
        {
            var succeeded = true;
            for (var index = _files.Count - 1; index >= 0; index--)
            {
                var file = _files[index];
                try
                {
                    if (file.Directory.FileExists(file.BackupName) && file.Directory.FileExists(file.OriginalAbsentName))
                    {
                        succeeded = false;
                        _logger.LogCritical(
                            "Restore recovery sidecars disagree about the original state of {File}; preserving both sidecars for startup recovery.",
                            file.TargetPath);
                        continue;
                    }

                    if (file.Directory.FileExists(file.BackupName))
                    {
                        file.Directory.DeleteNonDirectoryEntryIfExists(file.TargetName);
                        file.Directory.Rename(file.BackupName, file.Directory, file.TargetName);
                    }
                    else if (file.Directory.FileExists(file.OriginalAbsentName))
                    {
                        file.Directory.DeleteNonDirectoryEntryIfExists(file.TargetName);
                        file.Directory.DeleteFile(file.OriginalAbsentName);
                    }
                    else if (file.ReplacementInstalled)
                    {
                        file.Directory.DeleteNonDirectoryEntryIfExists(file.TargetName);
                    }
                }
                catch (Exception exception)
                {
                    succeeded = false;
                    _logger.LogCritical(exception, "Failed to roll back restored file {File}; recovery copy is {RecoveryFile}", file.TargetPath, Path.Combine(Path.GetDirectoryName(file.TargetPath)!, file.BackupName));
                }
            }

            return succeeded;
        }

        private bool CleanupTemporaryFiles(bool removeBackups)
        {
            var succeeded = true;
            foreach (var file in _files)
            {
                foreach (var path in removeBackups
                    ? new[] { file.StagingName, file.BackupName, file.OriginalAbsentName }
                    : new[] { file.StagingName })
                {
                    try
                    {
                        file.Directory.DeleteNonDirectoryEntryIfExists(path);
                    }
                    catch (Exception exception)
                    {
                        succeeded = false;
                        _logger.LogWarning(exception, "Could not remove temporary restore file {File}", path);
                    }
                }
            }

            return succeeded;
        }

        private bool RemoveCreatedDirectories()
        {
            var succeeded = true;
            for (var index = _createdDirectories.Count - 1; index >= 0; index--)
            {
                var created = _createdDirectories[index];
                try
                {
                    _openDirectories.Remove(created.Path);
                    created.Directory.Dispose();
                    created.Parent.ValidateTrustedNamespaceParent();
                    created.Parent.DeleteDirectoryIfExists(created.Name);
                }
                catch (UnauthorizedAccessException)
                {
                    succeeded = false;
                    _logger.LogDebug("Leaving restore directory {Directory} in place because its parent is not a trusted write boundary.", created.Path);
                }
                catch (Exception exception)
                {
                    if (exception is IOException or UnauthorizedAccessException)
                    {
                        succeeded = false;
                        _logger.LogDebug(exception, "Could not remove empty restore directory {Directory}; preserving it.", created.Path);
                        continue;
                    }
                    succeeded = false;
                    _logger.LogWarning(exception, "Could not remove empty restore directory {Directory}.", created.Path);
                }
            }

            return succeeded;
        }

        private static void EnsurePathWithinConfiguredRoot(string path, string root)
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var fullPath = Path.GetFullPath(path);
            var relativePath = Path.GetRelativePath(fullRoot, fullPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, "..", comparison)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, comparison)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison))
            {
                throw new InvalidDataException("Restore archive entry escapes the configured destination.");
            }
        }
    }

    private bool TestBackupVersionCompatibility(Version backupEngineVersion)
    {
        if (backupEngineVersion == _backupEngineVersion)
        {
            return true;
        }

        return false;
    }

    private static void ValidateArchiveEntryPaths(ZipArchive archive, BackupManifest manifest)
    {
        var names = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var manifestFound = false;
        foreach (var entry in archive.Entries)
        {
            var name = NormalizePathSeparator(entry.FullName);
            var isDirectory = name.EndsWith('/');
            var path = isDirectory ? name[..^1] : name;
            var segments = path.Split('/');
            if (path.Length == 0
                || path.StartsWith('/')
                || path.Contains(':', StringComparison.Ordinal)
                || segments.Any(segment => segment.Length == 0 || segment is "." or "..")
                || (OperatingSystem.IsWindows() && segments.Any(segment =>
                    segment[^1] is ' ' or '.'
                    || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || IsWindowsDeviceName(segment))))
            {
                throw new InvalidDataException($"Backup archive contains an unsafe entry path '{entry.FullName}'.");
            }

            if (!names.Add(path))
            {
                throw new InvalidDataException($"Backup archive contains duplicate entry path '{entry.FullName}'.");
            }

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            var isUnixDirectory = unixType == 0x4000;
            var isUnixRegularFile = unixType == 0x8000;
            if ((unixType != 0 && !isUnixDirectory && !isUnixRegularFile)
                || (isDirectory && isUnixRegularFile)
                || (!isDirectory && isUnixDirectory))
            {
                throw new InvalidDataException($"Backup archive contains an unsupported entry type at '{entry.FullName}'.");
            }

            if (string.Equals(path, ManifestEntryName, StringComparison.Ordinal))
            {
                if (isDirectory || unixType == 0x4000)
                {
                    throw new InvalidDataException("Backup manifest entry must be a regular file.");
                }

                manifestFound = true;
                continue;
            }

            var rootComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var supportedRoot = string.Equals(segments[0], "Config", rootComparison)
                || string.Equals(segments[0], "Data", rootComparison)
                || string.Equals(segments[0], "Root", rootComparison)
                || (manifest.Options.Database && string.Equals(segments[0], "Database", rootComparison));
            if (!supportedRoot)
            {
                throw new InvalidDataException($"Backup archive contains an unsupported entry path '{entry.FullName}'.");
            }

            if (segments.Length < 2 && !(isDirectory && segments.Length == 1))
            {
                throw new InvalidDataException($"Backup archive contains an entry outside a supported directory: '{entry.FullName}'.");
            }
        }

        if (!manifestFound)
        {
            throw new InvalidDataException("Backup archive must contain exactly one manifest.json file.");
        }
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].TrimEnd(' ', '.');
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && (stem[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3'));
    }

    private static (string Name, Type Type)[] GetExpectedDatabaseEntities(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        Type contextType,
        bool includeHistory = true)
    {
        var entities = contextType
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.PropertyType.IsAssignableTo(typeof(IQueryable)))
            .Select(property =>
            {
                var entityType = property.PropertyType.GetGenericArguments()[0];
                var mappedEntity = model.FindEntityType(entityType)
                    ?? throw new InvalidOperationException($"The database model does not contain DbSet entity '{entityType.Name}'.");
                return (Name: mappedEntity.GetSchemaQualifiedTableName()!, Type: entityType);
            })
            .ToArray();

        return includeHistory
            ? entities.Append((Name: nameof(HistoryRow), Type: typeof(HistoryRow))).ToArray()
            : entities;
    }

    private static string[] GetExpectedDatabaseTableNames(Microsoft.EntityFrameworkCore.Metadata.IModel model)
        => GetExpectedDatabaseEntities(model, typeof(MulletaFlixDbContext)).Select(entity => entity.Name).Distinct(StringComparer.Ordinal).ToArray();

    private static void ValidateDatabaseBackupEntries(BackupManifest manifest, ZipArchive zipArchive, Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var expectedTableNames = GetExpectedDatabaseTableNames(model).ToHashSet(StringComparer.Ordinal);
        var declaredTableNames = manifest.DatabaseTables?.ToHashSet(StringComparer.Ordinal) ?? [];
        var legacyGenericDbSetDeclaration = declaredTableNames.Remove("DbSet`1");
        var missingDeclarations = expectedTableNames.Except(declaredTableNames, StringComparer.Ordinal)
            .Where(tableName => !legacyGenericDbSetDeclaration || tableName == nameof(HistoryRow))
            .ToArray();
        var unexpectedDeclarations = declaredTableNames.Except(expectedTableNames, StringComparer.Ordinal).ToArray();
        var missingEntries = expectedTableNames
            .Where(tableName => zipArchive.GetEntry(NormalizePathSeparator(Path.Combine("Database", $"{tableName}.json"))) is null)
            .ToArray();

        if (missingDeclarations.Length > 0 || unexpectedDeclarations.Length > 0 || missingEntries.Length > 0)
        {
            var details = new List<string>();
            if (missingDeclarations.Length > 0)
            {
                details.Add($"undeclared tables: {string.Join(", ", missingDeclarations)}");
            }

            if (unexpectedDeclarations.Length > 0)
            {
                details.Add($"unsupported tables: {string.Join(", ", unexpectedDeclarations)}");
            }

            if (missingEntries.Length > 0)
            {
                details.Add($"missing entries: {string.Join(", ", missingEntries)}");
            }

            throw new InvalidDataException($"Cannot restore an incomplete database backup ({string.Join("; ", details)}).");
        }
    }

    private static async Task ValidateDatabaseBackupContentAsync(ZipArchive zipArchive, Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var entityTypes = GetExpectedDatabaseEntities(model, typeof(MulletaFlixDbContext));
        var entryPaths = entityTypes.ToDictionary(
            entityType => entityType.Type,
            entityType => NormalizePathSeparator(Path.Combine("Database", $"{entityType.Name}.json")));
        var foreignKeyPlans = CreateForeignKeyValidationPlans(model, entityTypes, entryPaths);
        var principalKeys = CreatePrincipalKeySets(foreignKeyPlans);
        var principalPlansByType = foreignKeyPlans
            .GroupBy(plan => plan.PrincipalType)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var entityType in entityTypes)
        {
            var entryPath = entryPaths[entityType.Type];
            var zipEntry = zipArchive.GetEntry(entryPath)!;
            var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
            await using (zipEntryStream.ConfigureAwait(false))
            {
                var constraints = CreateUniqueConstraintTrackers(model.FindEntityType(entityType.Type));
                try
                {
                    await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
                    {
                        var entity = item?.Deserialize(entityType.Type, _serializerSettings);
                        if (entity is null)
                        {
                            throw new JsonException($"The backup entry '{entryPath}' contains a null or invalid entity.");
                        }

                        ValidateUniqueConstraints(entryPath, entity, constraints);
                        AddPrincipalKeys(entityType.Type, entity, principalPlansByType, principalKeys);
                    }

                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                {
                    throw new InvalidDataException($"Cannot restore database table '{entityType.Name}' because its JSON content is invalid or incompatible.", exception);
                }
            }
        }

        await ValidateForeignKeyReferencesAsync(zipArchive, foreignKeyPlans, principalKeys).ConfigureAwait(false);
    }

    private static string GetDomainDatabaseEntryPath(string contextName, string tableName)
        => NormalizePathSeparator(Path.Combine("Database", "Domain", contextName, $"{tableName}.json"));

    private static string GetParticipantEntryPrefix(string participantName)
    {
        if (string.IsNullOrWhiteSpace(participantName)
            || participantName is "." or ".."
            || participantName.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new InvalidDataException($"Invalid backup database participant name '{participantName}'.");
        }

        return NormalizePathSeparator(Path.Combine("Database", "Participants", participantName));
    }

    private static void ValidateDomainDatabaseBackupEntries(
        BackupManifest manifest,
        ZipArchive zipArchive,
        IReadOnlyCollection<DomainDatabaseContext> contexts)
    {
        var declaredContexts = manifest.DomainDatabaseContexts?.ToHashSet(StringComparer.Ordinal) ?? [];
        var expectedContexts = SupportedDomainDatabaseContexts.ToHashSet(StringComparer.Ordinal);
        var missingContexts = expectedContexts.Except(declaredContexts, StringComparer.Ordinal).ToArray();
        var unavailableContexts = expectedContexts.Except(contexts.Select(context => context.Name), StringComparer.Ordinal).ToArray();
        var unexpectedContexts = declaredContexts.Except(expectedContexts, StringComparer.Ordinal).ToArray();
        var missingEntries = contexts
            .SelectMany(context => GetExpectedDatabaseEntities(context.Context.Model, context.Context.GetType(), includeHistory: false)
                .Select(entity => GetDomainDatabaseEntryPath(context.Name, entity.Name)))
            .Where(entryPath => zipArchive.GetEntry(entryPath) is null)
            .ToArray();

        if (missingContexts.Length > 0 || unavailableContexts.Length > 0 || unexpectedContexts.Length > 0 || missingEntries.Length > 0)
        {
            var details = new List<string>();
            if (missingContexts.Length > 0)
            {
                details.Add($"undeclared domain contexts: {string.Join(", ", missingContexts)}");
            }

            if (unavailableContexts.Length > 0)
            {
                details.Add($"unavailable domain contexts: {string.Join(", ", unavailableContexts)}");
            }

            if (unexpectedContexts.Length > 0)
            {
                details.Add($"unsupported domain contexts: {string.Join(", ", unexpectedContexts)}");
            }

            if (missingEntries.Length > 0)
            {
                details.Add($"missing domain entries: {string.Join(", ", missingEntries)}");
            }

            throw new InvalidDataException($"Cannot restore an incomplete media-domain backup ({string.Join("; ", details)}).");
        }
    }

    private static async Task ValidateDomainDatabaseBackupContentAsync(
        ZipArchive zipArchive,
        IReadOnlyCollection<DomainDatabaseContext> contexts)
    {
        foreach (var context in contexts)
        {
            var entityTypes = GetExpectedDatabaseEntities(context.Context.Model, context.Context.GetType(), includeHistory: false);
            var entryPaths = entityTypes.ToDictionary(
                entityType => entityType.Type,
                entityType => GetDomainDatabaseEntryPath(context.Name, entityType.Name));
            var foreignKeyPlans = CreateForeignKeyValidationPlans(context.Context.Model, entityTypes, entryPaths);
            var principalKeys = CreatePrincipalKeySets(foreignKeyPlans);
            var principalPlansByType = foreignKeyPlans
                .GroupBy(plan => plan.PrincipalType)
                .ToDictionary(group => group.Key, group => group.ToArray());

            foreach (var entityType in entityTypes)
            {
                var entryPath = entryPaths[entityType.Type];
                var zipEntry = zipArchive.GetEntry(entryPath)!;
                await using var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
                var constraints = CreateUniqueConstraintTrackers(context.Context.Model.FindEntityType(entityType.Type));
                try
                {
                    await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
                    {
                        var entity = item?.Deserialize(entityType.Type, _serializerSettings);
                        if (entity is null)
                        {
                            throw new JsonException($"The backup entry '{entryPath}' contains a null or invalid entity.");
                        }

                        ValidateUniqueConstraints(entryPath, entity, constraints);
                        AddPrincipalKeys(entityType.Type, entity, principalPlansByType, principalKeys);
                    }
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                {
                    throw new InvalidDataException($"Cannot restore domain table '{context.Name}/{entityType.Name}' because its JSON content is invalid or incompatible.", exception);
                }
            }

            await ValidateForeignKeyReferencesAsync(zipArchive, foreignKeyPlans, principalKeys).ConfigureAwait(false);
        }
    }

    private static ForeignKeyValidationPlan[] CreateForeignKeyValidationPlans(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        IReadOnlyCollection<(string Name, Type Type)> entityTypes,
        IReadOnlyDictionary<Type, string> entryPaths)
    {
        var exportedTypes = entityTypes.Select(entityType => entityType.Type).ToHashSet();
        var plans = new List<ForeignKeyValidationPlan>();
        foreach (var entityType in entityTypes)
        {
            // HistoryRow is exported through a provider-specific DbSet but is not part of the EF model.
            var dependentEntity = model.FindEntityType(entityType.Type);
            if (dependentEntity is null)
            {
                continue;
            }

            foreach (var foreignKey in dependentEntity.GetForeignKeys())
            {
                var principalType = foreignKey.PrincipalEntityType.ClrType;
                if (!exportedTypes.Contains(principalType))
                {
                    continue;
                }

                if (foreignKey.Properties.Any(property => property.IsShadowProperty())
                    || foreignKey.PrincipalKey.Properties.Any(property => property.IsShadowProperty()))
                {
                    throw new InvalidDataException(
                        $"Cannot validate database table '{entityType.Name}' because relationship '{foreignKey.GetConstraintName() ?? foreignKey.PrincipalEntityType.Name}' uses a shadow key that is not represented by a CLR property.");
                }

                var principalKeySetKey = BuildPrincipalKeySetKey(principalType, foreignKey.PrincipalKey.Properties);
                var relationshipName = foreignKey.GetConstraintName()
                    ?? $"{dependentEntity.Name}({string.Join(", ", foreignKey.Properties.Select(property => property.Name))})";
                plans.Add(new ForeignKeyValidationPlan(
                    entityType.Type,
                    entityType.Name,
                    entryPaths[entityType.Type],
                    principalType,
                    foreignKey.Properties,
                    foreignKey.PrincipalKey.Properties,
                    relationshipName,
                    principalKeySetKey));
            }
        }

        return plans.ToArray();
    }

    private static Dictionary<string, HashSet<string>> CreatePrincipalKeySets(IEnumerable<ForeignKeyValidationPlan> plans)
        => plans.Select(plan => plan.PrincipalSetKey)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

    private static void AddPrincipalKeys(
        Type entityType,
        object entity,
        IReadOnlyDictionary<Type, ForeignKeyValidationPlan[]> plansByPrincipalType,
        IDictionary<string, HashSet<string>> principalKeys)
    {
        if (!plansByPrincipalType.TryGetValue(entityType, out var plans))
        {
            return;
        }

        foreach (var plan in plans)
        {
            principalKeys[plan.PrincipalSetKey].Add(CreateCanonicalKey(entity, plan.PrincipalProperties));
        }
    }

    private static async Task ValidateForeignKeyReferencesAsync(
        ZipArchive zipArchive,
        IReadOnlyCollection<ForeignKeyValidationPlan> plans,
        IReadOnlyDictionary<string, HashSet<string>> principalKeys)
    {
        foreach (var dependentGroup in plans.GroupBy(plan => (plan.DependentType, plan.DependentPath)))
        {
            var entry = zipArchive.GetEntry(dependentGroup.Key.DependentPath)!;
            await using var stream = await entry.OpenAsync().ConfigureAwait(false);
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(stream, _serializerSettings).ConfigureAwait(false))
            {
                var entity = item?.Deserialize(dependentGroup.Key.DependentType, _serializerSettings);
                if (entity is null)
                {
                    throw new InvalidDataException($"Cannot restore database table '{dependentGroup.First().DependentName}' because its JSON content contains a null or invalid entity.");
                }

                foreach (var plan in dependentGroup)
                {
                    var values = GetPropertyValues(entity, plan.DependentProperties);
                    if (values.Where((value, index) => value is null && !plan.DependentProperties[index].IsNullable).Any())
                    {
                        throw CreateInvalidForeignKeyException(plan);
                    }

                    if (values.Any(value => value is null))
                    {
                        // Optional composite foreign keys use MATCH SIMPLE: a null component does not reference a principal row.
                        continue;
                    }

                    var key = CreateCanonicalKey(values, plan.DependentProperties);
                    if (!principalKeys[plan.PrincipalSetKey].Contains(key))
                    {
                        throw CreateInvalidForeignKeyException(plan);
                    }
                }
            }
        }
    }

    private static InvalidDataException CreateInvalidForeignKeyException(ForeignKeyValidationPlan plan)
        => new($"Cannot restore database table '{plan.DependentName}' because a record contains an invalid reference for relationship '{plan.RelationshipName}'.");

    private static string BuildPrincipalKeySetKey(
        Type principalType,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties)
        => $"{principalType.FullName}:{string.Join(",", properties.Select(property => property.Name))}";

    private static string CreateCanonicalKey(
        object entity,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties)
        => CreateCanonicalKey(GetPropertyValues(entity, properties), properties);

    private static string CreateCanonicalKey(
        IReadOnlyList<object?> values,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties)
    {
        var canonicalValues = values.Select((value, index) => JsonSerializer.Serialize(
            value,
            properties[index].ClrType,
            _serializerSettings));
        return JsonSerializer.Serialize(canonicalValues, _serializerSettings);
    }

    private static object?[] GetPropertyValues(
        object entity,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties)
        => properties.Select(property =>
            property.PropertyInfo?.GetValue(entity) ?? property.FieldInfo?.GetValue(entity)).ToArray();

    private static Dictionary<string, (IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> Properties, HashSet<string> Keys)>
        CreateUniqueConstraintTrackers(Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType)
    {
        var constraints = new Dictionary<string, (IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty>, HashSet<string>)>(StringComparer.Ordinal);
        if (entityType is null)
        {
            return constraints;
        }

        if (entityType.FindPrimaryKey() is { } primaryKey)
        {
            constraints.Add("primary key", (primaryKey.Properties, new HashSet<string>(StringComparer.Ordinal)));
        }

        foreach (var index in entityType.GetIndexes().Where(index => index.IsUnique && string.IsNullOrWhiteSpace(index.GetFilter())))
        {
            var constraintName = index.Name ?? $"unique index ({string.Join(", ", index.Properties.Select(property => property.Name))})";
            constraints.Add(constraintName, (index.Properties, new HashSet<string>(StringComparer.Ordinal)));
        }

        return constraints;
    }

    private static void ValidateUniqueConstraints(
        string entryPath,
        object entity,
        Dictionary<string, (IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> Properties, HashSet<string> Keys)> constraints)
    {
        foreach (var (constraintName, constraint) in constraints)
        {
            var values = constraint.Properties.Select(property =>
                property.PropertyInfo?.GetValue(entity) ?? property.FieldInfo?.GetValue(entity)).ToArray();
            if (constraintName != "primary key" && values.Any(value => value is null))
            {
                continue;
            }

            var canonicalValues = values.Select((value, index) => JsonSerializer.Serialize(
                value,
                constraint.Properties[index].ClrType,
                _serializerSettings)).ToArray();
            var key = JsonSerializer.Serialize(canonicalValues, _serializerSettings);
            if (!constraint.Keys.Add(key))
            {
                throw new InvalidDataException($"The backup entry '{entryPath}' contains duplicate values for {constraintName}.");
            }
        }
    }

    private async Task RestoreDomainDatabaseContextAsync(ZipArchive zipArchive, DomainDatabaseContext domainContext, DbTransaction transaction)
    {
        var context = domainContext.Context;
        context.Database.UseTransaction(transaction);
        var entityTypes = GetExpectedDatabaseEntities(context.Model, context.GetType(), includeHistory: false);
        var tableNames = entityTypes.Select(entity => entity.Name).Distinct(StringComparer.Ordinal).ToArray();
        var databaseProvider = (IMulletaFlixDomainDatabaseProvider)_MulletaFlixDatabaseProvider;
        await databaseProvider.PurgeDomainDatabase(context, tableNames).ConfigureAwait(false);

        foreach (var entityType in entityTypes)
        {
            var entryPath = GetDomainDatabaseEntryPath(domainContext.Name, entityType.Name);
            var zipEntry = zipArchive.GetEntry(entryPath)!;
            await using var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
            var records = 0;
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
            {
                var entity = item?.Deserialize(entityType.Type, _serializerSettings)
                    ?? throw new InvalidDataException($"Cannot deserialize an entity for domain table '{domainContext.Name}/{entityType.Name}'.");
                context.Add(entity);
                records++;
            }

            _logger.LogInformation("Prepared to restore {Number} entries for domain table {Context}/{Table}", records, domainContext.Name, entityType.Name);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        _logger.LogInformation("Restored media-domain database context {Context}", domainContext.Name);
    }

    private static async Task WriteDomainDatabaseBackupAsync(
        ZipArchive zipArchive,
        DomainDatabaseContext domainContext)
    {
        static IAsyncEnumerable<object> GetValues(IQueryable dbSet)
        {
            var method = dbSet.GetType().GetMethod(nameof(DbSet<object>.AsAsyncEnumerable))!;
            return (IAsyncEnumerable<object>)method.Invoke(dbSet, null)!;
        }

        var context = domainContext.Context;
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var entityTypes = GetExpectedDatabaseEntities(context.Model, context.GetType(), includeHistory: false);
        await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);
        foreach (var entityType in entityTypes)
        {
            var entryPath = GetDomainDatabaseEntryPath(domainContext.Name, entityType.Name);
            var zipEntry = zipArchive.CreateEntry(entryPath);
            await using (var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false))
            await using (var jsonWriter = new Utf8JsonWriter(zipEntryStream))
            {
                jsonWriter.WriteStartArray();
                var setMethod = typeof(DbContext).GetMethods()
                    .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0)
                    .MakeGenericMethod(entityType.Type);
                var set = (IQueryable)setMethod.Invoke(context, null)!;
                await foreach (var item in GetValues(set).ConfigureAwait(false))
                {
                    using var document = JsonSerializer.SerializeToDocument(item, _serializerSettings);
                    document.WriteTo(jsonWriter);
                }

                jsonWriter.WriteEndArray();
            }

        }

        await transaction.CommitAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BackupManifestDto> CreateBackupAsync(BackupOptionsDto backupOptions)
        => await CreateBackupWithHistoryAsync(backupOptions, "Full system backup", "FullSystemBackup").ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<BackupManifestDto> CreateScheduledBackupAsync(BackupOptionsDto backupOptions)
        => await CreateBackupWithHistoryAsync(backupOptions, "Scheduled backup", "BackupScheduledTask").ConfigureAwait(false);

    private async Task<BackupManifestDto> CreateBackupWithHistoryAsync(BackupOptionsDto backupOptions, string name, string key)
    {
        const string operation = "create";
        var startedAt = Stopwatch.GetTimestamp();
        if (!TryBeginOperation(operation))
        {
            throw new BackupOperationInProgressException();
        }

        var result = "failure";
        var startedAtUtc = DateTime.UtcNow;
        BackupManifestDto? manifest = null;
        var errorMessage = string.Empty;
        try
        {
            manifest = await CreateBackupCoreAsync(backupOptions).ConfigureAwait(false);
            result = "success";
            return manifest;
        }
        catch
        {
            errorMessage = "Backup failed. Check the server log for details.";
            throw;
        }
        finally
        {
            await RecordBackupExecutionAsync(new BackupExecutionHistoryDto
            {
                Path = manifest?.Path ?? string.Empty,
                StartTimeUtc = startedAtUtc,
                EndTimeUtc = DateTime.UtcNow,
                Status = result == "success" ? TaskCompletionStatus.Completed : TaskCompletionStatus.Failed,
                Name = name,
                Key = key,
                Id = Guid.NewGuid().ToString("N"),
                ErrorMessage = errorMessage,
                LongErrorMessage = string.Empty
            }).ConfigureAwait(false);
            CompleteOperation(operation, result, startedAt);
        }
    }

    private bool TryBeginOperation(string operation)
    {
        if (Interlocked.CompareExchange(ref _backupOperationInProgress, 1, 0) != 0)
        {
            RecordOperation(operation, "rejected", null);
            return false;
        }

        ActiveBackupOperations.Add(1, new KeyValuePair<string, object?>("operation", operation));
        return true;
    }

    private void CompleteOperation(string operation, string result, long startedAt)
    {
        RecordOperation(operation, result, startedAt);
        ActiveBackupOperations.Add(-1, new KeyValuePair<string, object?>("operation", operation));
        Volatile.Write(ref _backupOperationInProgress, 0);
    }

    private static void RecordOperation(string operation, string result, long? startedAt)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "result", result }
        };
        BackupOperationCounter.Add(1, tags);
        if (startedAt.HasValue)
        {
            BackupOperationDuration.Record(Stopwatch.GetElapsedTime(startedAt.Value).TotalSeconds, tags);
        }
    }

    private async Task<BackupManifestDto> CreateBackupCoreAsync(BackupOptionsDto backupOptions)
    {
        var manifest = new BackupManifest()
        {
            DateCreated = DateTime.UtcNow,
            ServerVersion = _applicationHost.ApplicationVersion,
            DatabaseTables = [],
            DatabaseParticipants = backupOptions.Database ? _backupDatabaseParticipants.Select(participant => participant.Name).ToArray() : null,
            BackupEngineVersion = _backupEngineVersion,
            Options = Map(backupOptions)
        };

        if (backupOptions.Database)
        {
            _logger.LogInformation("Running database optimization before backup");
            await _MulletaFlixDatabaseProvider.RunScheduledOptimisation(CancellationToken.None).ConfigureAwait(false);
        }

        var backupFolder = Path.Combine(_applicationPaths.BackupPath);

        if (!Directory.Exists(backupFolder))
        {
            Directory.CreateDirectory(backupFolder);
        }

        var backupStorageSpace = StorageHelper.GetFreeSpaceOf(_applicationPaths.BackupPath);

        const long FiveGigabyte = 5_368_709_115;
        if (backupStorageSpace.FreeSpace < FiveGigabyte)
        {
            throw new InvalidOperationException($"The backup directory '{backupStorageSpace.Path}' does not have at least '{StorageHelper.HumanizeStorageSize(FiveGigabyte)}' free space. Cannot create backup.");
        }

        var backupPath = Path.Combine(backupFolder, $"MulletaFlix-backup-{manifest.DateCreated.ToLocalTime():yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.zip");
        var temporaryBackupPath = backupPath + ".partial";

        try
        {
            _logger.LogInformation("Attempting to create a new backup at {BackupPath}", backupPath);
            var fileStream = new FileStream(temporaryBackupPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            await using (fileStream.ConfigureAwait(false))
            using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
            {
                _logger.LogInformation("Starting backup process");
                if (backupOptions.Database)
                {
                    var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                    await using (dbContext.ConfigureAwait(false))
                    {
                        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                        static IAsyncEnumerable<object> GetValues(IQueryable dbSet)
                        {
                            var method = dbSet.GetType().GetMethod(nameof(DbSet<object>.AsAsyncEnumerable))!;
                            var enumerable = method.Invoke(dbSet, null)!;
                            return (IAsyncEnumerable<object>)enumerable;
                        }

                        // include the migration history as well
                        var historyRepository = dbContext.GetService<IHistoryRepository>();
                        var migrations = await historyRepository.GetAppliedMigrationsAsync().ConfigureAwait(false);

                        ICollection<(Type Type, string SourceName, Func<IAsyncEnumerable<object>> ValueFactory)> entityTypes =
                        [
                            .. typeof(MulletaFlixDbContext)
                            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .Where(e => e.PropertyType.IsAssignableTo(typeof(IQueryable)))
                            .Select(e => (Type: e.PropertyType, dbContext.Model.FindEntityType(e.PropertyType.GetGenericArguments()[0])!.GetSchemaQualifiedTableName()!, ValueFactory: new Func<IAsyncEnumerable<object>>(() => GetValues((IQueryable)e.GetValue(dbContext)!)))),
                        (Type: typeof(HistoryRow), SourceName: nameof(HistoryRow), ValueFactory: () => migrations.ToAsyncEnumerable())
                        ];
                        manifest.DatabaseTables = GetExpectedDatabaseTableNames(dbContext.Model);
                        var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);

                        await using (transaction.ConfigureAwait(false))
                        {
                            _logger.LogInformation("Begin Database backup");

                            foreach (var entityType in entityTypes)
                            {
                                _logger.LogInformation("Begin backup of entity {Table}", entityType.SourceName);
                                var zipEntry = zipArchive.CreateEntry(NormalizePathSeparator(Path.Combine("Database", $"{entityType.SourceName}.json")));
                                var entities = 0;
                                var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
                                await using (zipEntryStream.ConfigureAwait(false))
                                {
                                    var jsonSerializer = new Utf8JsonWriter(zipEntryStream);
                                    await using (jsonSerializer.ConfigureAwait(false))
                                    {
                                        jsonSerializer.WriteStartArray();

                                        var set = entityType.ValueFactory().ConfigureAwait(false);
                                        await foreach (var item in set.ConfigureAwait(false))
                                        {
                                            entities++;
                                            try
                                            {
                                                using var document = JsonSerializer.SerializeToDocument(item, _serializerSettings);
                                                document.WriteTo(jsonSerializer);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError(ex, "Could not load entity {Entity}", item);
                                                throw;
                                            }
                                        }

                                        jsonSerializer.WriteEndArray();
                                    }
                                }

                                _logger.LogInformation("Backup of entity {Table} with {Number} created", entityType.SourceName, entities);
                            }
                        }
                    }
                }

                if (backupOptions.Database)
                {
                    var domainContexts = await CreateDomainDatabaseContextsAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        if (domainContexts.Length > 0)
                        {
                            manifest.DomainDatabaseContexts = domainContexts.Select(context => context.Name).ToArray();
                            foreach (var domainContext in domainContexts)
                            {
                                _logger.LogInformation("Begin backup of media-domain context {Context}", domainContext.Name);
                                await WriteDomainDatabaseBackupAsync(zipArchive, domainContext).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        await DisposeDomainDatabaseContextsAsync(domainContexts).ConfigureAwait(false);
                    }
                }

                if (backupOptions.Database)
                {
                    foreach (var participant in _backupDatabaseParticipants)
                    {
                        await participant.WriteBackupAsync(zipArchive, GetParticipantEntryPrefix(participant.Name), CancellationToken.None).ConfigureAwait(false);
                    }
                }

                _logger.LogInformation("Backup of folder {Table}", _applicationPaths.ConfigurationDirectoryPath);
                foreach (var item in Directory.EnumerateFiles(_applicationPaths.ConfigurationDirectoryPath, "*.xml", SearchOption.TopDirectoryOnly)
                             .Union(Directory.EnumerateFiles(_applicationPaths.ConfigurationDirectoryPath, "*.json", SearchOption.TopDirectoryOnly)))
                {
                    await zipArchive.CreateEntryFromFileAsync(item, NormalizePathSeparator(Path.Combine("Config", Path.GetFileName(item)))).ConfigureAwait(false);
                }

                void CopyDirectory(string source, string target, string filter = "*")
                {
                    if (!Directory.Exists(source))
                    {
                        return;
                    }

                    _logger.LogInformation("Backup of folder {Table}", source);

                    foreach (var item in Directory.EnumerateFiles(source, filter, SearchOption.AllDirectories))
                    {
                        // TODO: @bond make async
                        zipArchive.CreateEntryFromFile(item, NormalizePathSeparator(Path.Combine(target, Path.GetRelativePath(source, item))));
                    }
                }

                CopyDirectory(Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "users"), Path.Combine("Config", "users"));
                CopyDirectory(Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "ScheduledTasks"), Path.Combine("Config", "ScheduledTasks"));
                CopyDirectory(Path.Combine(_applicationPaths.RootFolderPath), "Root");
                CopyDirectory(Path.Combine(_applicationPaths.DataPath, "collections"), Path.Combine("Data", "collections"));
                CopyDirectory(Path.Combine(_applicationPaths.DataPath, "playlists"), Path.Combine("Data", "playlists"));
                CopyDirectory(Path.Combine(_applicationPaths.DataPath, "ScheduledTasks"), Path.Combine("Data", "ScheduledTasks"));
                if (backupOptions.Subtitles)
                {
                    CopyDirectory(Path.Combine(_applicationPaths.DataPath, "subtitles"), Path.Combine("Data", "subtitles"));
                }

                if (backupOptions.Trickplay)
                {
                    CopyDirectory(Path.Combine(_applicationPaths.DataPath, "trickplay"), Path.Combine("Data", "trickplay"));
                }

                if (backupOptions.Metadata)
                {
                    CopyDirectory(Path.Combine(_applicationPaths.InternalMetadataPath), Path.Combine("Data", "metadata"));

                    // If a custom metadata path is configured, the default location may still contain data.
                    if (!string.Equals(
                            Path.GetFullPath(_applicationPaths.DefaultInternalMetadataPath),
                            Path.GetFullPath(_applicationPaths.InternalMetadataPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        CopyDirectory(Path.Combine(_applicationPaths.DefaultInternalMetadataPath), Path.Combine("Data", "metadata-default"));
                    }
                }

                var manifestStream = await zipArchive.CreateEntry(ManifestEntryName).OpenAsync().ConfigureAwait(false);
                await using (manifestStream.ConfigureAwait(false))
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest).ConfigureAwait(false);
                }
            }

            await ValidateArchiveIntegrityAsync(temporaryBackupPath).ConfigureAwait(false);
            File.Move(temporaryBackupPath, backupPath);
            _logger.LogInformation("Backup created");
            PruneOldBackups(backupFolder, maxToKeep: 2);
            return Map(manifest, backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup, removing {BackupPath}", backupPath);
            try
            {
                if (File.Exists(temporaryBackupPath))
                {
                    File.Delete(temporaryBackupPath);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning(innerEx, "Unable to remove failed backup");
            }

            throw;
        }
    }

    internal static async Task ValidateArchiveIntegrityAsync(string archivePath)
    {
        await ValidateArchiveIntegrityAsync(
            archivePath,
            MaxBackupArchiveBytes,
            MaxBackupArchiveEntries,
            MaxBackupEntryBytes,
            MaxBackupExpandedBytes,
            MaxBackupCompressionRatio,
            MaxBackupCentralDirectoryBytes).ConfigureAwait(false);
    }

    internal static async Task ValidateArchiveIntegrityAsync(
        string archivePath,
        long maxArchiveBytes,
        int maxEntries,
        long maxEntryBytes,
        long maxExpandedBytes,
        double maxCompressionRatio,
        long maxCentralDirectoryBytes = MaxBackupCentralDirectoryBytes)
    {
        await using var fileStream = File.OpenRead(archivePath);
        await ValidateArchiveIntegrityAsync(fileStream, maxArchiveBytes, maxEntries, maxEntryBytes, maxExpandedBytes, maxCompressionRatio, maxCentralDirectoryBytes).ConfigureAwait(false);
    }

    private static async Task ValidateArchiveIntegrityAsync(Stream fileStream)
    {
        await ValidateArchiveIntegrityAsync(
            fileStream,
            MaxBackupArchiveBytes,
            MaxBackupArchiveEntries,
            MaxBackupEntryBytes,
            MaxBackupExpandedBytes,
            MaxBackupCompressionRatio,
            MaxBackupCentralDirectoryBytes).ConfigureAwait(false);
    }

    private static async Task ValidateArchiveIntegrityAsync(
        Stream fileStream,
        long maxArchiveBytes,
        int maxEntries,
        long maxEntryBytes,
        long maxExpandedBytes,
        double maxCompressionRatio,
        long maxCentralDirectoryBytes)
    {
        if (!fileStream.CanRead || !fileStream.CanSeek)
        {
            throw new ArgumentException("The backup archive stream must be readable and seekable.", nameof(fileStream));
        }

        if (fileStream.Length > maxArchiveBytes)
        {
            throw new InvalidDataException($"Backup archive exceeds the maximum compressed size of {maxArchiveBytes} bytes.");
        }

        if (maxArchiveBytes < 0 || maxEntries < 0 || maxEntryBytes < 0 || maxExpandedBytes < 0 || maxCentralDirectoryBytes < 0 || !double.IsFinite(maxCompressionRatio) || maxCompressionRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxArchiveBytes), "Backup archive limits must be non-negative and compression ratio must be finite and positive.");
        }

        var originalPosition = fileStream.Position;
        try
        {
            fileStream.Position = 0;
            ValidateArchiveEntryCount(fileStream, maxEntries, maxCentralDirectoryBytes);
            fileStream.Position = 0;
            using var countedArchiveStream = new CountingReadStream(fileStream);
            using var archive = new ZipArchive(countedArchiveStream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > maxEntries)
            {
                throw new InvalidDataException($"Backup archive exceeds the maximum entry count of {maxEntries}.");
            }

            long totalLength = 0;
            foreach (var entry in archive.Entries)
            {
                var remainingTotalBytes = maxExpandedBytes - totalLength;
                if (entry.Length > maxEntryBytes || entry.Length > remainingTotalBytes)
                {
                    throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds the configured expansion limit.");
                }

                var compressedLength = Math.Max(1, entry.CompressedLength);
                if (entry.Length / (double)compressedLength > maxCompressionRatio)
                {
                    throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds the maximum compression ratio of {maxCompressionRatio}:1.");
                }

                await using var entryStream = await entry.OpenAsync().ConfigureAwait(false);
                var compressedStart = countedArchiveStream.BytesRead;
                var (checksum, length) = await Crc32.ComputeBoundedAsync(
                    entryStream,
                    CancellationToken.None,
                    Math.Min(maxEntryBytes, remainingTotalBytes),
                    () => GetExpansionLimit(countedArchiveStream.BytesRead - compressedStart, maxCompressionRatio)).ConfigureAwait(false);
                if (checksum != entry.Crc32 || length != entry.Length)
                {
                    throw new InvalidDataException($"Backup entry '{entry.FullName}' failed its size or CRC-32 integrity check.");
                }

                var actualCompressedLength = Math.Max(1, countedArchiveStream.BytesRead - compressedStart);
                if (length / (double)actualCompressedLength > maxCompressionRatio)
                {
                    throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds the maximum compression ratio of {maxCompressionRatio}:1.");
                }

                totalLength += length;
            }

        }
        finally
        {
            fileStream.Position = originalPosition;
        }
    }

    private static long GetExpansionLimit(long compressedBytesRead, double maximumCompressionRatio)
    {
        var limit = Math.Max(0, compressedBytesRead) * maximumCompressionRatio;
        return limit >= long.MaxValue ? long.MaxValue : (long)Math.Floor(limit);
    }

    private static void ValidateArchiveEntryCount(Stream archiveStream, int maxEntries, long maxCentralDirectoryBytes)
    {
        const int EndOfCentralDirectorySignature = 0x06054b50;
        const int Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
        const int Zip64EndOfCentralDirectorySignature = 0x06064b50;
        const int CentralDirectoryEntrySignature = 0x02014b50;
        const int EndRecordLength = 22;
        const int MaxCommentLength = ushort.MaxValue;

        var tailLength = (int)Math.Min(archiveStream.Length, EndRecordLength + MaxCommentLength);
        var tail = new byte[tailLength];
        archiveStream.Position = archiveStream.Length - tailLength;
        archiveStream.ReadExactly(tail);

        var endRecordOffset = -1;
        for (var i = tail.Length - EndRecordLength; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == EndOfCentralDirectorySignature
                && i + EndRecordLength + BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20)) == tail.Length)
            {
                endRecordOffset = i;
                break;
            }
        }

        if (endRecordOffset < 0)
        {
            throw new InvalidDataException("Backup archive has no valid end-of-central-directory record.");
        }

        var endRecord = tail.AsSpan(endRecordOffset, EndRecordLength);
        var diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(endRecord[4..]);
        var centralDirectoryDisk = BinaryPrimitives.ReadUInt16LittleEndian(endRecord[6..]);
        var entriesOnDisk = BinaryPrimitives.ReadUInt16LittleEndian(endRecord[8..]);
        ulong totalEntries = BinaryPrimitives.ReadUInt16LittleEndian(endRecord[10..]);
        ulong centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(endRecord[12..]);
        ulong centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(endRecord[16..]);
        var centralDirectoryStartLimit = archiveStream.Length - tailLength + endRecordOffset;

        if (diskNumber != 0 || centralDirectoryDisk != 0 || entriesOnDisk != totalEntries)
        {
            throw new InvalidDataException("Multi-disk backup archives are not supported.");
        }

        if (totalEntries == ushort.MaxValue || centralDirectorySize == uint.MaxValue || centralDirectoryOffset == uint.MaxValue)
        {
            var locatorPosition = centralDirectoryStartLimit - 20;
            if (locatorPosition < 0)
            {
                throw new InvalidDataException("Backup archive has an invalid ZIP64 locator.");
            }

            archiveStream.Position = locatorPosition;
            Span<byte> locator = stackalloc byte[20];
            archiveStream.ReadExactly(locator);
            if (BinaryPrimitives.ReadUInt32LittleEndian(locator) != Zip64EndOfCentralDirectoryLocatorSignature
                || BinaryPrimitives.ReadUInt32LittleEndian(locator[4..]) != 0
                || BinaryPrimitives.ReadUInt32LittleEndian(locator[16..]) != 1)
            {
                throw new InvalidDataException("Backup archive has an invalid ZIP64 locator.");
            }

            var zip64RecordPosition = BinaryPrimitives.ReadUInt64LittleEndian(locator[8..]);
            if (archiveStream.Length < 56 || zip64RecordPosition > (ulong)(archiveStream.Length - 56))
            {
                throw new InvalidDataException("Backup archive has an invalid ZIP64 end record.");
            }

            archiveStream.Position = (long)zip64RecordPosition;
            Span<byte> zip64EndRecord = stackalloc byte[56];
            archiveStream.ReadExactly(zip64EndRecord);
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip64EndRecord) != Zip64EndOfCentralDirectorySignature
                || BinaryPrimitives.ReadUInt32LittleEndian(zip64EndRecord[16..]) != 0
                || BinaryPrimitives.ReadUInt32LittleEndian(zip64EndRecord[20..]) != 0
                || BinaryPrimitives.ReadUInt64LittleEndian(zip64EndRecord[24..]) != BinaryPrimitives.ReadUInt64LittleEndian(zip64EndRecord[32..]))
            {
                throw new InvalidDataException("Backup archive has an invalid ZIP64 end record.");
            }

            totalEntries = BinaryPrimitives.ReadUInt64LittleEndian(zip64EndRecord[32..]);
            centralDirectorySize = BinaryPrimitives.ReadUInt64LittleEndian(zip64EndRecord[40..]);
            centralDirectoryOffset = BinaryPrimitives.ReadUInt64LittleEndian(zip64EndRecord[48..]);
        }

        if (totalEntries > (ulong)maxEntries)
        {
            throw new InvalidDataException($"Backup archive exceeds the maximum entry count of {maxEntries}.");
        }

        if (centralDirectoryOffset > (ulong)centralDirectoryStartLimit
            || centralDirectorySize > (ulong)centralDirectoryStartLimit - centralDirectoryOffset)
        {
            throw new InvalidDataException("Backup archive has an invalid central directory range.");
        }

        if (centralDirectorySize > (ulong)maxCentralDirectoryBytes)
        {
            throw new InvalidDataException($"Backup archive central directory exceeds the maximum size of {maxCentralDirectoryBytes} bytes.");
        }

        var centralDirectoryEnd = (long)(centralDirectoryOffset + centralDirectorySize);
        archiveStream.Position = (long)centralDirectoryOffset;
        Span<byte> entryHeader = stackalloc byte[46];
        for (ulong entryIndex = 0; entryIndex < totalEntries; entryIndex++)
        {
            if (archiveStream.Position > centralDirectoryEnd - entryHeader.Length)
            {
                throw new InvalidDataException("Backup archive has a truncated central directory.");
            }

            archiveStream.ReadExactly(entryHeader);
            if (BinaryPrimitives.ReadUInt32LittleEndian(entryHeader) != CentralDirectoryEntrySignature)
            {
                throw new InvalidDataException("Backup archive has an invalid central directory entry.");
            }

            var variableLength = BinaryPrimitives.ReadUInt16LittleEndian(entryHeader[28..])
                + BinaryPrimitives.ReadUInt16LittleEndian(entryHeader[30..])
                + BinaryPrimitives.ReadUInt16LittleEndian(entryHeader[32..]);
            if (variableLength > centralDirectoryEnd - archiveStream.Position)
            {
                throw new InvalidDataException("Backup archive has a truncated central directory entry.");
            }

            archiveStream.Position += variableLength;
        }

        if (archiveStream.Position != centralDirectoryEnd)
        {
            throw new InvalidDataException("Backup archive central directory entry count does not match its size.");
        }
    }

    /// <inheritdoc/>
    public async Task<BackupManifestDto?> GetBackupManifest(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            return null;
        }

        BackupManifest? manifest;
        try
        {
            manifest = await GetManifest(archivePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tried to load manifest from archive {Path} but failed", archivePath);
            return null;
        }

        if (manifest is null)
        {
            return null;
        }

        return Map(manifest, archivePath);
    }

    /// <inheritdoc/>
    public async Task<BackupManifestDto[]> EnumerateBackups()
    {
        if (!Directory.Exists(_applicationPaths.BackupPath))
        {
            return [];
        }

        var archives = Directory.EnumerateFiles(_applicationPaths.BackupPath, "*.zip");
        var manifests = new List<BackupManifestDto>();
        foreach (var item in archives)
        {
            try
            {
                var manifest = await GetManifest(item).ConfigureAwait(false);

                if (manifest is null)
                {
                    continue;
                }

                manifests.Add(Map(manifest, item));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tried to load manifest from archive {Path} but failed", item);
            }
        }

        return manifests.ToArray();
    }

    private static async ValueTask<BackupManifest?> GetManifest(string archivePath)
    {
        await using var archiveStream = File.OpenRead(archivePath);
        if (archiveStream.Length > MaxBackupArchiveBytes)
        {
            throw new InvalidDataException($"Backup archive exceeds the maximum compressed size of {MaxBackupArchiveBytes} bytes.");
        }

        ValidateArchiveEntryCount(archiveStream, MaxBackupArchiveEntries, MaxBackupCentralDirectoryBytes);
        archiveStream.Position = 0;
        using var zipStream = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var manifestEntry = zipStream.GetEntry(ManifestEntryName);
        if (manifestEntry is null)
        {
            return null;
        }

        if (manifestEntry.Length > MaxBackupManifestBytes
            || manifestEntry.Length / (double)Math.Max(1, manifestEntry.CompressedLength) > MaxBackupCompressionRatio)
        {
            throw new InvalidDataException("Backup manifest exceeds its configured size or compression limit.");
        }

        await using var manifestStream = await manifestEntry.OpenAsync().ConfigureAwait(false);
        using var manifestBuffer = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int bytesRead;
            while ((bytesRead = await manifestStream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
            {
                if (bytesRead > MaxBackupManifestBytes - manifestBuffer.Length)
                {
                    throw new InvalidDataException($"Backup manifest exceeds the maximum size of {MaxBackupManifestBytes} bytes.");
                }

                await manifestBuffer.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            }

            manifestBuffer.Position = 0;
            return await JsonSerializer.DeserializeAsync<BackupManifest>(manifestBuffer, _serializerSettings).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static BackupManifestDto Map(BackupManifest manifest, string path)
    {
        return new BackupManifestDto()
        {
            BackupEngineVersion = manifest.BackupEngineVersion,
            DateCreated = manifest.DateCreated,
            ServerVersion = manifest.ServerVersion,
            Path = path,
            SizeBytes = new FileInfo(path).Length,
            Options = Map(manifest.Options)
        };
    }

    private static BackupOptionsDto Map(BackupOptions options)
    {
        return new BackupOptionsDto()
        {
            Metadata = options.Metadata,
            Subtitles = options.Subtitles,
            Trickplay = options.Trickplay,
            Database = options.Database
        };
    }

    private static BackupOptions Map(BackupOptionsDto options)
    {
        return new BackupOptions()
        {
            Metadata = options.Metadata,
            Subtitles = options.Subtitles,
            Trickplay = options.Trickplay,
            Database = options.Database
        };
    }

    /// <summary>
    /// Windows is able to handle '/' as a path seperator in zip files
    /// but linux isn't able to handle '\' as a path seperator in zip files,
    /// So normalize to '/'.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path. </returns>
    private static string NormalizePathSeparator(string path)
        => path.Replace('\\', '/');

    /// <inheritdoc/>
    public async Task<BackupExecutionHistoryDto[]> GetBackupHistory()
    {
        await _backupHistoryLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var historyFile = GetBackupHistoryFilePath();
            var results = await ReadPersistedBackupHistoryAsync(historyFile).ConfigureAwait(false) ?? [];
            var scheduledResult = await ReadScheduledBackupResultAsync().ConfigureAwait(false);
            if (scheduledResult is not null && !results.Any(item =>
                    item.Key == scheduledResult.Key
                    && item.Status == scheduledResult.Status
                    && item.StartTimeUtc < scheduledResult.EndTimeUtc
                    && scheduledResult.StartTimeUtc < item.EndTimeUtc))
            {
                results.Add(scheduledResult);
            }

            return results
                .OrderByDescending(item => item.EndTimeUtc)
                .Take(MaxBackupHistoryEntries)
                .ToArray();
        }
        finally
        {
            _backupHistoryLock.Release();
        }
    }

    private string GetBackupHistoryFilePath() => Path.Combine(_applicationPaths.DataPath, BackupHistoryFileName);

    private async Task RecordBackupExecutionAsync(BackupExecutionHistoryDto execution)
    {
        await _backupHistoryLock.WaitAsync().ConfigureAwait(false);
        string? temporaryFile = null;
        try
        {
            var historyFile = GetBackupHistoryFilePath();
            var historyDirectory = Path.GetDirectoryName(historyFile)!;
            Directory.CreateDirectory(historyDirectory);

            var results = await ReadPersistedBackupHistoryAsync(historyFile).ConfigureAwait(false);
            if (results is null)
            {
                _logger.LogError("Cannot append backup execution because persisted history could not be read");
                return;
            }

            results.Add(execution);
            var retainedResults = results
                .OrderByDescending(item => item.EndTimeUtc)
                .Take(MaxBackupHistoryEntries)
                .ToArray();

            temporaryFile = historyFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(retainedResults, _serializerSettings)).ConfigureAwait(false);
            File.Move(temporaryFile, historyFile, overwrite: true);
            temporaryFile = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist backup execution history");
        }
        finally
        {
            if (temporaryFile is not null)
            {
                try
                {
                    File.Delete(temporaryFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove temporary backup history file");
                }
            }

            _backupHistoryLock.Release();
        }
    }

    private async Task<List<BackupExecutionHistoryDto>?> ReadPersistedBackupHistoryAsync(string historyFile)
    {
        if (!File.Exists(historyFile))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(historyFile).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<BackupExecutionHistoryDto>>(json, _serializerSettings) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read persisted backup history from {Path}", historyFile);
            return null;
        }
    }

    private async Task<BackupExecutionHistoryDto?> ReadScheduledBackupResultAsync()
    {
        var historyFile = Path.Combine(_applicationPaths.DataPath, "ScheduledTasks", "BackupScheduledTask.js");
        if (!File.Exists(historyFile))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(historyFile).ConfigureAwait(false);
            var taskResult = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<TaskResult>(json, _serializerSettings);
            return taskResult is null
                ? null
                : new BackupExecutionHistoryDto
                {
                    Path = string.Empty,
                    StartTimeUtc = taskResult.StartTimeUtc,
                    EndTimeUtc = taskResult.EndTimeUtc,
                    Status = taskResult.Status,
                    Name = taskResult.Name,
                    Key = taskResult.Key,
                    Id = taskResult.Id,
                    ErrorMessage = taskResult.ErrorMessage,
                    LongErrorMessage = taskResult.LongErrorMessage
                };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read scheduled backup history from {Path}", historyFile);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateBackupIntegrity(string archivePath)
    {
        try
        {
            await ValidateArchiveIntegrityAsync(archivePath).ConfigureAwait(false);
            var manifest = await GetBackupManifest(archivePath).ConfigureAwait(false);
            return manifest is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a migration ID to prevent SQL injection through backup archives.
    /// Only allows alphanumeric characters, hyphens, and underscores.
    /// </summary>
    /// <param name="migrationId">The migration ID to sanitize.</param>
    /// <returns>The sanitized migration ID.</returns>
    private static string SanitizeMigrationId(string migrationId)
    {
        if (string.IsNullOrWhiteSpace(migrationId))
        {
            return string.Empty;
        }

        // Only allow safe characters: alphanumeric, hyphens, underscores
        return new string(migrationId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
    }

    private static bool IsRestorePlatformSupported()
        => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    private static void ThrowIfRestorePlatformUnsupported()
    {
        if (!IsRestorePlatformSupported())
        {
            throw new PlatformNotSupportedException("Backup restore requires Windows or Linux.");
        }
    }

    /// <summary>
    /// Deletes older backups to prevent unbounded disk usage, keeping at most <paramref name="maxToKeep"/> archives.
    /// </summary>
    internal void PruneOldBackups(string backupFolder, int maxToKeep = 2)
    {
        try
        {
            if (!Directory.Exists(backupFolder))
            {
                return;
            }

            var backupFiles = Directory.EnumerateFiles(backupFolder, "MulletaFlix-backup-*.zip", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);

            var candidates = backupFiles.Values.Select(file => new BackupRetentionCandidate(
                file.Name,
                file.LastWriteTimeUtc > DateTime.UnixEpoch ? file.LastWriteTimeUtc : null));

            var removableBackups = BackupRetentionPolicy.SelectForRemoval(candidates, maxToKeep, maximumAge: null);
            foreach (var candidate in removableBackups)
            {
                if (!backupFiles.TryGetValue(candidate.Name, out var oldBackup))
                {
                    continue;
                }

                try
                {
                    _logger.LogInformation("Pruning old backup to reclaim storage: {FileName}", oldBackup.Name);
                    oldBackup.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup {FileName}", oldBackup.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune old backups in {BackupFolder}", backupFolder);
        }
    }
}
