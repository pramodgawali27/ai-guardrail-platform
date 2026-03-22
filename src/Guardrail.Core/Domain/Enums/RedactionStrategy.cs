namespace Guardrail.Core.Domain.Enums;

public enum RedactionStrategy
{
    None = 0,
    Mask = 1,
    Replace = 2,
    Remove = 3,
    Hash = 4
}
