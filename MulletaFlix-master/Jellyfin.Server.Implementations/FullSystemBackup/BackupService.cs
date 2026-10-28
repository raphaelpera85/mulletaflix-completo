using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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
    private readonly IDbContextFactory<MoviesDbContext> _moviesDbProvider;
    private readonly IDbContextFactory<SeriesDbContext> _seriesDbProvider;
    private readonly IDbContextFactory<ChannelsDbContext> _channelsDbProvider;
    private readonly IDbContextFactory<BooksDbContext> _booksDbProvider;
    private Func<MoviesDbContext>? _createMoviesDbContext;
    private Func<SeriesDbContext>? _createSeriesDbContext;
    private Func<ChannelsDbContext>? _createChannelsDbContext;
    private Func<BooksDbContext>? _createBooksDbContext;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IServerApplicationPaths _applicationPaths;
    private readonly IMulletaFlixDatabaseProvider _MulletaFlixDatabaseProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IStorageSpaceProvider _storageSpaceProvider;
    private readonly SemaphoreSlim _backupHistoryLock = new(1, 1);
    private int _backupOperationInProgress;
    private static readonly JsonSerializerOptions _serializerSettings = new JsonSerializerOptions(JsonSerializerDefaults.General)
    {
        AllowTrailingCommas = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private static readonly Version LegacyBackupEngineVersion = new(0, 2, 0);
    private readonly Version _backupEngineVersion = new(0, 3, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="dbProvider">A Database Factory.</param>
    /// <param name="moviesDbProvider">The movies database factory.</param>
    /// <param name="seriesDbProvider">The series database factory.</param>
    /// <param name="channelsDbProvider">The channels database factory.</param>
    /// <param name="booksDbProvider">The books database factory.</param>
    /// <param name="applicationHost">The Application host.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="MulletaFlixDatabaseProvider">The MulletaFlix database Provider in use.</param>
    /// <param name="applicationLifetime">The SystemManager.</param>
    /// <param name="storageSpaceProvider">Provides free-space information for backup targets.</param>
    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IDbContextFactory<MoviesDbContext> moviesDbProvider,
        IDbContextFactory<SeriesDbContext> seriesDbProvider,
        IDbContextFactory<ChannelsDbContext> channelsDbProvider,
        IDbContextFactory<BooksDbContext> booksDbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime,
        IStorageSpaceProvider storageSpaceProvider)
    {
        _logger = logger;
        _dbProvider = dbProvider;
        _moviesDbProvider = moviesDbProvider;
        _seriesDbProvider = seriesDbProvider;
        _channelsDbProvider = channelsDbProvider;
        _booksDbProvider = booksDbProvider;
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _MulletaFlixDatabaseProvider = MulletaFlixDatabaseProvider;
        _hostApplicationLifetime = applicationLifetime;
        _storageSpaceProvider = storageSpaceProvider;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class with non-pooled contexts for atomic restores.
    /// </summary>
    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IDbContextFactory<MoviesDbContext> moviesDbProvider,
        IDbContextFactory<SeriesDbContext> seriesDbProvider,
        IDbContextFactory<ChannelsDbContext> channelsDbProvider,
        IDbContextFactory<BooksDbContext> booksDbProvider,
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IMulletaFlixDatabaseProvider MulletaFlixDatabaseProvider,
        IHostApplicationLifetime applicationLifetime,
        IStorageSpaceProvider storageSpaceProvider,
        DbContextOptions<MoviesDbContext> moviesOptions,
        ILogger<MoviesDbContext> moviesLogger,
        DbContextOptions<SeriesDbContext> seriesOptions,
        ILogger<SeriesDbContext> seriesLogger,
        DbContextOptions<ChannelsDbContext> channelsOptions,
        ILogger<ChannelsDbContext> channelsLogger,
        DbContextOptions<BooksDbContext> booksOptions,
        ILogger<BooksDbContext> booksLogger)
        : this(logger, dbProvider, moviesDbProvider, seriesDbProvider, channelsDbProvider, booksDbProvider, applicationHost, applicationPaths, MulletaFlixDatabaseProvider, applicationLifetime, storageSpaceProvider)
    {
        _createMoviesDbContext = () => new MoviesDbContext(moviesOptions, moviesLogger);
        _createSeriesDbContext = () => new SeriesDbContext(seriesOptions, seriesLogger);
        _createChannelsDbContext = () => new ChannelsDbContext(channelsOptions, channelsLogger);
        _createBooksDbContext = () => new BooksDbContext(booksOptions, booksLogger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
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
    public async Task RestoreBackupAsync(string archivePath)
    {
        const string operation = "restore";
        var startedAt = Stopwatch.GetTimestamp();
        if (!TryBeginOperation(operation))
        {
            throw new BackupOperationInProgressException();
        }

        var result = "failure";
        try
        {
            await RestoreBackupCoreAsync(archivePath).ConfigureAwait(false);
            result = "success";
        }
        finally
        {
            CompleteOperation(operation, result, startedAt);
        }
    }

    private async Task RestoreBackupCoreAsync(string archivePath)
    {
        _logger.LogWarning("Begin restoring system to {BackupArchive}", archivePath); // Info isn't cutting it
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"Requested backup file '{archivePath}' does not exist.");
        }

        await ValidateArchiveIntegrityAsync(archivePath).ConfigureAwait(false);
        StorageHelper.TestCommonPathsForStorageCapacity(_applicationPaths, _logger);

        var fileStream = File.OpenRead(archivePath);
        await using (fileStream.ConfigureAwait(false))
        {
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

            if (manifest.Options.Database)
            {
                ValidateDatabaseBackupEntries(manifest, zipArchive);
                await ValidateDatabaseBackupContentAsync(manifest, zipArchive).ConfigureAwait(false);
            }

            var includeDomainTables = manifest.BackupEngineVersion != LegacyBackupEngineVersion;
            if (manifest.Options.Database && includeDomainTables
                && (_createMoviesDbContext is null
                    || _createSeriesDbContext is null
                    || _createChannelsDbContext is null
                    || _createBooksDbContext is null))
            {
                throw new InvalidOperationException("Restoring this backup requires atomic relational context support; no data was modified.");
            }

            using var fileRestore = new FileSystemRestoreTransaction(_logger);

            async Task CopyDirectory(string source, string target, string[]? exclude = null)
            {
                var fullSourcePath = NormalizePathSeparator(Path.GetFullPath(source) + Path.DirectorySeparatorChar);
                var normalizedTargetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
                var fullTargetRoot = normalizedTargetRoot + Path.DirectorySeparatorChar;
                var excludePaths = exclude?.Select(e => $"{source}/{e}/").ToArray();
                foreach (var item in zipArchive.Entries)
                {
                    var sourcePath = NormalizePathSeparator(Path.GetFullPath(item.FullName));
                    var targetPath = Path.GetFullPath(Path.Combine(target, Path.GetRelativePath(source, item.FullName)));

                    if (excludePaths is not null && excludePaths.Any(e => item.FullName.StartsWith(e, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (!sourcePath.StartsWith(fullSourcePath, StringComparison.Ordinal)
                        || !targetPath.StartsWith(fullTargetRoot, StringComparison.Ordinal)
                        || Path.EndsInDirectorySeparator(item.FullName))
                    {
                        continue;
                    }

                    _logger.LogInformation("Restore and override {File}", targetPath);
                    await fileRestore.ApplyEntryAsync(item, targetPath, normalizedTargetRoot).ConfigureAwait(false);
                }
            }

            // Apply files under a compensating transaction before restoring the database. If either
            // this copy or the relational restore fails, disposal restores the previous file set.
            await CopyDirectory("Config", _applicationPaths.ConfigurationDirectoryPath).ConfigureAwait(false);
            await CopyDirectory("Data", _applicationPaths.DataPath, exclude: ["metadata", "metadata-default"]).ConfigureAwait(false);
            await CopyDirectory("Root", _applicationPaths.RootFolderPath).ConfigureAwait(false);
            await CopyDirectory("Data/metadata", _applicationPaths.InternalMetadataPath).ConfigureAwait(false);
            await CopyDirectory("Data/metadata-default", _applicationPaths.DefaultInternalMetadataPath).ConfigureAwait(false);

            if (manifest.Options.Database)
            {
                _logger.LogInformation("Begin restoring Database");
                var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                await using (dbContext.ConfigureAwait(false))
                {
                    var domainContexts = new List<(DbContext Context, DbConnection OriginalConnection)>();

                    try
                    {
                        if (includeDomainTables && _createMoviesDbContext is not null)
                        {
                            var sharedConnection = dbContext.Database.GetDbConnection();
                            AddDomainContext(_createMoviesDbContext(), sharedConnection, domainContexts);
                            AddDomainContext(_createSeriesDbContext!(), sharedConnection, domainContexts);
                            AddDomainContext(_createChannelsDbContext!(), sharedConnection, domainContexts);
                            AddDomainContext(_createBooksDbContext!(), sharedConnection, domainContexts);
                        }

                        // Restore migration history manually.
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

                        await using (var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false))
                        {
                            foreach (var (context, _) in domainContexts)
                            {
                                await context.Database.UseTransactionAsync(transaction.GetDbTransaction()).ConfigureAwait(false);
                            }

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
                            var coreTables = GetDatabaseTableDefinitions(typeof(MulletaFlixDbContext));
                            var domainTables = includeDomainTables
                                ? GetDatabaseTableDefinitions().Where(table => table.ContextType != typeof(MulletaFlixDbContext)).ToArray()
                                : [];
                            var tableNames = coreTables
                                .Select(table => dbContext.Model.FindEntityType(table.EntityType)!.GetSchemaQualifiedTableName()!)
                                .Concat(domainTables.Select(table => table.Name));
                            _logger.LogInformation("Begin purging database");
                            await _MulletaFlixDatabaseProvider.PurgeDatabase(dbContext, tableNames).ConfigureAwait(false);
                            _logger.LogInformation("Database Purged");

                            await RestoreDatabaseTablesAsync(dbContext, zipArchive, coreTables).ConfigureAwait(false);
                            _logger.LogInformation("Try restore Database");
                            await dbContext.SaveChangesAsync().ConfigureAwait(false);

                            foreach (var (context, _) in domainContexts)
                            {
                                await RestoreDomainContextAsync(context, context.GetType(), zipArchive).ConfigureAwait(false);
                                await context.Database.UseTransactionAsync(null).ConfigureAwait(false);
                            }

                            await transaction.CommitAsync().ConfigureAwait(false);
                            _logger.LogInformation("Restored core and domain database contexts");
                        }

                    }
                    finally
                    {
                        foreach (var (context, originalConnection) in domainContexts)
                        {
                            try
                            {
                                await context.Database.UseTransactionAsync(null).ConfigureAwait(false);
                                var sharedConnection = context.Database.GetDbConnection();
                                if (sharedConnection.State != System.Data.ConnectionState.Closed)
                                {
                                    await context.Database.CloseConnectionAsync().ConfigureAwait(false);
                                }

                                context.Database.SetDbConnection(originalConnection, contextOwnsConnection: false);
                                context.Database.SetDbConnection(originalConnection, contextOwnsConnection: true);
                            }
                            finally
                            {
                                await context.DisposeAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            fileRestore.Commit();

            _logger.LogInformation("Restored MulletaFlix system from {Date}", manifest.DateCreated);
        }
    }

    private bool TestBackupVersionCompatibility(Version backupEngineVersion)
        => backupEngineVersion == _backupEngineVersion || backupEngineVersion == LegacyBackupEngineVersion;

    private sealed class FileSystemRestoreTransaction : IDisposable
    {
        private sealed class RestoredFile
        {
            public required string TargetPath { get; init; }

            public required string StagingPath { get; init; }

            public required string BackupPath { get; init; }

            public bool OriginalMoved { get; set; }

            public bool ReplacementInstalled { get; set; }
        }

        private readonly ILogger _logger;
        private readonly List<RestoredFile> _files = [];
        private readonly List<string> _createdDirectories = [];
        private readonly HashSet<string> _trackedDirectories = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private bool _committed;

        public FileSystemRestoreTransaction(ILogger logger)
        {
            _logger = logger;
        }

        public async Task ApplyEntryAsync(ZipArchiveEntry archiveEntry, string targetPath, string targetRoot)
        {
            EnsurePathWithinConfiguredRoot(targetPath, targetRoot);
            var directory = Path.GetDirectoryName(targetPath)!;
            EnsureDirectoryExists(directory, targetRoot);

            var id = Guid.NewGuid().ToString("N");
            var restoredFile = new RestoredFile
            {
                TargetPath = targetPath,
                StagingPath = Path.Combine(directory, $".mfx-restore-{id}.tmp"),
                BackupPath = Path.Combine(directory, $".mfx-restore-{id}.bak")
            };
            _files.Add(restoredFile);

            using (var source = archiveEntry.Open())
            await using (var destination = new FileStream(
                restoredFile.StagingPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 81920,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                }))
            {
                await source.CopyToAsync(destination).ConfigureAwait(false);
            }

            // Revalidate after staging to catch a reparse point introduced while the
            // archive entry was copied. Path-based moves still rely on filesystem ACLs
            // to prevent a privileged concurrent actor from swapping a directory afterward.
            EnsurePathWithinConfiguredRoot(targetPath, targetRoot);
            if (Directory.Exists(targetPath))
            {
                throw new IOException($"Cannot restore a file over the existing directory '{targetPath}'.");
            }

            if (File.Exists(targetPath))
            {
                File.Move(targetPath, restoredFile.BackupPath);
                restoredFile.OriginalMoved = true;
            }

            File.Move(restoredFile.StagingPath, targetPath);
            restoredFile.ReplacementInstalled = true;
        }

        public void Commit()
        {
            _committed = true;
            CleanupTemporaryFiles(removeBackups: true);
            _createdDirectories.Clear();
        }

        public void Dispose()
        {
            if (_committed)
            {
                return;
            }

            Rollback();
            CleanupTemporaryFiles(removeBackups: false);
            RemoveCreatedDirectories();
        }

        private void EnsureDirectoryExists(string path, string targetRoot)
        {
            EnsurePathWithinConfiguredRoot(path, targetRoot);
            var missing = new List<string>();
            var current = Path.GetFullPath(path);
            while (!Directory.Exists(current))
            {
                if (File.Exists(current))
                {
                    throw new IOException($"Cannot create restore directory because a file exists at '{current}'.");
                }

                missing.Add(current);
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = parent;
            }

            Directory.CreateDirectory(path);
            EnsurePathWithinConfiguredRoot(path, targetRoot);
            for (var index = missing.Count - 1; index >= 0; index--)
            {
                if (_trackedDirectories.Add(missing[index]))
                {
                    _createdDirectories.Add(missing[index]);
                }
            }
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
                throw new IOException("Restore target is outside its configured directory.");
            }

            // The configured root itself is trusted (it may intentionally be a symlink
            // or junction); descendants are not. Reject links in every existing component
            // below that root, including an existing file target or a dangling link.
            var current = fullRoot;
            foreach (var component in relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, component);
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("Restore target contains a symbolic link or reparse point.");
                    }
                }
                catch (FileNotFoundException)
                {
                    break;
                }
                catch (DirectoryNotFoundException)
                {
                    break;
                }
            }
        }

        private void Rollback()
        {
            for (var index = _files.Count - 1; index >= 0; index--)
            {
                var file = _files[index];
                try
                {
                    if (file.ReplacementInstalled && File.Exists(file.TargetPath))
                    {
                        File.Delete(file.TargetPath);
                    }

                    if (file.OriginalMoved && File.Exists(file.BackupPath))
                    {
                        File.Move(file.BackupPath, file.TargetPath);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Failed to roll back restored file {File}; recovery copy is {RecoveryFile}", file.TargetPath, file.BackupPath);
                }
            }
        }

        private void CleanupTemporaryFiles(bool removeBackups)
        {
            foreach (var file in _files)
            {
                foreach (var path in removeBackups ? new[] { file.StagingPath, file.BackupPath } : new[] { file.StagingPath })
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "Could not remove temporary restore file {File}", path);
                    }
                }
            }
        }

        private void RemoveCreatedDirectories()
        {
            for (var index = _createdDirectories.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (Directory.Exists(_createdDirectories[index]) && !Directory.EnumerateFileSystemEntries(_createdDirectories[index]).Any())
                    {
                        Directory.Delete(_createdDirectories[index]);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not remove empty directory created during restore: {Directory}", _createdDirectories[index]);
                }
            }
        }
    }

    private static void AddDomainContext(
        DbContext context,
        DbConnection sharedConnection,
        ICollection<(DbContext Context, DbConnection OriginalConnection)> contexts)
    {
        var originalConnection = context.Database.GetDbConnection();
        try
        {
            context.Database.SetDbConnection(originalConnection, contextOwnsConnection: false);
            context.Database.SetDbConnection(sharedConnection, contextOwnsConnection: false);
            contexts.Add((context, originalConnection));
        }
        catch
        {
            context.Database.SetDbConnection(originalConnection, contextOwnsConnection: true);
            context.Dispose();
            throw;
        }
    }

    private async Task RestoreDomainContextAsync<TContext>(IDbContextFactory<TContext> factory, Type contextType, ZipArchive archive)
        where TContext : DbContext
    {
        await using var context = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await RestoreDomainContextAsync(context, contextType, archive).ConfigureAwait(false);
    }

    private async Task RestoreDomainContextAsync(DbContext context, Type contextType, ZipArchive archive)
    {
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        await RestoreDatabaseTablesAsync(context, archive, GetDatabaseTableDefinitions(contextType)).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task RestoreDatabaseTablesAsync(DbContext context, ZipArchive archive, IEnumerable<DatabaseTableDefinition> tables)
    {
        foreach (var table in tables)
        {
            var entryPath = NormalizePathSeparator(Path.Combine("Database", $"{table.Name}.json"));
            var zipEntry = archive.GetEntry(entryPath);
            if (zipEntry is null)
            {
                throw new InvalidDataException($"Cannot restore missing database table '{table.Name}'.");
            }

            var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
            await using (zipEntryStream.ConfigureAwait(false))
            {
                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
                {
                    var entity = item?.Deserialize(table.EntityType, _serializerSettings);
                    if (entity is null)
                    {
                        throw new InvalidDataException($"Cannot deserialize entity for database table '{table.Name}'.");
                    }

                    context.Add(entity);
                }
            }
        }
    }

    private static readonly Type[] DatabaseContextTypes =
    [
        typeof(MulletaFlixDbContext),
        typeof(MoviesDbContext),
        typeof(SeriesDbContext),
        typeof(ChannelsDbContext),
        typeof(BooksDbContext)
    ];

    private sealed record DatabaseTableDefinition(string Name, Type ContextType, Type EntityType, PropertyInfo Property);

    private static DatabaseTableDefinition[] GetDatabaseTableDefinitions(Type? contextType = null)
        => DatabaseContextTypes
            .Where(type => contextType is null || type == contextType)
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType.IsAssignableTo(typeof(IQueryable)))
                .Select(property => new DatabaseTableDefinition(
                    property.Name,
                    type,
                    property.PropertyType.GetGenericArguments()[0],
                    property)))
            .ToArray();

    private static string[] GetDatabaseBackupTableNames(Version version)
    {
        var definitions = version == LegacyBackupEngineVersion
            ? GetDatabaseTableDefinitions(typeof(MulletaFlixDbContext))
            : GetDatabaseTableDefinitions();
        return [.. definitions.Select(table => table.Name), nameof(HistoryRow)];
    }

    private static void ValidateDatabaseBackupEntries(BackupManifest manifest, ZipArchive zipArchive)
    {
        var expectedTableNames = GetDatabaseBackupTableNames(manifest.BackupEngineVersion);
        var expectedSet = expectedTableNames.ToHashSet(StringComparer.Ordinal);
        var declaredTableNames = manifest.DatabaseTables ?? [];
        var declaredSet = declaredTableNames.ToHashSet(StringComparer.Ordinal);
        var isLegacyGenericManifest = manifest.BackupEngineVersion == LegacyBackupEngineVersion
            && declaredTableNames.Length == expectedTableNames.Length
            && declaredTableNames.Count(name => name == "DbSet`1") == expectedTableNames.Length - 1
            && declaredTableNames.Count(name => name == nameof(HistoryRow)) == 1;
        var missingDeclarations = isLegacyGenericManifest ? [] : expectedSet.Except(declaredSet, StringComparer.Ordinal).ToArray();
        var unexpectedDeclarations = isLegacyGenericManifest ? [] : declaredSet.Except(expectedSet, StringComparer.Ordinal).ToArray();
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

    private static async Task ValidateDatabaseBackupContentAsync(BackupManifest manifest, ZipArchive zipArchive)
    {
        var entityTypes = GetDatabaseTableDefinitions(manifest.BackupEngineVersion == LegacyBackupEngineVersion
                ? typeof(MulletaFlixDbContext)
                : null)
            .Select(table => (Name: table.Name, EntityType: table.EntityType))
            .Append((Name: nameof(HistoryRow), EntityType: typeof(HistoryRow)));

        foreach (var entityType in entityTypes)
        {
            var entryPath = NormalizePathSeparator(Path.Combine("Database", $"{entityType.Name}.json"));
            var zipEntry = zipArchive.GetEntry(entryPath)!;
            var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
            await using (zipEntryStream.ConfigureAwait(false))
            {
                try
                {
                    await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonObject>(zipEntryStream, _serializerSettings).ConfigureAwait(false))
                    {
                        if (item is null || item.Deserialize(entityType.EntityType, _serializerSettings) is null)
                        {
                            throw new JsonException($"The backup entry '{entryPath}' contains a null or invalid entity.");
                        }
                    }
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                {
                    throw new InvalidDataException($"Cannot restore database table '{entityType.Name}' because its JSON content is invalid or incompatible.", exception);
                }
            }
        }
    }

    private async Task WriteDomainDatabaseTablesAsync(DbContext context, Type contextType, ZipArchive archive)
    {
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        foreach (var table in GetDatabaseTableDefinitions(contextType))
        {
            var query = (IQueryable)table.Property.GetValue(context)!;
            var method = query.GetType().GetMethod(nameof(DbSet<object>.AsAsyncEnumerable))!;
            var values = (IAsyncEnumerable<object>)method.Invoke(query, null)!;
            await WriteDatabaseTableAsync(table.Name, values, archive).ConfigureAwait(false);
        }
    }

    private async Task WriteDatabaseTableAsync(string tableName, IAsyncEnumerable<object> values, ZipArchive archive)
    {
        var zipEntry = archive.CreateEntry(NormalizePathSeparator(Path.Combine("Database", $"{tableName}.json")));
        var count = 0;
        var zipEntryStream = await zipEntry.OpenAsync().ConfigureAwait(false);
        await using (zipEntryStream.ConfigureAwait(false))
        {
            var jsonWriter = new Utf8JsonWriter(zipEntryStream);
            await using (jsonWriter.ConfigureAwait(false))
            {
                jsonWriter.WriteStartArray();
                await foreach (var item in values.ConfigureAwait(false))
                {
                    using var document = JsonSerializer.SerializeToDocument(item, _serializerSettings);
                    document.WriteTo(jsonWriter);
                    count++;
                }

                jsonWriter.WriteEndArray();
            }
        }

        _logger.LogInformation("Backup of entity {Table} with {Number} entries created", tableName, count);
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

        var backupStorageSpace = _storageSpaceProvider.GetFreeSpaceOf(_applicationPaths.BackupPath);

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
                    manifest.DatabaseTables = GetDatabaseBackupTableNames(_backupEngineVersion);
                    if (_createMoviesDbContext is null
                        || _createSeriesDbContext is null
                        || _createChannelsDbContext is null
                        || _createBooksDbContext is null)
                    {
                        throw new InvalidOperationException("A consistent relational backup requires non-pooled contexts for every media domain.");
                    }

                    var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                    await using (dbContext.ConfigureAwait(false))
                    {
                        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        var domainContexts = new List<(DbContext Context, DbConnection OriginalConnection)>();

                        try
                        {
                            var sharedConnection = dbContext.Database.GetDbConnection();
                            AddDomainContext(_createMoviesDbContext(), sharedConnection, domainContexts);
                            AddDomainContext(_createSeriesDbContext(), sharedConnection, domainContexts);
                            AddDomainContext(_createChannelsDbContext(), sharedConnection, domainContexts);
                            AddDomainContext(_createBooksDbContext(), sharedConnection, domainContexts);

                            await using var transaction = await dbContext.Database
                                .BeginTransactionAsync(IsolationLevel.RepeatableRead)
                                .ConfigureAwait(false);
                            foreach (var (context, _) in domainContexts)
                            {
                                await context.Database.UseTransactionAsync(transaction.GetDbTransaction()).ConfigureAwait(false);
                            }

                            static IAsyncEnumerable<object> GetValues(IQueryable dbSet)
                            {
                                var method = dbSet.GetType().GetMethod(nameof(DbSet<object>.AsAsyncEnumerable))!;
                                var enumerable = method.Invoke(dbSet, null)!;
                                return (IAsyncEnumerable<object>)enumerable;
                            }

                            // Read migration history inside the same snapshot as the tables.
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

                            foreach (var (context, _) in domainContexts)
                            {
                                await WriteDomainDatabaseTablesAsync(context, context.GetType(), zipArchive).ConfigureAwait(false);
                            }

                            await transaction.CommitAsync().ConfigureAwait(false);
                        }
                        finally
                        {
                            foreach (var (context, originalConnection) in domainContexts)
                            {
                                try
                                {
                                    await context.Database.UseTransactionAsync(null).ConfigureAwait(false);
                                    var sharedConnection = context.Database.GetDbConnection();
                                    if (sharedConnection.State != ConnectionState.Closed)
                                    {
                                        await context.Database.CloseConnectionAsync().ConfigureAwait(false);
                                    }

                                    context.Database.SetDbConnection(originalConnection, contextOwnsConnection: false);
                                    context.Database.SetDbConnection(originalConnection, contextOwnsConnection: true);
                                }
                                finally
                                {
                                    await context.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                        }
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
        await using var fileStream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            await using var entryStream = await entry.OpenAsync().ConfigureAwait(false);
            var (checksum, length) = await Crc32.ComputeAsync(entryStream).ConfigureAwait(false);
            if (checksum != entry.Crc32 || length != entry.Length)
            {
                throw new InvalidDataException($"Backup entry '{entry.FullName}' failed its size or CRC-32 integrity check.");
            }
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
        var archiveStream = File.OpenRead(archivePath);
        await using (archiveStream.ConfigureAwait(false))
        {
            using var zipStream = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            var manifestEntry = zipStream.GetEntry(ManifestEntryName);
            if (manifestEntry is null)
            {
                return null;
            }

            var manifestStream = await manifestEntry.OpenAsync().ConfigureAwait(false);
            await using (manifestStream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, _serializerSettings).ConfigureAwait(false);
            }
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
