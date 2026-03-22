using System.Collections.Generic;

namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Contains a named, versioned collection of test cases used in <see cref="EvaluationRun"/> executions.
/// </summary>
public sealed class EvaluationDataset : BaseEntity
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Domain or theme of this dataset (e.g., "healthcare-jailbreak", "pii-redaction", "general-safety").</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>Dataset version identifier (e.g., "1.0.0", "2024-Q4").</summary>
    public string Version { get; private set; } = "1.0.0";

    /// <summary>Total number of test cases in this dataset.</summary>
    public int CaseCount { get; private set; }

    /// <summary>JSON-serialized array of <c>EvaluationCase</c> objects.</summary>
    public string DatasetJson { get; private set; } = "[]";

    /// <summary>Descriptive tags for discovery and filtering (e.g., "red-team", "regression", "pii").</summary>
    public List<string> Tags { get; private set; } = new();

    private EvaluationDataset() { }

    /// <summary>
    /// Factory method to create a new <see cref="EvaluationDataset"/>.
    /// </summary>
    public static EvaluationDataset Create(
        Guid tenantId,
        string name,
        string category,
        string datasetJson,
        int caseCount,
        string version = "1.0.0",
        string? description = null,
        List<string>? tags = null,
        string? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(category, nameof(category));
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetJson, nameof(datasetJson));

        if (caseCount < 0)
            throw new ArgumentOutOfRangeException(nameof(caseCount), "CaseCount cannot be negative.");

        return new EvaluationDataset
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Category = category.Trim(),
            Version = version.Trim(),
            DatasetJson = datasetJson,
            CaseCount = caseCount,
            Tags = tags ?? new List<string>(),
            CreatedBy = createdBy
        };
    }

    /// <summary>Updates the dataset content and bumps the case count.</summary>
    public void UpdateDataset(string datasetJson, int caseCount, string newVersion, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetJson, nameof(datasetJson));
        ArgumentException.ThrowIfNullOrWhiteSpace(newVersion, nameof(newVersion));

        if (caseCount < 0)
            throw new ArgumentOutOfRangeException(nameof(caseCount));

        DatasetJson = datasetJson;
        CaseCount = caseCount;
        Version = newVersion.Trim();
        MarkUpdated(updatedBy);
    }

    public void AddTag(string tag, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));

        if (!Tags.Contains(tag))
            Tags.Add(tag.Trim());

        MarkUpdated(updatedBy);
    }

    public void RemoveTag(string tag, string updatedBy)
    {
        Tags.Remove(tag);
        MarkUpdated(updatedBy);
    }
}
