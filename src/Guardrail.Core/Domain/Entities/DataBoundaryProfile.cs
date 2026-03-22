using System.Collections.Generic;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Defines the data boundary policy controlling which sources and cross-tenant data flows are permitted.
/// </summary>
public sealed class DataBoundaryProfile : BaseEntity
{
    /// <summary>Foreign key to the owning tenant.</summary>
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Explicit list of source identifiers allowed to be accessed.</summary>
    public List<string> AllowedSourceIds { get; private set; } = new();

    /// <summary>Explicit list of source identifiers that are blocked regardless of other rules.</summary>
    public List<string> DeniedSourceIds { get; private set; } = new();

    /// <summary>Maps source identifiers to their assigned trust levels for risk weighting.</summary>
    public Dictionary<string, SourceTrustLevel> TrustLevels { get; private set; } = new();

    /// <summary>Maximum number of documents/sources that may be included in a single request.</summary>
    public int MaxDocumentsPerRequest { get; private set; } = 10;

    /// <summary>Whether data from other tenants may be included in requests (typically false).</summary>
    public bool CrossTenantAllowed { get; private set; }

    private DataBoundaryProfile() { }

    /// <summary>
    /// Factory method to create a new <see cref="DataBoundaryProfile"/>.
    /// </summary>
    public static DataBoundaryProfile Create(
        Guid tenantId,
        string name,
        string? description = null,
        List<string>? allowedSourceIds = null,
        List<string>? deniedSourceIds = null,
        Dictionary<string, SourceTrustLevel>? trustLevels = null,
        int maxDocumentsPerRequest = 10,
        bool crossTenantAllowed = false,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (maxDocumentsPerRequest < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDocumentsPerRequest), "MaxDocumentsPerRequest must be at least 1.");

        return new DataBoundaryProfile
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            AllowedSourceIds = allowedSourceIds ?? new List<string>(),
            DeniedSourceIds = deniedSourceIds ?? new List<string>(),
            TrustLevels = trustLevels ?? new Dictionary<string, SourceTrustLevel>(),
            MaxDocumentsPerRequest = maxDocumentsPerRequest,
            CrossTenantAllowed = crossTenantAllowed,
            CreatedBy = createdBy
        };
    }

    public void AllowSource(string sourceId, SourceTrustLevel trustLevel, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId, nameof(sourceId));

        if (!AllowedSourceIds.Contains(sourceId))
            AllowedSourceIds.Add(sourceId);

        DeniedSourceIds.Remove(sourceId);
        TrustLevels[sourceId] = trustLevel;
        MarkUpdated(updatedBy);
    }

    public void DenySource(string sourceId, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId, nameof(sourceId));

        if (!DeniedSourceIds.Contains(sourceId))
            DeniedSourceIds.Add(sourceId);

        AllowedSourceIds.Remove(sourceId);
        TrustLevels.Remove(sourceId);
        MarkUpdated(updatedBy);
    }

    public void SetTrustLevel(string sourceId, SourceTrustLevel trustLevel, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId, nameof(sourceId));
        TrustLevels[sourceId] = trustLevel;
        MarkUpdated(updatedBy);
    }

    public void UpdateDocumentLimit(int maxDocumentsPerRequest, string updatedBy)
    {
        if (maxDocumentsPerRequest < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDocumentsPerRequest));

        MaxDocumentsPerRequest = maxDocumentsPerRequest;
        MarkUpdated(updatedBy);
    }

    public void SetCrossTenantPolicy(bool allowed, string updatedBy)
    {
        CrossTenantAllowed = allowed;
        MarkUpdated(updatedBy);
    }

    /// <summary>Returns the effective trust level for a given source, defaulting to Untrusted.</summary>
    public SourceTrustLevel GetTrustLevel(string sourceId)
    {
        return TrustLevels.TryGetValue(sourceId, out var level) ? level : SourceTrustLevel.Untrusted;
    }
}
