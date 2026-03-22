using Guardrail.Core.Domain.Entities;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Persistence contract for <see cref="PolicyProfile"/> and associated <see cref="PolicyRule"/> records.
/// </summary>
public interface IPolicyRepository
{
    Task<PolicyProfile?> GetEffectivePolicyAsync(Guid tenantId, Guid applicationId, string? domain = null, CancellationToken ct = default);
    Task<PolicyProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyProfile>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyProfile>> GetApplicablePoliciesAsync(Guid tenantId, Guid applicationId, string? domain = null, CancellationToken ct = default);
    Task<PolicyProfile> AddAsync(PolicyProfile profile, CancellationToken ct = default);
    Task UpdateAsync(PolicyProfile profile, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyRule>> GetRulesForProfileAsync(Guid profileId, CancellationToken ct = default);
}
