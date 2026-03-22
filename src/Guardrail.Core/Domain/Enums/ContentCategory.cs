namespace Guardrail.Core.Domain.Enums;

public enum ContentCategory
{
    Safe = 0,
    HateSpeech = 1,
    Violence = 2,
    SelfHarm = 3,
    Sexual = 4,
    PII = 5,
    PHI = 6,
    ConfidentialBusiness = 7,
    PromptInjection = 8,
    Jailbreak = 9,
    Hallucination = 10,
    UnverifiedClaim = 11,
    Restricted = 12
}
