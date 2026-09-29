using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Model.Nebula;

/// <summary>
/// Severity of an operational alert.
/// </summary>
public enum OperationalAlertSeverity
{
    /// <summary>Informational; no action required.</summary>
    Info,

    /// <summary>Should be reviewed soon; not yet critical.</summary>
    Warning,

    /// <summary>Requires prompt operator action.</summary>
    Critical
}

/// <summary>
/// Machine-readable category identifying which rule raised the alert.
/// </summary>
public static class OperationalAlertKind
{
    /// <summary>Upload queue has items with no completed/uploaded progress for longer than the configured threshold.</summary>
    public const string QueueStalled = "queue_stalled";

    /// <summary>No successful backup (local or Supabase) within the configured retention window.</summary>
    public const string BackupOverdue = "backup_overdue";

    /// <summary>A monitored storage path is below the configured free-space threshold.</summary>
    public const string DiskSpaceLow = "disk_space_low";

    /// <summary>The same upload failure stage repeated at or above the configured threshold within the recent window.</summary>
    public const string RepeatedProviderError = "repeated_provider_error";

    /// <summary>The most recent restore attempt (Supabase or local backup) failed.</summary>
    public const string RestoreFailed = "restore_failed";
}

/// <summary>
/// A single operational alert produced by <see cref="OperationalAlertEvaluator"/>.
/// </summary>
public sealed class OperationalAlertDto
{
    /// <summary>Gets or sets the machine-readable alert kind (see <see cref="OperationalAlertKind"/>).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the alert severity.</summary>
    public OperationalAlertSeverity Severity { get; set; }

    /// <summary>Gets or sets a low-cardinality, human-readable summary. Must not contain file names, paths, tokens or other identifying data.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Inputs consumed by <see cref="OperationalAlertEvaluator"/>. All fields are already-aggregated,
/// low-cardinality operational signals; no titles, paths, tokens or user identifiers should be passed in.
/// </summary>
public sealed class OperationalAlertInputs
{
    /// <summary>Gets or sets the current UTC time used to evaluate age-based thresholds. Defaults to <see cref="DateTime.UtcNow"/> when unset by the caller.</summary>
    public DateTime NowUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the timestamp of the oldest pending/queued upload, if any.</summary>
    public DateTime? OldestPendingUploadAtUtc { get; set; }

    /// <summary>Gets or sets the maximum age, in minutes, a pending upload may sit in queue before it is considered stalled.</summary>
    public int QueueStalledThresholdMinutes { get; set; } = 120;

    /// <summary>Gets or sets the timestamp of the last successful backup (local ZIP or Supabase sync), if any.</summary>
    public DateTime? LastSuccessfulBackupUtc { get; set; }

    /// <summary>Gets or sets the maximum age, in days, since the last successful backup before it is considered overdue.</summary>
    public int BackupOverdueThresholdDays { get; set; } = 7;

    /// <summary>Gets or sets the free-space ratio (0.0-1.0) for each monitored storage folder, keyed by a low-cardinality folder role (e.g. "cache", "backup", "transcode").</summary>
    public IReadOnlyDictionary<string, double> StorageFreeRatioByRole { get; set; } = new Dictionary<string, double>();

    /// <summary>Gets or sets the minimum acceptable free-space ratio (0.0-1.0) before a disk-space alert fires.</summary>
    public double DiskSpaceCriticalRatio { get; set; } = 0.10;

    /// <summary>Gets or sets recent upload failure counts grouped by failure stage (see <see cref="NebulaUploadFailureStages"/>).</summary>
    public IReadOnlyList<NebulaUploadFailureStageCountDto> RecentFailuresByStage { get; set; } = Array.Empty<NebulaUploadFailureStageCountDto>();

