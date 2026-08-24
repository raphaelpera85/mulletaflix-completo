using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Querying;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Action Log Controller - provides audit trail for administrative actions.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[Tags("ActionLog")]
[ApiController]
[Route("ActionLog")]
public class ActionLogController : BaseMulletaFlixApiController
{
    private readonly IDbContextFactory<UsersDbContext> _dbContextFactory;
    private readonly ILogger<ActionLogController> _logger;

    public ActionLogController(
        IDbContextFactory<UsersDbContext> dbContextFactory,
        ILogger<ActionLogController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets action log entries with filtering and pagination.
    /// </summary>
    /// <param name="startIndex">The record index to start at.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <param name="minDate">The minimum date.</param>
    /// <param name="maxDate">The maximum date.</param>
    /// <param name="actionType">Filter by action type.</param>
    /// <param name="entityType">Filter by entity type.</param>
    /// <param name="userId">Filter by user id.</param>
    /// <param name="username">Filter by username.</param>
    /// <param name="isSuccess">Filter by success status.</param>
    /// <param name="category">Filter by category.</param>
    /// <param name="sortBy">Sort field.</param>
    /// <param name="sortOrder">Sort order.</param>
    /// <response code="200">Action log entries returned.</response>
    [HttpGet("Entries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<QueryResult<ActionLogDto>>> GetEntries(
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] DateTimeOffset? minDate,
        [FromQuery] DateTimeOffset? maxDate,
        [FromQuery] string? actionType,
        [FromQuery] string? entityType,
        [FromQuery] Guid? userId,
        [FromQuery] string? username,
        [FromQuery] bool? isSuccess,
        [FromQuery] string? category,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var query = context.ActionLogs.AsQueryable();

        if (minDate.HasValue)
            query = query.Where(a => a.DateCreated >= minDate.Value);

        if (maxDate.HasValue)
            query = query.Where(a => a.DateCreated <= maxDate.Value);

        if (!string.IsNullOrEmpty(actionType))
            query = query.Where(a => a.ActionType == actionType);

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (!string.IsNullOrEmpty(username))
            query = query.Where(a => a.Username.Contains(username));

        if (isSuccess.HasValue)
            query = query.Where(a => a.IsSuccess == isSuccess.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(a => a.Category.ToString() == category);

        // Sorting
        var sortByField = sortBy ?? "DateCreated";
        var isDescending = sortOrder?.Equals("Descending", StringComparison.OrdinalIgnoreCase) ?? true;

        query = sortByField switch
        {
            "ActionType" => isDescending ? query.OrderByDescending(a => a.ActionType) : query.OrderBy(a => a.ActionType),
            "EntityType" => isDescending ? query.OrderByDescending(a => a.EntityType) : query.OrderBy(a => a.EntityType),
            "Username" => isDescending ? query.OrderByDescending(a => a.Username) : query.OrderBy(a => a.Username),
            "IsSuccess" => isDescending ? query.OrderByDescending(a => a.IsSuccess) : query.OrderBy(a => a.IsSuccess),
            _ => isDescending ? query.OrderByDescending(a => a.DateCreated) : query.OrderBy(a => a.DateCreated)
        };

        var totalCount = await query.CountAsync().ConfigureAwait(false);

        var skip = startIndex ?? 0;
        var take = limit ?? 50;

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(a => new ActionLogDto
            {
                Id = a.Id,
                ActionType = a.ActionType,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                UserId = a.UserId.ToString(),
                Username = a.Username,
                DateCreated = a.DateCreated.ToString("O"),
                Details = a.Details,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent,
                IsSuccess = a.IsSuccess,
                ErrorMessage = a.ErrorMessage,
                Category = a.Category.ToString()
            })
            .ToListAsync().ConfigureAwait(false);

        return Ok(new QueryResult<ActionLogDto>
        {
            Items = items,
            TotalRecordCount = totalCount,
            StartIndex = skip
        });
    }

    /// <summary>
    /// Gets action log statistics for dashboard.
    /// </summary>
    /// <response code="200">Action log statistics returned.</response>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionLogStatsDto>> GetStats()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var totalCount = await context.ActionLogs.CountAsync().ConfigureAwait(false);
        var last24HoursCount = await context.ActionLogs.CountAsync(a => a.DateCreated >= last24Hours).ConfigureAwait(false);
        var last7DaysCount = await context.ActionLogs.CountAsync(a => a.DateCreated >= last7Days).ConfigureAwait(false);
        var last30DaysCount = await context.ActionLogs.CountAsync(a => a.DateCreated >= last30Days).ConfigureAwait(false);
        var failedCount = await context.ActionLogs.CountAsync(a => !a.IsSuccess).ConfigureAwait(false);
        var failed24Hours = await context.ActionLogs.CountAsync(a => !a.IsSuccess && a.DateCreated >= last24Hours).ConfigureAwait(false);

        var topActions = await context.ActionLogs
            .GroupBy(a => a.ActionType)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync().ConfigureAwait(false);

        var topUsers = await context.ActionLogs
            .GroupBy(a => a.Username)
            .Select(g => new { User = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync().ConfigureAwait(false);

        var topEntities = await context.ActionLogs
            .GroupBy(a => a.EntityType)
            .Select(g => new { Entity = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync().ConfigureAwait(false);

        return Ok(new ActionLogStatsDto
        {
            TotalCount = totalCount,
            Last24Hours = last24HoursCount,
            Last7Days = last7DaysCount,
            Last30Days = last30DaysCount,
            FailedTotal = failedCount,
            FailedLast24Hours = failed24Hours,
            TopActions = topActions.Select(x => new ActionStatDto { Name = x.Action, Count = x.Count }).ToArray(),
            TopUsers = topUsers.Select(x => new ActionStatDto { Name = x.User, Count = x.Count }).ToArray(),
            TopEntities = topEntities.Select(x => new ActionStatDto { Name = x.Entity, Count = x.Count }).ToArray()
        });
    }
}

/// <summary>
/// DTO for action log entry.
/// </summary>
public class ActionLogDto
{
    public long Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DateCreated { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Statistics DTO for action logs.
/// </summary>
public class ActionLogStatsDto
{
    public int TotalCount { get; set; }
    public int Last24Hours { get; set; }
    public int Last7Days { get; set; }
    public int Last30Days { get; set; }
    public int FailedTotal { get; set; }
    public int FailedLast24Hours { get; set; }
    public ActionStatDto[] TopActions { get; set; } = Array.Empty<ActionStatDto>();
    public ActionStatDto[] TopUsers { get; set; } = Array.Empty<ActionStatDto>();
    public ActionStatDto[] TopEntities { get; set; } = Array.Empty<ActionStatDto>();
}

/// <summary>
/// Simple stat entry for dashboard.
/// </summary>
public class ActionStatDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}