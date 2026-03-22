using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardrail.Infrastructure.Repositories;

public sealed class EvaluationRepository : IEvaluationRepository
{
    private readonly GuardrailDbContext _dbContext;

    public EvaluationRepository(GuardrailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EvaluationRun> AddRunAsync(EvaluationRun run, CancellationToken ct = default)
    {
        _dbContext.EvaluationRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct);
        return run;
    }

    public async Task UpdateRunAsync(EvaluationRun run, CancellationToken ct = default)
    {
        _dbContext.EvaluationRuns.Update(run);
        await _dbContext.SaveChangesAsync(ct);
    }

    public Task<EvaluationRun?> GetRunByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.EvaluationRuns.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<EvaluationRun>> GetRunsForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbContext.EvaluationRuns
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<EvaluationDataset?> GetDatasetByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.EvaluationDatasets.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<EvaluationDataset> AddDatasetAsync(EvaluationDataset dataset, CancellationToken ct = default)
    {
        _dbContext.EvaluationDatasets.Add(dataset);
        await _dbContext.SaveChangesAsync(ct);
        return dataset;
    }

    public async Task AddResultsAsync(IEnumerable<EvaluationResult> results, CancellationToken ct = default)
    {
        _dbContext.EvaluationResults.AddRange(results);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EvaluationResult>> GetResultsForRunAsync(Guid runId, CancellationToken ct = default)
        => await _dbContext.EvaluationResults
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.CaseId)
            .ToListAsync(ct);
}
