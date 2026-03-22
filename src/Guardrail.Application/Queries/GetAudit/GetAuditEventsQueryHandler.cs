using MediatR;
using Guardrail.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Guardrail.Application.Queries.GetAudit;

/// <summary>
/// Handles <see cref="GetAuditEventsQuery"/> by delegating to the
/// <see cref="IAuditRepository"/> search capability.
/// </summary>
public sealed class GetAuditEventsQueryHandler
    : IRequestHandler<GetAuditEventsQuery, GetAuditEventsResult>
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<GetAuditEventsQueryHandler> _logger;

    public GetAuditEventsQueryHandler(
        IAuditRepository auditRepository,
        ILogger<GetAuditEventsQueryHandler> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<GetAuditEventsResult> Handle(
        GetAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Searching audit events: tenant={TenantId}, from={From}, to={To}, page={Page}/{PageSize}",
            request.TenantId,
            request.From,
            request.To,
            request.Page,
            request.PageSize);

        var criteria = new AuditSearchCriteria
        {
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            UserId = request.UserId,
            CorrelationId = request.CorrelationId,
            From = request.From,
            To = request.To,
            EventType = request.EventType,
            IsIncident = request.IsIncident,
            Page = request.Page,
            PageSize = Math.Min(request.PageSize, 200)    // enforce max page size
        };

        var (items, total) = await _auditRepository.SearchAsync(criteria, cancellationToken);

        return new GetAuditEventsResult
        {
            Items = items,
            Total = total,
            Page = request.Page,
            PageSize = criteria.PageSize
        };
    }
}
