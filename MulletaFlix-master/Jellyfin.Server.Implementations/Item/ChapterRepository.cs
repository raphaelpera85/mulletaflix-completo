using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MySqlConnector;

namespace MulletaFlix.Server.Implementations.Item;

/// <summary>
/// The Chapter manager.
/// </summary>
public class ChapterRepository : IChapterRepository
{
    private const int MaxSaveAttempts = 3;
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly IImageProcessor _imageProcessor;
    private readonly ILogger<ChapterRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterRepository"/> class.
    /// </summary>
    /// <param name="dbProvider">The EFCore provider.</param>
    /// <param name="imageProcessor">The Image Processor.</param>
    public ChapterRepository(IDbContextFactory<MulletaFlixDbContext> dbProvider, IImageProcessor imageProcessor)
        : this(dbProvider, imageProcessor, NullLogger<ChapterRepository>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterRepository"/> class.
    /// </summary>
    /// <param name="dbProvider">The EFCore provider.</param>
    /// <param name="imageProcessor">The Image Processor.</param>
    /// <param name="logger">The logger.</param>
    public ChapterRepository(
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        IImageProcessor imageProcessor,
        ILogger<ChapterRepository> logger)
    {
        _dbProvider = dbProvider;
        _imageProcessor = imageProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public ChapterInfo? GetChapter(Guid baseItemId, int index)
    {
        using var context = _dbProvider.CreateDbContext();
        var chapter = context.Chapters.AsNoTracking()
            .Select(e => new
            {
                chapter = e,
                baseItemPath = e.Item.Path
            })
            .FirstOrDefault(e => e.chapter.ItemId.Equals(baseItemId) && e.chapter.ChapterIndex == index);
        if (chapter is not null)
        {
            return Map(chapter.chapter, chapter.baseItemPath!);
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ChapterInfo> GetChapters(Guid baseItemId)
    {
        using var context = _dbProvider.CreateDbContext();
        return context.Chapters.AsNoTracking().Where(e => e.ItemId.Equals(baseItemId))
            .OrderBy(e => e.StartPositionTicks)
            .Select(e => new
            {
                chapter = e,
                baseItemPath = e.Item.Path
            })
            .AsEnumerable()
            .Select(e => Map(e.chapter, e.baseItemPath!))
            .ToArray();
    }

    /// <inheritdoc />
    public void SaveChapters(Guid itemId, IReadOnlyList<ChapterInfo> chapters)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var context = _dbProvider.CreateDbContext();
                using var transaction = context.Database.BeginTransaction();
                context.Chapters.Where(e => e.ItemId.Equals(itemId)).ExecuteDelete();
                for (var i = 0; i < chapters.Count; i++)
                {
                    var chapter = chapters[i];
                    context.Chapters.Add(Map(chapter, i, itemId));
                }

                // Keep this sync path synchronous; replay the complete transaction on transient locks.
                context.SaveChanges();
                transaction.Commit();
                return;
            }
            catch (Exception exception) when (attempt < MaxSaveAttempts && IsTransientLockFailure(exception))
            {
                _logger.LogWarning(
                    exception,
                    "Transient MariaDB lock failure while saving chapters for item {ItemId}; retrying attempt {Attempt}",
                    itemId,
                    attempt + 1);
                Thread.Sleep(TimeSpan.FromMilliseconds(attempt * 150));
            }
        }
    }

    /// <inheritdoc />
    public async Task SaveChaptersAsync(Guid itemId, IReadOnlyList<ChapterInfo> chapters, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var context = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                await using (context.ConfigureAwait(false))
                {
                    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    await context.Chapters.Where(e => e.ItemId.Equals(itemId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < chapters.Count; i++)
                    {
                        var chapter = chapters[i];
                        context.Chapters.Add(Map(chapter, i, itemId));
                    }

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }
            catch (Exception exception) when (attempt < MaxSaveAttempts && IsTransientLockFailure(exception))
            {
                _logger.LogWarning(
                    exception,
                    "Transient MariaDB lock failure while saving chapters for item {ItemId}; retrying attempt {Attempt}",
                    itemId,
                    attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 150), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientLockFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mySqlException && mySqlException.Number is 1205 or 1213)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task DeleteChaptersAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            await dbContext.Chapters.Where(c => c.ItemId.Equals(itemId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Chapter Map(ChapterInfo chapterInfo, int index, Guid itemId)
    {
        return new Chapter()
        {
            ChapterIndex = index,
            StartPositionTicks = chapterInfo.StartPositionTicks,
            ImageDateModified = chapterInfo.ImageDateModified,
            ImagePath = chapterInfo.ImagePath,
            ItemId = itemId,
            Name = chapterInfo.Name,
            Item = null!
        };
    }

    private ChapterInfo Map(Chapter chapterInfo, string baseItemPath)
    {
        var chapterEntity = new ChapterInfo()
        {
            StartPositionTicks = chapterInfo.StartPositionTicks,
            ImageDateModified = chapterInfo.ImageDateModified.GetValueOrDefault(),
            ImagePath = chapterInfo.ImagePath,
            Name = chapterInfo.Name,
        };

        if (!string.IsNullOrEmpty(chapterInfo.ImagePath))
        {
            chapterEntity.ImageTag = _imageProcessor.GetImageCacheTag(baseItemPath, chapterEntity.ImageDateModified);
        }

        return chapterEntity;
    }
}

