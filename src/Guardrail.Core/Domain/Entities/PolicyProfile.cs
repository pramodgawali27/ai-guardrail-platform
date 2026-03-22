using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Defines a named set of guardrail policies applicable to one or more tenants/applications.
/// Supports hierarchical inheritance via <see cref="ParentProfileId"/>.
/// </summary>
public sealed class PolicyProfile : BaseEntity
{
    /// <summary>Optional tenant scope. Null means global.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Optional application scope. Null means tenant-wide or global.</summary>
    public Guid? ApplicationId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public PolicyScope Scope { get; private set; }

    /// <summary>Business domain this profile targets (e.g., healthcare, legal, finance, general).</summary>
    public string? Domain { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Monotonically increasing version counter, incremented on each policy update.</summary>
    public int Version { get; private set; } = 1;

    /// <summary>Optional reference to a parent profile for policy inheritance.</summary>
    public Guid? ParentProfileId { get; private set; }

    /// <summary>Full JSON representation of the policy configuration.</summary>
    public string PolicyJson { get; private set; } = "{}";

    /// <summary>UTC timestamp from which this policy is in effect.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>UTC timestamp at which this policy expires. Null means no expiry.</summary>
    public DateTimeOffset? EffectiveTo { get; private set; }

    private PolicyProfile() { }

    /// <summary>
    /// Factory method to create a new <see cref="PolicyProfile"/>.
    /// </summary>
    public static PolicyProfile Create(
        string name,
        PolicyScope scope,
        DateTimeOffset effectiveFrom,
        Guid? tenantId = null,
        Guid? applicationId = null,
        string? domain = null,
        string? description = null,
        string? policyJson = null,
        Guid? parentProfileId = null,
        DateTimeOffset? effectiveTo = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (effectiveTo.HasValue && effectiveTo.Value <= effectiveFrom)
            throw new ArgumentException("EffectiveTo must be later than EffectiveFrom.", nameof(effectiveTo));

        return new PolicyProfile
        {
            Name = name.Trim(),
            Scope = scope,
            TenantId = tenantId,
            ApplicationId = applicationId,
            Domain = domain?.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            ParentProfileId = parentProfileId,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            PolicyJson = string.IsNullOrWhiteSpace(policyJson) ? "{}" : policyJson,
            Version = 1,
            IsActive = true,
            CreatedBy = createdBy
        };
    }

    /// <summary>
    /// Replaces the policy JSON and increments the version counter.
    /// </summary>
    public void SetPolicyJson(string policyJson, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyJson, nameof(policyJson));

        PolicyJson = policyJson;
        Version++;
        MarkUpdated(updatedBy);
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

    public void SetEffectivePeriod(DateTimeOffset from, DateTimeOffset? to, string updatedBy)
    {
        if (to.HasValue && to.Value <= from)
            throw new ArgumentException("EffectiveTo must be later than EffectiveFrom.");

        EffectiveFrom = from;
        EffectiveTo = to;
        MarkUpdated(updatedBy);
    }

    public void InheritFrom(Guid parentProfileId, string updatedBy)
    {
        if (parentProfileId == Id)
            throw new InvalidOperationException("A profile cannot inherit from itself.");

        ParentProfileId = parentProfileId;
        MarkUpdated(updatedBy);
    }

    public bool IsCurrentlyEffective()
    {
        var now = DateTimeOffset.UtcNow;
        return IsActive && now >= EffectiveFrom && (!EffectiveTo.HasValue || now < EffectiveTo.Value);
    }
}
