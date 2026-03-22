using System.Collections.Generic;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Defines the allowed configuration for a specific AI model within a tenant.
/// </summary>
public sealed class ModelProfile : BaseEntity
{
    /// <summary>Foreign key to the owning tenant.</summary>
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Model provider (e.g., "OpenAI", "Anthropic", "Azure", "Google").</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Provider-specific model identifier (e.g., "gpt-4o", "claude-3-5-sonnet").</summary>
    public string ModelId { get; private set; } = string.Empty;

    /// <summary>Maximum token budget allowed per request for this model.</summary>
    public int MaxTokens { get; private set; } = 4096;

    /// <summary>Temperature setting controlling output randomness (0.0 – 2.0).</summary>
    public decimal Temperature { get; private set; } = 0.7m;

    /// <summary>Capabilities this model is explicitly permitted to use (e.g., "tool_use", "vision").</summary>
    public List<string> AllowedCapabilities { get; private set; } = new();

    /// <summary>Capabilities this model is explicitly prohibited from using.</summary>
    public List<string> RestrictedCapabilities { get; private set; } = new();

    /// <summary>Whether this is the default model profile for the tenant.</summary>
    public bool IsDefault { get; private set; }

    private ModelProfile() { }

    /// <summary>
    /// Factory method to create a new <see cref="ModelProfile"/>.
    /// </summary>
    public static ModelProfile Create(
        Guid tenantId,
        string name,
        string provider,
        string modelId,
        int maxTokens = 4096,
        decimal temperature = 0.7m,
        List<string>? allowedCapabilities = null,
        List<string>? restrictedCapabilities = null,
        bool isDefault = false,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(provider, nameof(provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId, nameof(modelId));

        if (maxTokens < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "MaxTokens must be at least 1.");

        if (temperature < 0m || temperature > 2m)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 2.0.");

        return new ModelProfile
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Provider = provider.Trim(),
            ModelId = modelId.Trim(),
            MaxTokens = maxTokens,
            Temperature = temperature,
            AllowedCapabilities = allowedCapabilities ?? new List<string>(),
            RestrictedCapabilities = restrictedCapabilities ?? new List<string>(),
            IsDefault = isDefault,
            CreatedBy = createdBy
        };
    }

    public void SetAsDefault(string updatedBy)
    {
        IsDefault = true;
        MarkUpdated(updatedBy);
    }

    public void UnsetDefault(string updatedBy)
    {
        IsDefault = false;
        MarkUpdated(updatedBy);
    }

    public void UpdateTemperature(decimal temperature, string updatedBy)
    {
        if (temperature < 0m || temperature > 2m)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 2.0.");

        Temperature = temperature;
        MarkUpdated(updatedBy);
    }

    public void UpdateTokenLimit(int maxTokens, string updatedBy)
    {
        if (maxTokens < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTokens));

        MaxTokens = maxTokens;
        MarkUpdated(updatedBy);
    }

    public void UpdateCapabilities(List<string> allowed, List<string> restricted, string updatedBy)
    {
        AllowedCapabilities = allowed ?? new List<string>();
        RestrictedCapabilities = restricted ?? new List<string>();
        MarkUpdated(updatedBy);
    }
}
