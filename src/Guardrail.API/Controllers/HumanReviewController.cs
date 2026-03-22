using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.API.Models;

namespace Guardrail.API.Controllers;

/// <summary>
/// Human review queue management. When the automated guardrail pipeline cannot make
/// a confident decision (e.g., borderline risk score or policy requires human approval),
/// a <see cref="HumanReviewCase"/> is created and routed to a human reviewer.
/// </summary>
[ApiController]
[Route("api/human-review")]
[Authorize("AdminPolicy")]
[Produces("application/json")]
public sealed class HumanReviewController : ControllerBase
{
    private readonly IHumanReviewRepository _humanReviewRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<HumanReviewController> _logger;

    public HumanReviewController(
        IHumanReviewRepository humanReviewRepository,
        IAuditRepository auditRepository,
        ILogger<HumanReviewController> logger)
    {
        _humanReviewRepository = humanReviewRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    // ── GET /api/human-review/cases ───────────────────────────────────────────

    /// <summary>
    /// List human review cases, with optional filters.
    /// </summary>
    /// <remarks>
    /// Returns cases ordered by creation time descending (newest first).
    /// Use <c>status</c> to filter by review status (Pending, InReview, Approved, Rejected, Escalated, Closed).
    /// Use <c>assignedTo</c> to list cases assigned to a specific reviewer.
    /// </remarks>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="applicationId">Optional application filter.</param>
    /// <param name="status">Optional review status filter.</param>
    /// <param name="assignedTo">Optional reviewer identity filter.</param>
    /// <param name="riskLevel">Optional risk level filter.</param>
    /// <param name="from">Filter cases created at or after this UTC timestamp.</param>
    /// <param name="to">Filter cases created before this UTC timestamp.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Results per page (default: 25, max: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("cases")]
    [ProducesResponseType(typeof(ListHumanReviewCasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListHumanReviewCasesResponse>> ListCases(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? applicationId,
        [FromQuery] ReviewStatus? status,
        [FromQuery] string? assignedTo,
        [FromQuery] RiskLevel? riskLevel,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new { error = "Page must be >= 1." });

        pageSize = Math.Clamp(pageSize, 1, 100);

        _logger.LogInformation(
            "ListCases: tenantId={TenantId}, status={Status}, page={Page}",
            tenantId, status, page);

        var result = await _humanReviewRepository.SearchAsync(
            new HumanReviewSearchCriteria
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Status = status,
                AssignedTo = assignedTo,
                RiskLevel = riskLevel,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return Ok(new ListHumanReviewCasesResponse
        {
            Items = result.Items.Select(x => new HumanReviewCaseSummary
            {
                Id = x.Id,
                TenantId = x.TenantId,
                ApplicationId = x.ApplicationId,
                ExecutionId = x.ExecutionId,
                Status = x.Status,
                RiskLevel = x.RiskLevel,
                AutomatedDecision = x.Decision,
                AssignedTo = x.AssignedTo,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt
            }).ToArray(),
            Total = result.Total,
            Page = page,
            PageSize = pageSize
        });
    }

    // ── GET /api/human-review/cases/{id} ──────────────────────────────────────

    /// <summary>
    /// Get a single human review case by ID.
    /// </summary>
    /// <param name="id">Review case GUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("cases/{id:guid}")]
    [ProducesResponseType(typeof(HumanReviewCase), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HumanReviewCase>> GetCase(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetCase: id={CaseId}", id);

        var reviewCase = await _humanReviewRepository.GetByIdAsync(id, cancellationToken);

        if (reviewCase is null)
            return NotFound(new { error = $"Review case '{id}' not found." });

        return Ok(reviewCase);
    }

    // ── POST /api/human-review/cases/{id}/assign ──────────────────────────────

    /// <summary>
    /// Assign a review case to a specific reviewer.
    /// </summary>
    /// <remarks>
    /// Transitions the case from <c>Pending</c> to <c>InReview</c> status and
    /// records the assigned reviewer's identity.
    /// </remarks>
    /// <param name="id">Review case GUID.</param>
    /// <param name="request">Assignment details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("cases/{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCase(
        [FromRoute] Guid id,
        [FromBody] AssignReviewCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReviewerIdentity))
            return BadRequest(new { error = "ReviewerIdentity is required." });

        _logger.LogInformation(
            "AssignCase: id={CaseId}, reviewer={Reviewer}",
            id, request.ReviewerIdentity);

        var reviewCase = await _humanReviewRepository.GetByIdAsync(id, cancellationToken);
        if (reviewCase is null)
            return NotFound(new { error = $"Review case '{id}' not found." });

        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        reviewCase.Assign(request.ReviewerIdentity, actor);
        await _humanReviewRepository.UpdateAsync(reviewCase, cancellationToken);

        return Ok(new
        {
            caseId = id,
            status = ReviewStatus.InReview.ToString(),
            assignedTo = request.ReviewerIdentity,
            message = "Case assigned successfully."
        });
    }

    // ── POST /api/human-review/cases/{id}/complete ────────────────────────────

    /// <summary>
    /// Complete a human review with a final enforcement decision.
    /// </summary>
    /// <remarks>
    /// The reviewer's decision overrides the automated guardrail decision for this case.
    /// Valid decisions: <c>Allow</c>, <c>AllowWithConstraints</c>, <c>Redact</c>, <c>Block</c>.
    ///
    /// On completion, the associated <see cref="GuardrailExecution"/> is updated with
    /// the reviewer's decision, and downstream systems are notified via the audit log.
    /// </remarks>
    /// <param name="id">Review case GUID.</param>
    /// <param name="request">Completion details including the final decision and optional notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("cases/{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteCase(
        [FromRoute] Guid id,
        [FromBody] CompleteReviewCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FinalDecision))
            return BadRequest(new { error = "FinalDecision is required." });

        if (!Enum.TryParse<DecisionType>(request.FinalDecision, ignoreCase: true, out var decisionType))
        {
            return BadRequest(new
            {
                error = $"Invalid FinalDecision '{request.FinalDecision}'. " +
                        $"Valid values: {string.Join(", ", Enum.GetNames<DecisionType>())}."
            });
        }

        var reviewerIdentity = User.FindFirst("sub")?.Value
            ?? User.Identity?.Name
            ?? "system";

        _logger.LogInformation(
            "CompleteCase: id={CaseId}, decision={Decision}, reviewer={Reviewer}",
            id, decisionType, reviewerIdentity);

        var reviewCase = await _humanReviewRepository.GetByIdAsync(id, cancellationToken);
        if (reviewCase is null)
            return NotFound(new { error = $"Review case '{id}' not found." });

        reviewCase.Complete(decisionType, reviewerIdentity, request.Notes);
        await _humanReviewRepository.UpdateAsync(reviewCase, cancellationToken);

        var execution = await _auditRepository.GetExecutionByIdAsync(reviewCase.ExecutionId, cancellationToken);
        if (execution is not null)
        {
            execution.OverrideDecision(decisionType, reviewerIdentity);
            await _auditRepository.UpdateExecutionAsync(execution, cancellationToken);
        }

        return Ok(new
        {
            caseId = id,
            finalDecision = decisionType.ToString(),
            reviewedBy = reviewerIdentity,
            reviewedAt = DateTimeOffset.UtcNow,
            status = decisionType is DecisionType.Block or DecisionType.Redact
                ? ReviewStatus.Rejected.ToString()
                : ReviewStatus.Approved.ToString(),
            message = "Review case completed successfully."
        });
    }

