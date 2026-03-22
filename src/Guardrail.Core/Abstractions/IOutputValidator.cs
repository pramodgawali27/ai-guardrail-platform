using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Validates model output against schema constraints, content policies, and redaction requirements.
/// </summary>
public interface IOutputValidator
{
    Task<OutputValidationResult> ValidateAsync(OutputValidationRequest request, CancellationToken ct = default);
}

public class OutputValidationRequest
{
    public string Output { get; set; } = string.Empty;
    public string? OutputSchemaJson { get; set; }
    public ConstraintSet Constraints { get; set; } = ConstraintSet.Default;
    public TenantContext TenantContext { get; set; } = null!;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
