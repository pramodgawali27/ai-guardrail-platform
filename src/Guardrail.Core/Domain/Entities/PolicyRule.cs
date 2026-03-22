using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// A single evaluable rule within a <see cref="PolicyProfile"/>.
/// </summary>
public sealed class PolicyRule : BaseEntity
{
    /// <summary>Foreign key to the owning <see cref="PolicyProfile"/>.</summary>
    public Guid PolicyProfileId { get; private set; }

    /// <summary>Machine-readable unique key for the rule (e.g., "no-pii-in-output").</summary>
    public string RuleKey { get; private set; } = string.Empty;

    public string RuleName { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public RuleSeverity Severity { get; private set; }
    public ContentCategory Category { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    /// <summary>Execution priority — lower numbers run first.</summary>
    public int Priority { get; private set; } = 100;

    /// <summary>JSON-encoded conditions that must be satisfied to trigger this rule.</summary>
    public string Conditions { get; private set; } = "{}";

    /// <summary>JSON-encoded actions to execute when the rule triggers.</summary>
    public string Actions { get; private set; } = "{}";

    /// <summary>Whether a downstream system or tenant-level policy may override this rule.</summary>
    public bool OverrideAllowed { get; private set; }

    private PolicyRule() { }

    /// <summary>
    /// Factory method to create a new <see cref="PolicyRule"/>.
    /// </summary>
    public static PolicyRule Create(
        Guid policyProfileId,
        string ruleKey,
        string ruleName,
        RuleSeverity severity,
        ContentCategory category,
        string conditions,
        string actions,
        string? description = null,
        int priority = 100,
        bool overrideAllowed = false,
        string? createdBy = null)
    {
        if (policyProfileId == Guid.Empty)
            throw new ArgumentException("PolicyProfileId must not be empty.", nameof(policyProfileId));

        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey, nameof(ruleKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName, nameof(ruleName));
        ArgumentException.ThrowIfNullOrWhiteSpace(conditions, nameof(conditions));
        ArgumentException.ThrowIfNullOrWhiteSpace(actions, nameof(actions));

        return new PolicyRule
        {
            PolicyProfileId = policyProfileId,
            RuleKey = ruleKey.Trim().ToLowerInvariant(),
            RuleName = ruleName.Trim(),
            Description = description?.Trim(),
            Severity = severity,
            Category = category,
            Conditions = conditions,
            Actions = actions,
            Priority = priority,
            OverrideAllowed = overrideAllowed,
            IsEnabled = true,
            CreatedBy = createdBy
        };
    }

    public void Enable(string updatedBy)
    {
        IsEnabled = true;
        MarkUpdated(updatedBy);
    }

    public void Disable(string updatedBy)
    {
        IsEnabled = false;
        MarkUpdated(updatedBy);
    }

    public void UpdateConditions(string conditions, string actions, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conditions, nameof(conditions));
        ArgumentException.ThrowIfNullOrWhiteSpace(actions, nameof(actions));

        Conditions = conditions;
        Actions = actions;
        MarkUpdated(updatedBy);
    }

    public void UpdatePriority(int priority, string updatedBy)
    {
        Priority = priority;
        MarkUpdated(updatedBy);
    }

    public void UpdateSeverity(RuleSeverity severity, string updatedBy)
    {
        Severity = severity;
        MarkUpdated(updatedBy);
    }
}
