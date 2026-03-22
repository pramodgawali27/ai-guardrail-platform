using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Coordinates asynchronous evaluation suite execution against stored datasets.
/// </summary>
public interface IEvaluationService
{
    Task<EvaluationRun> QueueRunAsync(QueueEvaluationRunRequest request, CancellationToken ct = default);
}

public sealed class QueueEvaluationRunRequest
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DatasetId { get; set; }
    public Guid? PolicyProfileId { get; set; }
    public string RequestedBy { get; set; } = "system";
}

public sealed class EvaluationCaseDefinition
{
    public string CaseId { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? ModelOutput { get; set; }
    public DecisionType ExpectedDecision { get; set; }
    public string? OutputSchemaJson { get; set; }
    public List<SourceDescriptor> DataSources { get; set; } = new();
    public List<ToolCallDescriptor> RequestedTools { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}
