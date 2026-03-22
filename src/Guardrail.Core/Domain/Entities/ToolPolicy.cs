using System.Collections.Generic;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Defines the allowed usage policy for a specific AI tool (function/plugin) within a tenant or application.
/// </summary>
public sealed class ToolPolicy : BaseEntity
{
    /// <summary>Foreign key to the owning tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Optional scope restriction to a specific application. Null means tenant-wide.</summary>
    public Guid? ApplicationId { get; private set; }

    public string ToolName { get; private set; } = string.Empty;
    public string? ToolDescription { get; private set; }

    /// <summary>Risk classification of actions this tool can perform.</summary>
    public ActionRiskLevel ActionRisk { get; private set; }

    /// <summary>Whether this tool is permitted to be invoked at all.</summary>
    public bool IsAllowed { get; private set; }

    /// <summary>Whether a human or elevated approval is required before invocation.</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>JSON array of parameter schemas/names that are explicitly permitted.</summary>
    public string AllowedParameters { get; private set; } = "[]";

    /// <summary>JSON array of parameter schemas/names that are explicitly denied.</summary>
    public string DeniedParameters { get; private set; } = "[]";

    /// <summary>List of environments where this tool is permitted to run (e.g., "production", "staging").</summary>
    public List<string> EnvironmentRestrictions { get; private set; } = new();

    private ToolPolicy() { }

    /// <summary>
    /// Factory method to create a new <see cref="ToolPolicy"/>.
    /// </summary>
    public static ToolPolicy Create(
        Guid tenantId,
        string toolName,
        ActionRiskLevel actionRisk,
        bool isAllowed,
        bool requiresApproval = false,
        Guid? applicationId = null,
        string? toolDescription = null,
        string allowedParameters = "[]",
        string deniedParameters = "[]",
        List<string>? environmentRestrictions = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));

        return new ToolPolicy
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            ToolName = toolName.Trim(),
            ToolDescription = toolDescription?.Trim(),
            ActionRisk = actionRisk,
            IsAllowed = isAllowed,
            RequiresApproval = requiresApproval,
            AllowedParameters = allowedParameters,
            DeniedParameters = deniedParameters,
            EnvironmentRestrictions = environmentRestrictions ?? new List<string>(),
            CreatedBy = createdBy
        };
    }

    public void Allow(string updatedBy)
    {
        IsAllowed = true;
        MarkUpdated(updatedBy);
    }

    public void Deny(string updatedBy)
    {
        IsAllowed = false;
        MarkUpdated(updatedBy);
    }

    public void SetApprovalRequired(bool required, string updatedBy)
    {
        RequiresApproval = required;
        MarkUpdated(updatedBy);
    }

    public void UpdateParameterPolicies(string allowedParameters, string deniedParameters, string updatedBy)
    {
        AllowedParameters = allowedParameters;
        DeniedParameters = deniedParameters;
        MarkUpdated(updatedBy);
    }

    public void SetEnvironmentRestrictions(List<string> environments, string updatedBy)
    {
        EnvironmentRestrictions = environments ?? new List<string>();
        MarkUpdated(updatedBy);
    }

    public void UpdateRiskLevel(ActionRiskLevel risk, string updatedBy)
    {
        ActionRisk = risk;
        MarkUpdated(updatedBy);
    }
}
