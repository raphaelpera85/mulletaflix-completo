using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.CatalogAudit;

namespace MediaBrowser.Controller.CatalogAudit;

/// <summary>
/// Service interface for deterministic catalog validation, inconsistency detection, and safe remediation.
/// </summary>
public interface ICatalogAuditService
{
    /// <summary>
    /// Executes a read-only deterministic audit across library items.
    /// </summary>
    CatalogAuditReport RunAudit(CatalogAuditFilter? filter = null);

    /// <summary>
    /// Analyzes a single library item for deterministic inconsistencies without mutating state.
    /// </summary>
    IReadOnlyList<CatalogInconsistency> AnalyzeItem(BaseItem item);

    /// <summary>
    /// Applies an approved fix to an item with state snapshotting for rollback capability.
    /// </summary>
    Task<CatalogAuditHistoryEntry> ApplyFixAsync(CatalogAuditFixRequest request, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a previously applied fix to restore original metadata state.
    /// </summary>
    Task<bool> RollbackFixAsync(CatalogAuditRollbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the audit remediation history.
    /// </summary>
    IReadOnlyList<CatalogAuditHistoryEntry> GetHistory();

    /// <summary>
    /// Returns the structured rights, terms, and rate limit guidelines for external metadata providers.
    /// </summary>
    IReadOnlyList<ProviderTermsInfo> GetProviderTerms();
}
