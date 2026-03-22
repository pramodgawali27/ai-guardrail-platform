using Guardrail.Core.Domain.Entities;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Persistence contract for audit events, guardrail executions, and risk assessments.
/// </summary>
public interface IAuditRepository
{
    Task<AuditEvent> AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditEvent> Items, int Total)> SearchAsync(AuditSearchCriteria criteria, CancellationToken ct = default);
    Task<GuardrailExecution> AddExecutionAsync(GuardrailExecution execution, CancellationToken ct = default);
    Task<GuardrailExecution?> GetExecutionByIdAsync(Guid executionId, CancellationToken ct = default);
    Task UpdateExecutionAsync(GuardrailExecution execution, CancellationToken ct = default);
    Task<RiskAssessment> AddRiskAssessmentAsync(RiskAssessment assessment, CancellationToken ct = default);
}

public class AuditSearchCriteria
{
    public Guid? TenantId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? EventType { get; set; }
    public bool? IsIncident { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
