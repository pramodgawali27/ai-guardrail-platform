using System.Collections.Generic;

namespace Guardrail.Core.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    /// <summary>Unique slug identifier (e.g., "acme-corp").</summary>
    public string TenantId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Compliance profile identifier (e.g., HIPAA, SOC2, GDPR).</summary>
    public string ComplianceProfile { get; private set; } = "default";

    /// <summary>Number of days to retain audit data.</summary>
    public int RetentionDays { get; private set; } = 90;

    /// <summary>Rate limit for API requests per minute.</summary>
    public int MaxRequestsPerMinute { get; private set; } = 1000;

    /// <summary>List of allowed geographic regions (e.g., "us-east-1", "eu-west-1").</summary>
    public List<string> AllowedRegions { get; private set; } = new();

    /// <summary>Key-value settings for tenant-level configuration overrides.</summary>
    public Dictionary<string, string> Settings { get; private set; } = new();

    private Tenant() { }

    /// <summary>
    /// Factory method to create a new <see cref="Tenant"/> with validated inputs.
    /// </summary>
    public static Tenant Create(
        string tenantId,
        string name,
        string? description = null,
        string complianceProfile = "default",
        int retentionDays = 90,
        int maxRequestsPerMinute = 1000,
        List<string>? allowedRegions = null,
        Dictionary<string, string>? settings = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "RetentionDays must be at least 1.");

        if (maxRequestsPerMinute < 1)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute), "MaxRequestsPerMinute must be at least 1.");

        return new Tenant
        {
            TenantId = tenantId.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            ComplianceProfile = complianceProfile.Trim(),
            RetentionDays = retentionDays,
            MaxRequestsPerMinute = maxRequestsPerMinute,
            AllowedRegions = allowedRegions ?? new List<string>(),
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

    public void UpdateSettings(Dictionary<string, string> settings, string updatedBy)
    {
        Settings = settings ?? new Dictionary<string, string>();
        MarkUpdated(updatedBy);
    }

    public void UpdateRateLimit(int maxRequestsPerMinute, string updatedBy)
    {
        if (maxRequestsPerMinute < 1)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute));

        MaxRequestsPerMinute = maxRequestsPerMinute;
        MarkUpdated(updatedBy);
    }

    public void UpdateRetentionPolicy(int retentionDays, string updatedBy)
    {
        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays));

        RetentionDays = retentionDays;
        MarkUpdated(updatedBy);
    }

    public void SetAllowedRegions(List<string> regions, string updatedBy)
    {
        AllowedRegions = regions ?? new List<string>();
        MarkUpdated(updatedBy);
    }
}
