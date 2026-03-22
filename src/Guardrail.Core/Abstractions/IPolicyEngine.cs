using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Resolves the effective policy for a given tenant/application context and evaluates inputs against it.
/// </summary>
public interface IPolicyEngine
{
    Task<EffectivePolicy> ResolveEffectivePolicyAsync(TenantContext context, CancellationToken ct = default);
    Task<PolicyEvaluationResult> EvaluatePolicyAsync(TenantContext context, EvaluationInput input, EffectivePolicy policy, CancellationToken ct = default);
}

public class EffectivePolicy
{
    public Guid? ProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public int Version { get; set; }
    public PolicyConfiguration Configuration { get; set; } = new();
    public List<PolicyRule> ApplicableRules { get; set; } = new();
    public ConstraintSet Constraints { get; set; } = ConstraintSet.Default;
    public DataBoundaryConfig DataBoundary { get; set; } = new();
}

public class PolicyConfiguration
{
    public decimal ContentRiskThreshold { get; set; } = 0.7m;
    public decimal PrivacyRiskThreshold { get; set; } = 0.6m;
    public decimal InjectionRiskThreshold { get; set; } = 0.5m;
    public decimal EscalationThreshold { get; set; } = 0.8m;
    public decimal BlockThreshold { get; set; } = 0.9m;
    public bool PIIRedactionEnabled { get; set; } = true;
    public bool PHIRedactionEnabled { get; set; } = true;
    public bool RequireCitations { get; set; }
    public List<string> AllowedTools { get; set; } = new();
    public List<string> DeniedTools { get; set; } = new();
    public List<string> ApprovalRequiredTools { get; set; } = new();
    public List<string> AllowedSourceTypes { get; set; } = new();
    public List<string> DeniedSourceTypes { get; set; } = new();
    public List<string> AllowedRegions { get; set; } = new();
    public string? MandatoryDisclaimer { get; set; }
    public List<string> ForbiddenPhrases { get; set; } = new();
    public decimal MinimumOutputQualityScore { get; set; } = 0.60m;
    public bool RequireEvidenceForRegulatedResponses { get; set; }
    public bool SimulationMode { get; set; }
    public RedactionStrategy RedactionStrategy { get; set; } = RedactionStrategy.Mask;
    public RiskWeights RiskWeights { get; set; } = RiskWeights.Default;
}

public class RiskWeights
{
    public decimal ContentWeight { get; set; } = 0.25m;
    public decimal PrivacyWeight { get; set; } = 0.20m;
    public decimal InjectionWeight { get; set; } = 0.25m;
    public decimal BusinessPolicyWeight { get; set; } = 0.15m;
    public decimal ActionWeight { get; set; } = 0.10m;
    public decimal OutputQualityWeight { get; set; } = 0.05m;

    public static RiskWeights Default => new RiskWeights();
}

public class EvaluationInput
{
    public string? UserPrompt { get; set; }
    public string? SystemPrompt { get; set; }
    public string? ModelOutput { get; set; }
    public List<string> RequestedTools { get; set; } = new();
    public List<SourceDescriptor> DataSources { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PolicyEvaluationResult
{
    public bool HasViolations { get; set; }
    public List<PolicyViolation> Violations { get; set; } = new();
    public decimal PolicyRiskScore { get; set; }
    public ConstraintSet EffectiveConstraints { get; set; } = ConstraintSet.Default;
}

public class PolicyViolation
{
    public string RuleKey { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public decimal Score { get; set; }
}
