using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardrail.Infrastructure.Repositories;

public sealed class PolicyRepository : IPolicyRepository
{
    private readonly GuardrailDbContext _dbContext;

    public PolicyRepository(GuardrailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PolicyProfile?> GetEffectivePolicyAsync(
        Guid tenantId,
        Guid applicationId,
        string? domain = null,
        CancellationToken ct = default)
    {
        var profiles = await GetApplicablePoliciesAsync(tenantId, applicationId, domain, ct);
        return profiles.LastOrDefault();
    }

    public Task<PolicyProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.PolicyProfiles
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PolicyProfile>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbContext.PolicyProfiles
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PolicyProfile>> GetApplicablePoliciesAsync(
        Guid tenantId,
        Guid applicationId,
        string? domain = null,
        CancellationToken ct = default)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? null
            : domain.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        // Fetch all active profiles, then filter and sort in memory.
        // This avoids provider-specific LINQ translation issues (nullable types, enum casts)
        // and is correct for any realistic policy count (<1000).
        var all = await _dbContext.PolicyProfiles.ToListAsync(ct);

        return all
            .Where(x =>
                x.IsActive &&
                x.EffectiveFrom <= now &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > now) &&
                (normalizedDomain == null || x.Domain == null || x.Domain == normalizedDomain) &&
                (x.Scope == PolicyScope.Global ||
                 (x.Scope == PolicyScope.Tenant && x.TenantId == tenantId && x.ApplicationId == null) ||
                 (x.Scope == PolicyScope.Application && x.TenantId == tenantId && x.ApplicationId == applicationId)))
            .OrderBy(x => x.Scope == PolicyScope.Global ? 0 : x.Scope == PolicyScope.Tenant ? 1 : 2)
            .ThenBy(x => string.IsNullOrWhiteSpace(x.Domain) ? 0 : 1)
            .ThenBy(x => x.EffectiveFrom)
            .ThenBy(x => x.Version)
            .ToList();
    }

    public async Task<PolicyProfile> AddAsync(PolicyProfile profile, CancellationToken ct = default)
    {
        _dbContext.PolicyProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateAsync(PolicyProfile profile, CancellationToken ct = default)
    {
        _dbContext.PolicyProfiles.Update(profile);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PolicyRule>> GetRulesForProfileAsync(Guid profileId, CancellationToken ct = default)
        => await _dbContext.PolicyRules
            .Where(x => x.PolicyProfileId == profileId && x.IsEnabled)
            .OrderBy(x => x.Priority)
            .ToListAsync(ct);
}