    /// <summary>Gets or sets the minimum recent failure count for the same stage before it is reported as a repeated provider error.</summary>
    public long RepeatedProviderErrorThreshold { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether the most recent Supabase-to-MongoDB restore attempt failed.</summary>
    public bool LastRestoreFailed { get; set; }

    /// <summary>Gets or sets the message reported by the last failed restore attempt, if any.</summary>
    public string? LastRestoreFailureMessage { get; set; }
}

/// <summary>
/// Pure, dependency-free evaluator that turns already-aggregated operational signals into a list of
/// actionable alerts. No I/O, no logging, no file/database access: callers gather the inputs and this
/// class only decides which named alerts (queue stalled, backup overdue, disk near limit, repeated
/// provider error, restore failure) should fire, keeping the decision testable in isolation.
/// </summary>
public static class OperationalAlertEvaluator
{
    /// <summary>
    /// Evaluates the given inputs and returns the list of alerts that should currently be active.
    /// Deterministic: the same inputs always produce the same alerts, with no hidden clock or randomness
    /// beyond <see cref="OperationalAlertInputs.NowUtc"/>, which callers control explicitly.
    /// </summary>
    /// <param name="inputs">The aggregated operational signals to evaluate.</param>
    /// <returns>A list of alerts, ordered by severity (critical first) and then by kind name.</returns>
    public static IReadOnlyList<OperationalAlertDto> Evaluate(OperationalAlertInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var alerts = new List<OperationalAlertDto>();

        EvaluateQueueStalled(inputs, alerts);
        EvaluateBackupOverdue(inputs, alerts);
        EvaluateDiskSpaceLow(inputs, alerts);
        EvaluateRepeatedProviderError(inputs, alerts);
        EvaluateRestoreFailed(inputs, alerts);

        return alerts
            .OrderBy(a => a.Severity == OperationalAlertSeverity.Critical ? 0 : a.Severity == OperationalAlertSeverity.Warning ? 1 : 2)
            .ThenBy(a => a.Kind, StringComparer.Ordinal)
            .ToList();
    }

    private static void EvaluateQueueStalled(OperationalAlertInputs inputs, List<OperationalAlertDto> alerts)
    {
        if (inputs.OldestPendingUploadAtUtc is not { } oldest)
        {
            return;
        }

        var thresholdMinutes = Math.Max(1, inputs.QueueStalledThresholdMinutes);
        var ageMinutes = (inputs.NowUtc - oldest).TotalMinutes;
        if (ageMinutes < thresholdMinutes)
        {
            return;
        }

        alerts.Add(new OperationalAlertDto
        {
            Kind = OperationalAlertKind.QueueStalled,
            Severity = OperationalAlertSeverity.Warning,
            Message = $"Fila de upload sem progresso há mais de {thresholdMinutes} minutos."
        });
    }

    private static void EvaluateBackupOverdue(OperationalAlertInputs inputs, List<OperationalAlertDto> alerts)
    {
        var thresholdDays = Math.Max(1, inputs.BackupOverdueThresholdDays);

        if (inputs.LastSuccessfulBackupUtc is not { } lastBackup)
        {
            alerts.Add(new OperationalAlertDto
            {
                Kind = OperationalAlertKind.BackupOverdue,
                Severity = OperationalAlertSeverity.Critical,
                Message = "Nenhum backup bem-sucedido registrado."
            });
            return;
        }

        var ageDays = (inputs.NowUtc - lastBackup).TotalDays;
        if (ageDays < thresholdDays)
        {
            return;
        }

        alerts.Add(new OperationalAlertDto
        {
            Kind = OperationalAlertKind.BackupOverdue,
            Severity = OperationalAlertSeverity.Warning,
            Message = $"Último backup bem-sucedido há mais de {thresholdDays} dias."
        });
    }

    private static void EvaluateDiskSpaceLow(OperationalAlertInputs inputs, List<OperationalAlertDto> alerts)
    {
        foreach (var entry in inputs.StorageFreeRatioByRole)
        {
            if (entry.Value > inputs.DiskSpaceCriticalRatio)
            {
                continue;
            }

            alerts.Add(new OperationalAlertDto
            {
                Kind = OperationalAlertKind.DiskSpaceLow,
                Severity = OperationalAlertSeverity.Critical,
                Message = $"Espaço livre abaixo do limite configurado em '{entry.Key}' ({entry.Value:P0})."
            });
        }
    }

    private static void EvaluateRepeatedProviderError(OperationalAlertInputs inputs, List<OperationalAlertDto> alerts)
    {
        foreach (var stage in inputs.RecentFailuresByStage)
        {
            if (stage.Count < inputs.RepeatedProviderErrorThreshold)
            {
                continue;
            }

            alerts.Add(new OperationalAlertDto
            {
                Kind = OperationalAlertKind.RepeatedProviderError,
                Severity = OperationalAlertSeverity.Warning,
                Message = $"Falha repetida na etapa '{stage.Stage}' ({stage.Count} ocorrências recentes)."
            });
        }
    }

    private static void EvaluateRestoreFailed(OperationalAlertInputs inputs, List<OperationalAlertDto> alerts)
    {
        if (!inputs.LastRestoreFailed)
        {
            return;
        }

        var suffix = string.IsNullOrWhiteSpace(inputs.LastRestoreFailureMessage)
            ? string.Empty
            : $": {inputs.LastRestoreFailureMessage}";

        alerts.Add(new OperationalAlertDto
        {
            Kind = OperationalAlertKind.RestoreFailed,
            Severity = OperationalAlertSeverity.Critical,
            Message = $"A última restauração falhou{suffix}"
        });
    }
}
