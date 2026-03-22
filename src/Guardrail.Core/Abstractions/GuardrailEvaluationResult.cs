using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// The top-level response returned by every guardrail evaluation endpoint.
/// Encapsulates the final decision, risk score, constraints, and audit metadata.
/// </summary>
public class GuardrailEvaluationResult
{
    public Guid ExecutionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DecisionType Decision { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal NormalizedRiskScore { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public ConstraintSet AppliedConstraints { get; set; } = ConstraintSet.Default;
    public bool RequiresHumanReview { get; set; }
    public Guid? HumanReviewCaseId { get; set; }
    public List<string> AppliedPolicies { get; set; } = new();
    public List<string> DetectedSignals { get; set; } = new();
    public string? RedactedOutput { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long DurationMs { get; set; }

    // Convenience properties
    public bool IsAllowed => Decision is DecisionType.Allow or DecisionType.AllowWithConstraints;
    public bool IsBlocked => Decision == DecisionType.Block;

    public static GuardrailEvaluationResult Allow(Guid executionId, string correlationId, ConstraintSet constraints) => new()
    {
        ExecutionId = executionId,
        CorrelationId = correlationId,
        Decision = DecisionType.Allow,
        RiskLevel = RiskLevel.None,
        NormalizedRiskScore = 0,
        AppliedConstraints = constraints
    };

    public static GuardrailEvaluationResult Block(Guid executionId, string correlationId, string rationale, decimal score) => new()
    {
        ExecutionId = executionId,
        CorrelationId = correlationId,
        Decision = DecisionType.Block,
        RiskLevel = RiskLevel.Critical,
        NormalizedRiskScore = score,
        Rationale = rationale
    };
}

/// <summary>Request payload for evaluating AI model input before execution.</summary>
public class InputEvaluationRequest
{
    public TenantContext TenantContext { get; set; } = null!;
    public string UserPrompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public List<SourceDescriptor> DataSources { get; set; } = new();
    public List<ToolCallDescriptor> RequestedTools { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>Request payload for evaluating AI model output before returning it to the caller.</summary>
public class OutputEvaluationRequest
{
    public TenantContext TenantContext { get; set; } = null!;
    public Guid? InputExecutionId { get; set; }
    public string ModelOutput { get; set; } = string.Empty;
    public string? OutputSchemaJson { get; set; }
    public ConstraintSet AppliedConstraints { get; set; } = ConstraintSet.Default;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>Request payload for evaluating both model input and output in a single atomic call.</summary>
public class FullEvaluationRequest
{
    public TenantContext TenantContext { get; set; } = null!;
    public string UserPrompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string ModelOutput { get; set; } = string.Empty;
    public List<SourceDescriptor> DataSources { get; set; } = new();
    public List<ToolCallDescriptor> RequestedTools { get; set; } = new();
    public string? OutputSchemaJson { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
