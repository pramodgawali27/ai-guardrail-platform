using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents a case queued for human review when automated guardrails cannot make a confident decision.
/// </summary>
public sealed class HumanReviewCase : BaseEntity
{
    /// <summary>FK to the <see cref="GuardrailExecution"/> that triggered this review.</summary>
    public Guid ExecutionId { get; private set; }

    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }

    /// <summary>Human-readable reason why this case was escalated for review.</summary>
    public string ReviewReason { get; private set; } = string.Empty;

    /// <summary>Risk level at the time of escalation.</summary>
    public RiskLevel RiskLevel { get; private set; }

    /// <summary>The automated system's initial decision before human review.</summary>
    public DecisionType Decision { get; private set; }

    /// <summary>
    /// Safe, non-sensitive summary of the context for the reviewer.
    /// Must NOT contain raw prompts, PII, or PHI.
    /// </summary>
    public string SafeContextSummary { get; private set; } = string.Empty;

    /// <summary>Identity of the human reviewer assigned to this case. Null if unassigned.</summary>
    public string? AssignedTo { get; private set; }

    public ReviewStatus Status { get; private set; }

    /// <summary>Free-text notes added by the human reviewer.</summary>
    public string? ReviewNotes { get; private set; }

    /// <summary>UTC timestamp when the review was completed.</summary>
    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>Identity of the person who completed the review.</summary>
    public string? ReviewedBy { get; private set; }

    /// <summary>The human reviewer's final enforcement decision, overriding the automated decision.</summary>
    public DecisionType? FinalDecision { get; private set; }

    private HumanReviewCase() { }

    /// <summary>
    /// Factory method to create a new <see cref="HumanReviewCase"/>.
    /// </summary>
    public static HumanReviewCase Create(
        Guid executionId,
        Guid tenantId,
        Guid applicationId,
        string reviewReason,
        RiskLevel riskLevel,
        DecisionType decision,
        string safeContextSummary,
        string? createdBy = null)
    {
        if (executionId == Guid.Empty)
            throw new ArgumentException("ExecutionId must not be empty.", nameof(executionId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId must not be empty.", nameof(applicationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(reviewReason, nameof(reviewReason));

        return new HumanReviewCase
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            ApplicationId = applicationId,
            ReviewReason = reviewReason.Trim(),
            RiskLevel = riskLevel,
            Decision = decision,
            SafeContextSummary = safeContextSummary,
            Status = ReviewStatus.Pending,
            CreatedBy = createdBy
        };
    }

    /// <summary>
    /// Assigns this review case to a specific reviewer, transitioning it to InReview status.
    /// </summary>
    public void Assign(string reviewerIdentity, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerIdentity, nameof(reviewerIdentity));

        if (Status == ReviewStatus.Closed || Status == ReviewStatus.Approved || Status == ReviewStatus.Rejected)
            throw new InvalidOperationException($"Cannot assign a case with status '{Status}'.");

        AssignedTo = reviewerIdentity;
        Status = ReviewStatus.InReview;
        MarkUpdated(updatedBy);
    }

    /// <summary>
    /// Completes the human review with a final decision and optional notes.
    /// </summary>
    public void Complete(
        DecisionType finalDecision,
        string reviewedBy,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy, nameof(reviewedBy));

        if (Status == ReviewStatus.Closed)
            throw new InvalidOperationException("Review case is already closed.");

        FinalDecision = finalDecision;
        ReviewedBy = reviewedBy;
        ReviewNotes = notes?.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;

        Status = finalDecision == DecisionType.Block || finalDecision == DecisionType.Redact
            ? ReviewStatus.Rejected
            : ReviewStatus.Approved;

        MarkUpdated(reviewedBy);
    }

    /// <summary>Escalates this review case to a higher-tier reviewer.</summary>
    public void Escalate(string escalatedBy, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy, nameof(escalatedBy));

        Status = ReviewStatus.Escalated;
        ReviewNotes = string.IsNullOrWhiteSpace(ReviewNotes)
            ? $"Escalated: {reason}"
            : $"{ReviewNotes} | Escalated: {reason}";

        MarkUpdated(escalatedBy);
    }
}
