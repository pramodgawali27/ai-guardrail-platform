namespace Guardrail.Core.Domain.Enums;

public enum ReviewStatus
{
    Pending = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3,
    Escalated = 4,
    Closed = 5
}
