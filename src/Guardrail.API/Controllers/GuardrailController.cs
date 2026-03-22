using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Application.Commands.EvaluateInput;
using Guardrail.Application.Commands.EvaluateOutput;
using Guardrail.Application.Commands.EvaluateFull;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Core.Abstractions;
using Guardrail.API.Models;

namespace Guardrail.API.Controllers;

/// <summary>
/// Core guardrail evaluation endpoints. Every AI interaction should pass through
/// at least one of these endpoints before reaching the model (evaluate-input) and
/// before the response is returned to the end user (evaluate-output).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("EvaluatePolicy")]
[Produces("application/json")]
public sealed class GuardrailController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GuardrailController> _logger;

    public GuardrailController(IMediator mediator, ILogger<GuardrailController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // ── POST /api/guardrail/evaluate-input ────────────────────────────────────

    /// <summary>
    /// Evaluate AI model input before execution.
    /// </summary>
    /// <remarks>
    /// Runs the full input guardrail pipeline: policy resolution, prompt-injection detection
    /// (Prompt Shield), content-safety scanning (Azure Content Safety), context-firewall
    /// (data-source access control), and tool-firewall (tool allowlist/denylist enforcement).
    ///
    /// If the request is blocked or escalated, the caller MUST NOT invoke the AI model.
    /// </remarks>
    /// <param name="request">Input evaluation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guardrail evaluation result including the enforcement decision and risk score.</returns>
    [HttpPost("evaluate-input")]
    [ProducesResponseType(typeof(GuardrailEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GuardrailEvaluationResult>> EvaluateInput(
        [FromBody] EvaluateInputApiRequest request,
        CancellationToken cancellationToken)
    {
        var tenantContext = ExtractTenantContext(request.CorrelationId);

        var command = new EvaluateInputCommand
        {
            TenantContext = tenantContext,
            UserPrompt = request.UserPrompt,
            SystemPrompt = request.SystemPrompt,
            DataSources = request.DataSources?
                .Select(s => new SourceDescriptor
                {
                    SourceId = s.SourceId,
                    SourceType = s.SourceType,
                    TenantId = s.TenantId,
                    TrustLevel = s.TrustLevel,
                    Uri = s.Uri,
                    Metadata = s.Metadata ?? new()
                }).ToList() ?? new(),
            RequestedTools = request.RequestedTools?
                .Select(t => new ToolCallDescriptor
                {
                    ToolName = t.ToolName,
                    Parameters = t.Parameters?
                        .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                        ?? new()
                }).ToList() ?? new(),
            Metadata = request.Metadata ?? new()
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // ── POST /api/guardrail/evaluate-output ───────────────────────────────────

    /// <summary>
    /// Evaluate AI model output before returning it to the end user.
    /// </summary>
    /// <remarks>
    /// Runs the output guardrail pipeline: PII/PHI redaction, output schema validation,
    /// grounding/hallucination detection, and a final content-safety scan of the response.
    ///
    /// Use the <c>InputExecutionId</c> returned by a prior evaluate-input call to link
    /// input and output audit records for end-to-end traceability.
    /// </remarks>
    /// <param name="request">Output evaluation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guardrail evaluation result; <c>RedactedOutput</c> contains the safe-to-return text.</returns>
    [HttpPost("evaluate-output")]
    [ProducesResponseType(typeof(GuardrailEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GuardrailEvaluationResult>> EvaluateOutput(
        [FromBody] EvaluateOutputApiRequest request,
        CancellationToken cancellationToken)
    {
        var tenantContext = ExtractTenantContext(request.CorrelationId);

        var command = new EvaluateOutputCommand
        {
            TenantContext = tenantContext,
            InputExecutionId = request.InputExecutionId,
            ModelOutput = request.ModelOutput,
            OutputSchemaJson = request.OutputSchemaJson,
            Metadata = request.Metadata ?? new()
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // ── POST /api/guardrail/evaluate-full ─────────────────────────────────────

    /// <summary>
    /// Evaluate both AI model input and output in a single atomic call.
    /// </summary>
    /// <remarks>
    /// Convenience endpoint for clients that have both the prompt and the response available
    /// at the same time (e.g., when wrapping a synchronous AI call). Internally runs the
    /// input pipeline first; if blocked, output evaluation is skipped.
    /// </remarks>
    /// <param name="request">Full evaluation payload containing both prompt and model response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guardrail evaluation result for the combined pipeline run.</returns>
    [HttpPost("evaluate-full")]
    [ProducesResponseType(typeof(GuardrailEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GuardrailEvaluationResult>> EvaluateFull(
        [FromBody] EvaluateFullApiRequest request,
        CancellationToken cancellationToken)
    {
        var tenantContext = ExtractTenantContext(request.CorrelationId);

        var command = new EvaluateFullCommand
        {
            TenantContext = tenantContext,
            UserPrompt = request.UserPrompt,
            SystemPrompt = request.SystemPrompt,
            ModelOutput = request.ModelOutput,
            DataSources = request.DataSources?
                .Select(s => new SourceDescriptor
                {
                    SourceId = s.SourceId,
                    SourceType = s.SourceType,
                    TenantId = s.TenantId,
                    TrustLevel = s.TrustLevel,
                    Uri = s.Uri,
                    Metadata = s.Metadata ?? new()
                }).ToList() ?? new(),
            RequestedTools = request.RequestedTools?
                .Select(t => new ToolCallDescriptor
                {
                    ToolName = t.ToolName,
                    Parameters = t.Parameters?
                        .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                        ?? new()
                }).ToList() ?? new(),
            OutputSchemaJson = request.OutputSchemaJson,
            Metadata = request.Metadata ?? new()
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="TenantContext"/> from HTTP headers and JWT claims.
    /// Throws <see cref="ArgumentException"/> if required headers are missing or malformed.
    /// </summary>
    private TenantContext ExtractTenantContext(string? correlationId)
    {
        var tenantIdStr = Request.Headers["X-Tenant-Id"].ToString();
        var appIdStr = Request.Headers["X-Application-Id"].ToString();
        var sessionId = Request.Headers["X-Session-Id"].ToString();

        // Resolve user identity from JWT claims (standard OpenID Connect "sub" claim).
        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "anonymous";

        if (!Guid.TryParse(tenantIdStr, out var tenantId))
            throw new ArgumentException(
                $"Header 'X-Tenant-Id' is missing or is not a valid GUID (received: '{tenantIdStr}').");

        if (!Guid.TryParse(appIdStr, out var appId))
            throw new ArgumentException(
                $"Header 'X-Application-Id' is missing or is not a valid GUID (received: '{appIdStr}').");

        return new TenantContext
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserId = userId,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId,
            CorrelationId = correlationId ?? HttpContext.TraceIdentifier,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "production"
        };
    }
}
