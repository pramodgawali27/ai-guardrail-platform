using System.Text.Json;
using System.Text.Json.Serialization;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardrail.Infrastructure.Policies;

public sealed class JsonPolicyEngine : IPolicyEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPolicyRepository _policyRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<JsonPolicyEngine> _logger;
    private readonly GuardrailPlatformOptions _platformOptions;

    static JsonPolicyEngine()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public JsonPolicyEngine(
        IPolicyRepository policyRepository,
        IMemoryCache memoryCache,
        IOptions<GuardrailPlatformOptions> platformOptions,
        ILogger<JsonPolicyEngine> logger)
    {
        _policyRepository = policyRepository;
        _memoryCache = memoryCache;
        _logger = logger;
        _platformOptions = platformOptions.Value;
    }

    public async Task<EffectivePolicy> ResolveEffectivePolicyAsync(TenantContext context, CancellationToken ct = default)
    {
        var cacheKey = $"effective-policy:{context.TenantId:N}:{context.ApplicationId:N}:{context.Environment}";

        if (_memoryCache.TryGetValue(cacheKey, out EffectivePolicy? cachedPolicy) && cachedPolicy is not null)
            return cachedPolicy;

        var profiles = await _policyRepository.GetApplicablePoliciesAsync(context.TenantId, context.ApplicationId, null, ct);
        EffectivePolicy effectivePolicy;

        if (profiles.Count == 0)
        {
            effectivePolicy = CreateDefaultPolicy();
        }
        else
        {
            var mergedDocument = new PolicyDocument();
            var applicableRules = new List<PolicyRule>();

            foreach (var profile in profiles)
            {
                var document = DeserializePolicy(profile.PolicyJson);
                MergeInto(mergedDocument, document);
                applicableRules.AddRange(BuildRuleEntities(profile, document.Rules));
            }

            var lastProfile = profiles.Last();
            effectivePolicy = new EffectivePolicy
            {
                ProfileId = lastProfile.Id,
                ProfileName = lastProfile.Name,
                Version = lastProfile.Version,
                Configuration = MapConfiguration(mergedDocument),
                Constraints = MapConstraints(mergedDocument),
                DataBoundary = MapBoundary(mergedDocument),
                ApplicableRules = applicableRules
                    .Where(x => x.IsEnabled)
                    .OrderBy(x => x.Priority)
                    .ToList()
            };
        }

        _memoryCache.Set(
            cacheKey,
            effectivePolicy,
            TimeSpan.FromMinutes(Math.Max(_platformOptions.PolicyCacheMinutes, 1)));

        return effectivePolicy;
    }

    public Task<PolicyEvaluationResult> EvaluatePolicyAsync(
        TenantContext context,
        EvaluationInput input,
        EffectivePolicy policy,
        CancellationToken ct = default)
    {
        var violations = new List<PolicyViolation>();
        var aggregateText = string.Join(
            Environment.NewLine,
            new[] { input.UserPrompt, input.SystemPrompt, input.ModelOutput }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        foreach (var forbiddenPhrase in policy.Configuration.ForbiddenPhrases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(aggregateText) &&
                aggregateText.Contains(forbiddenPhrase, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new PolicyViolation
                {
                    RuleKey = $"forbidden-phrase:{forbiddenPhrase}",
                    RuleName = "Forbidden phrase detected",
                    Description = $"Content matched restricted phrase '{forbiddenPhrase}'.",
                    Severity = RuleSeverity.High.ToString(),
                    Score = 0.75m
                });
            }
        }

        foreach (var requestedTool in input.RequestedTools)
        {
            if (policy.Configuration.DeniedTools.Contains(requestedTool, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(new PolicyViolation
                {
                    RuleKey = $"tool-denied:{requestedTool}",
                    RuleName = "Denied tool requested",
                    Description = $"Tool '{requestedTool}' is denied by policy.",
                    Severity = RuleSeverity.Critical.ToString(),
                    Score = 1.0m
                });
            }
        }

        foreach (var source in input.DataSources)
        {
            if (policy.Configuration.DeniedSourceTypes.Contains(source.SourceType, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(new PolicyViolation
                {
                    RuleKey = $"source-denied:{source.SourceType}",
                    RuleName = "Denied source type requested",
                    Description = $"Source type '{source.SourceType}' is not allowed by policy.",
                    Severity = RuleSeverity.High.ToString(),
                    Score = 0.70m
                });
            }
        }

        if (policy.Configuration.RequireCitations &&
            !string.IsNullOrWhiteSpace(input.ModelOutput) &&
            !ContainsCitation(input.ModelOutput))
        {
            violations.Add(new PolicyViolation
            {
                RuleKey = "citations-required",
                RuleName = "Evidence required",
                Description = "Policy requires citations or evidence markers in the output.",
                Severity = RuleSeverity.High.ToString(),
                Score = 0.65m
            });
        }

        if (policy.Configuration.RequireEvidenceForRegulatedResponses &&
            !string.IsNullOrWhiteSpace(input.ModelOutput) &&
            !ContainsEvidenceCue(input.ModelOutput))
        {
            violations.Add(new PolicyViolation
            {
                RuleKey = "regulated-evidence-required",
                RuleName = "Regulated evidence missing",
                Description = "Regulated response is missing evidence or grounding cues.",
                Severity = RuleSeverity.Critical.ToString(),
                Score = 0.80m
            });
        }

        var score = violations.Count == 0
            ? 0m
            : Math.Clamp(violations.Max(x => x.Score), 0m, 1m);

        var result = new PolicyEvaluationResult
        {
            HasViolations = violations.Count > 0,
            Violations = violations,
            PolicyRiskScore = score,
            EffectiveConstraints = policy.Constraints
        };

        return Task.FromResult(result);
    }

    private static PolicyDocument DeserializePolicy(string policyJson)
    {
        if (string.IsNullOrWhiteSpace(policyJson))
            return new PolicyDocument();

        try
        {
            return JsonSerializer.Deserialize<PolicyDocument>(policyJson, JsonOptions) ?? new PolicyDocument();
        }
        catch (JsonException)
        {
            return new PolicyDocument();
        }
    }

    private static void MergeInto(PolicyDocument target, PolicyDocument source)
    {
        target.ContentRiskThreshold = source.ContentRiskThreshold ?? target.ContentRiskThreshold;
        target.PrivacyRiskThreshold = source.PrivacyRiskThreshold ?? target.PrivacyRiskThreshold;
        target.InjectionRiskThreshold = source.InjectionRiskThreshold ?? target.InjectionRiskThreshold;
        target.EscalationThreshold = source.EscalationThreshold ?? target.EscalationThreshold;
        target.BlockThreshold = source.BlockThreshold ?? target.BlockThreshold;
        target.PIIRedactionEnabled = source.PIIRedactionEnabled ?? target.PIIRedactionEnabled;
        target.PHIRedactionEnabled = source.PHIRedactionEnabled ?? target.PHIRedactionEnabled;
        target.RequireCitations = source.RequireCitations ?? target.RequireCitations;
        target.RequireEvidenceForRegulatedResponses = source.RequireEvidenceForRegulatedResponses ?? target.RequireEvidenceForRegulatedResponses;
        target.SimulationMode = source.SimulationMode ?? target.SimulationMode;
        target.MinimumOutputQualityScore = source.MinimumOutputQualityScore ?? target.MinimumOutputQualityScore;
        target.RedactionStrategy = source.RedactionStrategy ?? target.RedactionStrategy;
        target.AllowToolUse = source.AllowToolUse ?? target.AllowToolUse;
        target.CrossTenantAllowed = source.CrossTenantAllowed ?? target.CrossTenantAllowed;
        target.MaxDocuments = source.MaxDocuments ?? target.MaxDocuments;
        target.MinimumSourceTrustLevel = source.MinimumSourceTrustLevel ?? target.MinimumSourceTrustLevel;
        target.MandatoryDisclaimer = source.MandatoryDisclaimer ?? target.MandatoryDisclaimer;
        target.AllowedTools = MergeList(target.AllowedTools, source.AllowedTools);
        target.DeniedTools = MergeList(target.DeniedTools, source.DeniedTools);
        target.ApprovalRequiredTools = MergeList(target.ApprovalRequiredTools, source.ApprovalRequiredTools);
        target.AllowedSourceTypes = MergeList(target.AllowedSourceTypes, source.AllowedSourceTypes);
        target.DeniedSourceTypes = MergeList(target.DeniedSourceTypes, source.DeniedSourceTypes);
        target.AllowedRegions = MergeList(target.AllowedRegions, source.AllowedRegions);
        target.ForbiddenPhrases = MergeList(target.ForbiddenPhrases, source.ForbiddenPhrases);

        if (source.RiskWeights is not null)
        {
            target.RiskWeights ??= new RiskWeightDocument();
            target.RiskWeights.ContentWeight = source.RiskWeights.ContentWeight ?? target.RiskWeights.ContentWeight;
            target.RiskWeights.PrivacyWeight = source.RiskWeights.PrivacyWeight ?? target.RiskWeights.PrivacyWeight;
            target.RiskWeights.InjectionWeight = source.RiskWeights.InjectionWeight ?? target.RiskWeights.InjectionWeight;
            target.RiskWeights.BusinessPolicyWeight = source.RiskWeights.BusinessPolicyWeight ?? target.RiskWeights.BusinessPolicyWeight;
            target.RiskWeights.ActionWeight = source.RiskWeights.ActionWeight ?? target.RiskWeights.ActionWeight;
            target.RiskWeights.OutputQualityWeight = source.RiskWeights.OutputQualityWeight ?? target.RiskWeights.OutputQualityWeight;
        }

        if (source.Rules is not null)
        {
            var mergedRules = (target.Rules ?? new List<PolicyRuleDocument>())
                .ToDictionary(x => x.RuleKey, StringComparer.OrdinalIgnoreCase);

            foreach (var rule in source.Rules)
                mergedRules[rule.RuleKey] = rule;

            target.Rules = mergedRules.Values.OrderBy(x => x.Priority).ToList();
        }
    }

    private static EffectivePolicy CreateDefaultPolicy()
        => new()
        {
            ProfileName = "system-default",
            Version = 1,
            Configuration = new PolicyConfiguration(),
            Constraints = ConstraintSet.Strict,
            DataBoundary = new DataBoundaryConfig
            {
                CrossTenantAllowed = false,
                MaxDocuments = 10,
                MinimumTrustLevel = SourceTrustLevel.Internal
            }
        };

    private static PolicyConfiguration MapConfiguration(PolicyDocument document)
        => new()
        {
            ContentRiskThreshold = document.ContentRiskThreshold ?? 0.7m,
            PrivacyRiskThreshold = document.PrivacyRiskThreshold ?? 0.6m,
            InjectionRiskThreshold = document.InjectionRiskThreshold ?? 0.5m,
            EscalationThreshold = document.EscalationThreshold ?? 0.8m,
            BlockThreshold = document.BlockThreshold ?? 0.9m,
            PIIRedactionEnabled = document.PIIRedactionEnabled ?? true,
            PHIRedactionEnabled = document.PHIRedactionEnabled ?? true,
            RequireCitations = document.RequireCitations ?? false,
            RequireEvidenceForRegulatedResponses = document.RequireEvidenceForRegulatedResponses ?? false,
            SimulationMode = document.SimulationMode ?? false,
            MinimumOutputQualityScore = document.MinimumOutputQualityScore ?? 0.60m,
            RedactionStrategy = document.RedactionStrategy ?? RedactionStrategy.Mask,
            AllowedTools = document.AllowedTools ?? new List<string>(),
            DeniedTools = document.DeniedTools ?? new List<string>(),
            ApprovalRequiredTools = document.ApprovalRequiredTools ?? new List<string>(),
            AllowedSourceTypes = document.AllowedSourceTypes ?? new List<string>(),
            DeniedSourceTypes = document.DeniedSourceTypes ?? new List<string>(),
            AllowedRegions = document.AllowedRegions ?? new List<string>(),
            MandatoryDisclaimer = document.MandatoryDisclaimer,
            ForbiddenPhrases = document.ForbiddenPhrases ?? new List<string>(),
            RiskWeights = new RiskWeights
            {
                ContentWeight = document.RiskWeights?.ContentWeight ?? RiskWeights.Default.ContentWeight,
                PrivacyWeight = document.RiskWeights?.PrivacyWeight ?? RiskWeights.Default.PrivacyWeight,
                InjectionWeight = document.RiskWeights?.InjectionWeight ?? RiskWeights.Default.InjectionWeight,
                BusinessPolicyWeight = document.RiskWeights?.BusinessPolicyWeight ?? RiskWeights.Default.BusinessPolicyWeight,
                ActionWeight = document.RiskWeights?.ActionWeight ?? RiskWeights.Default.ActionWeight,
                OutputQualityWeight = document.RiskWeights?.OutputQualityWeight ?? RiskWeights.Default.OutputQualityWeight
            }
        };

    private static ConstraintSet MapConstraints(PolicyDocument document)
        => new()
        {
            RequireCitations = document.RequireCitations ?? false,
            RequireDisclaimer = !string.IsNullOrWhiteSpace(document.MandatoryDisclaimer),
            MandatoryDisclaimer = document.MandatoryDisclaimer,
            RedactPII = document.PIIRedactionEnabled ?? true,
            RedactPHI = document.PHIRedactionEnabled ?? true,
            AllowToolUse = document.AllowToolUse ?? false,
            AllowedTools = document.AllowedTools ?? new List<string>(),
            DeniedTools = document.DeniedTools ?? new List<string>(),
            ForbiddenTopics = document.ForbiddenPhrases ?? new List<string>()
        };

    private static DataBoundaryConfig MapBoundary(PolicyDocument document)
        => new()
        {
            AllowedSourceTypes = document.AllowedSourceTypes ?? new List<string>(),
            DeniedSourceTypes = document.DeniedSourceTypes ?? new List<string>(),
            CrossTenantAllowed = document.CrossTenantAllowed ?? false,
            MaxDocuments = document.MaxDocuments ?? 10,
            MinimumTrustLevel = document.MinimumSourceTrustLevel ?? SourceTrustLevel.Internal
        };

    private static List<PolicyRule> BuildRuleEntities(PolicyProfile profile, List<PolicyRuleDocument>? rules)
    {
        if (rules is null || rules.Count == 0)
            return new List<PolicyRule>();

        return rules
            .Select(rule =>
            {
                var entity = PolicyRule.Create(
                    profile.Id,
                    rule.RuleKey,
                    string.IsNullOrWhiteSpace(rule.RuleName) ? rule.RuleKey : rule.RuleName,
                    rule.Severity,
                    rule.Category,
                    JsonSerializer.Serialize(rule.Conditions, JsonOptions),
                    JsonSerializer.Serialize(rule.Actions, JsonOptions),
                    rule.Description,
                    rule.Priority,
                    rule.OverrideAllowed,
                    "policy-engine");

                if (!rule.IsEnabled)
                    entity.Disable("policy-engine");

                return entity;
            })
            .ToList();
    }

    private static List<string>? MergeList(List<string>? current, List<string>? incoming)
    {
        if (incoming is null || incoming.Count == 0)
            return current;

        return (current ?? new List<string>())
            .Concat(incoming)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsCitation(string text)
        => text.Contains("[1]", StringComparison.OrdinalIgnoreCase)
           || text.Contains("source:", StringComparison.OrdinalIgnoreCase)
           || text.Contains("citation", StringComparison.OrdinalIgnoreCase)
           || text.Contains("according to", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsEvidenceCue(string text)
        => text.Contains("based on", StringComparison.OrdinalIgnoreCase)
           || text.Contains("according to", StringComparison.OrdinalIgnoreCase)
           || text.Contains("evidence", StringComparison.OrdinalIgnoreCase)
           || ContainsCitation(text);
}
