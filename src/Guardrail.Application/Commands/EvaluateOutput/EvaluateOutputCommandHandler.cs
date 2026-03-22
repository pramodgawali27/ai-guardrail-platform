using MediatR;
using Microsoft.Extensions.Logging;
using Guardrail.Core.Abstractions;

namespace Guardrail.Application.Commands.EvaluateOutput;

/// <summary>
/// Handles <see cref="EvaluateOutputCommand"/> by delegating to the
/// <see cref="IGuardrailOrchestrator"/> output evaluation pipeline.
/// </summary>
public sealed class EvaluateOutputCommandHandler
    : IRequestHandler<EvaluateOutputCommand, GuardrailEvaluationResult>
{
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly ILogger<EvaluateOutputCommandHandler> _logger;

    public EvaluateOutputCommandHandler(
        IGuardrailOrchestrator orchestrator,
        ILogger<EvaluateOutputCommandHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<GuardrailEvaluationResult> Handle(
        EvaluateOutputCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Evaluating output for tenant {TenantId}, correlation {CorrelationId}, inputExecution {InputExecutionId}",
            request.TenantContext.TenantId,
            request.TenantContext.CorrelationId,
            request.InputExecutionId);

        var evaluationRequest = new OutputEvaluationRequest
        {
            TenantContext = request.TenantContext,
            InputExecutionId = request.InputExecutionId,
            ModelOutput = request.ModelOutput,
            OutputSchemaJson = request.OutputSchemaJson,
            AppliedConstraints = request.AppliedConstraints,
            Metadata = request.Metadata
        };

        var result = await _orchestrator.EvaluateOutputAsync(evaluationRequest, cancellationToken);

        _logger.LogInformation(
            "Output evaluation complete for correlation {CorrelationId}: decision={Decision}, riskLevel={RiskLevel}, durationMs={DurationMs}",
            request.TenantContext.CorrelationId,
            result.Decision,
            result.RiskLevel,
            result.DurationMs);

        return result;
    }
}
