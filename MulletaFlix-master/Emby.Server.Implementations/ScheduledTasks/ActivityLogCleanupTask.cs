using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations.Contexts;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks;

/// <summary>
/// Scheduled task that cleans up old activity log entries.
/// </summary>
public class ActivityLogCleanupTask : IScheduledTask
{
    private static readonly TimeSpan ActivityLogRetention = TimeSpan.FromDays(90);
    private readonly IDbContextFactory<UsersDbContext> _dbContextFactory;
    private readonly ILogger<ActivityLogCleanupTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityLogCleanupTask"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory for the users database context.</param>
    /// <param name="logger">The logger.</param>
    public ActivityLogCleanupTask(
        IDbContextFactory<UsersDbContext> dbContextFactory,
        ILogger<ActivityLogCleanupTask> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Limpar trilha de auditoria";

    /// <inheritdoc />
    public string Key => "ActivityLogCleanup";

    /// <inheritdoc />
    public string Description => "Remove entradas antigas da trilha de atividade e auditoria. Executa a cada 24 horas.";

    /// <inheritdoc />
    public string Category => "Manutenção";

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "success";
        long expiredCandidates = 0;
        long deletedEntries = 0;
        ActivityLogCleanupMetrics.RecordActive(1);

        try
        {
            _logger.LogInformation("Iniciando limpeza da trilha de auditoria...");

            var cutoff = DateTime.UtcNow.Subtract(ActivityLogRetention);
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var query = dbContext.ActivityLogs.Where(entry => entry.DateCreated < cutoff);
            expiredCandidates = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            if (expiredCandidates == 0)
            {
                result = "no_items";
                _logger.LogInformation("Nenhuma entrada de auditoria antiga encontrada para limpeza.");
                progress.Report(100);
                return;
            }

            deletedEntries = await query.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Limpeza da trilha de auditoria concluída. Total removido: {DeletedCount}. Corte aplicado: {Cutoff}",
                deletedEntries,
                cutoff);

            progress.Report(100);
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
            ActivityLogCleanupMetrics.RecordRun(
                result,
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                expiredCandidates,
                deletedEntries);
            ActivityLogCleanupMetrics.RecordActive(-1);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            }
        };
    }

    /// <summary>
    /// Determines whether an activity log entry should be deleted.
    /// </summary>
    /// <param name="dateCreated">The entry creation time.</param>
    /// <param name="cutoff">The retention cutoff.</param>
    /// <returns>A value indicating whether the entry should be deleted.</returns>
    public static bool ShouldDeleteActivityLog(DateTime dateCreated, DateTime cutoff)
    {
        return dateCreated < cutoff;
    }
}
