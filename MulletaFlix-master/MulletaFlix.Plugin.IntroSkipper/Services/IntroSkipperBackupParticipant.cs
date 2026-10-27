// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Reflection;
using System.Text;
using IntroSkipper.Db;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IntroSkipper.Services;

internal sealed class IntroSkipperBackupParticipant : IBackupDatabaseParticipant
{
    private const string SnapshotFileName = "data.json";
    private const int ReadBufferSize = 64 * 1024;
    private static readonly JsonSerializerSettings SnapshotJsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ContractResolver = new PrivateSetterContractResolver()
    };
    private readonly IDbContextFactory<IntroSkipperDbContext> _contextFactory;
    private readonly IDbContextFactory<DetectionCacheDbContext> _cacheContextFactory;

    public IntroSkipperBackupParticipant(
        IDbContextFactory<IntroSkipperDbContext> contextFactory,
        IDbContextFactory<DetectionCacheDbContext> cacheContextFactory)
    {
        _contextFactory = contextFactory;
        _cacheContextFactory = cacheContextFactory;
    }

    public string Name => "IntroSkipper";

    public async Task WriteBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await IntroSkipperSchema.EnsureAsync(db, cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await ReadSnapshotAsync(db, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var entry = archive.CreateEntry(GetEntryName(entryPrefix), CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), ReadBufferSize, leaveOpen: false);
        await writer.WriteAsync(JsonConvert.SerializeObject(snapshot, SnapshotJsonSettings)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task ValidateBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
    {
        _ = await ReadAndValidateSnapshotAsync(archive, entryPrefix, cancellationToken).ConfigureAwait(false);
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await IntroSkipperSchema.EnsureAsync(db, cancellationToken).ConfigureAwait(false);
        await IntroSkipperSchema.ValidateAsync(db, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreBackupAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAndValidateSnapshotAsync(archive, entryPrefix, cancellationToken).ConfigureAwait(false);
        await using (var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            await IntroSkipperSchema.EnsureAsync(db, cancellationToken).ConfigureAwait(false);
            await IntroSkipperSchema.ValidateAsync(db, cancellationToken).ConfigureAwait(false);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await db.ProjectionExternalOperations.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.ProjectionQueue.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.DisabledItems.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.ImportHistory.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.AnalyzedItems.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.SeasonAnalysisOverrides.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.SeasonStates.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Segments.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await AddAsync(db, db.Segments, snapshot.Segments, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.SeasonStates, snapshot.SeasonStates, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.SeasonAnalysisOverrides, snapshot.SeasonAnalysisOverrides, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.AnalyzedItems, snapshot.AnalyzedItems, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.DisabledItems, snapshot.DisabledItems, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.ImportHistory, snapshot.ImportHistory, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.ProjectionQueue, snapshot.ProjectionQueue, cancellationToken).ConfigureAwait(false);
            await AddAsync(db, db.ProjectionExternalOperations, snapshot.ProjectionExternalOperations, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cache = await _cacheContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await IntroSkipperSchema.EnsureAsync(cache, cancellationToken).ConfigureAwait(false);
        await cache.DetectionCache.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddAsync<TEntity>(IntroSkipperDbContext db, DbSet<TEntity> set, List<TEntity> entities, CancellationToken cancellationToken)
        where TEntity : class
    {
        set.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.ChangeTracker.Clear();
    }

    // shortcut: the durable snapshot is buffered as one object; stream per-table entries if IntroSkipper data grows beyond memory-safe size.
    private static async Task<BackupSnapshot> ReadSnapshotAsync(IntroSkipperDbContext db, CancellationToken cancellationToken)
        => new()
        {
            Segments = await db.Segments.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            SeasonStates = await db.SeasonStates.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            SeasonAnalysisOverrides = await db.SeasonAnalysisOverrides.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            AnalyzedItems = await db.AnalyzedItems.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            ImportHistory = await db.ImportHistory.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            DisabledItems = await db.DisabledItems.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            ProjectionQueue = await db.ProjectionQueue.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            ProjectionExternalOperations = await db.ProjectionExternalOperations.AsNoTracking().OrderBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false)
        };

    private static async Task<BackupSnapshot> ReadAndValidateSnapshotAsync(ZipArchive archive, string entryPrefix, CancellationToken cancellationToken)
    {
        var entryName = GetEntryName(entryPrefix);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"IntroSkipper backup entry '{entryName}' is missing.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, ReadBufferSize, leaveOpen: false);
        var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        BackupSnapshot snapshot;
        try
        {
            snapshot = JsonConvert.DeserializeObject<BackupSnapshot>(json, SnapshotJsonSettings)
                ?? throw new InvalidDataException("IntroSkipper backup snapshot is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("IntroSkipper backup snapshot is malformed or incomplete.", exception);
        }
        snapshot.Validate();
        return snapshot;
    }

    private static string GetEntryName(string entryPrefix)
        => $"{entryPrefix.TrimEnd('/')}/{SnapshotFileName}";

    private sealed class PrivateSetterContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (member is PropertyInfo info && info.GetSetMethod(nonPublic: true) is not null)
            {
                property.Writable = true;
            }

            return property;
        }
    }

    private sealed class BackupSnapshot
    {
        [JsonProperty(Required = Required.Always)]
        public List<DbSegment> Segments { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbSeasonState> SeasonStates { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbSeasonAnalysisOverride> SeasonAnalysisOverrides { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbAnalyzedItem> AnalyzedItems { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbImportRecord> ImportHistory { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbDisabledItem> DisabledItems { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbProjectionQueueItem> ProjectionQueue { get; set; } = [];
        [JsonProperty(Required = Required.Always)]
        public List<DbProjectionExternalOperation> ProjectionExternalOperations { get; set; } = [];

        public void Validate()
        {
            if (Segments is null || SeasonStates is null || SeasonAnalysisOverrides is null || AnalyzedItems is null
                || ImportHistory is null || DisabledItems is null || ProjectionQueue is null || ProjectionExternalOperations is null)
            {
                throw new InvalidDataException("IntroSkipper backup snapshot is missing a durable table.");
            }

            Check(Segments.Any(row => row.Id == Guid.Empty || row.ItemId == Guid.Empty || row.StartTicks < 0 || row.EndTicks <= row.StartTicks || row.ConfigHash is null
                    || !Enum.IsDefined(row.Type) || !Enum.IsDefined(row.Source) || !Enum.IsDefined(row.State))
                || Segments.Select(row => row.Id).Distinct().Count() != Segments.Count
                || Segments.Select(row => (row.ItemId, row.Type, row.StartTicks, row.EndTicks)).Distinct().Count() != Segments.Count, "Segments");
            Check(SeasonStates.Any(row => row.SeasonId == Guid.Empty || !Enum.IsDefined(row.Type) || !Enum.IsDefined(row.Action))
                || SeasonStates.Select(row => (row.SeasonId, row.Type)).Distinct().Count() != SeasonStates.Count, "SeasonStates");
            Check(SeasonAnalysisOverrides.Any(row => row.SeasonId == Guid.Empty)
                || SeasonAnalysisOverrides.Select(row => row.SeasonId).Distinct().Count() != SeasonAnalysisOverrides.Count, "SeasonAnalysisOverrides");
            Check(AnalyzedItems.Any(row => row.ItemId == Guid.Empty || row.ConfigHash is null || !Enum.IsDefined(row.Type))
                || AnalyzedItems.Select(row => (row.ItemId, row.Type)).Distinct().Count() != AnalyzedItems.Count, "AnalyzedItems");
            Check(ImportHistory.Any(row => row.Id < 0 || row.Notes is null)
                || ImportHistory.Select(row => row.Id).Where(id => id != 0).Distinct().Count() != ImportHistory.Count(row => row.Id != 0), "ImportHistory");
            Check(DisabledItems.Any(row => row.ItemId == Guid.Empty)
                || DisabledItems.Select(row => row.ItemId).Distinct().Count() != DisabledItems.Count, "DisabledItems");
            Check(ProjectionQueue.Any(row => row.ItemId == Guid.Empty || row.Version < 0 || row.AttemptCount < 0)
                || ProjectionQueue.Select(row => row.ItemId).Distinct().Count() != ProjectionQueue.Count, "ProjectionQueue");
            Check(ProjectionExternalOperations.Any(row => row.Id < 0 || row.ItemId == Guid.Empty || row.ExternalSegmentId == Guid.Empty || row.EndTicks <= row.StartTicks || !Enum.IsDefined(row.ExpectedType))
                || ProjectionExternalOperations.Select(row => row.Id).Where(id => id != 0).Distinct().Count() != ProjectionExternalOperations.Count(row => row.Id != 0), "ProjectionExternalOperations");
        }

        private static void Check(bool invalid, string table)
        {
            if (invalid)
            {
                throw new InvalidDataException($"IntroSkipper backup contains invalid or duplicate rows in '{table}'.");
            }
        }
    }
}
