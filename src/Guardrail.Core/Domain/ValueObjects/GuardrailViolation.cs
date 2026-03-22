using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.ValueObjects;

/// <summary>
/// Represents a single policy violation detected during an evaluation.
/// Multiple violations may be present in a single <see cref="GuardrailEvaluationResult"/>.
/// </summary>
public sealed class GuardrailViolation
{
    /// <summary>Machine-readable code identifying the type of violation.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description of the violation.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Severity of the violation.</summary>
    public RuleSeverity Severity { get; init; }

    /// <summary>Content category implicated by this violation, if applicable.</summary>
    public ContentCategory? Category { get; init; }

    /// <summary>Name of the guardrail component that raised this violation.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Confidence score (0–1) assigned by the detecting component.</summary>
    public double Confidence { get; init; }

    /// <summary>Optional excerpt of the content that triggered the violation (redacted if sensitive).</summary>
    public string? Excerpt { get; init; }

    /// <summary>Additional structured metadata supplied by the detecting component.</summary>
    public IReadOnlyDictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}
