using System.Text.Json;
using System.Text.Json.Serialization;
using Guardrail.API.Models;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardrail.API.Controllers;

[ApiController]
[Authorize("EvaluatePolicy")]
[Produces("application/json")]
public sealed class ChatCompletionsController : ControllerBase
{
    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly HuggingFaceInferenceClient _hfClient;
    private readonly ILogger<ChatCompletionsController> _logger;

    public ChatCompletionsController(
        IGuardrailOrchestrator orchestrator,
        HuggingFaceInferenceClient hfClient,
        ILogger<ChatCompletionsController> logger)
    {
        _orchestrator = orchestrator;
        _hfClient = hfClient;
        _logger = logger;
    }

    [HttpPost("/v1/chat/completions")]
    [HttpPost("/api/proxy/chat/completions")]
    public async Task<IActionResult> CreateChatCompletion(
        [FromBody] OpenAiChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Messages.Count == 0)
            return BadRequest(BuildProxyError("At least one message is required.", "invalid_request_error", "messages_required"));

        var tenantContext = ExtractTenantContext();
        var systemPrompt = string.Join(
            Environment.NewLine,
            request.Messages
                .Where(message => message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Select(message => FlattenContent(message.Content))
                .Where(content => !string.IsNullOrWhiteSpace(content)));

        var promptTranscript = string.Join(
            Environment.NewLine + Environment.NewLine,
            request.Messages
                .Where(message => !message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Select(message => $"{NormalizeRole(message.Role)}: {FlattenContent(message.Content)}")
                .Where(content => !string.IsNullOrWhiteSpace(content)));

        if (string.IsNullOrWhiteSpace(promptTranscript))
        {
            return BadRequest(BuildProxyError(
                "At least one non-system message with content is required.",
                "invalid_request_error",
                "prompt_required"));
        }

        var inputResult = await _orchestrator.EvaluateInputAsync(new InputEvaluationRequest
        {
            TenantContext = tenantContext,
            UserPrompt = promptTranscript,
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            DataSources = request.DataSources?.Select(source => new SourceDescriptor
            {
                SourceId = source.SourceId,
                SourceType = source.SourceType,
                TenantId = source.TenantId,
                TrustLevel = source.TrustLevel,
                Uri = source.Uri,
                Metadata = source.Metadata ?? new()
            }).ToList() ?? new(),
            RequestedTools = request.RequestedTools?.Select(tool => new ToolCallDescriptor
            {
                ToolName = tool.ToolName,
                Parameters = tool.Parameters?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new()
            }).ToList() ?? new(),
            Metadata = BuildMetadata(request.Metadata)
        }, cancellationToken);

        if (inputResult.Decision is DecisionType.Block or DecisionType.Escalate)
        {
            return StatusCode(StatusCodes.Status403Forbidden, BuildGuardrailBlockedResponse(
                "Prompt was blocked by the input guardrail.",
                inputResult,
                null,
                inputResult.Decision == DecisionType.Escalate ? "guardrail_escalation" : "guardrail_blocked"));
        }

        var modelResponse = await _hfClient.ChatAsync(
            request.Messages.Select(message => new HfMessage(NormalizeRole(message.Role), FlattenContent(message.Content))).ToList(),
            request.Model,
            request.MaxTokens,
            request.Temperature,
            cancellationToken);

        if (!modelResponse.Success)
        {
            _logger.LogWarning("Chat proxy model invocation failed for correlation {CorrelationId}: {Error}", tenantContext.CorrelationId, modelResponse.ErrorMessage);
            return StatusCode(StatusCodes.Status502BadGateway, BuildProxyError(
                modelResponse.ErrorMessage ?? "Downstream model invocation failed.",
                "model_error",
                "upstream_model_failed",
                new
                {
                    input = BuildGuardrailSummary(inputResult)
                }));
        }

        var outputResult = await _orchestrator.EvaluateOutputAsync(new OutputEvaluationRequest
        {
            TenantContext = tenantContext,
            InputExecutionId = inputResult.ExecutionId,
            ModelOutput = modelResponse.Content,
            OutputSchemaJson = request.OutputSchemaJson,
            AppliedConstraints = inputResult.AppliedConstraints,
            Metadata = BuildMetadata(request.Metadata)
        }, cancellationToken);

        if (outputResult.Decision is DecisionType.Block or DecisionType.Escalate)
        {
            return StatusCode(StatusCodes.Status403Forbidden, BuildGuardrailBlockedResponse(
                "Model output was blocked by the output guardrail.",
                inputResult,
                outputResult,
                outputResult.Decision == DecisionType.Escalate ? "guardrail_escalation" : "guardrail_blocked"));
        }

        var finalContent = outputResult.RedactedOutput ?? modelResponse.Content;
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Ok(new
        {
            id = $"chatcmpl_{inputResult.ExecutionId:N}",
            @object = "chat.completion",
            created,
            model = modelResponse.ModelId,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = finalContent
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0
            },
            guardrail = new
            {
                input = BuildGuardrailSummary(inputResult),
                output = BuildGuardrailSummary(outputResult),
                wasRedacted = outputResult.RedactedOutput is not null,
                appliedConstraints = outputResult.AppliedConstraints
            }
        });
    }

