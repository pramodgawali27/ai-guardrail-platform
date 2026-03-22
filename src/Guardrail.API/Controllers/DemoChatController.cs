using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardrail.API.Controllers;

[ApiController]
[Route("api/demo")]
[AllowAnonymous]
public class DemoChatController : ControllerBase
{
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly HuggingFaceInferenceClient _hfClient;
    private readonly ILogger<DemoChatController> _logger;

    // Fixed demo tenant/app IDs that match the seeded policies
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");

    private static readonly Dictionary<string, Guid> AppIds = new()
    {
        ["plain-language"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"),
        ["enterprise-copilot"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002"),
        ["healthcare"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003"),
        ["developer"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0004"),
    };

    private static readonly Dictionary<string, string> SystemPrompts = new()
    {
        ["plain-language"]    = "You are a plain language summarization assistant. Explain things simply and clearly.",
        ["enterprise-copilot"] = "You are an enterprise assistant. Be professional, concise, and accurate.",
        ["healthcare"]        = "You are a healthcare information assistant. Always recommend consulting a licensed physician.",
        ["developer"]         = "You are a developer assistant. Help with code, architecture, and technical questions.",
    };

    public DemoChatController(
        IGuardrailOrchestrator orchestrator,
        HuggingFaceInferenceClient hfClient,
        ILogger<DemoChatController> logger)
    {
        _orchestrator = orchestrator;
        _hfClient = hfClient;
        _logger = logger;
    }

    /// <summary>
    /// Full guardrail demo: evaluate input → call AI model → evaluate output.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(DemoChatResponse), 200)]
    public async Task<ActionResult<DemoChatResponse>> Chat(
        [FromBody] DemoChatRequest request,
        CancellationToken ct)
    {
        var appKey = request.AppType ?? "plain-language";
        if (!AppIds.TryGetValue(appKey, out var appId))
            appId = AppIds["plain-language"];

        var tenantCtx = new TenantContext
        {
            TenantId = DemoTenantId,
            ApplicationId = appId,
            UserId = "demo-user",
            CorrelationId = HttpContext.TraceIdentifier,
            Environment = "demo"
        };

        var response = new DemoChatResponse { CorrelationId = tenantCtx.CorrelationId };

        // ── Step 1: Evaluate input ────────────────────────────────────────────
        _logger.LogInformation("Demo chat step 1: evaluating input for correlation {Id}", tenantCtx.CorrelationId);

        var inputResult = await _orchestrator.EvaluateInputAsync(new InputEvaluationRequest
        {
            TenantContext = tenantCtx,
            UserPrompt = request.Prompt,
            SystemPrompt = SystemPrompts.GetValueOrDefault(appKey)
        }, ct);

        response.InputGuardrail = ToSummary(inputResult);

        if (inputResult.Decision == Core.Domain.Enums.DecisionType.Block)
        {
            response.Blocked = true;
            response.BlockReason = "Input was blocked by the guardrail. " + inputResult.Rationale;
            return Ok(response);
        }

        // ── Step 2: Call the AI model ─────────────────────────────────────────
        _logger.LogInformation("Demo chat step 2: calling HF model");

        var modelResponse = await _hfClient.ChatAsync(
            request.Prompt,
            SystemPrompts.GetValueOrDefault(appKey),
            ct);

        if (!modelResponse.Success)
        {
            response.ModelError = modelResponse.ErrorMessage;
            response.ModelId = modelResponse.ModelId;
            return Ok(response);
        }

        response.ModelId = modelResponse.ModelId;
        var rawOutput = modelResponse.Content;

        // ── Step 3: Evaluate output ───────────────────────────────────────────
        _logger.LogInformation("Demo chat step 3: evaluating output for correlation {Id}", tenantCtx.CorrelationId);

        var outputResult = await _orchestrator.EvaluateOutputAsync(new OutputEvaluationRequest
        {
            TenantContext = tenantCtx,
            InputExecutionId = inputResult.ExecutionId,
            ModelOutput = rawOutput,
            AppliedConstraints = inputResult.AppliedConstraints
        }, ct);

        response.OutputGuardrail = ToSummary(outputResult);

        // Use redacted output if the guardrail modified it, otherwise use raw
        response.FinalResponse = outputResult.RedactedOutput ?? rawOutput;
        response.WasRedacted = outputResult.RedactedOutput != null;

        if (outputResult.Decision == Core.Domain.Enums.DecisionType.Block)
        {
            response.Blocked = true;
            response.BlockReason = "AI response was blocked after output evaluation. " + outputResult.Rationale;
            response.FinalResponse = null;
        }

        return Ok(response);
    }

    private static GuardrailSummary ToSummary(GuardrailEvaluationResult r) => new()
    {
        Decision = r.Decision.ToString(),
        RiskLevel = r.RiskLevel.ToString(),
        NormalizedScore = r.NormalizedRiskScore,
        DetectedSignals = r.DetectedSignals,
        AppliedPolicies = r.AppliedPolicies,
        Rationale = r.Rationale,
        ExecutionId = r.ExecutionId
    };
}

public class DemoChatRequest
{
    public string Prompt  { get; set; } = string.Empty;
    /// <summary>plain-language | enterprise-copilot | healthcare | developer</summary>
    public string? AppType { get; set; }
}

public class DemoChatResponse
{
    public string CorrelationId  { get; set; } = string.Empty;
    public string? ModelId       { get; set; }
    public GuardrailSummary? InputGuardrail  { get; set; }
    public GuardrailSummary? OutputGuardrail { get; set; }
    public string? FinalResponse { get; set; }
    public bool   Blocked        { get; set; }
    public bool   WasRedacted    { get; set; }
    public string? BlockReason   { get; set; }
    public string? ModelError    { get; set; }
}

public class GuardrailSummary
{
    public string Decision        { get; set; } = string.Empty;
    public string RiskLevel       { get; set; } = string.Empty;
    public decimal NormalizedScore { get; set; }
    public List<string> DetectedSignals { get; set; } = new();
    public List<string> AppliedPolicies { get; set; } = new();
    public string Rationale       { get; set; } = string.Empty;
    public Guid ExecutionId       { get; set; }
}
