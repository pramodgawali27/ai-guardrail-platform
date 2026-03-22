using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Application.Queries.GetPolicy;
using Guardrail.Core.Abstractions;
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
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        IMediator mediator,
        IPolicyRepository policyRepository,
        ILogger<PoliciesController> logger)
    {
        _mediator = mediator;
        _policyRepository = policyRepository;
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
}
