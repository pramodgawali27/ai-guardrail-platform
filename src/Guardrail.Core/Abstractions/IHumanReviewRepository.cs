using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Core.Abstractions;

/// <summary>
/// Persistence contract for cases requiring human review and approval workflows.
/// </summary>
public interface IHumanReviewRepository
{
    Task<HumanReviewCase> AddAsync(HumanReviewCase reviewCase, CancellationToken ct = default);
    Task<HumanReviewCase?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(HumanReviewCase reviewCase, CancellationToken ct = default);
    Task<(IReadOnlyList<HumanReviewCase> Items, int Total)> SearchAsync(
        HumanReviewSearchCriteria criteria,
        CancellationToken ct = default);
}

public sealed class HumanReviewSearchCriteria
{
    public Guid? TenantId { get; set; }
    public Guid? ApplicationId { get; set; }
    public ReviewStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
