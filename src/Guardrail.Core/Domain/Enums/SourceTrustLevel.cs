namespace Guardrail.Core.Domain.Enums;

public enum SourceTrustLevel
{
    Untrusted = 0,
    External = 1,
    Internal = 2,
    Verified = 3,
    Privileged = 4
}
