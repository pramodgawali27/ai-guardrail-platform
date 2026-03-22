namespace Guardrail.Core.Domain.Enums;

public enum DecisionType
{
    Allow = 0,
    AllowWithConstraints = 1,
    Redact = 2,
    Escalate = 3,
    Block = 4
}
