using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Application.Queries.GetAudit;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;

namespace Guardrail.API.Controllers;

/// <summary>
/// Read-only access to the immutable guardrail audit log.
/// Supports filtering, pagination, and single-event lookup.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("ReadPolicy")]
[Produces("application/json")]
public sealed class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IMediator mediator,
        IAuditRepository auditRepository,
        ILogger<AuditController> logger)
    {
        _mediator = mediator;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    // ── GET /api/audit ────────────────────────────────────────────────────────

    /// <summary>
    /// Search and page through audit events.
    /// </summary>
    /// <remarks>
    /// All filter parameters are optional. Combine them to narrow results.
    /// Results are ordered by event occurrence time descending (newest first).
    ///
    /// Page size is capped at 200. Use the <c>totalPages</c> field in the response
    /// to determine how many additional pages are available.
    /// </remarks>
    /// <param name="tenantId">Filter by tenant GUID.</param>
    /// <param name="applicationId">Filter by application GUID.</param>
    /// <param name="userId">Filter by end-user subject identifier.</param>
    /// <param name="correlationId">Filter by correlation ID (exact match).</param>
    /// <param name="from">Return events occurring at or after this UTC timestamp.</param>
    /// <param name="to">Return events occurring before this UTC timestamp.</param>
    /// <param name="eventType">Filter by machine-readable event type (e.g., "InputBlocked").</param>
    /// <param name="isIncident">When true, return only incident-flagged events.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Results per page (default: 50, max: 200).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(GetAuditEventsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetAuditEventsResult>> GetAuditEvents(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? applicationId,
        [FromQuery] string? userId,
        [FromQuery] string? correlationId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? eventType,
        [FromQuery] bool? isIncident,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new { error = "Page number must be greater than or equal to 1." });

        if (pageSize < 1 || pageSize > 200)
            return BadRequest(new { error = "Page size must be between 1 and 200." });

        if (from.HasValue && to.HasValue && from > to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        var query = new GetAuditEventsQuery
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserId = userId,
            CorrelationId = correlationId,
            From = from,
            To = to,
            EventType = eventType,
            IsIncident = isIncident,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ── GET /api/audit/{id} ───────────────────────────────────────────────────

    /// <summary>
    /// Retrieve a single audit event by its unique identifier.
    /// </summary>
    /// <param name="id">Audit event GUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit event, or 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditEvent), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuditEvent>> GetAuditEventById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Audit event ID must not be empty." });

        _logger.LogInformation("GetAuditEventById: id={AuditEventId}", id);
        var auditEvent = await _auditRepository.GetByIdAsync(id, cancellationToken);

        if (auditEvent is null)
            return NotFound(new { error = $"Audit event '{id}' not found." });

        return Ok(auditEvent);
    }

    // ── GET /api/audit/incidents ───────────────────────────────────────────────

    /// <summary>
    /// List all audit events classified as security or compliance incidents.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size (max 200).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("incidents")]
    [Authorize("AdminPolicy")]
    [ProducesResponseType(typeof(GetAuditEventsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetAuditEventsResult>> GetIncidents(
        [FromQuery] Guid? tenantId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditEventsQuery
        {
            TenantId = tenantId,
            From = from,
            To = to,
            IsIncident = true,
            Page = page,
            PageSize = Math.Min(pageSize, 200)
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
