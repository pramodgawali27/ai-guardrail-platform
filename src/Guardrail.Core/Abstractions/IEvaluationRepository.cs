using Guardrail.Core.Domain.Entities;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Persistence contract for evaluation runs, datasets, and per-case results.
/// </summary>
public interface IEvaluationRepository
{
    Task<EvaluationRun> AddRunAsync(EvaluationRun run, CancellationToken ct = default);
    Task UpdateRunAsync(EvaluationRun run, CancellationToken ct = default);
    Task<EvaluationRun?> GetRunByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationRun>> GetRunsForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<EvaluationDataset?> GetDatasetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EvaluationDataset> AddDatasetAsync(EvaluationDataset dataset, CancellationToken ct = default);
    Task AddResultsAsync(IEnumerable<EvaluationResult> results, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationResult>> GetResultsForRunAsync(Guid runId, CancellationToken ct = default);
}
