using System.Collections.Generic;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Immutable audit record for a guardrail-related event.
/// Raw prompts and responses are NEVER stored — only safe summaries and metadata.
/// </summary>
public sealed class AuditEvent : BaseEntity
{
    /// <summary>Optional FK to the <see cref="GuardrailExecution"/> that produced this event.</summary>
    public Guid? ExecutionId { get; private set; }

    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }

    /// <summary>Identifier of the end-user associated with this event.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Machine-readable event type (e.g., "InputBlocked", "OutputRedacted", "PolicyViolation").</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Grouping category for the event (e.g., "Security", "Privacy", "Compliance").</summary>
    public string EventCategory { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Safe, non-sensitive textual summary of the request/response for audit purposes.
    /// Must NOT contain PII, PHI, or raw user prompts.
    /// </summary>
    public string SafePayloadSummary { get; private set; } = string.Empty;

    public RiskLevel RiskLevel { get; private set; }
    public DecisionType? Decision { get; private set; }

    /// <summary>Distributed tracing correlation identifier.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>Arbitrary key-value tags for filtering and routing (e.g., region, environment).</summary>
    public Dictionary<string, string> Tags { get; private set; } = new();

    /// <summary>Whether this event has been classified as a security/compliance incident.</summary>
    public bool IsIncident { get; private set; }

    private AuditEvent() { }

    /// <summary>
    /// Factory method to create a new <see cref="AuditEvent"/>.
    /// </summary>
    public static AuditEvent Create(
        Guid tenantId,
        Guid applicationId,
        string userId,
        string eventType,
        string eventCategory,
        string description,
        string safePayloadSummary,
        RiskLevel riskLevel,
        string correlationId,
        Guid? executionId = null,
        DecisionType? decision = null,
        bool isIncident = false,
        Dictionary<string, string>? tags = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId must not be empty.", nameof(applicationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventCategory, nameof(eventCategory));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId, nameof(correlationId));

        return new AuditEvent
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserId = userId,
            EventType = eventType.Trim(),
            EventCategory = eventCategory.Trim(),
            Description = description.Trim(),
            SafePayloadSummary = safePayloadSummary,
            RiskLevel = riskLevel,
            Decision = decision,
            CorrelationId = correlationId,
            IsIncident = isIncident,
            Tags = tags ?? new Dictionary<string, string>(),
            CreatedBy = createdBy
        };
    }

    /// <summary>Escalates this audit event to incident status.</summary>
    public void MarkAsIncident(string updatedBy)
    {
        IsIncident = true;
        MarkUpdated(updatedBy);
    }

    /// <summary>Adds or updates a tag on the audit event.</summary>
    public void AddTag(string key, string value, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        Tags[key] = value;
        MarkUpdated(updatedBy);
    }
}
