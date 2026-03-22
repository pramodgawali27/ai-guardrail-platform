using System.Collections.Generic;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents an AI application registered under a tenant.
/// </summary>
public sealed class Application : BaseEntity
{
    /// <summary>Foreign key reference to the owning <see cref="Tenant"/> entity.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Unique slug identifier for the application within the tenant (e.g., "claims-assistant").</summary>
    public string ApplicationId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Business domain this application belongs to (e.g., healthcare, legal, finance, general).</summary>
    public string Domain { get; private set; } = "general";

    public bool IsActive { get; private set; } = true;

    /// <summary>Hashed API key used to authenticate requests from this application.</summary>
    public string ApiKey { get; private set; } = string.Empty;

    /// <summary>Key-value settings for application-level configuration overrides.</summary>
    public Dictionary<string, string> Settings { get; private set; } = new();

    private Application() { }

    /// <summary>
    /// Factory method to create a new <see cref="Application"/>.
    /// </summary>
    /// <param name="tenantId">Owning tenant's primary key.</param>
    /// <param name="applicationId">Unique slug within the tenant.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="apiKeyHash">Pre-hashed API key — never the raw key.</param>
    /// <param name="domain">Business domain (defaults to "general").</param>
    /// <param name="description">Optional description.</param>
    /// <param name="settings">Optional initial settings.</param>
    /// <param name="createdBy">Actor performing the creation.</param>
    public static Application Create(
        Guid tenantId,
        string applicationId,
        string name,
        string apiKeyHash,
        string domain = "general",
        string? description = null,
        Dictionary<string, string>? settings = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId, nameof(applicationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKeyHash, nameof(apiKeyHash));

        return new Application
        {
            TenantId = tenantId,
            ApplicationId = applicationId.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Domain = domain.Trim().ToLowerInvariant(),
            ApiKey = apiKeyHash,
            Settings = settings ?? new Dictionary<string, string>(),
            CreatedBy = createdBy,
            IsActive = true
        };
    }

    public void Activate(string updatedBy)
    {
        IsActive = true;
        MarkUpdated(updatedBy);
    }

    public void Deactivate(string updatedBy)
    {
        IsActive = false;
        MarkUpdated(updatedBy);
    }

    public void RotateApiKey(string newApiKeyHash, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newApiKeyHash, nameof(newApiKeyHash));
        ApiKey = newApiKeyHash;
        MarkUpdated(updatedBy);
    }

    public void UpdateSettings(Dictionary<string, string> settings, string updatedBy)
    {
        Settings = settings ?? new Dictionary<string, string>();
        MarkUpdated(updatedBy);
    }

    public void UpdateDomain(string domain, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain, nameof(domain));
        Domain = domain.Trim().ToLowerInvariant();
        MarkUpdated(updatedBy);
    }
}
