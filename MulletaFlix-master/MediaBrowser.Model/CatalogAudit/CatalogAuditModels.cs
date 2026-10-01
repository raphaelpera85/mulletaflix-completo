using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.CatalogAudit;

/// <summary>
/// Categories of deterministic catalog inconsistencies.
/// </summary>
public enum CatalogInconsistencyCategory
{
    YearMismatch,
    TitleMismatch,
    MissingProviderId,
    MissingPrimaryImage,
    MissingBackdrop,
    LockedFieldConflict,
    MediaTypeMismatch
}

/// <summary>
/// Severity level of detected catalog inconsistency.
/// </summary>
public enum InconsistencySeverity
{
    Notice,
    Warning,
    Error
}

/// <summary>
/// Details of a single detected catalog inconsistency.
/// </summary>
public class CatalogInconsistency
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public CatalogInconsistencyCategory Category { get; set; }

    public InconsistencySeverity Severity { get; set; }

    public string Field { get; set; } = string.Empty;

    public string? CurrentValue { get; set; }

    public string? ExpectedValue { get; set; }

    public string Evidence { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Filter options for catalog audit execution.
/// </summary>
public class CatalogAuditFilter
{
    public string? Category { get; set; }

    public string? ItemType { get; set; }

    public double? MinConfidence { get; set; }

    public string? Severity { get; set; }
}

/// <summary>
/// Summary report of catalog inconsistencies found.
/// </summary>
public class CatalogAuditReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public int TotalItemsScanned { get; set; }

    public int InconsistenciesCount => Inconsistencies.Count;

    public IReadOnlyList<CatalogInconsistency> Inconsistencies { get; set; } = Array.Empty<CatalogInconsistency>();
}

/// <summary>
/// Request to apply an approved deterministic fix to a catalog item.
/// </summary>
public class CatalogAuditFixRequest
{
    public Guid ItemId { get; set; }

    public string Field { get; set; } = string.Empty;

    public string ApprovedValue { get; set; } = string.Empty;

    public bool PreserveLockedFields { get; set; } = true;
}

/// <summary>
/// Historical record of an applied catalog fix for safe rollback.
/// </summary>
public class CatalogAuditHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ItemId { get; set; }

    public string Field { get; set; } = string.Empty;

    public string? PreviousValue { get; set; }

    public string? AppliedValue { get; set; }

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public string AppliedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request to rollback a previously applied fix.
/// </summary>
public class CatalogAuditRollbackRequest
{
    public Guid HistoryEntryId { get; set; }
}

/// <summary>
/// Response for a rollback operation.
/// </summary>
public class CatalogAuditRollbackResponse
{
    public bool Success { get; set; }

    public Guid HistoryEntryId { get; set; }
}

/// <summary>
/// Metadata and usage terms for an external metadata provider.
/// </summary>
public class ProviderTermsInfo
{
    public string ProviderName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string SupportedIdentifiers { get; set; } = string.Empty;

    public string RateLimits { get; set; } = string.Empty;

    public string AttributionRequired { get; set; } = string.Empty;

    public string TermsSummary { get; set; } = string.Empty;

    public bool AllowsCommercialUse { get; set; }

    public bool RequiresApiKey { get; set; }
}
