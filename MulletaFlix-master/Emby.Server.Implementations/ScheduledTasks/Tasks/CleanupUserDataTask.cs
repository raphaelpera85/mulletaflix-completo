#pragma warning disable RS0030 // Do not use banned APIs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Item;
using Emby.Server.Implementations.ScheduledTasks;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Task to clean up any detached userdata from the database.
/// </summary>
public class CleanupUserDataTask : IScheduledTask
{
    private readonly ILocalizationManager _localization;
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbProvider;
    private readonly ILogger<CleanupUserDataTask> _logger;
    private readonly Func<IQueryable<UserData>, CancellationToken, Task<int>> _deleteExpiredUserDataAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupUserDataTask"/> class.
    /// </summary>
    /// <param name="localization">The localisation Provider.</param>
    /// <param name="dbProvider">The DB context factory.</param>
    /// <param name="logger">A logger.</param>
    public CleanupUserDataTask(ILocalizationManager localization, IDbContextFactory<MulletaFlixDbContext> dbProvider, ILogger<CleanupUserDataTask> logger)
        : this(localization, dbProvider, logger, static (query, cancellationToken) => query.ExecuteDeleteAsync(cancellationToken))
    {
    }

    internal CleanupUserDataTask(
        ILocalizationManager localization,
        IDbContextFactory<MulletaFlixDbContext> dbProvider,
        ILogger<CleanupUserDataTask> logger,
        Func<IQueryable<UserData>, CancellationToken, Task<int>> deleteExpiredUserDataAsync)
    {
        _localization = localization;
        _dbProvider = dbProvider;
        _logger = logger;
        _deleteExpiredUserDataAsync = deleteExpiredUserDataAsync;
    }

    /// <inheritdoc />
    public string Name => _localization.GetLocalizedString("CleanupUserDataTask");

    /// <inheritdoc />
    public string Description => _localization.GetLocalizedString("CleanupUserDataTaskDescription");

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public string Key => nameof(CleanupUserDataTask);

    /// <inheritdoc/>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        const int LimitDays = 90;
        var startedAt = Stopwatch.GetTimestamp();
        long detachedCount = 0;
        long expiredCount = 0;
        long deletedCount = 0;
        var result = "failure";
        UserDataCleanupMetrics.RecordActive(1);
        try
        {
            var userDataDate = DateTime.UtcNow.AddDays(LimitDays * -1);
            var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var detachedUserData = dbContext.UserData.Where(e => e.ItemId == BaseItemRepository.PlaceholderId);
                detachedCount = detachedUserData.Count();
                _logger.LogInformation("There are {NoDetached} detached UserData entries.", detachedCount);

                detachedUserData = FilterExpiredDetachedUserData(detachedUserData, userDataDate);
                expiredCount = detachedUserData.Count();
                _logger.LogInformation("{NoDetached} are older then {Limit} days.", expiredCount, LimitDays);

                if (expiredCount > 0)
                {
                    deletedCount = await _deleteExpiredUserDataAsync(detachedUserData, cancellationToken).ConfigureAwait(false);
                }
            }

            progress.Report(100);
            result = expiredCount == 0 ? "no_items" : "success";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = "cancelled";
            throw;
        }
        catch
        {
            result = "failure";
            throw;
        }
        finally
        {
            UserDataCleanupMetrics.RecordRun(
                result,
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                detachedCount,
                expiredCount,
                deletedCount);
            UserDataCleanupMetrics.RecordActive(-1);
        }
    }

    internal static IQueryable<UserData> FilterExpiredDetachedUserData(IQueryable<UserData> detachedUserData, DateTime cutoff)
    {
        return detachedUserData.Where(entry => entry.RetentionDate < cutoff);
    }

    /// <inheritdoc/>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield break;
    }
}
