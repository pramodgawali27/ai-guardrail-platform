using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Enforces data-boundary policies, validating which sources a request is permitted to access.
/// </summary>
public interface IContextFirewall
{
    Task<ContextFirewallResult> ValidateContextAsync(ContextFirewallRequest request, CancellationToken ct = default);
}

public class ContextFirewallRequest
{
    public TenantContext TenantContext { get; set; } = null!;
    public List<SourceDescriptor> RequestedSources { get; set; } = new();
    public DataBoundaryConfig? BoundaryConfig { get; set; }
}

public class DataBoundaryConfig
{
    public List<string> AllowedSourceIds { get; set; } = new();
    public List<string> DeniedSourceIds { get; set; } = new();
    public List<string> AllowedSourceTypes { get; set; } = new();
    public List<string> DeniedSourceTypes { get; set; } = new();
    public bool CrossTenantAllowed { get; set; }
    public int MaxDocuments { get; set; } = 10;
    public SourceTrustLevel MinimumTrustLevel { get; set; } = SourceTrustLevel.Internal;
}
