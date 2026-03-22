using System.Text.Json.Nodes;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Infrastructure.Policies;

internal sealed class PolicyDocument
{
    public decimal? ContentRiskThreshold { get; set; }
    public decimal? PrivacyRiskThreshold { get; set; }
    public decimal? InjectionRiskThreshold { get; set; }
    public decimal? EscalationThreshold { get; set; }
    public decimal? BlockThreshold { get; set; }
    public bool? PIIRedactionEnabled { get; set; }
    public bool? PHIRedactionEnabled { get; set; }
    public bool? RequireCitations { get; set; }
    public bool? RequireEvidenceForRegulatedResponses { get; set; }
    public bool? SimulationMode { get; set; }
    public decimal? MinimumOutputQualityScore { get; set; }
    public RedactionStrategy? RedactionStrategy { get; set; }
    public bool? AllowToolUse { get; set; }
    public bool? CrossTenantAllowed { get; set; }
    public int? MaxDocuments { get; set; }
    public SourceTrustLevel? MinimumSourceTrustLevel { get; set; }
    public string? MandatoryDisclaimer { get; set; }
    public List<string>? AllowedTools { get; set; }
    public List<string>? DeniedTools { get; set; }
    public List<string>? ApprovalRequiredTools { get; set; }
    public List<string>? AllowedSourceTypes { get; set; }
    public List<string>? DeniedSourceTypes { get; set; }
    public List<string>? AllowedRegions { get; set; }
    public List<string>? ForbiddenPhrases { get; set; }
    public List<PolicyRuleDocument>? Rules { get; set; }
    public RiskWeightDocument? RiskWeights { get; set; }
}

internal sealed class RiskWeightDocument
{
    public decimal? ContentWeight { get; set; }
    public decimal? PrivacyWeight { get; set; }
    public decimal? InjectionWeight { get; set; }
    public decimal? BusinessPolicyWeight { get; set; }
    public decimal? ActionWeight { get; set; }
    public decimal? OutputQualityWeight { get; set; }
}

internal sealed class PolicyRuleDocument
{
    public string RuleKey { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleSeverity Severity { get; set; } = RuleSeverity.Medium;
    public ContentCategory Category { get; set; } = ContentCategory.Restricted;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public bool OverrideAllowed { get; set; }
    public JsonObject Conditions { get; set; } = new();
    public JsonObject Actions { get; set; } = new();
}

internal sealed class SeedPolicyFile
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PolicyScope Scope { get; set; } = PolicyScope.Global;
    public Guid? TenantId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string? Domain { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? ParentProfileId { get; set; }
    public PolicyDocument Policy { get; set; } = new();
}
