namespace Guardrail.Core.Domain.Enums;

public enum PolicyScope
{
    Global = 0,
    Tenant = 1,
    Application = 2,
    Domain = 3,
    Region = 4,
    Environment = 5
}
