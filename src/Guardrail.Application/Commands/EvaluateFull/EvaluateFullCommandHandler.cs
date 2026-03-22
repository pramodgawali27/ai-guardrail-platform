using MediatR;
using Microsoft.Extensions.Logging;
using Guardrail.Core.Abstractions;

namespace Guardrail.Application.Commands.EvaluateFull;

/// <summary>
/// Handles <see cref="EvaluateFullCommand"/> by delegating to the
/// <see cref="IGuardrailOrchestrator"/> full evaluation pipeline.
/// </summary>
public sealed class EvaluateFullCommandHandler
    : IRequestHandler<EvaluateFullCommand, GuardrailEvaluationResult>
{
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly ILogger<EvaluateFullCommandHandler> _logger;

    public EvaluateFullCommandHandler(
        IGuardrailOrchestrator orchestrator,
        ILogger<EvaluateFullCommandHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<GuardrailEvaluationResult> Handle(
        EvaluateFullCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Full evaluation for tenant {TenantId}, application {ApplicationId}, correlation {CorrelationId}",
            request.TenantContext.TenantId,
            request.TenantContext.ApplicationId,
            request.TenantContext.CorrelationId);

        var evaluationRequest = new FullEvaluationRequest
        {
            TenantContext = request.TenantContext,
            UserPrompt = request.UserPrompt,
            SystemPrompt = request.SystemPrompt,
            ModelOutput = request.ModelOutput,
            DataSources = request.DataSources,
            RequestedTools = request.RequestedTools,
            OutputSchemaJson = request.OutputSchemaJson,
            Metadata = request.Metadata
        };

        var result = await _orchestrator.EvaluateFullAsync(evaluationRequest, cancellationToken);

        _logger.LogInformation(
            "Full evaluation complete for correlation {CorrelationId}: decision={Decision}, riskLevel={RiskLevel}, signals={SignalCount}, durationMs={DurationMs}",
            request.TenantContext.CorrelationId,
            result.Decision,
            result.RiskLevel,
            result.DetectedSignals.Count,
            result.DurationMs);

        return result;
    }
}
