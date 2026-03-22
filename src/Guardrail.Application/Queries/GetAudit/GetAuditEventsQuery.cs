using MediatR;
using Guardrail.Core.Domain.Entities;

namespace Guardrail.Application.Queries.GetAudit;

/// <summary>
/// MediatR query to search and page audit events matching the supplied criteria.
/// All filter parameters are optional; omitting a field matches any value.
/// </summary>
public record GetAuditEventsQuery : IRequest<GetAuditEventsResult>
{
    public Guid? TenantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string? UserId { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    /// <summary>Filter by machine-readable event type, e.g. "InputBlocked".</summary>
    public string? EventType { get; init; }

    /// <summary>When true, returns only events classified as incidents.</summary>
    public bool? IsIncident { get; init; }

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Maximum number of items per page (default 50, max 200).</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Paginated result returned by <see cref="GetAuditEventsQuery"/>.
/// </summary>
public sealed class GetAuditEventsResult
{
    public IReadOnlyList<AuditEvent> Items { get; set; } = Array.Empty<AuditEvent>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}
