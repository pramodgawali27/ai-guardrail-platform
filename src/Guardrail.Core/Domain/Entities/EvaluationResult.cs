using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Records the outcome of a single test case within an <see cref="EvaluationRun"/>.
/// </summary>
public sealed class EvaluationResult : BaseEntity
{
    /// <summary>FK to the parent <see cref="EvaluationRun"/>.</summary>
    public Guid RunId { get; private set; }

    /// <summary>Unique identifier of the test case within the dataset.</summary>
    public string CaseId { get; private set; } = string.Empty;

    public string CaseName { get; private set; } = string.Empty;

    /// <summary>
    /// Safe, non-sensitive summary of the input used in this case.
    /// Must NOT contain raw prompts or PII.
    /// </summary>
    public string InputSummary { get; private set; } = string.Empty;

    /// <summary>The decision that was expected according to the test case definition.</summary>
    public DecisionType ExpectedDecision { get; private set; }

    /// <summary>The decision that the guardrail actually produced.</summary>
    public DecisionType ActualDecision { get; private set; }

    /// <summary>Whether the actual decision matched the expected decision.</summary>
    public bool Passed { get; private set; }

    /// <summary>True when the guardrail blocked/flagged content that should have been allowed.</summary>
    public bool IsFalsePositive { get; private set; }

    /// <summary>True when the guardrail allowed content that should have been blocked.</summary>
    public bool IsFalseNegative { get; private set; }

    public RiskLevel RiskLevel { get; private set; }

    /// <summary>Normalized risk score (0–100) produced by the guardrail for this case.</summary>
    public decimal NormalizedScore { get; private set; }

    /// <summary>Total evaluation latency for this case in milliseconds.</summary>
    public long LatencyMs { get; private set; }

    /// <summary>Optional evaluator notes or failure explanation.</summary>
    public string? Notes { get; private set; }

    private EvaluationResult() { }

    /// <summary>
    /// Factory method to create a new <see cref="EvaluationResult"/>.
    /// </summary>
    public static EvaluationResult Create(
        Guid runId,
        string caseId,
        string caseName,
        string inputSummary,
        DecisionType expectedDecision,
        DecisionType actualDecision,
        RiskLevel riskLevel,
        decimal normalizedScore,
        long latencyMs,
        string? notes = null,
        string? createdBy = null)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("RunId must not be empty.", nameof(runId));

        ArgumentException.ThrowIfNullOrWhiteSpace(caseId, nameof(caseId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseName, nameof(caseName));

        var passed = expectedDecision == actualDecision;
        var isFalsePositive = !passed
            && (expectedDecision == DecisionType.Allow || expectedDecision == DecisionType.AllowWithConstraints)
            && (actualDecision == DecisionType.Block || actualDecision == DecisionType.Escalate);

        var isFalseNegative = !passed
            && (expectedDecision == DecisionType.Block || expectedDecision == DecisionType.Escalate)
            && (actualDecision == DecisionType.Allow || actualDecision == DecisionType.AllowWithConstraints);

        return new EvaluationResult
        {
            RunId = runId,
            CaseId = caseId.Trim(),
            CaseName = caseName.Trim(),
            InputSummary = inputSummary,
            ExpectedDecision = expectedDecision,
            ActualDecision = actualDecision,
            Passed = passed,
            IsFalsePositive = isFalsePositive,
            IsFalseNegative = isFalseNegative,
            RiskLevel = riskLevel,
            NormalizedScore = Math.Clamp(normalizedScore, 0m, 100m),
            LatencyMs = latencyMs,
            Notes = notes?.Trim(),
            CreatedBy = createdBy
        };
    }
}
