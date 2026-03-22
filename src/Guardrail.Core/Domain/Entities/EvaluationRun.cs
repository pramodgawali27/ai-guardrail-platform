using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents a batch evaluation run that tests a policy profile against a dataset of cases.
/// Used for offline policy validation, regression testing, and red-teaming.
/// </summary>
public sealed class EvaluationRun : BaseEntity
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>FK to the <see cref="EvaluationDataset"/> used for this run.</summary>
    public Guid DatasetId { get; private set; }

    /// <summary>Optional FK to the <see cref="PolicyProfile"/> being evaluated. Null means default policy.</summary>
    public Guid? PolicyProfileId { get; private set; }

    public EvaluationStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public int TotalCases { get; private set; }
    public int PassedCases { get; private set; }
    public int FailedCases { get; private set; }

    /// <summary>Cases where the guardrail blocked/flagged content that was actually safe (over-blocking).</summary>
    public int FalsePositives { get; private set; }

    /// <summary>Cases where the guardrail allowed content that should have been blocked (under-blocking).</summary>
    public int FalseNegatives { get; private set; }

    /// <summary>Mean evaluation latency in milliseconds across all cases in the run.</summary>
    public double AverageLatencyMs { get; private set; }

    /// <summary>JSON-serialized summary statistics and metadata for the completed run.</summary>
    public string SummaryJson { get; private set; } = "{}";

    private EvaluationRun() { }

    /// <summary>
    /// Factory method to create a new <see cref="EvaluationRun"/>.
    /// </summary>
    public static EvaluationRun Create(
        Guid tenantId,
        string name,
        Guid datasetId,
        string? description = null,
        Guid? policyProfileId = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        if (datasetId == Guid.Empty)
            throw new ArgumentException("DatasetId must not be empty.", nameof(datasetId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        return new EvaluationRun
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            DatasetId = datasetId,
            PolicyProfileId = policyProfileId,
            Status = EvaluationStatus.Pending,
            CreatedBy = createdBy
        };
    }

    /// <summary>Transitions the run to Running status and records the start time.</summary>
    public void Start(string updatedBy)
    {
        if (Status != EvaluationStatus.Pending)
            throw new InvalidOperationException($"Cannot start a run with status '{Status}'.");

        Status = EvaluationStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        MarkUpdated(updatedBy);
    }

    /// <summary>
    /// Transitions the run to Completed and records all result statistics.
    /// </summary>
    public void Complete(
        int totalCases,
        int passedCases,
        int failedCases,
        int falsePositives,
        int falseNegatives,
        double averageLatencyMs,
        string summaryJson,
        string updatedBy)
    {
        if (Status != EvaluationStatus.Running)
            throw new InvalidOperationException($"Cannot complete a run with status '{Status}'.");

        TotalCases = totalCases;
        PassedCases = passedCases;
        FailedCases = failedCases;
        FalsePositives = falsePositives;
        FalseNegatives = falseNegatives;
        AverageLatencyMs = averageLatencyMs;
        SummaryJson = summaryJson;
        Status = EvaluationStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        MarkUpdated(updatedBy);
    }

    /// <summary>Transitions the run to Failed status and records the failure reason in the summary.</summary>
    public void Fail(string reason, string updatedBy)
    {
        if (Status == EvaluationStatus.Completed)
            throw new InvalidOperationException("Cannot fail a completed run.");

        SummaryJson = $"{{\"failureReason\":\"{reason?.Replace("\"", "\\\"")}\"}}";
        Status = EvaluationStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        MarkUpdated(updatedBy);
    }

    /// <summary>Computed pass rate as a percentage (0.0 – 100.0). Returns 0 if no cases.</summary>
    public double PassRate => TotalCases > 0 ? Math.Round((double)PassedCases / TotalCases * 100.0, 2) : 0.0;

    /// <summary>Computed false-positive rate as a percentage. Returns 0 if no cases.</summary>
    public double FalsePositiveRate => TotalCases > 0 ? Math.Round((double)FalsePositives / TotalCases * 100.0, 2) : 0.0;

    /// <summary>Computed false-negative rate as a percentage. Returns 0 if no cases.</summary>
    public double FalseNegativeRate => TotalCases > 0 ? Math.Round((double)FalseNegatives / TotalCases * 100.0, 2) : 0.0;
}
