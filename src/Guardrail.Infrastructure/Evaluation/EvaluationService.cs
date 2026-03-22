using Guardrail.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Guardrail.Infrastructure.Evaluation;

public sealed class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _evaluationRepository;
    private readonly EvaluationBackgroundQueue _backgroundQueue;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        IEvaluationRepository evaluationRepository,
        EvaluationBackgroundQueue backgroundQueue,
        ILogger<EvaluationService> logger)
    {
        _evaluationRepository = evaluationRepository;
        _backgroundQueue = backgroundQueue;
        _logger = logger;
    }

    public async Task<Core.Domain.Entities.EvaluationRun> QueueRunAsync(
        QueueEvaluationRunRequest request,
        CancellationToken ct = default)
    {
        var run = Core.Domain.Entities.EvaluationRun.Create(
            request.TenantId,
            request.Name,
            request.DatasetId,
            request.Description,
            request.PolicyProfileId,
            request.RequestedBy);

        await _evaluationRepository.AddRunAsync(run, ct);
        await _backgroundQueue.QueueAsync(run.Id, ct);

        _logger.LogInformation(
            "Queued evaluation run {RunId} for tenant {TenantId} and dataset {DatasetId}",
            run.Id,
            request.TenantId,
            request.DatasetId);

        return run;
    }
}
