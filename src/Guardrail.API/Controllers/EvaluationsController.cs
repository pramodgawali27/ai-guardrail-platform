using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.API.Controllers;

/// <summary>
/// Manages offline policy evaluation runs — batch jobs that test a policy profile
/// against a dataset of known-good and known-bad prompts. Used for policy validation,
/// regression testing, and red-teaming.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("AdminPolicy")]
[Produces("application/json")]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly IEvaluationRepository _evaluationRepository;
    private readonly ILogger<EvaluationsController> _logger;

    public EvaluationsController(
        IEvaluationService evaluationService,
        IEvaluationRepository evaluationRepository,
        ILogger<EvaluationsController> logger)
    {
        _evaluationService = evaluationService;
        _evaluationRepository = evaluationRepository;
        _logger = logger;
    }

    // ── POST /api/evaluations/run ─────────────────────────────────────────────

    /// <summary>
    /// Trigger a new policy evaluation run against a dataset.
    /// </summary>
    /// <remarks>
    /// Creates an <see cref="EvaluationRun"/> and enqueues it for background processing.
    /// Poll <c>GET /api/evaluations/{id}</c> to monitor status and retrieve results once
    /// the run transitions to <c>Completed</c>.
    ///
    /// Evaluation runs are tenant-scoped and require the <c>guardrail-admin</c> role.
    /// </remarks>
    /// <param name="request">Evaluation run configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with the new run ID and a status polling URL.</returns>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerRun(
        [FromBody] TriggerEvaluationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
            return BadRequest(new { error = "TenantId is required." });

        if (request.DatasetId == Guid.Empty)
            return BadRequest(new { error = "DatasetId is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Evaluation run name is required." });

        _logger.LogInformation(
            "TriggerRun: tenantId={TenantId}, datasetId={DatasetId}, policyProfileId={PolicyProfileId}",
            request.TenantId, request.DatasetId, request.PolicyProfileId);

        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        var run = await _evaluationService.QueueRunAsync(
            new QueueEvaluationRunRequest
            {
                TenantId = request.TenantId,
                Name = request.Name,
                Description = request.Description,
                DatasetId = request.DatasetId,
                PolicyProfileId = request.PolicyProfileId,
                RequestedBy = actor
            },
            cancellationToken);

        return AcceptedAtAction(
            nameof(GetRunResult),
            new { id = run.Id },
            new
            {
                runId = run.Id,
                status = EvaluationStatus.Pending.ToString(),
                statusUrl = Url.Action(nameof(GetRunResult), new { id = run.Id }),
                message = "Evaluation run enqueued. Poll the statusUrl for updates."
            });
    }

    // ── GET /api/evaluations/{id} ─────────────────────────────────────────────

    /// <summary>
    /// Get the current status and results of an evaluation run.
    /// </summary>
    /// <remarks>
    /// Poll this endpoint after calling <c>POST /api/evaluations/run</c>.
    /// When <c>status</c> is <c>Completed</c>, the response includes full statistics:
    /// pass rate, false-positive rate, false-negative rate, and average latency.
    /// </remarks>
    /// <param name="id">Evaluation run GUID returned by the trigger endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation run with its current status and result statistics.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationRun), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvaluationRun>> GetRunResult(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Run ID must not be empty." });

        _logger.LogInformation("GetRunResult: id={RunId}", id);

        _logger.LogInformation("GetRunResult: id={RunId}", id);
        var run = await _evaluationRepository.GetRunByIdAsync(id, cancellationToken);

        if (run is null)
            return NotFound(new { error = $"Evaluation run '{id}' not found." });

        return Ok(run);
    }

    // ── GET /api/evaluations ──────────────────────────────────────────────────

    /// <summary>
    /// List evaluation runs for a tenant, optionally filtered by status.
    /// </summary>
    /// <param name="tenantId">Tenant GUID filter (required).</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Results per page (max 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ListEvaluationRunsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListEvaluationRunsResponse>> ListRuns(
        [FromQuery] Guid tenantId,
        [FromQuery] EvaluationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "TenantId is required." });

        var runs = await _evaluationRepository.GetRunsForTenantAsync(tenantId, cancellationToken);
        var filteredRuns = status.HasValue
            ? runs.Where(x => x.Status == status.Value)
            : runs;
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var items = filteredRuns
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new EvaluationRunSummary
            {
                Id = x.Id,
                Name = x.Name,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt,
                PassRate = x.PassRate
            })
            .ToArray();

        return Ok(new ListEvaluationRunsResponse
        {
            Items = items,
            Total = filteredRuns.Count(),
            Page = page,
            PageSize = normalizedPageSize
        });
    }
}

// ── Request / response DTOs ───────────────────────────────────────────────────

/// <summary>Request body for POST /api/evaluations/run.</summary>
public sealed class TriggerEvaluationRunRequest
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid DatasetId { get; init; }
    public Guid? PolicyProfileId { get; init; }
}

/// <summary>Paginated list of evaluation run summaries.</summary>
public sealed class ListEvaluationRunsResponse
{
    public IReadOnlyList<EvaluationRunSummary> Items { get; set; } = Array.Empty<EvaluationRunSummary>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Summary row for listing evaluation runs.</summary>
public sealed class EvaluationRunSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EvaluationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double PassRate { get; set; }
}
