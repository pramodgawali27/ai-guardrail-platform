using MediatR;
using Guardrail.Core.Abstractions;

namespace Guardrail.Application.Queries.GetPolicy;

/// <summary>
/// MediatR query to retrieve the effective merged policy for a given tenant and application.
/// </summary>
public record GetEffectivePolicyQuery : IRequest<EffectivePolicy?>
{
    /// <summary>Tenant identifier.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Application identifier within the tenant.</summary>
    public Guid ApplicationId { get; init; }

    /// <summary>Optional business domain filter (e.g., "healthcare", "finance").</summary>
    public string? Domain { get; init; }
}
