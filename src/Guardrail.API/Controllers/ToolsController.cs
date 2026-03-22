using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.API.Controllers;

/// <summary>
/// Exposes tool-firewall validation as a standalone endpoint.
/// Use this to validate tool calls outside of a full evaluation pipeline,
/// e.g., from an orchestration layer that pre-validates tool requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize("EvaluatePolicy")]
[Produces("application/json")]
public sealed class ToolsController : ControllerBase
{
    private readonly IToolFirewall _toolFirewall;
    private readonly IPolicyEngine _policyEngine;
    private readonly ILogger<ToolsController> _logger;

    public ToolsController(
        IToolFirewall toolFirewall,
        IPolicyEngine policyEngine,
        ILogger<ToolsController> logger)
    {
        _toolFirewall = toolFirewall;
        _policyEngine = policyEngine;
        _logger = logger;
    }

    // ── POST /api/tools/validate ──────────────────────────────────────────────

    /// <summary>
    /// Validate a set of tool calls against the active tool-firewall policy.
    /// </summary>
    /// <remarks>
    /// Checks each requested tool against the tenant/application allowlist and denylist,
    /// then returns a per-tool validation verdict. Tools on the denylist are blocked;
    /// tools not on the allowlist are blocked when the policy is in strict mode.
    ///
    /// Required headers: <c>X-Tenant-Id</c> (GUID), <c>X-Application-Id</c> (GUID).
    /// </remarks>
    /// <param name="request">Tool validation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool validation result with per-tool verdicts.</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ToolValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ToolValidationResult>> ValidateTools(
        [FromBody] ValidateToolsApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tools == null || request.Tools.Count == 0)
            return BadRequest(new { error = "At least one tool must be specified." });

        if (request.Tools.Count > 20)
            return BadRequest(new { error = "A maximum of 20 tools may be validated per request." });

        var tenantContext = ExtractTenantContext();

        _logger.LogInformation(
            "ValidateTools: tenant={TenantId}, application={ApplicationId}, toolCount={ToolCount}",
            tenantContext.TenantId, tenantContext.ApplicationId, request.Tools.Count);

        // Resolve the effective policy for this tenant/application.
        var policy = await _policyEngine.ResolveEffectivePolicyAsync(tenantContext, cancellationToken);

        var toolValidationRequest = new ToolValidationRequest
        {
            TenantContext = tenantContext,
            RequestedTools = request.Tools.Select(t => new ToolCallDescriptor
            {
                ToolName = t.ToolName,
                Parameters = t.Parameters?
                    .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    ?? new()
            }).ToList(),
            Policy = policy
        };

        var result = await _toolFirewall.ValidateToolsAsync(toolValidationRequest, cancellationToken);
        return Ok(result);
    }

    // ── GET /api/tools/registry ───────────────────────────────────────────────

    /// <summary>
    /// List all registered tools visible to the current tenant and application.
    /// </summary>
    /// <remarks>
    /// Returns tools from the registry that are allowed by the active policy.
    /// Tools on the global denylist are excluded from the response.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("registry")]
    [Authorize("ReadPolicy")]
    [ProducesResponseType(typeof(ToolRegistryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ToolRegistryResponse>> GetRegistry(
        CancellationToken cancellationToken)
    {
        // TODO: Implement tool registry query once ToolRegistry service is available.
        await Task.CompletedTask;

        return Ok(new ToolRegistryResponse { Tools = Array.Empty<ToolRegistryEntry>() });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private TenantContext ExtractTenantContext()
    {
        var tenantIdStr = Request.Headers["X-Tenant-Id"].ToString();
        var appIdStr = Request.Headers["X-Application-Id"].ToString();
        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        if (!Guid.TryParse(tenantIdStr, out var tenantId))
            throw new ArgumentException($"Header 'X-Tenant-Id' is missing or not a valid GUID.");

        if (!Guid.TryParse(appIdStr, out var appId))
            throw new ArgumentException($"Header 'X-Application-Id' is missing or not a valid GUID.");

        return new TenantContext
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserId = userId,
            CorrelationId = HttpContext.TraceIdentifier
        };
    }
}

// ── Request / response DTOs ───────────────────────────────────────────────────

/// <summary>Request body for POST /api/tools/validate.</summary>
public sealed class ValidateToolsApiRequest
{
    /// <summary>Tool calls to validate.</summary>
    public List<ToolCallApiItem> Tools { get; init; } = new();
}

/// <summary>A single tool call item in a validate-tools request.</summary>
public sealed class ToolCallApiItem
{
    public string ToolName { get; init; } = string.Empty;
    public Dictionary<string, string>? Parameters { get; init; }
}

/// <summary>Tool registry listing response.</summary>
public sealed class ToolRegistryResponse
{
    public IReadOnlyList<ToolRegistryEntry> Tools { get; init; } = Array.Empty<ToolRegistryEntry>();
}

/// <summary>Summary entry for a registered tool.</summary>
public sealed class ToolRegistryEntry
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsAllowed { get; init; }
}
