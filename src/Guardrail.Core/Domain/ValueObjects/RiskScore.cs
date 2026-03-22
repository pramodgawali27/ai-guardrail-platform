using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the multi-dimensional risk scores for a guardrail evaluation.
/// </summary>
public sealed record RiskScore
{
    public decimal ContentRisk { get; init; }
    public decimal PrivacyRisk { get; init; }
    public decimal InjectionRisk { get; init; }
    public decimal BusinessPolicyRisk { get; init; }
    public decimal ActionRisk { get; init; }
    public decimal OutputQualityRisk { get; init; }
    public decimal WeightedTotal { get; init; }
    public decimal NormalizedScore { get; init; }
    public RiskLevel Level { get; init; }

    public static RiskScore Zero => new RiskScore
    {
        ContentRisk = 0,
        PrivacyRisk = 0,
        InjectionRisk = 0,
        BusinessPolicyRisk = 0,
        ActionRisk = 0,
        OutputQualityRisk = 0,
        WeightedTotal = 0,
        NormalizedScore = 0,
        Level = RiskLevel.None
    };

    /// <summary>
    /// Determines the <see cref="RiskLevel"/> from a normalized score (0–100).
    /// </summary>
    public static RiskLevel DetermineLevel(decimal normalizedScore) => normalizedScore switch
    {
        <= 15 => RiskLevel.None,
        <= 35 => RiskLevel.Low,
        <= 60 => RiskLevel.Medium,
        <= 80 => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    /// <summary>Returns true if the score represents a critical risk requiring immediate action.</summary>
    public bool IsCritical => Level == RiskLevel.Critical;

    /// <summary>Returns true if the score is high or critical.</summary>
    public bool IsHighOrAbove => Level >= RiskLevel.High;

    /// <summary>Returns true if any individual dimension score exceeds the given threshold.</summary>
    public bool AnyDimensionExceeds(decimal threshold) =>
        ContentRisk > threshold ||
        PrivacyRisk > threshold ||
        InjectionRisk > threshold ||
        BusinessPolicyRisk > threshold ||
        ActionRisk > threshold ||
        OutputQualityRisk > threshold;
}
