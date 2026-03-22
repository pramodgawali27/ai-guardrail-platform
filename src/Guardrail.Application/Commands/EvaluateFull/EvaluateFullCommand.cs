using MediatR;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Application.Commands.EvaluateFull;

/// <summary>
/// MediatR command for evaluating both input and output in a single atomic pipeline run.
/// Used by clients that process prompt and response inline (e.g., streaming adapters).
/// </summary>
public record EvaluateFullCommand : IRequest<GuardrailEvaluationResult>
{
    /// <summary>Tenant, application, and user identity for this request.</summary>
    public TenantContext TenantContext { get; init; } = null!;

    /// <summary>The user-supplied prompt to be evaluated on the input side.</summary>
    public string UserPrompt { get; init; } = string.Empty;

    /// <summary>Optional system prompt provided by the application.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Raw model output to be evaluated on the output side.</summary>
    public string ModelOutput { get; init; } = string.Empty;

    /// <summary>Data sources the model was permitted to access.</summary>
    public List<SourceDescriptor> DataSources { get; init; } = new();

    /// <summary>Tools the model was asked to invoke.</summary>
    public List<ToolCallDescriptor> RequestedTools { get; init; } = new();

    /// <summary>JSON Schema string the output must conform to. Null means no schema enforcement.</summary>
    public string? OutputSchemaJson { get; init; }

    /// <summary>Arbitrary key-value metadata passed through to the audit log.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
