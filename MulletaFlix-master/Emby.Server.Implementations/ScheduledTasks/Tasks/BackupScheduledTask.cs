using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Scheduled task for creating automatic backups.
/// </summary>
public class BackupScheduledTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILogger<BackupScheduledTask> _logger;
    private readonly IBackupService _backupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupScheduledTask"/> class.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="backupService">The backup service.</param>
    public BackupScheduledTask(ILogger<BackupScheduledTask> logger, IBackupService backupService)
    {
        _logger = logger;
        _backupService = backupService;
    }

    /// <inheritdoc />
    public string Name => "Scheduled Backup";

    /// <inheritdoc />
    public string Key => "BackupScheduledTask";

    /// <inheritdoc />
    public string Description => "Creates automatic system backups on a schedule";

    /// <inheritdoc />
    public string Category => "Backup";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled backup");

        var options = new BackupOptionsDto
        {
            Database = true,
            Metadata = true,
            Subtitles = true,
            Trickplay = false
        };

        var manifest = await _backupService.CreateBackupAsync(options).ConfigureAwait(false);

        _logger.LogInformation("Scheduled backup completed: {Path}", manifest.Path);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new List<TaskTriggerInfo>
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }
}