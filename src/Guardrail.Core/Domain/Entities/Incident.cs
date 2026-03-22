using System.Collections.Generic;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents a security or compliance incident raised from an <see cref="AuditEvent"/>.
/// </summary>
public sealed class Incident : BaseEntity
{
    /// <summary>FK to the <see cref="AuditEvent"/> that triggered this incident.</summary>
    public Guid AuditEventId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public RuleSeverity Severity { get; private set; }
    public ReviewStatus Status { get; private set; }

    /// <summary>Identity of the reviewer/responder assigned to this incident. Null if unassigned.</summary>
    public string? AssignedTo { get; private set; }

    /// <summary>UTC timestamp when the incident was resolved. Null if still open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Free-text description of the resolution or outcome.</summary>
    public string? Resolution { get; private set; }

    /// <summary>Arbitrary tags for categorization and routing (e.g., "pii-leak", "jailbreak-attempt").</summary>
    public List<string> Tags { get; private set; } = new();

    private Incident() { }

    /// <summary>
    /// Factory method to create a new <see cref="Incident"/>.
    /// </summary>
    public static Incident Create(
        Guid auditEventId,
        Guid tenantId,
        string title,
        string description,
        RuleSeverity severity,
        List<string>? tags = null,
        string? createdBy = null)
    {
        if (auditEventId == Guid.Empty)
            throw new ArgumentException("AuditEventId must not be empty.", nameof(auditEventId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        return new Incident
        {
            AuditEventId = auditEventId,
            TenantId = tenantId,
            Title = title.Trim(),
            Description = description.Trim(),
            Severity = severity,
            Status = ReviewStatus.Pending,
            Tags = tags ?? new List<string>(),
            CreatedBy = createdBy
        };
    }

    /// <summary>Assigns the incident to a reviewer and moves it to InReview status.</summary>
    public void Assign(string assignee, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee, nameof(assignee));

        AssignedTo = assignee;
        Status = ReviewStatus.InReview;
        MarkUpdated(updatedBy);
    }

    /// <summary>Resolves the incident with a resolution summary.</summary>
    public void Resolve(string resolution, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution, nameof(resolution));

        Resolution = resolution.Trim();
        ResolvedAt = DateTimeOffset.UtcNow;
        Status = ReviewStatus.Closed;
        MarkUpdated(updatedBy);
    }

    /// <summary>Escalates the incident to a higher tier.</summary>
    public void Escalate(string updatedBy)
    {
        Status = ReviewStatus.Escalated;
        MarkUpdated(updatedBy);
    }

    /// <summary>Updates the severity classification of the incident.</summary>
    public void UpdateSeverity(RuleSeverity severity, string updatedBy)
    {
        Severity = severity;
        MarkUpdated(updatedBy);
    }

    public void AddTag(string tag, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));

        if (!Tags.Contains(tag))
            Tags.Add(tag.Trim());

        MarkUpdated(updatedBy);
    }
}