    private TenantContext ExtractTenantContext()
    {
        var tenantIdStr = Request.Headers["X-Tenant-Id"].ToString();
        var appIdStr = Request.Headers["X-Application-Id"].ToString();
        var sessionId = Request.Headers["X-Session-Id"].ToString();
        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "proxy-client";

        if (!Guid.TryParse(tenantIdStr, out var tenantId))
            throw new ArgumentException("Header 'X-Tenant-Id' is missing or is not a valid GUID.");

        if (!Guid.TryParse(appIdStr, out var appId))
            throw new ArgumentException("Header 'X-Application-Id' is missing or is not a valid GUID.");

        return new TenantContext
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserId = userId,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId,
            CorrelationId = HttpContext.TraceIdentifier,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "production"
        };
    }

    private static string FlattenContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(
                Environment.NewLine,
                content.EnumerateArray().Select(FlattenContentPart).Where(part => !string.IsNullOrWhiteSpace(part))),
            JsonValueKind.Object => FlattenContentPart(content),
            _ => content.GetRawText()
        };
    }

    private static string FlattenContentPart(JsonElement part)
    {
        if (part.ValueKind == JsonValueKind.String)
            return part.GetString() ?? string.Empty;

        if (part.ValueKind == JsonValueKind.Object &&
            part.TryGetProperty("text", out var textElement) &&
            textElement.ValueKind == JsonValueKind.String)
        {
            return textElement.GetString() ?? string.Empty;
        }

        return part.GetRawText();
    }

    private static string NormalizeRole(string role)
        => role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
            ? "assistant"
            : role.Equals("system", StringComparison.OrdinalIgnoreCase)
                ? "system"
                : "user";

    private static Dictionary<string, string> BuildMetadata(Dictionary<string, string>? metadata)
    {
        var result = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

        result["proxySurface"] = "openai-chat-completions";
        return result;
    }

    private static object BuildGuardrailSummary(GuardrailEvaluationResult result)
        => new
        {
            executionId = result.ExecutionId,
            decision = result.Decision.ToString(),
            riskLevel = result.RiskLevel.ToString(),
            normalizedRiskScore = result.NormalizedRiskScore,
            rationale = result.Rationale,
            detectedSignals = result.DetectedSignals,
            requiresHumanReview = result.RequiresHumanReview,
            humanReviewCaseId = result.HumanReviewCaseId,
            appliedPolicies = result.AppliedPolicies
        };

    private static object BuildGuardrailBlockedResponse(string message, GuardrailEvaluationResult inputResult, GuardrailEvaluationResult? outputResult, string code)
        => BuildProxyError(message, "guardrail_error", code, new
        {
            input = BuildGuardrailSummary(inputResult),
            output = outputResult is null ? null : BuildGuardrailSummary(outputResult)
        });

    private static object BuildProxyError(string message, string type, string code, object? guardrail = null)
        => new
        {
            error = new
            {
                message,
                type,
                code,
                guardrail
            }
        };
}

public sealed class OpenAiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("messages")]
    public List<OpenAiChatMessage> Messages { get; init; } = new();

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("data_sources")]
    public List<SourceDescriptorApiModel>? DataSources { get; init; }

    [JsonPropertyName("requested_tools")]
    public List<ToolCallApiModel>? RequestedTools { get; init; }

    [JsonPropertyName("output_schema_json")]
    public string? OutputSchemaJson { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement Content { get; init; }
}
