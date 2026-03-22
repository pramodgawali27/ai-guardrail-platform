using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;

namespace Guardrail.Infrastructure.Policies;

public sealed class WeightedRiskEngine : IRiskEngine
{
    public Task<RiskEvaluationResult> EvaluateRiskAsync(RiskEvaluationInput input, CancellationToken ct = default)
    {
        var contentRisk = Math.Clamp(input.ContentSafetyResult?.OverallScore ?? 0m, 0m, 1m);
        var privacyRisk = Math.Clamp(
            input.ContentSafetyResult?.Flags
                .Where(x => x.Category is "PII" or "PHI")
                .Select(x => x.Score)
                .DefaultIfEmpty(0m)
                .Max() ?? 0m,
            0m,
            1m);
        var injectionRisk = Math.Clamp(input.PromptShieldResult?.InjectionScore ?? 0m, 0m, 1m);
        var businessRisk = Math.Clamp(
            Math.Max(input.PolicyEvaluationResult?.PolicyRiskScore ?? 0m, input.ContextFirewallResult?.ContextRiskScore ?? 0m),
            0m,
            1m);
        var actionRisk = Math.Clamp(input.ToolValidationResult?.ToolRiskScore ?? 0m, 0m, 1m);
        var outputQualityRisk = Math.Clamp(
            input.OutputValidationResult is null ? 0m : 1m - input.OutputValidationResult.QualityScore,
            0m,
            1m);

        var weightedTotal =
            (contentRisk * input.Weights.ContentWeight) +
            (privacyRisk * input.Weights.PrivacyWeight) +
            (injectionRisk * input.Weights.InjectionWeight) +
            (businessRisk * input.Weights.BusinessPolicyWeight) +
            (actionRisk * input.Weights.ActionWeight) +
            (outputQualityRisk * input.Weights.OutputQualityWeight);

        weightedTotal = Math.Clamp(weightedTotal, 0m, 1m);
        var normalized = Math.Round(weightedTotal * 100m, 2);
        var level = RiskScore.DetermineLevel(normalized);

        var score = new RiskScore
        {
            ContentRisk = contentRisk,
            PrivacyRisk = privacyRisk,
            InjectionRisk = injectionRisk,
            BusinessPolicyRisk = businessRisk,
            ActionRisk = actionRisk,
            OutputQualityRisk = outputQualityRisk,
            WeightedTotal = weightedTotal,
            NormalizedScore = normalized,
            Level = level
        };

        var appliedSignals = new List<string>();
        appliedSignals.AddRange(input.ContentSafetyResult?.Flags.Where(x => x.Flagged).Select(x => $"{x.Category}:{x.Score:0.00}") ?? []);
        appliedSignals.AddRange(input.PromptShieldResult?.Signals.Select(x => $"{x.SignalType}:{x.Score:0.00}") ?? []);
        appliedSignals.AddRange(input.PolicyEvaluationResult?.Violations.Select(x => $"{x.RuleKey}:{x.Score:0.00}") ?? []);
        appliedSignals.AddRange(input.OutputValidationResult?.Violations.Select(x => $"{x.ViolationType}:{x.Severity}") ?? []);

        var hasDeniedTools = input.ToolValidationResult?.DeniedTools.Count > 0;
        var hasApprovalRequiredTools = input.ToolValidationResult?.ApprovalRequiredTools.Count > 0;
        var crossTenantAttempt = input.ContextFirewallResult?.CrossTenantAttemptDetected == true;
        var requiresRedaction = input.OutputValidationResult?.RequiresRedaction == true;
        var invalidOutput = input.OutputValidationResult is { IsValid: false };

        var decision =
            hasDeniedTools ||
            crossTenantAttempt ||
            contentRisk >= input.BlockThreshold ||
            privacyRisk >= input.BlockThreshold ||
            injectionRisk >= input.BlockThreshold ||
            weightedTotal >= input.BlockThreshold
                ? DecisionType.Block
                : requiresRedaction
                    ? DecisionType.Redact
                    : hasApprovalRequiredTools ||
                      invalidOutput ||
                      contentRisk >= input.ContentRiskThreshold ||
                      privacyRisk >= input.PrivacyRiskThreshold ||
                      injectionRisk >= input.InjectionRiskThreshold ||
                      weightedTotal >= input.EscalationThreshold
                        ? DecisionType.Escalate
                        : weightedTotal >= 0.35m || (input.PolicyEvaluationResult?.HasViolations ?? false)
                            ? DecisionType.AllowWithConstraints
                            : DecisionType.Allow;

        var recommendedConstraints = input.OutputValidationResult?.EffectiveConstraints
            ?? input.PolicyEvaluationResult?.EffectiveConstraints
            ?? ConstraintSet.Default;

        if (hasDeniedTools)
        {
            recommendedConstraints = recommendedConstraints with
            {
                AllowToolUse = false,
                DeniedTools = (recommendedConstraints.DeniedTools ?? new List<string>())
                    .Concat(input.ToolValidationResult!.DeniedTools)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        if (hasApprovalRequiredTools)
        {
            recommendedConstraints = recommendedConstraints with
            {
                AllowToolUse = false
            };
        }

        var rationale = BuildRationale(score, decision, hasDeniedTools, hasApprovalRequiredTools, crossTenantAttempt, requiresRedaction);

        return Task.FromResult(new RiskEvaluationResult
        {
            Score = score,
            Decision = decision,
            Rationale = rationale,
            RecommendedConstraints = recommendedConstraints,
            AppliedSignals = appliedSignals,
            RequiresHumanReview = decision == DecisionType.Escalate || hasApprovalRequiredTools
        });
    }

    private static string BuildRationale(
        RiskScore score,
        DecisionType decision,
        bool hasDeniedTools,
        bool hasApprovalRequiredTools,
        bool crossTenantAttempt,
        bool requiresRedaction)
    {
        var reasons = new List<string>
        {
            $"overall={score.NormalizedScore:0.##}",
            $"content={score.ContentRisk:0.##}",
            $"privacy={score.PrivacyRisk:0.##}",
            $"injection={score.InjectionRisk:0.##}",
            $"business={score.BusinessPolicyRisk:0.##}",
            $"action={score.ActionRisk:0.##}",
            $"quality={score.OutputQualityRisk:0.##}"
        };

        if (hasDeniedTools)
            reasons.Add("denied-tools");

        if (hasApprovalRequiredTools)
            reasons.Add("approval-required-tools");

        if (crossTenantAttempt)
            reasons.Add("cross-tenant-context");

        if (requiresRedaction)
            reasons.Add("output-redaction");

        reasons.Add($"decision={decision}");
        return string.Join("; ", reasons);
    }
}
