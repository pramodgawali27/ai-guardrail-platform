using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Records the execution context and outcome of a single guardrail evaluation.
/// Raw payloads are NEVER stored — only hashes and risk metadata.
/// </summary>
public sealed class GuardrailExecution : BaseEntity
{
    /// <summary>Foreign key to the owning tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Foreign key to the application that initiated the request.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>Identifier of the end-user who submitted the request.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Logical session grouping multiple correlated requests.</summary>
    public string SessionId { get; private set; } = string.Empty;

    /// <summary>Distributed tracing correlation identifier.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the original request payload.
    /// The raw payload is NEVER persisted for privacy and compliance.
    /// </summary>
    public string RequestPayloadHash { get; private set; } = string.Empty;

    /// <summary>Risk level assessed for the inbound request/prompt.</summary>
    public RiskLevel InputRiskLevel { get; private set; }

    /// <summary>Risk level assessed for the outbound model response. Null if not yet evaluated.</summary>
    public RiskLevel? OutputRiskLevel { get; private set; }

    /// <summary>The guardrail's final enforcement decision for this execution.</summary>
    public DecisionType FinalDecision { get; private set; }

    /// <summary>Total wall-clock duration of the guardrail pipeline in milliseconds.</summary>
    public long ExecutionDurationMs { get; private set; }

    /// <summary>Optional FK to the policy profile that governed this evaluation.</summary>
    public Guid? PolicyProfileId { get; private set; }

    /// <summary>UTC timestamp when the request entered the guardrail pipeline.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>UTC timestamp when the evaluation pipeline completed. Null if still in-flight.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private GuardrailExecution() { }

    /// <summary>
    /// Factory method to create a new <see cref="GuardrailExecution"/> at pipeline entry.
    /// </summary>
    public static GuardrailExecution Create(
        Guid tenantId,
        Guid applicationId,
        string userId,
        string sessionId,
        string correlationId,
        string requestPayloadHash,
        Guid? policyProfileId = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId must not be empty.", nameof(applicationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId, nameof(correlationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPayloadHash, nameof(requestPayloadHash));

        return new GuardrailExecution
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserId = userId,
            SessionId = sessionId,
            CorrelationId = correlationId,
            RequestPayloadHash = requestPayloadHash,
            PolicyProfileId = policyProfileId,
            InputRiskLevel = RiskLevel.None,
            FinalDecision = DecisionType.Allow,
            ProcessedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Records the final outcome after the full evaluation pipeline completes.</summary>
    public void Complete(
        RiskLevel inputRiskLevel,
        RiskLevel? outputRiskLevel,
        DecisionType decision,
        long durationMs,
        string updatedBy)
    {
        InputRiskLevel = inputRiskLevel;
        OutputRiskLevel = outputRiskLevel;
        FinalDecision = decision;
        ExecutionDurationMs = durationMs;
        CompletedAt = DateTimeOffset.UtcNow;
        MarkUpdated(updatedBy);
    }

    /// <summary>Updates only the input-phase risk assessment.</summary>
    public void SetInputRisk(RiskLevel riskLevel, string updatedBy)
    {
        InputRiskLevel = riskLevel;
        MarkUpdated(updatedBy);
    }

    /// <summary>Updates only the output-phase risk assessment.</summary>
    public void SetOutputRisk(RiskLevel riskLevel, string updatedBy)
    {
        OutputRiskLevel = riskLevel;
        MarkUpdated(updatedBy);
    }

    /// <summary>Overrides the final decision (e.g., after human review).</summary>
    public void OverrideDecision(DecisionType decision, string updatedBy)
    {
        FinalDecision = decision;
        MarkUpdated(updatedBy);
    }
}
