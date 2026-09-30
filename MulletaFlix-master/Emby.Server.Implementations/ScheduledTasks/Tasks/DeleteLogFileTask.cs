using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Deletes old log files.
/// </summary>
public class DeleteLogFileTask : IScheduledTask, IConfigurableScheduledTask
{
    /// <summary>
    /// Piso aplicado quando a retenção configurada é inválida (zero ou negativa).
    /// </summary>
    private const int DefaultLogFileRetentionDays = 3;

    private readonly IConfigurationManager _configurationManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILocalizationManager _localization;
    private readonly ILogger<DeleteLogFileTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteLogFileTask" /> class.
    /// </summary>
    /// <param name="configurationManager">Instance of the <see cref="IConfigurationManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{DeleteLogFileTask}"/> interface.</param>
    public DeleteLogFileTask(
        IConfigurationManager configurationManager,
        IFileSystem fileSystem,
        ILocalizationManager localization,
        ILogger<DeleteLogFileTask>? logger = null)
    {
        _configurationManager = configurationManager;
        _fileSystem = fileSystem;
        _localization = localization;
        _logger = logger ?? NullLogger<DeleteLogFileTask>.Instance;
    }

    /// <inheritdoc />
    public string Name => _localization.GetLocalizedString("TaskCleanLogs");

    /// <inheritdoc />
    public string Description => string.Format(
        CultureInfo.InvariantCulture,
        _localization.GetLocalizedString("TaskCleanLogsDescription"),
        _configurationManager.CommonConfiguration.LogFileRetentionDays);

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public string Key => "CleanLogFiles";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Uma retenção não positiva colocaria o corte em "agora" (ou no futuro),
        // tornando TODO log elegível — inclusive o arquivo que está sendo escrito
        // neste instante. Isso apagaria justamente o rastro de diagnóstico que a
        // tarefa deveria preservar, então o padrão é aplicado como piso.
        var retentionDays = _configurationManager.CommonConfiguration.LogFileRetentionDays;
        if (retentionDays <= 0)
        {
            retentionDays = DefaultLogFileRetentionDays;
        }

        // Delete log files more than n days old
        var minDateModified = DateTime.UtcNow.AddDays(-retentionDays);

        var filesToDelete = _fileSystem.GetFiles(_configurationManager.CommonApplicationPaths.LogDirectoryPath, true)
            .Where(f => _fileSystem.GetLastWriteTimeUtc(f) < minDateModified)
            .ToList();

        var index = 0;

        foreach (var file in filesToDelete)
        {
            double percent = index / (double)filesToDelete.Count;

            progress.Report(100 * percent);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _fileSystem.DeleteFile(file.FullName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Em Windows o log ativo fica bloqueado pelo sink. Abortar aqui
                // deixaria de remover os arquivos seguintes, que estão de fato
                // vencidos, e a retenção pararia de funcionar silenciosamente.
                _logger.LogWarning(
                    ex,
                    "Não foi possível remover o log vencido {Name}; a limpeza continua com os demais arquivos.",
                    file.Name);
            }

            index++;
        }

        progress.Report(100);

        return Task.CompletedTask;
    }
}
