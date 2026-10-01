using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.CatalogAudit;
using MediaBrowser.Model.CatalogAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Catalog Audit Controller for deterministic consistency reporting, provenance preservation, and safe remediation.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Route("Catalog/Audit")]
public class CatalogAuditController : BaseMulletaFlixApiController
{
    private readonly ICatalogAuditService _auditService;

    public CatalogAuditController(ICatalogAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Executes a read-only deterministic catalog audit report.
    /// </summary>
    /// <param name="category">Optional inconsistency category filter.</param>
    /// <param name="itemType">Optional item type filter (e.g. Movie, Series, Book).</param>
    /// <param name="minConfidence">Minimum confidence threshold (0.0 to 1.0).</param>
    /// <param name="severity">Optional severity level filter (Notice, Warning, Error).</param>
    /// <returns>Deterministic audit report.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<CatalogAuditReport> GetAudit(
        [FromQuery] string? category,
        [FromQuery] string? itemType,
        [FromQuery] double? minConfidence,
        [FromQuery] string? severity)
    {
        var filter = new CatalogAuditFilter
        {
            Category = category,
            ItemType = itemType,
            MinConfidence = minConfidence,
            Severity = severity
        };

        var report = _auditService.RunAudit(filter);
        return Ok(report);
    }

    /// <summary>
    /// Applies an approved deterministic fix to a catalog item.
    /// </summary>
    /// <param name="request">Fix request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical record of the applied fix.</returns>
    [HttpPost("Fix")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogAuditHistoryEntry>> ApplyFix(
        [FromBody] CatalogAuditFixRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var username = User?.Identity?.Name ?? "Admin";
            var result = await _auditService.ApplyFixAsync(request, username, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rolls back a previously applied fix using the history record.
    /// </summary>
    /// <param name="request">Rollback request containing HistoryEntryId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether rollback succeeded.</returns>
    [HttpPost("Rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogAuditRollbackResponse>> RollbackFix(
        [FromBody] CatalogAuditRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var success = await _auditService.RollbackFixAsync(request, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            return NotFound(new { error = "History entry or target item not found for rollback." });
        }

        return Ok(new CatalogAuditRollbackResponse
        {
            Success = true,
            HistoryEntryId = request.HistoryEntryId
        });
    }

    /// <summary>
    /// Retrieves all recorded catalog remediation history entries.
    /// </summary>
    /// <returns>List of history entries.</returns>
    [HttpGet("History")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CatalogAuditHistoryEntry>> GetHistory()
    {
        return Ok(_auditService.GetHistory());
    }

    /// <summary>
    /// Retrieves rights, terms of service, and rate-limiting rules for metadata providers.
    /// </summary>
    /// <returns>List of provider terms information.</returns>
    [HttpGet("ProviderTerms")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ProviderTermsInfo>> GetProviderTerms()
    {
        return Ok(_auditService.GetProviderTerms());
    }
}
