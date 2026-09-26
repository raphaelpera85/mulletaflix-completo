using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Class PeopleValidationTask.
/// </summary>
public class PeopleValidationTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILocalizationManager _localization;
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeopleValidationTask" /> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="dbContextFactory">Instance of the <see cref="IDbContextFactory{TContext}"/> interface.</param>
    public PeopleValidationTask(ILibraryManager libraryManager, ILocalizationManager localization, IDbContextFactory<MulletaFlixDbContext> dbContextFactory)
    {
        _libraryManager = libraryManager;
        _localization = localization;
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public string Name => _localization.GetLocalizedString("TaskRefreshPeople");

    /// <inheritdoc />
    public string Description => _localization.GetLocalizedString("TaskRefreshPeopleDescription");

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc />
    public string Key => "RefreshPeople";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <summary>
    /// Creates the triggers that define when the task will run.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{TaskTriggerInfo}"/> containing the default trigger infos for this task.</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromDays(7).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        IProgress<double> subProgress = new Progress<double>((val) => progress.Report(val / 2));
        await _libraryManager.ValidatePeopleAsync(subProgress, cancellationToken).ConfigureAwait(false);

        subProgress = new Progress<double>((val) => progress.Report((val / 2) + 50));
        var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            var dupQuery = context.Peoples
                    .GroupBy(e => new { e.Name, e.PersonType })
                    .Where(e => e.Count() > 1)
                    .Select(e => e.Select(f => f.Id).ToArray());

            // Snapshot the duplicate groups exactly once, in a deterministic order.
            // The previous loop paged with Take(PartitionSize) and no Skip or cursor, so it only
            // advanced as a side effect of the ExecuteDelete below. Any group whose duplicates could
            // not be deleted (for example when a PeopleBaseItemMap row still references it, or the
            // update/delete affected zero rows) kept returning the same first page, and because
            // itemCounter stayed equal to PartitionSize the do/while never terminated. It also
            // re-ran the full GROUP BY over Peoples for every page, which is quadratic in the number
            // of duplicate groups. Materialising the groups removes both problems: every group is
            // processed once and the task always terminates.
            var duplicateGroups = await dupQuery
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var total = duplicateGroups.Count;

            const int PartitionSize = 100;
            for (var offset = 0; offset < duplicateGroups.Count; offset += PartitionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchSize = Math.Min(PartitionSize, duplicateGroups.Count - offset);

                for (var i = 0; i < batchSize; i++)
                {
                    var item = duplicateGroups[offset + i];
                    if (item.Length < 2)
                    {
                        continue;
                    }

                    var reference = item[0];
                    var dupsList = item[1..].ToList();
                    await context.PeopleBaseItemMap.WhereOneOrMany(dupsList, e => e.PeopleId)
                        .ExecuteUpdateAsync(e => e.SetProperty(f => f.PeopleId, reference), cancellationToken)
                        .ConfigureAwait(false);
                    await context.Peoples.Where(e => dupsList.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                    subProgress.Report(100f / total * (offset + i));
                }
            }

            subProgress.Report(100);
        }
    }
}

