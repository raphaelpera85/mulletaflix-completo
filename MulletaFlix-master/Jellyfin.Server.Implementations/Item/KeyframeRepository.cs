using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MySqlConnector;

namespace MulletaFlix.Server.Implementations.Item;

/// <summary>
/// Repository for obtaining Keyframe data.
/// </summary>
public class KeyframeRepository : IKeyframeRepository
{
    private const int MaxSaveAttempts = 3;
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly ILogger<KeyframeRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyframeRepository"/> class.
    /// </summary>
    /// <param name="dbProvider">The EFCore db factory.</param>
    public KeyframeRepository(IDbContextFactory<MulletaFlixDbContext> dbProvider)
        : this(dbProvider, NullLogger<KeyframeRepository>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyframeRepository"/> class.
    /// </summary>
    /// <param name="dbProvider">The EFCore db factory.</param>
    /// <param name="logger">The logger.</param>
    public KeyframeRepository(IDbContextFactory<MulletaFlixDbContext> dbProvider, ILogger<KeyframeRepository> logger)
    {
        _dbProvider = dbProvider;
        _logger = logger;
    }

    private static MediaEncoding.Keyframes.KeyframeData Map(KeyframeData entity)
    {
        return new MediaEncoding.Keyframes.KeyframeData(
            entity.TotalDuration,
            (entity.KeyframeTicks ?? []).ToList());
    }

    private KeyframeData Map(MediaEncoding.Keyframes.KeyframeData dto, Guid itemId)
    {
        return new()
        {
            ItemId = itemId,
            TotalDuration = dto.TotalDuration,
            KeyframeTicks = dto.KeyframeTicks.ToList()
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<MediaEncoding.Keyframes.KeyframeData> GetKeyframeData(Guid itemId)
    {
        using var context = _dbProvider.CreateDbContext();

        return context.KeyframeData.AsNoTracking().Where(e => e.ItemId.Equals(itemId)).Select(e => Map(e)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaEncoding.Keyframes.KeyframeData>> GetKeyframeDataAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var context = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.KeyframeData.AsNoTracking()
            .Where(e => e.ItemId.Equals(itemId))
            .Select(e => Map(e))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveKeyframeDataAsync(Guid itemId, MediaEncoding.Keyframes.KeyframeData data, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var context = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                await using (context.ConfigureAwait(false))
                {
                    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    await context.KeyframeData.Where(e => e.ItemId.Equals(itemId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                    await context.KeyframeData.AddAsync(Map(data, itemId), cancellationToken).ConfigureAwait(false);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }
            catch (Exception exception) when (attempt < MaxSaveAttempts && IsTransientLockFailure(exception))
            {
                _logger.LogWarning(
                    exception,
                    "Transient MariaDB lock failure while saving keyframe data for item {ItemId}; retrying attempt {Attempt}",
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
    public async Task DeleteKeyframeDataAsync(Guid itemId, CancellationToken cancellationToken)
    {
        using var context = _dbProvider.CreateDbContext();
        await context.KeyframeData.Where(e => e.ItemId.Equals(itemId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

