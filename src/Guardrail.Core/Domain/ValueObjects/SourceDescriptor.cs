using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.ValueObjects;

/// <summary>
/// Describes a data source submitted with a request.
/// Used by the Context Firewall to enforce source-level access and trust controls.
/// </summary>
public sealed record SourceDescriptor
{
    public string SourceId { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public SourceTrustLevel TrustLevel { get; init; }
    public string? Uri { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
