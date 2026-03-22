using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guardrail.Infrastructure.Evaluation;

public sealed class EvaluationWorker : BackgroundService
{
    private readonly EvaluationBackgroundQueue _backgroundQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EvaluationWorker> _logger;

    public EvaluationWorker(
        EvaluationBackgroundQueue backgroundQueue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EvaluationWorker> logger)
    {
        _backgroundQueue = backgroundQueue;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in _backgroundQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EvaluationRunProcessor>();
                await processor.ProcessAsync(runId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluation worker failed while processing run {RunId}", runId);
            }
        }
    }
}
