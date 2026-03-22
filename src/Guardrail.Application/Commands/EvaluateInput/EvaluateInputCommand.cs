using MediatR;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;
// ToolCallDescriptor is defined in Guardrail.Core.Abstractions (IToolFirewall.cs)

namespace Guardrail.Application.Commands.EvaluateInput;

/// <summary>
/// MediatR command to evaluate AI model input before the model is invoked.
/// Triggers the full input guardrail pipeline: policy resolution, prompt-shield,
/// content-safety, context firewall, and tool firewall checks.
/// </summary>
public record EvaluateInputCommand : IRequest<GuardrailEvaluationResult>
{
    /// <summary>Tenant, application, and user identity for this request.</summary>
    public TenantContext TenantContext { get; init; } = null!;

    /// <summary>The user-supplied prompt to be evaluated.</summary>
    public string UserPrompt { get; init; } = string.Empty;

    /// <summary>Optional system prompt provided by the application.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Data sources the model is permitted to access.</summary>
    public List<SourceDescriptor> DataSources { get; init; } = new();

    /// <summary>Tools the model has been asked to invoke.</summary>
    public List<ToolCallDescriptor> RequestedTools { get; init; } = new();

    /// <summary>Arbitrary key-value metadata passed through to the audit log.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
