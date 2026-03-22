using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardrail.Infrastructure.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly GuardrailDbContext _dbContext;

    public AuditRepository(GuardrailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuditEvent> AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _dbContext.AuditEvents.Add(auditEvent);
        await _dbContext.SaveChangesAsync(ct);
        return auditEvent;
    }

    public Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.AuditEvents.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<AuditEvent> Items, int Total)> SearchAsync(
        AuditSearchCriteria criteria,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditEvents.AsQueryable();

        if (criteria.TenantId.HasValue)
            query = query.Where(x => x.TenantId == criteria.TenantId.Value);

        if (criteria.ApplicationId.HasValue)
            query = query.Where(x => x.ApplicationId == criteria.ApplicationId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.UserId))
            query = query.Where(x => x.UserId == criteria.UserId);

        if (!string.IsNullOrWhiteSpace(criteria.CorrelationId))
            query = query.Where(x => x.CorrelationId == criteria.CorrelationId);

        if (criteria.From.HasValue)
            query = query.Where(x => x.CreatedAt >= criteria.From.Value);

        if (criteria.To.HasValue)
            query = query.Where(x => x.CreatedAt <= criteria.To.Value);

        if (!string.IsNullOrWhiteSpace(criteria.EventType))
            query = query.Where(x => x.EventType == criteria.EventType);

        if (criteria.IsIncident.HasValue)
            query = query.Where(x => x.IsIncident == criteria.IsIncident.Value);

        var total = await query.CountAsync(ct);
        var page = Math.Max(criteria.Page, 1);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<GuardrailExecution> AddExecutionAsync(GuardrailExecution execution, CancellationToken ct = default)
    {
        _dbContext.GuardrailExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(ct);
        return execution;
    }

    public Task<GuardrailExecution?> GetExecutionByIdAsync(Guid executionId, CancellationToken ct = default)
        => _dbContext.GuardrailExecutions.FirstOrDefaultAsync(x => x.Id == executionId, ct);

    public async Task UpdateExecutionAsync(GuardrailExecution execution, CancellationToken ct = default)
    {
        _dbContext.GuardrailExecutions.Update(execution);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<RiskAssessment> AddRiskAssessmentAsync(RiskAssessment assessment, CancellationToken ct = default)
    {
        _dbContext.RiskAssessments.Add(assessment);
        await _dbContext.SaveChangesAsync(ct);
        return assessment;
    }
}
