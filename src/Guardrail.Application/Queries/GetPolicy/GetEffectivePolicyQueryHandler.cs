using MediatR;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Guardrail.Application.Queries.GetPolicy;

/// <summary>
/// Handles <see cref="GetEffectivePolicyQuery"/> by delegating to the
/// <see cref="IPolicyEngine"/> to resolve and merge the applicable policy hierarchy.
/// </summary>
public sealed class GetEffectivePolicyQueryHandler
    : IRequestHandler<GetEffectivePolicyQuery, EffectivePolicy?>
{
    private readonly IPolicyEngine _policyEngine;
    private readonly ILogger<GetEffectivePolicyQueryHandler> _logger;

    public GetEffectivePolicyQueryHandler(
        IPolicyEngine policyEngine,
        ILogger<GetEffectivePolicyQueryHandler> logger)
    {
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public async Task<EffectivePolicy?> Handle(
        GetEffectivePolicyQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Resolving effective policy for tenant {TenantId}, application {ApplicationId}",
            request.TenantId,
            request.ApplicationId);

        // Build a minimal TenantContext for the policy engine lookup.
        var context = new TenantContext
        {
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            UserId = "system",
            CorrelationId = Guid.NewGuid().ToString()
        };

        try
        {
            var policy = await _policyEngine.ResolveEffectivePolicyAsync(context, cancellationToken);
            return policy;
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "No effective policy found for tenant {TenantId}, application {ApplicationId}",
                request.TenantId,
                request.ApplicationId);
            return null;
        }
    }
}
