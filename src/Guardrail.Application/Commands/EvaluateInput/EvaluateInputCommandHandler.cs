using MediatR;
using Microsoft.Extensions.Logging;
using Guardrail.Core.Abstractions;

namespace Guardrail.Application.Commands.EvaluateInput;

/// <summary>
/// Handles <see cref="EvaluateInputCommand"/> by delegating to the
/// <see cref="IGuardrailOrchestrator"/> and surfacing the result.
/// </summary>
public sealed class EvaluateInputCommandHandler
    : IRequestHandler<EvaluateInputCommand, GuardrailEvaluationResult>
{
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly ILogger<EvaluateInputCommandHandler> _logger;

    public EvaluateInputCommandHandler(
        IGuardrailOrchestrator orchestrator,
        ILogger<EvaluateInputCommandHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<GuardrailEvaluationResult> Handle(
        EvaluateInputCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Evaluating input for tenant {TenantId}, application {ApplicationId}, correlation {CorrelationId}",
            request.TenantContext.TenantId,
            request.TenantContext.ApplicationId,
            request.TenantContext.CorrelationId);

        var evaluationRequest = new InputEvaluationRequest
        {
            TenantContext = request.TenantContext,
            UserPrompt = request.UserPrompt,
            SystemPrompt = request.SystemPrompt,
            DataSources = request.DataSources,
            RequestedTools = request.RequestedTools,
            Metadata = request.Metadata
        };

        var result = await _orchestrator.EvaluateInputAsync(evaluationRequest, cancellationToken);

        _logger.LogInformation(
            "Input evaluation complete for correlation {CorrelationId}: decision={Decision}, riskLevel={RiskLevel}, durationMs={DurationMs}",
            request.TenantContext.CorrelationId,
            result.Decision,
            result.RiskLevel,
            result.DurationMs);

        return result;
    }
}
