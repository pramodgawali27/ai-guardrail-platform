using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Stores the multi-dimensional risk scores computed for a single <see cref="GuardrailExecution"/>.
/// </summary>
public sealed class RiskAssessment : BaseEntity
{
    /// <summary>Foreign key to the parent <see cref="GuardrailExecution"/>.</summary>
    public Guid ExecutionId { get; private set; }

    /// <summary>Score for content safety violations (hate speech, violence, etc.).</summary>
    public decimal ContentRiskScore { get; private set; }

    /// <summary>Score for privacy-related risks (PII, PHI detection).</summary>
    public decimal PrivacyRiskScore { get; private set; }

    /// <summary>Score for prompt injection and jailbreak attempts.</summary>
    public decimal InjectionRiskScore { get; private set; }

    /// <summary>Score for business policy violations (domain-specific rules).</summary>
    public decimal BusinessPolicyRiskScore { get; private set; }

    /// <summary>Score for risky tool/action invocations.</summary>
    public decimal ActionRiskScore { get; private set; }

    /// <summary>Score for output quality issues (hallucinations, unverified claims).</summary>
    public decimal OutputQualityRiskScore { get; private set; }

    /// <summary>Weighted aggregate of all individual risk scores (0.0 – 1.0).</summary>
    public decimal WeightedTotalScore { get; private set; }

    /// <summary>Human-readable normalized score on a 0–100 scale.</summary>
    public decimal NormalizedScore { get; private set; }

    public RiskLevel RiskLevel { get; private set; }
    public DecisionType Decision { get; private set; }

    /// <summary>Human-readable explanation of the assessment outcome.</summary>
    public string Rationale { get; private set; } = string.Empty;

    /// <summary>JSON-encoded list of recommended constraints to apply given this risk profile.</summary>
    public string RecommendedConstraints { get; private set; } = "[]";

    /// <summary>Number of individual risk signals that contributed to this assessment.</summary>
    public int SignalCount { get; private set; }

    private RiskAssessment() { }

    /// <summary>
    /// Factory method to create a new <see cref="RiskAssessment"/> with all dimension scores.
    /// </summary>
    public static RiskAssessment Create(
        Guid executionId,
        decimal contentRiskScore,
        decimal privacyRiskScore,
        decimal injectionRiskScore,
        decimal businessPolicyRiskScore,
        decimal actionRiskScore,
        decimal outputQualityRiskScore,
        decimal weightedTotalScore,
        DecisionType decision,
        string rationale,
        int signalCount,
        string recommendedConstraints = "[]",
        string? createdBy = null)
    {
        if (executionId == Guid.Empty)
            throw new ArgumentException("ExecutionId must not be empty.", nameof(executionId));

        var normalized = ComputeNormalizedScore(weightedTotalScore);
        var level = DetermineRiskLevel(normalized);

        return new RiskAssessment
        {
            ExecutionId = executionId,
            ContentRiskScore = ClampScore(contentRiskScore),
            PrivacyRiskScore = ClampScore(privacyRiskScore),
            InjectionRiskScore = ClampScore(injectionRiskScore),
            BusinessPolicyRiskScore = ClampScore(businessPolicyRiskScore),
            ActionRiskScore = ClampScore(actionRiskScore),
            OutputQualityRiskScore = ClampScore(outputQualityRiskScore),
            WeightedTotalScore = ClampScore(weightedTotalScore),
            NormalizedScore = normalized,
            RiskLevel = level,
            Decision = decision,
            Rationale = rationale,
            RecommendedConstraints = recommendedConstraints,
            SignalCount = signalCount,
            CreatedBy = createdBy
        };
    }

    /// <summary>
    /// Converts a weighted score in the range [0.0, 1.0] to a normalized 0–100 scale.
    /// </summary>
    public static decimal ComputeNormalizedScore(decimal weightedScore)
    {
        var clamped = Math.Clamp(weightedScore, 0m, 1m);
        return Math.Round(clamped * 100m, 2);
    }

    /// <summary>
    /// Determines the <see cref="RiskLevel"/> from a normalized score (0–100).
    /// </summary>
    public static RiskLevel DetermineRiskLevel(decimal normalizedScore) => normalizedScore switch
    {
        <= 15m => RiskLevel.None,
        <= 35m => RiskLevel.Low,
        <= 60m => RiskLevel.Medium,
        <= 80m => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    private static decimal ClampScore(decimal score) => Math.Clamp(score, 0m, 1m);

    public void UpdateRecommendedConstraints(string constraintsJson, string updatedBy)
    {
        RecommendedConstraints = constraintsJson;
        MarkUpdated(updatedBy);
    }
}
