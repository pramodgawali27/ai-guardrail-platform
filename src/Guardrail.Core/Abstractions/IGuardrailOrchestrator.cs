using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Orchestrates the full guardrail evaluation pipeline, coordinating the Policy Engine,
/// Risk Engine, Content Safety adapter, Prompt Shield adapter, Context Firewall,
/// Tool Firewall, Output Validator, and Audit Service.
/// </summary>
public interface IGuardrailOrchestrator
{
    /// <summary>Evaluate AI model input before execution.</summary>
    Task<GuardrailEvaluationResult> EvaluateInputAsync(InputEvaluationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Evaluate AI model output before returning it to the caller.</summary>
    Task<GuardrailEvaluationResult> EvaluateOutputAsync(OutputEvaluationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Evaluate both model input and output in a single atomic pipeline execution.</summary>
    Task<GuardrailEvaluationResult> EvaluateFullAsync(FullEvaluationRequest request, CancellationToken cancellationToken = default);
}
