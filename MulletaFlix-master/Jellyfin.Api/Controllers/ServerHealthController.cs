using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Server Health Controller - aggregates health metrics for dashboard.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Route("ServerHealth")]
public class ServerHealthController : BaseMulletaFlixApiController
{
    private readonly IServerApplicationHost _applicationHost;
    private readonly IServerApplicationPaths _applicationPaths;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITaskManager _taskManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IPluginManager _pluginManager;
    private readonly IBackupService _backupService;
    private readonly ISystemManager _systemManager;

    public ServerHealthController(
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IServerConfigurationManager configurationManager,
        IHostApplicationLifetime applicationLifetime,
        ITaskManager taskManager,
        ILibraryManager libraryManager,
        IPluginManager pluginManager,
        IBackupService backupService,
        ISystemManager systemManager)
    {
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _configurationManager = configurationManager;
        _applicationLifetime = applicationLifetime;
        _taskManager = taskManager;
        _libraryManager = libraryManager;
        _pluginManager = pluginManager;
        _backupService = backupService;
        _systemManager = systemManager;
    }

    /// <summary>
    /// Gets comprehensive server health summary.
    /// </summary>
    /// <response code="200">Health summary returned.</response>
    [HttpGet("Summary")]
    [ProducesResponseType(typeof(ServerHealthSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServerHealthSummaryDto>> GetHealthSummary()
    {
        var summary = new ServerHealthSummaryDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            ServerName = _applicationHost.FriendlyName,
            Version = _applicationHost.ApplicationVersionString,
            HasPendingRestart = _applicationHost.HasPendingRestart,
            IsShuttingDown = _applicationLifetime.ApplicationStopping.IsCancellationRequested
        };

        // Storage health
        summary.Storage = await GetStorageHealthAsync().ConfigureAwait(false);

        // Task health
        summary.Tasks = GetTaskHealth();

        // Plugin health
        summary.Plugins = GetPluginHealth();

        // Backup health
        summary.Backup = await GetBackupHealthAsync().ConfigureAwait(false);

        // System health
        summary.System = GetSystemHealth();

        // Overall status
        summary.OverallStatus = CalculateOverallStatus(summary);

        return Ok(summary);
    }

    private async Task<StorageHealthDto> GetStorageHealthAsync()
    {
        var storageInfo = _systemManager.GetSystemStorageInfo();

        var folders = new[]
        {
            storageInfo.ProgramDataFolder,
            storageInfo.WebFolder,
            storageInfo.LogFolder,
            storageInfo.ImageCacheFolder,
            storageInfo.CacheFolder,
            storageInfo.InternalMetadataFolder,
            storageInfo.TranscodingTempFolder
        };

        var libraryFolders = storageInfo.Libraries.SelectMany(l => l.Folders).ToArray();
        var allFolders = folders.Concat(libraryFolders).Where(f => f != null).ToArray();

        var criticalFolders = allFolders.Where(f => f.FreeSpace > 0 && (double)f.FreeSpace / (f.FreeSpace + f.UsedSpace) < 0.1).ToArray();
        var warningFolders = allFolders.Where(f => f.FreeSpace > 0 && (double)f.FreeSpace / (f.FreeSpace + f.UsedSpace) < 0.2).Except(criticalFolders).ToArray();

        return new StorageHealthDto
        {
            TotalFreeSpace = allFolders.Where(f => f.FreeSpace > 0).Sum(f => f.FreeSpace),
            TotalUsedSpace = allFolders.Where(f => f.FreeSpace > 0).Sum(f => f.UsedSpace),
            CriticalCount = criticalFolders.Length,
            WarningCount = warningFolders.Length,
            HealthyCount = allFolders.Length - criticalFolders.Length - warningFolders.Length,
            CriticalPaths = criticalFolders.Select(f => f.Path).ToArray(),
            WarningPaths = warningFolders.Select(f => f.Path).ToArray(),
            Status = criticalFolders.Length > 0 ? HealthStatus.Critical : warningFolders.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private TaskHealthDto GetTaskHealth()
    {
        var taskWorkers = _taskManager.ScheduledTasks.ToArray();
        var failedTasks = taskWorkers.Where(t => t.LastExecutionResult?.Status == TaskCompletionStatus.Failed).ToArray();
        var runningTasks = taskWorkers.Where(t => t.State == TaskState.Running).ToArray();
        // Note: NextTrigger is not available in TaskTriggerInfo DTO, overdue check would require accessing trigger implementations
        var overdueTasks = Array.Empty<IScheduledTaskWorker>();

        return new TaskHealthDto
        {
            TotalCount = taskWorkers.Length,
            RunningCount = runningTasks.Length,
            FailedCount = failedTasks.Length,
            OverdueCount = overdueTasks.Length,
            FailedTaskNames = failedTasks.Select(t => t.Name).ToArray(),
            OverdueTaskNames = overdueTasks.Select(t => t.Name).ToArray(),
            RunningTaskNames = runningTasks.Select(t => t.Name).ToArray(),
            Status = failedTasks.Length > 0 ? HealthStatus.Critical : overdueTasks.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private PluginHealthDto GetPluginHealth()
    {
        var plugins = _pluginManager.Plugins.ToArray();
        var incompatiblePlugins = plugins.Where(p => p.Manifest != null &&
            p.Manifest.TargetAbi != null &&
            !IsCompatible(p.Manifest.TargetAbi)).ToArray();
        var updateAvailable = plugins.Where(p => HasUpdateAvailable(p)).ToArray();
        var disabledPlugins = plugins.Where(p => !p.IsEnabledAndSupported && p.Manifest.Status == PluginStatus.Disabled).ToArray();

        return new PluginHealthDto
        {
            TotalCount = plugins.Length,
            EnabledCount = plugins.Count(p => p.IsEnabledAndSupported),
            DisabledCount = disabledPlugins.Length,
            IncompatibleCount = incompatiblePlugins.Length,
            UpdateAvailableCount = updateAvailable.Length,
            IncompatibleNames = incompatiblePlugins.Select(p => p.Name).ToArray(),
            UpdateAvailableNames = updateAvailable.Select(p => p.Name).ToArray(),
            Status = incompatiblePlugins.Length > 0 ? HealthStatus.Critical : updateAvailable.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private async Task<BackupHealthDto> GetBackupHealthAsync()
    {
        var backups = await _backupService.EnumerateBackups().ConfigureAwait(false);
        var lastBackup = backups.OrderByDescending(b => b.DateCreated).FirstOrDefault();
        var lastSuccessfulBackup = backups.Where(b => b.Options.Database == true).OrderByDescending(b => b.DateCreated).FirstOrDefault();

        return new BackupHealthDto
        {
            TotalBackups = backups.Length,
            LastBackupTime = lastBackup?.DateCreated,
            LastSuccessfulBackupTime = lastSuccessfulBackup?.DateCreated,
            LastBackupResult = lastBackup?.Options?.Database == true ? TaskCompletionStatus.Completed : TaskCompletionStatus.Failed,
            LastBackupSize = lastBackup?.Options?.Database == true ? "Includes DB" : "Config only",
            Status = lastSuccessfulBackup == null ? HealthStatus.Critical :
                (DateTimeOffset.UtcNow - lastSuccessfulBackup.DateCreated).TotalDays > 7 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private SystemHealthDto GetSystemHealth()
    {
        var hasUpdateAvailable = false; // TODO: check update service
        var startupWizardCompleted = _configurationManager.CommonConfiguration.IsStartupWizardCompleted;

        return new SystemHealthDto
        {
            HasPendingRestart = _applicationHost.HasPendingRestart,
            HasUpdateAvailable = hasUpdateAvailable,
            StartupWizardCompleted = startupWizardCompleted,
            Status = _applicationHost.HasPendingRestart ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private bool IsCompatible(string targetAbi)
    {
        // Simplified - would need proper version comparison
        return true;
    }

    private bool HasUpdateAvailable(MediaBrowser.Common.Plugins.LocalPlugin plugin)
    {
        // Simplified - would check catalog
        return false;
    }

    private HealthStatus CalculateOverallStatus(ServerHealthSummaryDto summary)
    {
        var statuses = new[]
        {
            summary.Storage.Status,
            summary.Tasks.Status,
            summary.Plugins.Status,
            summary.Backup.Status,
            summary.System.Status
        };

        if (statuses.Any(s => s == HealthStatus.Critical)) return HealthStatus.Critical;
        if (statuses.Any(s => s == HealthStatus.Warning)) return HealthStatus.Warning;
        return HealthStatus.Ok;
    }
}

/// <summary>
/// Health status enum.
/// </summary>
public enum HealthStatus
{
    Ok,
    Warning,
    Critical
}

/// <summary>
/// Complete server health summary.
/// </summary>
public class ServerHealthSummaryDto
{
    public DateTimeOffset Timestamp { get; set; }
    public string ServerName { get; set; }
    public string Version { get; set; }
    public bool HasPendingRestart { get; set; }
    public bool IsShuttingDown { get; set; }
    public StorageHealthDto Storage { get; set; }
    public TaskHealthDto Tasks { get; set; }
    public PluginHealthDto Plugins { get; set; }
    public BackupHealthDto Backup { get; set; }
    public SystemHealthDto System { get; set; }
    public HealthStatus OverallStatus { get; set; }
}

/// <summary>
/// Storage health metrics.
/// </summary>
public class StorageHealthDto
{
    public long TotalFreeSpace { get; set; }
    public long TotalUsedSpace { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int HealthyCount { get; set; }
    public string[] CriticalPaths { get; set; } = Array.Empty<string>();
    public string[] WarningPaths { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Scheduled task health metrics.
/// </summary>
public class TaskHealthDto
{
    public int TotalCount { get; set; }
    public int RunningCount { get; set; }
    public int FailedCount { get; set; }
    public int OverdueCount { get; set; }
    public string[] FailedTaskNames { get; set; } = Array.Empty<string>();
    public string[] OverdueTaskNames { get; set; } = Array.Empty<string>();
    public string[] RunningTaskNames { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Plugin health metrics.
/// </summary>
public class PluginHealthDto
{
    public int TotalCount { get; set; }
    public int EnabledCount { get; set; }
    public int DisabledCount { get; set; }
    public int IncompatibleCount { get; set; }
    public int UpdateAvailableCount { get; set; }
    public string[] IncompatibleNames { get; set; } = Array.Empty<string>();
    public string[] UpdateAvailableNames { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Backup health metrics.
/// </summary>
public class BackupHealthDto
{
    public int TotalBackups { get; set; }
    public DateTimeOffset? LastBackupTime { get; set; }
    public DateTimeOffset? LastSuccessfulBackupTime { get; set; }
    public TaskCompletionStatus? LastBackupResult { get; set; }
    public string LastBackupSize { get; set; }
    public HealthStatus Status { get; set; }
}

/// <summary>
/// System health metrics.
/// </summary>
public class SystemHealthDto
{
    public bool HasPendingRestart { get; set; }
    public bool HasUpdateAvailable { get; set; }
    public bool StartupWizardCompleted { get; set; }
    public HealthStatus Status { get; set; }
}