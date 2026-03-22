namespace Guardrail.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the set of constraints to apply when allowing a request with conditions.
/// </summary>
public sealed record ConstraintSet
{
    public bool RequireCitations { get; init; }
    public bool RequireDisclaimer { get; init; }
    public string? MandatoryDisclaimer { get; init; }
    public int? MaxOutputTokens { get; init; }
    public List<string> ForbiddenTopics { get; init; } = new();
    public List<string> RequiredPhrases { get; init; } = new();
    public bool RedactPII { get; init; }
    public bool RedactPHI { get; init; }
    public bool AllowToolUse { get; init; }
    public List<string> AllowedTools { get; init; } = new();
    public List<string> DeniedTools { get; init; } = new();
    public string? OutputSchemaRef { get; init; }

    public static ConstraintSet Default => new ConstraintSet { AllowToolUse = false };

    /// <summary>A permissive constraint set allowing tool use with no additional restrictions.</summary>
    public static ConstraintSet Permissive => new ConstraintSet { AllowToolUse = true };

    /// <summary>A strict constraint set enabling all redaction and disclaimer requirements.</summary>
    public static ConstraintSet Strict => new ConstraintSet
    {
        AllowToolUse = false,
        RedactPII = true,
        RedactPHI = true,
        RequireDisclaimer = true,
        RequireCitations = true
    };

    /// <summary>Returns true if this constraint set requires any redaction operations.</summary>
    public bool RequiresRedaction => RedactPII || RedactPHI;

    /// <summary>Returns true if there are any active constraints beyond the default state.</summary>
    public bool HasActiveConstraints =>
        RequireCitations || RequireDisclaimer || MaxOutputTokens.HasValue ||
        ForbiddenTopics.Count > 0 || RequiredPhrases.Count > 0 ||
        RedactPII || RedactPHI || !AllowToolUse ||
        AllowedTools.Count > 0 || DeniedTools.Count > 0 ||
        OutputSchemaRef is not null;
}