    // ── POST /api/human-review/cases/{id}/escalate ────────────────────────────

    /// <summary>
    /// Escalate a review case to a higher-tier reviewer.
    /// </summary>
    /// <param name="id">Review case GUID.</param>
    /// <param name="request">Escalation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("cases/{id:guid}/escalate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateCase(
        [FromRoute] Guid id,
        [FromBody] EscalateReviewCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Escalation reason is required." });

        _logger.LogInformation("EscalateCase: id={CaseId}, reason={Reason}", id, request.Reason);

        var reviewCase = await _humanReviewRepository.GetByIdAsync(id, cancellationToken);
        if (reviewCase is null)
            return NotFound(new { error = $"Review case '{id}' not found." });

        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        reviewCase.Escalate(actor, request.Reason);
        await _humanReviewRepository.UpdateAsync(reviewCase, cancellationToken);

        return Ok(new
        {
            caseId = id,
            status = ReviewStatus.Escalated.ToString(),
            reason = request.Reason,
            message = "Review case escalated successfully."
        });
    }
}

// ── Request / response DTOs ───────────────────────────────────────────────────

/// <summary>Request body for POST /api/human-review/cases/{id}/escalate.</summary>
public sealed class EscalateReviewCaseRequest
{
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Paginated list of human review case summaries.</summary>
public sealed class ListHumanReviewCasesResponse
{
    public IReadOnlyList<HumanReviewCaseSummary> Items { get; set; } = Array.Empty<HumanReviewCaseSummary>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Summary row for listing human review cases.</summary>
public sealed class HumanReviewCaseSummary
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ExecutionId { get; set; }
    public ReviewStatus Status { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public DecisionType AutomatedDecision { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
