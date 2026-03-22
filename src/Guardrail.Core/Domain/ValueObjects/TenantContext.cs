namespace Guardrail.Core.Domain.ValueObjects;

/// <summary>
/// Carries per-request tenant, application, and user identity information
/// through the entire guardrail evaluation pipeline.
/// </summary>
public sealed record TenantContext
{
    public Guid TenantId { get; init; }
    public string TenantSlug { get; init; } = string.Empty;
    public Guid ApplicationId { get; init; }
    public string ApplicationSlug { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = "production";
    public string? Region { get; init; }
    public Dictionary<string, string> Claims { get; init; } = new();
}
