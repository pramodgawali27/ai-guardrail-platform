using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Aggregates signals from all guardrail components into a single risk score and enforcement decision.
/// </summary>
public interface IRiskEngine
{
    Task<RiskEvaluationResult> EvaluateRiskAsync(RiskEvaluationInput input, CancellationToken ct = default);
}

public class RiskEvaluationInput
{
    public TenantContext TenantContext { get; set; } = null!;
    public ContentSafetyResult? ContentSafetyResult { get; set; }
    public PromptShieldResult? PromptShieldResult { get; set; }
    public PolicyEvaluationResult? PolicyEvaluationResult { get; set; }
    public ToolValidationResult? ToolValidationResult { get; set; }
    public ContextFirewallResult? ContextFirewallResult { get; set; }
    public OutputValidationResult? OutputValidationResult { get; set; }
    public RiskWeights Weights { get; set; } = RiskWeights.Default;
    public decimal ContentRiskThreshold { get; set; } = 0.7m;
    public decimal PrivacyRiskThreshold { get; set; } = 0.6m;
    public decimal InjectionRiskThreshold { get; set; } = 0.5m;
    public decimal EscalationThreshold { get; set; } = 0.8m;
    public decimal BlockThreshold { get; set; } = 0.9m;
}

public class RiskEvaluationResult
{
    public RiskScore Score { get; set; } = RiskScore.Zero;
    public DecisionType Decision { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public ConstraintSet RecommendedConstraints { get; set; } = ConstraintSet.Default;
    public List<string> AppliedSignals { get; set; } = new();
    public bool RequiresHumanReview { get; set; }
}

public class ToolValidationResult
{
    public bool AllToolsAllowed { get; set; }
    public List<string> AllowedTools { get; set; } = new();
    public List<string> DeniedTools { get; set; } = new();
    public List<string> ApprovalRequiredTools { get; set; } = new();
    public decimal ToolRiskScore { get; set; }
}

public class ContextFirewallResult
{
    public bool AllSourcesAllowed { get; set; }
    public List<string> AllowedSources { get; set; } = new();
    public List<string> BlockedSources { get; set; } = new();
    public bool CrossTenantAttemptDetected { get; set; }
    public decimal ContextRiskScore { get; set; }
}

public class OutputValidationResult
{
    public bool IsValid { get; set; }
    public List<OutputViolation> Violations { get; set; } = new();
    public bool RequiresRedaction { get; set; }
    public decimal QualityScore { get; set; }
    public int RedactionCount { get; set; }
    public string? RedactedOutput { get; set; }
    public ConstraintSet EffectiveConstraints { get; set; } = ConstraintSet.Default;
}

public class OutputViolation
{
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
