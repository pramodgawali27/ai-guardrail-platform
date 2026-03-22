using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents an individual risk indicator detected during a <see cref="RiskAssessment"/>.
/// Multiple signals compose to form the overall risk score.
/// </summary>
public sealed class RiskSignal : BaseEntity
{
    /// <summary>Foreign key to the parent <see cref="RiskAssessment"/>.</summary>
    public Guid AssessmentId { get; private set; }

    /// <summary>Classifier type that produced this signal (e.g., "ContentSafety", "PromptShield", "PolicyRule").</summary>
    public string SignalType { get; private set; } = string.Empty;

    /// <summary>Content category this signal belongs to.</summary>
    public ContentCategory Category { get; private set; }

    /// <summary>Severity of this individual signal.</summary>
    public RuleSeverity Severity { get; private set; }

    /// <summary>Raw confidence/risk score for this signal in the range [0.0, 1.0].</summary>
    public decimal Score { get; private set; }

    /// <summary>Human-readable description of what was detected.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Name or identifier of the detector/provider that produced this signal.</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Optional FK to the <see cref="PolicyRule"/> that triggered this signal.</summary>
    public Guid? PolicyRuleId { get; private set; }

    /// <summary>UTC timestamp when this signal was detected.</summary>
    public DateTimeOffset DetectedAt { get; private set; }

    private RiskSignal() { }

    /// <summary>
    /// Factory method to create a new <see cref="RiskSignal"/>.
    /// </summary>
    public static RiskSignal Create(
        Guid assessmentId,
        string signalType,
        ContentCategory category,
        RuleSeverity severity,
        decimal score,
        string description,
        string source,
        Guid? policyRuleId = null,
        string? createdBy = null)
    {
        if (assessmentId == Guid.Empty)
            throw new ArgumentException("AssessmentId must not be empty.", nameof(assessmentId));

        ArgumentException.ThrowIfNullOrWhiteSpace(signalType, nameof(signalType));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(source, nameof(source));

        if (score < 0m || score > 1m)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be in the range [0.0, 1.0].");

        return new RiskSignal
        {
            AssessmentId = assessmentId,
            SignalType = signalType.Trim(),
            Category = category,
            Severity = severity,
            Score = score,
            Description = description.Trim(),
            Source = source.Trim(),
            PolicyRuleId = policyRuleId,
            DetectedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }
}
