using Guardrail.Core.Domain.Enums;

namespace Guardrail.API.Models;

// ── Shared sub-models ────────────────────────────────────────────────────────

/// <summary>API-layer data source descriptor.</summary>
public sealed class SourceDescriptorApiModel
{
    /// <summary>Unique identifier of the data source.</summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>Logical type: "sharepoint", "database", "api", "blob", etc.</summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>Tenant that owns this source. May be different from the requesting tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>Trust classification for this source.</summary>
    public SourceTrustLevel TrustLevel { get; init; } = SourceTrustLevel.Internal;

    /// <summary>Optional URI pointing to the source.</summary>
    public string? Uri { get; init; }

    /// <summary>Optional additional metadata.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>API-layer tool call descriptor.</summary>
public sealed class ToolCallApiModel
{
    /// <summary>Canonical name of the tool as registered in the tool registry.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>Parameters passed to the tool by the model.</summary>
    public Dictionary<string, string>? Parameters { get; init; }

    /// <summary>Optional correlation identifier for this specific tool call.</summary>
    public string? CallId { get; init; }

    /// <summary>Whether the model marked this as a high-privilege / destructive operation.</summary>
    public bool IsDestructive { get; init; }
}

// ── Input evaluation ─────────────────────────────────────────────────────────

/// <summary>Request body for POST /api/guardrail/evaluate-input.</summary>
public sealed class EvaluateInputApiRequest
{
    /// <summary>Optional caller-supplied correlation ID. A new UUID is assigned if absent.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The user-supplied prompt to evaluate.</summary>
    public string UserPrompt { get; init; } = string.Empty;

    /// <summary>Optional system prompt provided by the application.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Data sources the model is permitted to access.</summary>
    public List<SourceDescriptorApiModel>? DataSources { get; init; }

    /// <summary>Tools the model is requesting permission to invoke.</summary>
    public List<ToolCallApiModel>? RequestedTools { get; init; }

    /// <summary>Arbitrary key-value metadata forwarded to the audit log.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

// ── Output evaluation ────────────────────────────────────────────────────────

/// <summary>Request body for POST /api/guardrail/evaluate-output.</summary>
public sealed class EvaluateOutputApiRequest
{
    /// <summary>Optional caller-supplied correlation ID.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Execution ID returned by a prior evaluate-input call, for audit linkage.</summary>
    public Guid? InputExecutionId { get; init; }

    /// <summary>Raw model output to evaluate.</summary>
    public string ModelOutput { get; init; } = string.Empty;

    /// <summary>JSON Schema string the output must conform to. Null means no schema enforcement.</summary>
    public string? OutputSchemaJson { get; init; }

    /// <summary>Arbitrary key-value metadata forwarded to the audit log.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

// ── Full evaluation ──────────────────────────────────────────────────────────

/// <summary>Request body for POST /api/guardrail/evaluate-full.</summary>
public sealed class EvaluateFullApiRequest
{
    /// <summary>Optional caller-supplied correlation ID.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The user-supplied prompt to evaluate.</summary>
    public string UserPrompt { get; init; } = string.Empty;

    /// <summary>Optional system prompt provided by the application.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Raw model output to evaluate.</summary>
    public string ModelOutput { get; init; } = string.Empty;

    /// <summary>Data sources the model was permitted to access.</summary>
    public List<SourceDescriptorApiModel>? DataSources { get; init; }

    /// <summary>Tools the model was asked to invoke.</summary>
    public List<ToolCallApiModel>? RequestedTools { get; init; }

    /// <summary>JSON Schema string the output must conform to.</summary>
    public string? OutputSchemaJson { get; init; }

    /// <summary>Arbitrary key-value metadata forwarded to the audit log.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

// ── Policy ───────────────────────────────────────────────────────────────────

/// <summary>Request body for creating or updating a policy profile.</summary>
public sealed class PolicyProfileRequest
{
    /// <summary>Human-readable name for this policy profile.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Target tenant ID. Null means global policy.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Target application ID. Null means tenant-wide.</summary>
    public Guid? ApplicationId { get; init; }

    /// <summary>Business domain: "healthcare", "finance", "legal", "general", etc.</summary>
    public string? Domain { get; init; }

    /// <summary>JSON representation of the policy configuration.</summary>
    public string PolicyJson { get; init; } = "{}";

    /// <summary>UTC timestamp from which this policy is in effect.</summary>
    public DateTimeOffset EffectiveFrom { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp at which this policy expires. Null means no expiry.</summary>
    public DateTimeOffset? EffectiveTo { get; init; }

    /// <summary>Optional parent profile ID for policy inheritance.</summary>
    public Guid? ParentProfileId { get; init; }
}

// ── Human Review ─────────────────────────────────────────────────────────────

/// <summary>Request body for assigning a human review case.</summary>
public sealed class AssignReviewCaseRequest
{
    /// <summary>Identity of the reviewer (email or user ID).</summary>
    public string ReviewerIdentity { get; init; } = string.Empty;
}

/// <summary>Request body for completing a human review case.</summary>
public sealed class CompleteReviewCaseRequest
{
    /// <summary>The reviewer's final enforcement decision.</summary>
    public string FinalDecision { get; init; } = string.Empty;

    /// <summary>Optional free-text notes from the reviewer.</summary>
    public string? Notes { get; init; }
}
