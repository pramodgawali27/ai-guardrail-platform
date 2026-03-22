using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.Observability;
using Guardrail.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Guardrail.Infrastructure.Orchestration;

public sealed class GuardrailOrchestrator : IGuardrailOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPolicyEngine _policyEngine;
    private readonly IContentSafetyProvider _contentSafetyProvider;
    private readonly IPromptShieldProvider _promptShieldProvider;
    private readonly IContextFirewall _contextFirewall;
    private readonly IToolFirewall _toolFirewall;
    private readonly IOutputValidator _outputValidator;
    private readonly IRiskEngine _riskEngine;
    private readonly IAuditRepository _auditRepository;
    private readonly IHumanReviewRepository _humanReviewRepository;
    private readonly GuardrailDbContext _dbContext;
    private readonly GuardrailMetrics _metrics;
    private readonly ILogger<GuardrailOrchestrator> _logger;

    static GuardrailOrchestrator()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public GuardrailOrchestrator(
        IPolicyEngine policyEngine,
        IContentSafetyProvider contentSafetyProvider,
        IPromptShieldProvider promptShieldProvider,
        IContextFirewall contextFirewall,
        IToolFirewall toolFirewall,
        IOutputValidator outputValidator,
        IRiskEngine riskEngine,
        IAuditRepository auditRepository,
        IHumanReviewRepository humanReviewRepository,
        GuardrailDbContext dbContext,
        GuardrailMetrics metrics,
        ILogger<GuardrailOrchestrator> logger)
    {
        _policyEngine = policyEngine;
        _contentSafetyProvider = contentSafetyProvider;
        _promptShieldProvider = promptShieldProvider;
        _contextFirewall = contextFirewall;
        _toolFirewall = toolFirewall;
        _outputValidator = outputValidator;
        _riskEngine = riskEngine;
        _auditRepository = auditRepository;
        _humanReviewRepository = humanReviewRepository;
        _dbContext = dbContext;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<GuardrailEvaluationResult> EvaluateInputAsync(
        InputEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var policy = await _policyEngine.ResolveEffectivePolicyAsync(request.TenantContext, cancellationToken);
        var execution = GuardrailExecution.Create(
            request.TenantContext.TenantId,
            request.TenantContext.ApplicationId,
            request.TenantContext.UserId,
            request.TenantContext.SessionId,
            request.TenantContext.CorrelationId,
            ComputeInputHash(request),
            policy.ProfileId,
            request.TenantContext.UserId);

        await _auditRepository.AddExecutionAsync(execution, cancellationToken);

        var contentSafetyResult = await _contentSafetyProvider.AnalyzeTextAsync(
            $"{request.SystemPrompt}{Environment.NewLine}{request.UserPrompt}",
            ct: cancellationToken);

        var promptShieldResult = await _promptShieldProvider.DetectInjectionAsync(
            new PromptShieldRequest
            {
                UserPrompt = request.UserPrompt,
                Documents = request.DataSources.Select(source => new DocumentContext
                {
                    DocumentId = source.SourceId,
                    Content = $"{source.Uri} {string.Join(' ', source.Metadata.Values)}"
                }).ToList()
            },
            cancellationToken);

        var policyEvaluation = await _policyEngine.EvaluatePolicyAsync(
            request.TenantContext,
            new EvaluationInput
            {
                UserPrompt = request.UserPrompt,
                SystemPrompt = request.SystemPrompt,
                RequestedTools = request.RequestedTools.Select(x => x.ToolName).ToList(),
                DataSources = request.DataSources,
                Metadata = request.Metadata
            },
            policy,
            cancellationToken);

        var toolValidation = await _toolFirewall.ValidateToolsAsync(
            new ToolValidationRequest
            {
                TenantContext = request.TenantContext,
                RequestedTools = request.RequestedTools,
                Policy = policy
            },
            cancellationToken);

        var contextValidation = await _contextFirewall.ValidateContextAsync(
            new ContextFirewallRequest
            {
                TenantContext = request.TenantContext,
                RequestedSources = request.DataSources,
                BoundaryConfig = policy.DataBoundary
            },
            cancellationToken);

        var riskEvaluation = await _riskEngine.EvaluateRiskAsync(
            BuildRiskInput(request.TenantContext, policy, contentSafetyResult, promptShieldResult, policyEvaluation, toolValidation, contextValidation, null),
            cancellationToken);

        stopwatch.Stop();
        execution.Complete(riskEvaluation.Score.Level, null, riskEvaluation.Decision, stopwatch.ElapsedMilliseconds, request.TenantContext.UserId);
        await _auditRepository.UpdateExecutionAsync(execution, cancellationToken);

        var assessment = await PersistAssessmentAsync(execution.Id, request.TenantContext.UserId, riskEvaluation, cancellationToken);
        await PersistSignalsAsync(assessment.Id, contentSafetyResult, promptShieldResult, policyEvaluation, null, cancellationToken);
        var reviewCaseId = await CreateReviewCaseIfRequiredAsync(
            execution,
            request.TenantContext,
            riskEvaluation,
            BuildSafeSummary("input", request.UserPrompt, request.DataSources.Count, request.RequestedTools.Count),
            cancellationToken);

        await PersistAuditEventAsync(
            execution.Id,
            request.TenantContext,
            "InputEvaluated",
            "Input",
            riskEvaluation,
            BuildSafeSummary("input", request.UserPrompt, request.DataSources.Count, request.RequestedTools.Count),
            cancellationToken);

        _metrics.RecordEvaluation("input", riskEvaluation.Decision, riskEvaluation.Score.NormalizedScore, stopwatch.ElapsedMilliseconds);

        return new GuardrailEvaluationResult
        {
            ExecutionId = execution.Id,
            CorrelationId = request.TenantContext.CorrelationId,
            Decision = riskEvaluation.Decision,
            RiskLevel = riskEvaluation.Score.Level,
            NormalizedRiskScore = riskEvaluation.Score.NormalizedScore,
            Rationale = riskEvaluation.Rationale,
            AppliedConstraints = riskEvaluation.RecommendedConstraints,
            RequiresHumanReview = riskEvaluation.RequiresHumanReview,
            HumanReviewCaseId = reviewCaseId,
            AppliedPolicies = [policy.ProfileName],
            DetectedSignals = riskEvaluation.AppliedSignals,
            Metadata = BuildMetadata(policy, contentSafetyResult.ProviderName, _promptShieldProvider.ProviderName),
            EvaluatedAt = DateTimeOffset.UtcNow,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    public async Task<GuardrailEvaluationResult> EvaluateOutputAsync(
        OutputEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var policy = await _policyEngine.ResolveEffectivePolicyAsync(request.TenantContext, cancellationToken);
        var execution = request.InputExecutionId.HasValue
            ? await _auditRepository.GetExecutionByIdAsync(request.InputExecutionId.Value, cancellationToken)
            : null;

        if (execution is null)
        {
            execution = GuardrailExecution.Create(
                request.TenantContext.TenantId,
                request.TenantContext.ApplicationId,
                request.TenantContext.UserId,
                request.TenantContext.SessionId,
                request.TenantContext.CorrelationId,
                Sha256Hasher.Hash(request.ModelOutput),
                policy.ProfileId,
                request.TenantContext.UserId);
            await _auditRepository.AddExecutionAsync(execution, cancellationToken);
        }

        var effectiveConstraints = request.AppliedConstraints.HasActiveConstraints
            ? request.AppliedConstraints
            : policy.Constraints;

        var outputValidation = await _outputValidator.ValidateAsync(
            new OutputValidationRequest
            {
                Output = request.ModelOutput,
                OutputSchemaJson = request.OutputSchemaJson,
                Constraints = effectiveConstraints,
                TenantContext = request.TenantContext,
                Metadata = request.Metadata
            },
            cancellationToken);

        var contentSafetyResult = await _contentSafetyProvider.AnalyzeTextAsync(
            outputValidation.RedactedOutput ?? request.ModelOutput,
            ct: cancellationToken);

        var policyEvaluation = await _policyEngine.EvaluatePolicyAsync(
            request.TenantContext,
            new EvaluationInput
            {
                ModelOutput = request.ModelOutput,
                Metadata = request.Metadata
            },
            policy,
            cancellationToken);

        var riskEvaluation = await _riskEngine.EvaluateRiskAsync(
            BuildRiskInput(request.TenantContext, policy, contentSafetyResult, null, policyEvaluation, null, null, outputValidation),
            cancellationToken);

        stopwatch.Stop();
        execution.Complete(execution.InputRiskLevel, riskEvaluation.Score.Level, riskEvaluation.Decision, stopwatch.ElapsedMilliseconds, request.TenantContext.UserId);
        await _auditRepository.UpdateExecutionAsync(execution, cancellationToken);

        var assessment = await PersistAssessmentAsync(execution.Id, request.TenantContext.UserId, riskEvaluation, cancellationToken);
        await PersistSignalsAsync(assessment.Id, contentSafetyResult, null, policyEvaluation, outputValidation, cancellationToken);

        if (outputValidation.RequiresRedaction && !string.IsNullOrWhiteSpace(outputValidation.RedactedOutput))
        {
            var redactionResult = RedactionResult.Create(
                execution.Id,
                Sha256Hasher.Hash(request.ModelOutput),
                Sha256Hasher.Hash(outputValidation.RedactedOutput),
                outputValidation.RedactionCount,
                policy.Configuration.RedactionStrategy,
                JsonSerializer.Serialize(outputValidation.Violations, JsonOptions),
                request.TenantContext.UserId);

            _dbContext.RedactionResults.Add(redactionResult);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var reviewCaseId = await CreateReviewCaseIfRequiredAsync(
            execution,
            request.TenantContext,
            riskEvaluation,
            BuildSafeSummary("output", request.ModelOutput, 0, 0),
            cancellationToken);

        await PersistAuditEventAsync(
            execution.Id,
            request.TenantContext,
            "OutputEvaluated",
            "Output",
            riskEvaluation,
            BuildSafeSummary("output", request.ModelOutput, 0, 0),
            cancellationToken);

        _metrics.RecordEvaluation(
            "output",
            riskEvaluation.Decision,
            riskEvaluation.Score.NormalizedScore,
            stopwatch.ElapsedMilliseconds,
            outputValidation.RedactionCount);

        return new GuardrailEvaluationResult
        {
            ExecutionId = execution.Id,
            CorrelationId = request.TenantContext.CorrelationId,
            Decision = riskEvaluation.Decision,
            RiskLevel = riskEvaluation.Score.Level,
            NormalizedRiskScore = riskEvaluation.Score.NormalizedScore,
            Rationale = riskEvaluation.Rationale,
            AppliedConstraints = riskEvaluation.RecommendedConstraints,
            RequiresHumanReview = riskEvaluation.RequiresHumanReview,
            HumanReviewCaseId = reviewCaseId,
            AppliedPolicies = [policy.ProfileName],
            DetectedSignals = riskEvaluation.AppliedSignals,
            RedactedOutput = outputValidation.RedactedOutput,
            Metadata = BuildMetadata(policy, contentSafetyResult.ProviderName, null),
            EvaluatedAt = DateTimeOffset.UtcNow,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    public async Task<GuardrailEvaluationResult> EvaluateFullAsync(
        FullEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var inputResult = await EvaluateInputAsync(
            new InputEvaluationRequest
            {
                TenantContext = request.TenantContext,
                UserPrompt = request.UserPrompt,
                SystemPrompt = request.SystemPrompt,
                DataSources = request.DataSources,
                RequestedTools = request.RequestedTools,
                Metadata = request.Metadata
            },
            cancellationToken);

        if (inputResult.Decision is DecisionType.Block or DecisionType.Escalate)
            return inputResult;

        return await EvaluateOutputAsync(
            new OutputEvaluationRequest
            {
                TenantContext = request.TenantContext,
                InputExecutionId = inputResult.ExecutionId,
                ModelOutput = request.ModelOutput,
                OutputSchemaJson = request.OutputSchemaJson,
                AppliedConstraints = inputResult.AppliedConstraints,
                Metadata = request.Metadata
            },
            cancellationToken);
    }

    private static string ComputeInputHash(InputEvaluationRequest request)
        => Sha256Hasher.Hash(
            JsonSerializer.Serialize(
                new
                {
                    request.UserPrompt,
                    request.SystemPrompt,
                    Sources = request.DataSources.Select(x => new { x.SourceId, x.SourceType, x.TenantId }),
                    Tools = request.RequestedTools.Select(x => x.ToolName),
                    request.Metadata
                },
                JsonOptions));

    private static RiskEvaluationInput BuildRiskInput(
        TenantContext tenantContext,
        EffectivePolicy policy,
        ContentSafetyResult? contentSafetyResult,
        PromptShieldResult? promptShieldResult,
        PolicyEvaluationResult? policyEvaluationResult,
        ToolValidationResult? toolValidationResult,
        ContextFirewallResult? contextFirewallResult,
        OutputValidationResult? outputValidationResult)
        => new()
        {
            TenantContext = tenantContext,
            ContentSafetyResult = contentSafetyResult,
            PromptShieldResult = promptShieldResult,
            PolicyEvaluationResult = policyEvaluationResult,
            ToolValidationResult = toolValidationResult,
            ContextFirewallResult = contextFirewallResult,
            OutputValidationResult = outputValidationResult,
            Weights = policy.Configuration.RiskWeights,
            ContentRiskThreshold = policy.Configuration.ContentRiskThreshold,
            PrivacyRiskThreshold = policy.Configuration.PrivacyRiskThreshold,
            InjectionRiskThreshold = policy.Configuration.InjectionRiskThreshold,
            EscalationThreshold = policy.Configuration.EscalationThreshold,
            BlockThreshold = policy.Configuration.BlockThreshold
        };

    private async Task<RiskAssessment> PersistAssessmentAsync(
        Guid executionId,
        string actor,
        RiskEvaluationResult riskEvaluation,
        CancellationToken cancellationToken)
    {
        var assessment = RiskAssessment.Create(
            executionId,
            riskEvaluation.Score.ContentRisk,
            riskEvaluation.Score.PrivacyRisk,
            riskEvaluation.Score.InjectionRisk,
            riskEvaluation.Score.BusinessPolicyRisk,
            riskEvaluation.Score.ActionRisk,
            riskEvaluation.Score.OutputQualityRisk,
            riskEvaluation.Score.WeightedTotal,
            riskEvaluation.Decision,
            riskEvaluation.Rationale,
            riskEvaluation.AppliedSignals.Count,
            JsonSerializer.Serialize(riskEvaluation.RecommendedConstraints, JsonOptions),
            actor);

        return await _auditRepository.AddRiskAssessmentAsync(assessment, cancellationToken);
    }

    private async Task PersistSignalsAsync(
        Guid assessmentId,
        ContentSafetyResult? contentSafetyResult,
        PromptShieldResult? promptShieldResult,
        PolicyEvaluationResult? policyEvaluationResult,
        OutputValidationResult? outputValidationResult,
        CancellationToken cancellationToken)
    {
        var signals = new List<RiskSignal>();

        if (contentSafetyResult is not null)
        {
            signals.AddRange(contentSafetyResult.Flags
                .Where(x => x.Flagged)
                .Select(flag => RiskSignal.Create(
                    assessmentId,
                    "ContentSafety",
                    flag.Category switch
                    {
                        "PII" => ContentCategory.PII,
                        "PHI" => ContentCategory.PHI,
                        "Violence" => ContentCategory.Violence,
                        "SelfHarm" => ContentCategory.SelfHarm,
                        "Sexual" => ContentCategory.Sexual,
                        "Hate" => ContentCategory.HateSpeech,
                        _ => ContentCategory.Restricted
                    },
                    flag.Score >= 0.85m ? RuleSeverity.Critical : RuleSeverity.High,
                    flag.Score,
                    flag.Detail ?? flag.Category,
                    contentSafetyResult.ProviderName,
                    createdBy: "system")));
        }

        if (promptShieldResult is not null)
        {
            signals.AddRange(promptShieldResult.Signals.Select(signal =>
                RiskSignal.Create(
                    assessmentId,
                    "PromptShield",
                    ContentCategory.PromptInjection,
                    signal.Score >= 0.85m ? RuleSeverity.Critical : RuleSeverity.High,
                    signal.Score,
                    signal.Description,
                    _promptShieldProvider.ProviderName,
                    createdBy: "system")));
        }

        if (policyEvaluationResult is not null)
        {
            signals.AddRange(policyEvaluationResult.Violations.Select(signal =>
                RiskSignal.Create(
                    assessmentId,
                    "Policy",
                    ContentCategory.Restricted,
                    Enum.TryParse<RuleSeverity>(signal.Severity, true, out var severity) ? severity : RuleSeverity.Medium,
                    signal.Score,
                    signal.Description,
                    "policy-engine",
                    createdBy: "system")));
        }

        if (outputValidationResult is not null)
        {
            signals.AddRange(outputValidationResult.Violations.Select(signal =>
                RiskSignal.Create(
                    assessmentId,
                    "OutputValidation",
                    ContentCategory.UnverifiedClaim,
                    Enum.TryParse<RuleSeverity>(signal.Severity, true, out var severity) ? severity : RuleSeverity.Medium,
                    signal.Severity.Equals("High", StringComparison.OrdinalIgnoreCase) ? 0.8m : 0.5m,
                    signal.Description,
                    "output-validator",
                    createdBy: "system")));
        }

        if (signals.Count == 0)
            return;

        _dbContext.RiskSignals.AddRange(signals);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> CreateReviewCaseIfRequiredAsync(
        GuardrailExecution execution,
        TenantContext tenantContext,
        RiskEvaluationResult riskEvaluation,
        string safeSummary,
        CancellationToken cancellationToken)
    {
        if (!riskEvaluation.RequiresHumanReview)
            return null;

        var reviewCase = HumanReviewCase.Create(
            execution.Id,
            tenantContext.TenantId,
            tenantContext.ApplicationId,
            riskEvaluation.Rationale,
            riskEvaluation.Score.Level,
            riskEvaluation.Decision,
            safeSummary,
            tenantContext.UserId);

        await _humanReviewRepository.AddAsync(reviewCase, cancellationToken);
        return reviewCase.Id;
    }

    private async Task PersistAuditEventAsync(
        Guid executionId,
        TenantContext tenantContext,
        string eventType,
        string eventCategory,
        RiskEvaluationResult riskEvaluation,
        string safeSummary,
        CancellationToken cancellationToken)
    {
        var auditEvent = AuditEvent.Create(
            tenantContext.TenantId,
            tenantContext.ApplicationId,
            tenantContext.UserId,
            eventType,
            eventCategory,
            riskEvaluation.Rationale,
            safeSummary,
            riskEvaluation.Score.Level,
            tenantContext.CorrelationId,
            executionId,
            riskEvaluation.Decision,
            riskEvaluation.Decision is DecisionType.Block or DecisionType.Escalate,
            new Dictionary<string, string>
            {
                ["environment"] = tenantContext.Environment,
                ["applicationId"] = tenantContext.ApplicationId.ToString("D")
            },
            tenantContext.UserId);

        await _auditRepository.AddAsync(auditEvent, cancellationToken);
    }

    private static string BuildSafeSummary(string phase, string content, int sourceCount, int toolCount)
        => JsonSerializer.Serialize(
            new
            {
                phase,
                contentLength = content?.Length ?? 0,
                sourceCount,
                toolCount
            },
            JsonOptions);

    private static Dictionary<string, object> BuildMetadata(EffectivePolicy policy, string contentSafetyProvider, string? promptShieldProvider)
    {
        var metadata = new Dictionary<string, object>
        {
            ["policyProfile"] = policy.ProfileName,
            ["policyVersion"] = policy.Version,
            ["contentSafetyProvider"] = contentSafetyProvider
        };

        if (!string.IsNullOrWhiteSpace(promptShieldProvider))
            metadata["promptShieldProvider"] = promptShieldProvider;

        return metadata;
    }
}
