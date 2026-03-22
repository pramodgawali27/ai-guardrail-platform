using System.Text.Json;
using System.Text.RegularExpressions;
using Guardrail.Core.Abstractions;

namespace Guardrail.Infrastructure.Validation;

public sealed partial class DefaultOutputValidator : IOutputValidator
{
    public Task<OutputValidationResult> ValidateAsync(OutputValidationRequest request, CancellationToken ct = default)
    {
        var violations = new List<OutputViolation>();
        var redactedOutput = request.Output;
        var redactionCount = 0;

        if (!string.IsNullOrWhiteSpace(request.OutputSchemaJson))
        {
            ValidateSchema(request.Output, request.OutputSchemaJson, violations);
        }

        foreach (var forbiddenTopic in request.Constraints.ForbiddenTopics)
        {
            if (request.Output.Contains(forbiddenTopic, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new OutputViolation
                {
                    ViolationType = "ForbiddenTopic",
                    Description = $"Output contains forbidden topic '{forbiddenTopic}'.",
                    Severity = "High"
                });
            }
        }

        foreach (var requiredPhrase in request.Constraints.RequiredPhrases)
        {
            if (!request.Output.Contains(requiredPhrase, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new OutputViolation
                {
                    ViolationType = "RequiredPhraseMissing",
                    Description = $"Output is missing required phrase '{requiredPhrase}'.",
                    Severity = "Medium"
                });
            }
        }

        if (request.Constraints.RequireDisclaimer &&
            !string.IsNullOrWhiteSpace(request.Constraints.MandatoryDisclaimer) &&
            !request.Output.Contains(request.Constraints.MandatoryDisclaimer, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(new OutputViolation
            {
                ViolationType = "DisclaimerMissing",
                Description = "Mandatory disclaimer is missing from the output.",
                Severity = "High"
            });
        }

        if (request.Constraints.RequireCitations &&
            !ContainsCitation(request.Output))
        {
            violations.Add(new OutputViolation
            {
                ViolationType = "CitationMissing",
                Description = "Output is missing citations or evidence markers.",
                Severity = "High"
            });
        }

        if (request.Constraints.RedactPII)
            redactedOutput = ApplyMask(EmailPattern(), redactedOutput, ref redactionCount, "[REDACTED-EMAIL]");

        if (request.Constraints.RedactPII)
            redactedOutput = ApplyMask(PhonePattern(), redactedOutput, ref redactionCount, "[REDACTED-PHONE]");

        if (request.Constraints.RedactPII)
            redactedOutput = ApplyMask(SsnPattern(), redactedOutput, ref redactionCount, "[REDACTED-SSN]");

        if (request.Constraints.RedactPHI)
            redactedOutput = ApplyMask(MedicalRecordPattern(), redactedOutput, ref redactionCount, "[REDACTED-PHI]");

        var requiresRedaction = redactionCount > 0;
        var qualityPenalty = (violations.Count * 0.18m) + (redactionCount * 0.04m);
        var qualityScore = Math.Clamp(1m - qualityPenalty, 0m, 1m);

        return Task.FromResult(new OutputValidationResult
        {
            IsValid = violations.Count == 0,
            Violations = violations,
            RequiresRedaction = requiresRedaction,
            QualityScore = qualityScore,
            RedactionCount = redactionCount,
            RedactedOutput = requiresRedaction ? redactedOutput : null,
            EffectiveConstraints = request.Constraints
        });
    }

    private static void ValidateSchema(string output, string schemaJson, List<OutputViolation> violations)
    {
        try
        {
            using var outputDocument = JsonDocument.Parse(output);
            using var schemaDocument = JsonDocument.Parse(schemaJson);

            var outputRoot = outputDocument.RootElement;
            var schemaRoot = schemaDocument.RootElement;

            if (schemaRoot.TryGetProperty("type", out var typeProperty) &&
                typeProperty.ValueKind == JsonValueKind.String &&
                !MatchesType(outputRoot, typeProperty.GetString()))
            {
                violations.Add(new OutputViolation
                {
                    ViolationType = "SchemaTypeMismatch",
                    Description = $"Output root type does not match expected schema type '{typeProperty.GetString()}'.",
                    Severity = "High"
                });
                return;
            }

            if (schemaRoot.TryGetProperty("required", out var requiredProperty) &&
                outputRoot.ValueKind == JsonValueKind.Object)
            {
                foreach (var required in requiredProperty.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    if (!outputRoot.TryGetProperty(required!, out _))
                    {
                        violations.Add(new OutputViolation
                        {
                            ViolationType = "RequiredPropertyMissing",
                            Description = $"Required property '{required}' is missing.",
                            Severity = "High"
                        });
                    }
                }
            }

            if (schemaRoot.TryGetProperty("properties", out var propertiesProperty) &&
                propertiesProperty.ValueKind == JsonValueKind.Object &&
                outputRoot.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertySchema in propertiesProperty.EnumerateObject())
                {
                    if (!outputRoot.TryGetProperty(propertySchema.Name, out var outputProperty))
                        continue;

                    if (propertySchema.Value.TryGetProperty("type", out var propertyType) &&
                        propertyType.ValueKind == JsonValueKind.String &&
                        !MatchesType(outputProperty, propertyType.GetString()))
                    {
                        violations.Add(new OutputViolation
                        {
                            ViolationType = "PropertyTypeMismatch",
                            Description = $"Property '{propertySchema.Name}' does not match expected type '{propertyType.GetString()}'.",
                            Severity = "Medium"
                        });
                    }
                }
            }
        }
        catch (JsonException)
        {
            violations.Add(new OutputViolation
            {
                ViolationType = "SchemaValidationError",
                Description = "Output or provided schema is not valid JSON.",
                Severity = "High"
            });
        }
    }

    private static bool MatchesType(JsonElement element, string? type)
        => type switch
        {
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            _ => true
        };

    private static string ApplyMask(Regex regex, string input, ref int redactionCount, string token)
    {
        redactionCount += regex.Matches(input).Count;
        return regex.Replace(input, token);
    }

    private static bool ContainsCitation(string text)
        => text.Contains("[1]", StringComparison.OrdinalIgnoreCase)
           || text.Contains("source:", StringComparison.OrdinalIgnoreCase)
           || text.Contains("according to", StringComparison.OrdinalIgnoreCase)
           || text.Contains("citation", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:\+?\d{1,2}\s*)?(?:\(?\d{3}\)?[-.\s]*)\d{3}[-.\s]*\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b(?:MRN|Medical Record Number|Patient ID)\s*[:#]?\s*[A-Z0-9-]{4,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MedicalRecordPattern();
}
