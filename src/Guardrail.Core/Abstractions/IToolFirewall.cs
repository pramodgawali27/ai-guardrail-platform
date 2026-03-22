using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Enforces tool-invocation policies, determining which tools a model is permitted to call.
/// </summary>
public interface IToolFirewall
{
    Task<ToolValidationResult> ValidateToolsAsync(ToolValidationRequest request, CancellationToken ct = default);
}

public class ToolValidationRequest
{
    public TenantContext TenantContext { get; set; } = null!;
    public List<ToolCallDescriptor> RequestedTools { get; set; } = new();
    public EffectivePolicy Policy { get; set; } = null!;
}

public class ToolCallDescriptor
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? ToolVersion { get; set; }
}
