using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardrail.Infrastructure.Repositories;

public sealed class HumanReviewRepository : IHumanReviewRepository
{
    private readonly GuardrailDbContext _dbContext;

    public HumanReviewRepository(GuardrailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HumanReviewCase> AddAsync(HumanReviewCase reviewCase, CancellationToken ct = default)
    {
        _dbContext.HumanReviewCases.Add(reviewCase);
        await _dbContext.SaveChangesAsync(ct);
        return reviewCase;
    }

    public Task<HumanReviewCase?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.HumanReviewCases.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task UpdateAsync(HumanReviewCase reviewCase, CancellationToken ct = default)
    {
        _dbContext.HumanReviewCases.Update(reviewCase);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<HumanReviewCase> Items, int Total)> SearchAsync(
        HumanReviewSearchCriteria criteria,
        CancellationToken ct = default)
    {
        var query = _dbContext.HumanReviewCases.AsQueryable();

        if (criteria.TenantId.HasValue)
            query = query.Where(x => x.TenantId == criteria.TenantId.Value);

        if (criteria.ApplicationId.HasValue)
            query = query.Where(x => x.ApplicationId == criteria.ApplicationId.Value);

        if (criteria.Status.HasValue)
            query = query.Where(x => x.Status == criteria.Status.Value);

        if (!string.IsNullOrWhiteSpace(criteria.AssignedTo))
            query = query.Where(x => x.AssignedTo == criteria.AssignedTo);

        if (criteria.RiskLevel.HasValue)
            query = query.Where(x => x.RiskLevel == criteria.RiskLevel.Value);

        if (criteria.From.HasValue)
            query = query.Where(x => x.CreatedAt >= criteria.From.Value);

        if (criteria.To.HasValue)
            query = query.Where(x => x.CreatedAt <= criteria.To.Value);

        var total = await query.CountAsync(ct);
        var page = Math.Max(criteria.Page, 1);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
