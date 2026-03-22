using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Records the outcome of a redaction operation applied to model output during a guardrail evaluation.
/// Raw or redacted content is NEVER stored — only hashes and safe metadata.
/// </summary>
public sealed class RedactionResult : BaseEntity
{
    /// <summary>FK to the <see cref="GuardrailExecution"/> that triggered the redaction.</summary>
    public Guid ExecutionId { get; private set; }

    /// <summary>SHA-256 hash of the original (pre-redaction) content.</summary>
    public string OriginalHash { get; private set; } = string.Empty;

    /// <summary>SHA-256 hash of the redacted content for integrity verification.</summary>
    public string RedactedContentHash { get; private set; } = string.Empty;

    /// <summary>Number of individual redaction operations applied.</summary>
    public int RedactionsApplied { get; private set; }

    /// <summary>
    /// JSON-encoded metadata describing what was redacted (e.g., types, counts, positions).
    /// Must NOT contain any actual redacted values — only safe structural metadata.
    /// </summary>
    public string RedactionDetails { get; private set; } = "[]";

    /// <summary>The redaction strategy applied (Mask, Replace, Remove, Hash).</summary>
    public RedactionStrategy Strategy { get; private set; }

    /// <summary>UTC timestamp when the redaction was performed.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    private RedactionResult() { }

    /// <summary>
    /// Factory method to create a new <see cref="RedactionResult"/>.
    /// </summary>
    public static RedactionResult Create(
        Guid executionId,
        string originalHash,
        string redactedContentHash,
        int redactionsApplied,
        RedactionStrategy strategy,
        string redactionDetails = "[]",
        string? createdBy = null)
    {
        if (executionId == Guid.Empty)
            throw new ArgumentException("ExecutionId must not be empty.", nameof(executionId));

        ArgumentException.ThrowIfNullOrWhiteSpace(originalHash, nameof(originalHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(redactedContentHash, nameof(redactedContentHash));

        if (redactionsApplied < 0)
            throw new ArgumentOutOfRangeException(nameof(redactionsApplied), "RedactionsApplied cannot be negative.");

        return new RedactionResult
        {
            ExecutionId = executionId,
            OriginalHash = originalHash,
            RedactedContentHash = redactedContentHash,
            RedactionsApplied = redactionsApplied,
            Strategy = strategy,
            RedactionDetails = redactionDetails,
            ProcessedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Indicates whether any redactions were actually applied.</summary>
    public bool HasRedactions => RedactionsApplied > 0;

    /// <summary>Verifies that the original and redacted hashes differ, confirming modifications were made.</summary>
    public bool ContentWasModified => !string.Equals(OriginalHash, RedactedContentHash, StringComparison.OrdinalIgnoreCase);
}
