using MediatR;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Application.Commands.EvaluateOutput;

/// <summary>
/// MediatR command to evaluate AI model output before releasing it to the caller.
/// Triggers the output guardrail pipeline: PII redaction, output schema validation,
/// hallucination/grounding checks, and content-safety scanning.
/// </summary>
public record EvaluateOutputCommand : IRequest<GuardrailEvaluationResult>
{
    /// <summary>Tenant, application, and user identity for this request.</summary>
    public TenantContext TenantContext { get; init; } = null!;

    /// <summary>
    /// Execution ID from the corresponding input evaluation.
    /// Used to link input and output audit records for end-to-end traceability.
    /// </summary>
    public Guid? InputExecutionId { get; init; }

    /// <summary>Raw model output to be evaluated.</summary>
    public string ModelOutput { get; init; } = string.Empty;

    /// <summary>JSON Schema string the output must conform to. Null means no schema enforcement.</summary>
    public string? OutputSchemaJson { get; init; }

    /// <summary>Active constraint set applied during this evaluation.</summary>
    public ConstraintSet AppliedConstraints { get; init; } = ConstraintSet.Default;

    /// <summary>Arbitrary key-value metadata passed through to the audit log.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
