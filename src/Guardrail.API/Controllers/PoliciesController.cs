using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Application.Queries.GetPolicy;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.API.Models;

namespace Guardrail.API.Controllers;

/// <summary>
/// Manages guardrail policy profiles. Policy profiles define the enforcement rules,
/// risk thresholds, and constraint sets that govern AI guardrail behavior for a
/// specific tenant, application, or business domain.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class PoliciesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPolicyRepository _policyRepository;
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        IMediator mediator,
        IPolicyRepository policyRepository,
        IGuardrailOrchestrator orchestrator,
        ILogger<PoliciesController> logger)
    {
        _mediator = mediator;
        _policyRepository = policyRepository;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // ── GET /api/policies/{tenantId}/{applicationId} ───────────────────────────

    /// <summary>
    /// Get the effective merged policy for a tenant and application.
    /// </summary>
    /// <remarks>
    /// Returns the fully resolved policy after merging global, tenant-level,
    /// and application-level profiles in priority order. The result is cached
    /// by the policy engine until a profile update invalidates the cache.
    /// </remarks>
    /// <param name="tenantId">Tenant GUID.</param>
    /// <param name="applicationId">Application GUID within the tenant.</param>
    /// <param name="domain">Optional business domain filter (e.g., "healthcare").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective policy, or 404 if no policy is configured.</returns>
    [HttpGet("{tenantId:guid}/{applicationId:guid}")]
    [Authorize("ReadPolicy")]
    [ProducesResponseType(typeof(EffectivePolicy), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EffectivePolicy>> GetEffectivePolicy(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid applicationId,
        [FromQuery] string? domain,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "GetEffectivePolicy: tenantId={TenantId}, applicationId={ApplicationId}",
            tenantId, applicationId);

        var query = new GetEffectivePolicyQuery
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Domain = domain
        };

        var policy = await _mediator.Send(query, cancellationToken);

        if (policy is null)
            return NotFound(new { error = "No policy configured for the specified tenant and application." });

        return Ok(policy);
    }

    // ── POST /api/policies ────────────────────────────────────────────────────

    /// <summary>
    /// Create a new policy profile.
    /// </summary>
    /// <remarks>
    /// Creates a new policy profile in the repository. The profile takes effect
    /// at <c>effectiveFrom</c> and is optionally bounded by <c>effectiveTo</c>.
    /// Use <c>parentProfileId</c> to inherit from an existing profile.
    ///
    /// After creation, the policy cache for the target tenant/application is automatically
    /// invalidated on the next evaluation request.
    /// </remarks>
    /// <param name="request">Policy profile configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the ID of the newly created profile.</returns>
    [HttpPost]
    [Authorize("AdminPolicy")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] PolicyProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Policy name is required." });

        _logger.LogInformation(
            "CreatePolicy: name={Name}, tenantId={TenantId}, applicationId={ApplicationId}",
            request.Name, request.TenantId, request.ApplicationId);

        var scope = ResolveScope(request);
        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";

        var profile = Core.Domain.Entities.PolicyProfile.Create(
            request.Name,
            scope,
            request.EffectiveFrom,
            request.TenantId,
            request.ApplicationId,
            request.Domain,
            request.Description,
            request.PolicyJson,
            request.ParentProfileId,
            request.EffectiveTo,
            actor);

        await _policyRepository.AddAsync(profile, cancellationToken);

        return Created(
            $"/api/policies/{profile.Id}",
            new { id = profile.Id, name = request.Name, version = profile.Version });
    }

    // ── POST /api/policies/dry-run ───────────────────────────────────────────

    /// <summary>
    /// Simulate guardrail behavior without writing audit, review, risk, or redaction rows.
    /// </summary>
    /// <remarks>
    /// This endpoint evaluates the currently effective policy for the supplied tenant
    /// and application. Use it from the admin UI before promoting policy changes and
    /// from CI/CD to validate prompts, contexts, outputs, and proposed tool calls.
    /// </remarks>
    [HttpPost("dry-run")]
    [Authorize("AdminPolicy")]
    [ProducesResponseType(typeof(GuardrailEvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GuardrailEvaluationResult>> DryRun(
        [FromBody] GuardrailDryRunApiRequest request,
        CancellationToken cancellationToken)
    {
        var tenantContext = ExtractTenantContext(request.CorrelationId);
        var dataSources = ToSourceDescriptors(request.DataSources);
        var requestedTools = ToToolCallDescriptors(request.RequestedTools);
        var metadata = request.Metadata ?? new();

        if (!string.IsNullOrWhiteSpace(request.UserPrompt) && !string.IsNullOrWhiteSpace(request.ModelOutput))
        {
            return Ok(await _orchestrator.EvaluateFullAsync(
                new FullEvaluationRequest
                {
                    TenantContext = tenantContext,
                    UserPrompt = request.UserPrompt,
                    SystemPrompt = request.SystemPrompt,
                    ModelOutput = request.ModelOutput,
                    DataSources = dataSources,
                    RequestedTools = requestedTools,
                    OutputSchemaJson = request.OutputSchemaJson,
                    Metadata = metadata,
                    PersistAudit = false
                },
                cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return Ok(await _orchestrator.EvaluateInputAsync(
                new InputEvaluationRequest
                {
                    TenantContext = tenantContext,
                    UserPrompt = request.UserPrompt,
                    SystemPrompt = request.SystemPrompt,
                    DataSources = dataSources,
                    RequestedTools = requestedTools,
                    Metadata = metadata,
                    PersistAudit = false
                },
                cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(request.ModelOutput))
        {
            return Ok(await _orchestrator.EvaluateOutputAsync(
                new OutputEvaluationRequest
                {
                    TenantContext = tenantContext,
                    ModelOutput = request.ModelOutput,
                    OutputSchemaJson = request.OutputSchemaJson,
                    Metadata = metadata,
                    PersistAudit = false
                },
                cancellationToken));
        }

        if (requestedTools.Count > 0)
        {
            return Ok(await _orchestrator.EvaluateToolCallAsync(
                new ToolCallEvaluationRequest
                {
                    TenantContext = tenantContext,
                    RequestedTools = requestedTools,
                    Metadata = metadata,
                    PersistAudit = false
                },
                cancellationToken));
        }

        if (dataSources.Count > 0)
        {
            return Ok(await _orchestrator.EvaluateContextAsync(
                new ContextEvaluationRequest
                {
                    TenantContext = tenantContext,
                    DataSources = dataSources,
                    Metadata = metadata,
                    PersistAudit = false
                },
                cancellationToken));
        }

        return BadRequest(new { error = "Dry run requires at least one of userPrompt, modelOutput, requestedTools, or dataSources." });
    }

    // ── PUT /api/policies/{id} ────────────────────────────────────────────────

    /// <summary>
    /// Update an existing policy profile.
    /// </summary>
    /// <remarks>
    /// Replaces the policy JSON and increments the profile version counter.
    /// Triggers a cache invalidation for the affected tenant/application combination.
    /// </remarks>
    /// <param name="id">Policy profile ID to update.</param>
    /// <param name="request">Updated policy configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK on success, 404 if the profile does not exist.</returns>
    [HttpPut("{id:guid}")]
    [Authorize("AdminPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePolicy(
        [FromRoute] Guid id,
        [FromBody] PolicyProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Policy ID must not be empty." });

        _logger.LogInformation("UpdatePolicy: id={PolicyId}, name={Name}", id, request.Name);

        var profile = await _policyRepository.GetByIdAsync(id, cancellationToken);
        if (profile is null)
            return NotFound(new { error = $"Policy profile '{id}' not found." });

        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        profile.SetPolicyJson(request.PolicyJson, actor);
        profile.SetEffectivePeriod(request.EffectiveFrom, request.EffectiveTo, actor);

        if (request.ParentProfileId.HasValue)
            profile.InheritFrom(request.ParentProfileId.Value, actor);

        await _policyRepository.UpdateAsync(profile, cancellationToken);

        return Ok(new { id, version = profile.Version, message = "Policy updated successfully." });
    }

    // ── DELETE /api/policies/{id} ─────────────────────────────────────────────

    /// <summary>
    /// Deactivate a policy profile (soft delete).
    /// </summary>
    /// <param name="id">Policy profile ID to deactivate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize("AdminPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePolicy(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("DeactivatePolicy: id={PolicyId}", id);

        var profile = await _policyRepository.GetByIdAsync(id, cancellationToken);
        if (profile is null)
            return NotFound(new { error = $"Policy profile '{id}' not found." });

        var actor = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        profile.Deactivate(actor);
        await _policyRepository.UpdateAsync(profile, cancellationToken);

        return NoContent();
    }

    private static Core.Domain.Enums.PolicyScope ResolveScope(PolicyProfileRequest request)
    {
        if (request.ApplicationId.HasValue)
            return Core.Domain.Enums.PolicyScope.Application;

        if (request.TenantId.HasValue)
            return Core.Domain.Enums.PolicyScope.Tenant;

        if (!string.IsNullOrWhiteSpace(request.Domain))
            return Core.Domain.Enums.PolicyScope.Domain;

        return Core.Domain.Enums.PolicyScope.Global;
    }

    private TenantContext ExtractTenantContext(string? correlationId)
    {
        var tenantIdStr = Request.Headers["X-Tenant-Id"].ToString();
        var appIdStr = Request.Headers["X-Application-Id"].ToString();
        var sessionId = Request.Headers["X-Session-Id"].ToString();

        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "admin";

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

    private static List<SourceDescriptor> ToSourceDescriptors(List<SourceDescriptorApiModel>? dataSources)
        => dataSources?
            .Select(s => new SourceDescriptor
            {
                SourceId = s.SourceId,
                SourceType = s.SourceType,
                TenantId = s.TenantId,
                TrustLevel = s.TrustLevel,
                Uri = s.Uri,
                Metadata = s.Metadata ?? new()
            }).ToList() ?? new();

    private static List<ToolCallDescriptor> ToToolCallDescriptors(List<ToolCallApiModel>? requestedTools)
        => requestedTools?
            .Select(t => new ToolCallDescriptor
            {
                ToolName = t.ToolName,
                Parameters = t.Parameters?
                    .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    ?? new()
            }).ToList() ?? new();
}
