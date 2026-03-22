using System.Text.Json;
using System.Text.Json.Serialization;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Guardrail.Infrastructure.Evaluation;

public sealed class EvaluationRunProcessor
{
    private static readonly Guid DefaultApplicationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IEvaluationRepository _evaluationRepository;
    private readonly IGuardrailOrchestrator _guardrailOrchestrator;
    private readonly ILogger<EvaluationRunProcessor> _logger;

    static EvaluationRunProcessor()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public EvaluationRunProcessor(
        IEvaluationRepository evaluationRepository,
        IGuardrailOrchestrator guardrailOrchestrator,
        ILogger<EvaluationRunProcessor> logger)
    {
        _evaluationRepository = evaluationRepository;
        _guardrailOrchestrator = guardrailOrchestrator;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid runId, CancellationToken ct)
    {
        var run = await _evaluationRepository.GetRunByIdAsync(runId, ct)
            ?? throw new KeyNotFoundException($"Evaluation run '{runId}' was not found.");

        var dataset = await _evaluationRepository.GetDatasetByIdAsync(run.DatasetId, ct)
            ?? throw new KeyNotFoundException($"Evaluation dataset '{run.DatasetId}' was not found.");

        var cases = JsonSerializer.Deserialize<List<EvaluationCaseDefinition>>(dataset.DatasetJson, JsonOptions) ?? [];
        var results = new List<Core.Domain.Entities.EvaluationResult>(cases.Count);

        try
        {
            run.Start("evaluation-worker");
            await _evaluationRepository.UpdateRunAsync(run, ct);

            foreach (var evaluationCase in cases)
            {
                var tenantContext = BuildTenantContext(run, evaluationCase);

                try
                {
                    GuardrailEvaluationResult result;
                    if (!string.IsNullOrWhiteSpace(evaluationCase.ModelOutput))
                    {
                        result = await _guardrailOrchestrator.EvaluateFullAsync(
                            new FullEvaluationRequest
                            {
                                TenantContext = tenantContext,
                                UserPrompt = evaluationCase.UserPrompt,
                                SystemPrompt = evaluationCase.SystemPrompt,
                                ModelOutput = evaluationCase.ModelOutput,
                                DataSources = evaluationCase.DataSources,
                                RequestedTools = evaluationCase.RequestedTools,
                                OutputSchemaJson = evaluationCase.OutputSchemaJson,
                                Metadata = evaluationCase.Metadata
                            },
                            ct);
                    }
                    else
                    {
                        result = await _guardrailOrchestrator.EvaluateInputAsync(
                            new InputEvaluationRequest
                            {
                                TenantContext = tenantContext,
                                UserPrompt = evaluationCase.UserPrompt,
                                SystemPrompt = evaluationCase.SystemPrompt,
                                DataSources = evaluationCase.DataSources,
                                RequestedTools = evaluationCase.RequestedTools,
                                Metadata = evaluationCase.Metadata
                            },
                            ct);
                    }

                    results.Add(Core.Domain.Entities.EvaluationResult.Create(
                        run.Id,
                        evaluationCase.CaseId,
                        evaluationCase.CaseName,
                        BuildInputSummary(evaluationCase),
                        evaluationCase.ExpectedDecision,
                        result.Decision,
                        result.RiskLevel,
                        result.NormalizedRiskScore,
                        result.DurationMs,
                        result.Rationale,
                        "evaluation-worker"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Evaluation case {CaseId} failed during run {RunId}", evaluationCase.CaseId, run.Id);

                    results.Add(Core.Domain.Entities.EvaluationResult.Create(
                        run.Id,
                        evaluationCase.CaseId,
                        evaluationCase.CaseName,
                        BuildInputSummary(evaluationCase),
                        evaluationCase.ExpectedDecision,
                        DecisionType.Block,
                        RiskLevel.Critical,
                        100m,
                        0,
                        $"Case execution failed: {ex.Message}",
                        "evaluation-worker"));
                }
            }

            await _evaluationRepository.AddResultsAsync(results, ct);

            var totalCases = results.Count;
            var passedCases = results.Count(x => x.Passed);
            var failedCases = totalCases - passedCases;
            var falsePositives = results.Count(x => x.IsFalsePositive);
            var falseNegatives = results.Count(x => x.IsFalseNegative);
            var averageLatency = totalCases == 0 ? 0d : results.Average(x => x.LatencyMs);

            run.Complete(
                totalCases,
                passedCases,
                failedCases,
                falsePositives,
                falseNegatives,
                averageLatency,
                JsonSerializer.Serialize(new
                {
                    dataset = dataset.Name,
                    datasetVersion = dataset.Version,
                    executedAt = DateTimeOffset.UtcNow,
                    summary = new
                    {
                        totalCases,
                        passedCases,
                        failedCases,
                        falsePositives,
                        falseNegatives,
                        averageLatency
                    }
                }, JsonOptions),
                "evaluation-worker");

            await _evaluationRepository.UpdateRunAsync(run, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation run {RunId} failed", run.Id);
            run.Fail(ex.Message, "evaluation-worker");
            await _evaluationRepository.UpdateRunAsync(run, ct);
        }
    }

    private static TenantContext BuildTenantContext(Core.Domain.Entities.EvaluationRun run, EvaluationCaseDefinition evaluationCase)
    {
        var applicationId = evaluationCase.Metadata.TryGetValue("applicationId", out var rawApplicationId) &&
                            Guid.TryParse(rawApplicationId, out var parsedApplicationId)
            ? parsedApplicationId
            : DefaultApplicationId;

        return new TenantContext
        {
            TenantId = run.TenantId,
            ApplicationId = applicationId,
            UserId = "evaluation-runner",
            SessionId = run.Id.ToString("N"),
            CorrelationId = $"{run.Id:N}-{evaluationCase.CaseId}",
            Environment = "evaluation"
        };
    }

    private static string BuildInputSummary(EvaluationCaseDefinition evaluationCase)
        => JsonSerializer.Serialize(new
        {
            promptLength = evaluationCase.UserPrompt.Length,
            hasOutput = !string.IsNullOrWhiteSpace(evaluationCase.ModelOutput),
            toolCount = evaluationCase.RequestedTools.Count,
            sourceCount = evaluationCase.DataSources.Count
        }, JsonOptions);
}
