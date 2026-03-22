using Guardrail.Core.Abstractions;

namespace Guardrail.Infrastructure.Firewalls;

public sealed class DefaultContextFirewall : IContextFirewall
{
    public Task<ContextFirewallResult> ValidateContextAsync(ContextFirewallRequest request, CancellationToken ct = default)
    {
        var boundary = request.BoundaryConfig ?? new DataBoundaryConfig();
        var result = new ContextFirewallResult
        {
            AllSourcesAllowed = true
        };

        foreach (var source in request.RequestedSources.Take(boundary.MaxDocuments))
        {
            var blocked =
                (!boundary.CrossTenantAllowed &&
                 !string.IsNullOrWhiteSpace(source.TenantId) &&
                 !string.Equals(source.TenantId, request.TenantContext.TenantId.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                boundary.DeniedSourceIds.Contains(source.SourceId, StringComparer.OrdinalIgnoreCase) ||
                boundary.DeniedSourceTypes.Contains(source.SourceType, StringComparer.OrdinalIgnoreCase) ||
                (boundary.AllowedSourceIds.Count > 0 &&
                 !boundary.AllowedSourceIds.Contains(source.SourceId, StringComparer.OrdinalIgnoreCase)) ||
                (boundary.AllowedSourceTypes.Count > 0 &&
                 !boundary.AllowedSourceTypes.Contains(source.SourceType, StringComparer.OrdinalIgnoreCase)) ||
                source.TrustLevel < boundary.MinimumTrustLevel;

            if (blocked)
            {
                result.AllSourcesAllowed = false;
                result.BlockedSources.Add(source.SourceId);

                if (!boundary.CrossTenantAllowed &&
                    !string.IsNullOrWhiteSpace(source.TenantId) &&
                    !string.Equals(source.TenantId, request.TenantContext.TenantId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    result.CrossTenantAttemptDetected = true;
                }
            }
            else
            {
                result.AllowedSources.Add(source.SourceId);
            }
        }

        if (request.RequestedSources.Count > boundary.MaxDocuments)
        {
            result.AllSourcesAllowed = false;
            result.BlockedSources.AddRange(
                request.RequestedSources
                    .Skip(boundary.MaxDocuments)
                    .Select(x => x.SourceId));
        }

        result.ContextRiskScore = result.CrossTenantAttemptDetected
            ? 1.0m
            : result.BlockedSources.Count == 0
                ? 0m
                : Math.Min(0.8m, (decimal)result.BlockedSources.Count / Math.Max(request.RequestedSources.Count, 1));

        return Task.FromResult(result);
    }
}
