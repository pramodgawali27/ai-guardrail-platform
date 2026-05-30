using System.Text.Json;
using System.Text.Json.Nodes;
using Guardrail.API.Models;
using Guardrail.API.Services;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardrail.API.Controllers;

[ApiController]
[Route("mcp")]
[Authorize("EvaluatePolicy")]
[Produces("application/json")]
public sealed class McpController : ControllerBase
{
    private const string ProtocolVersion = "2025-11-25";
    private const string ServerName = "enterprise-ai-guardrail-platform";
    private const string ServerVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IGuardrailOrchestrator _orchestrator;
    private readonly IToolRegistryService _toolRegistryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpController> _logger;

    public McpController(
        IGuardrailOrchestrator orchestrator,
        IToolRegistryService toolRegistryService,
        IConfiguration configuration,
        ILogger<McpController> logger)
    {
        _orchestrator = orchestrator;
        _toolRegistryService = toolRegistryService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        Response.Headers["MCP-Protocol-Version"] = ProtocolVersion;
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            error = "This MCP endpoint supports JSON-RPC over HTTP POST. SSE streams are not enabled."
        });
    }

    [HttpDelete]
    [AllowAnonymous]
    public IActionResult Delete()
    {
        Response.Headers["MCP-Protocol-Version"] = ProtocolVersion;
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            error = "Session termination is not required because this MCP endpoint is stateless."
        });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        Response.Headers["MCP-Protocol-Version"] = ProtocolVersion;

        if (!IsOriginAllowed(Request.Headers.Origin))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Invalid Origin header for MCP endpoint."
            });
        }

        if (payload.ValueKind != JsonValueKind.Object)
            return Ok(BuildJsonRpcError(null, -32600, "Invalid JSON-RPC request."));

        payload.TryGetProperty("id", out var idElement);
        var id = CloneNode(idElement);

        if (!payload.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
        {
            if (id is null)
                return Accepted();

            return Ok(BuildJsonRpcError(id, -32600, "JSON-RPC method is required."));
        }

        var method = methodElement.GetString() ?? string.Empty;
        if (id is null)
        {
            _logger.LogDebug("Ignoring MCP notification {Method}", method);
            return Accepted();
        }

        try
        {
            return method switch
            {
                "initialize" => Ok(BuildJsonRpcResult(id, HandleInitialize(payload))),
                "ping" => Ok(BuildJsonRpcResult(id, new { })),
                "tools/list" => Ok(BuildJsonRpcResult(id, new { tools = BuildTools() })),
                "tools/call" => Ok(BuildJsonRpcResult(id, await HandleToolsCallAsync(payload, cancellationToken))),
                _ => Ok(BuildJsonRpcError(id, -32601, $"Method '{method}' is not supported by this MCP server."))
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "MCP argument validation failed for method {Method}", method);
            return Ok(BuildJsonRpcResult(id, BuildToolError(ex.Message)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP request failed for method {Method}", method);
            return Ok(BuildJsonRpcError(id, -32603, "MCP server failed to process the request."));
        }
    }

    private object HandleInitialize(JsonElement payload)
    {
        if (payload.TryGetProperty("params", out var paramsElement) &&
            paramsElement.ValueKind == JsonValueKind.Object &&
            paramsElement.TryGetProperty("protocolVersion", out var clientProtocolElement) &&
            clientProtocolElement.ValueKind == JsonValueKind.String)
        {
            var clientProtocol = clientProtocolElement.GetString();
            if (!string.IsNullOrWhiteSpace(clientProtocol) &&
                clientProtocol is not "2025-03-26" and not "2025-06-18" and not ProtocolVersion)
            {
                throw new ArgumentException(
                    $"Unsupported MCP protocol version '{clientProtocol}'. Supported versions: 2025-03-26, 2025-06-18, {ProtocolVersion}.");
            }
        }

        return new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = ServerName,
                version = ServerVersion
            }
        };
    }

    private async Task<object> HandleToolsCallAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!payload.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("tools/call requires an object params payload.");

        if (!paramsElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            throw new ArgumentException("tools/call requires a tool name.");

        var toolName = nameElement.GetString() ?? string.Empty;
        var argumentsElement = paramsElement.TryGetProperty("arguments", out var argsElement) ? argsElement : default;

            return toolName switch
        {
            "guardrail.evaluate_input" => await ExecuteEvaluateInputAsync(argumentsElement, cancellationToken),
            "guardrail.evaluate_context" => await ExecuteEvaluateContextAsync(argumentsElement, cancellationToken),
            "guardrail.evaluate_tool_call" => await ExecuteEvaluateToolCallAsync(argumentsElement, cancellationToken),
            "guardrail.evaluate_output" => await ExecuteEvaluateOutputAsync(argumentsElement, cancellationToken),
            "guardrail.evaluate_full" => await ExecuteEvaluateFullAsync(argumentsElement, cancellationToken),
            "guardrail.get_tool_registry" => await ExecuteGetToolRegistryAsync(argumentsElement, cancellationToken),
            "guardrail.get_manifest" => ExecuteGetManifest(),
            _ => throw new ArgumentException($"Unknown tool: {toolName}")
        };
    }

    private async Task<object> ExecuteEvaluateInputAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpEvaluateInputArguments>(argumentsElement);
        var result = await _orchestrator.EvaluateInputAsync(new InputEvaluationRequest
        {
            TenantContext = BuildTenantContext(args.Tenant),
            UserPrompt = args.UserPrompt,
            SystemPrompt = args.SystemPrompt,
            DataSources = args.DataSources?.Select(source => new SourceDescriptor
            {
                SourceId = source.SourceId,
                SourceType = source.SourceType,
                TenantId = source.TenantId,
                TrustLevel = source.TrustLevel,
                Uri = source.Uri,
                Metadata = source.Metadata ?? new()
            }).ToList() ?? new(),
            RequestedTools = args.RequestedTools?.Select(tool => new ToolCallDescriptor
            {
                ToolName = tool.ToolName,
                Parameters = tool.Parameters?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new()
            }).ToList() ?? new(),
            Metadata = args.Metadata ?? new()
        }, cancellationToken);

        return BuildToolSuccess(result);
    }

    private async Task<object> ExecuteEvaluateContextAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpEvaluateContextArguments>(argumentsElement);
        var result = await _orchestrator.EvaluateContextAsync(new ContextEvaluationRequest
        {
            TenantContext = BuildTenantContext(args.Tenant),
            DataSources = args.DataSources?.Select(source => new SourceDescriptor
            {
                SourceId = source.SourceId,
                SourceType = source.SourceType,
                TenantId = source.TenantId,
                TrustLevel = source.TrustLevel,
                Uri = source.Uri,
                Metadata = source.Metadata ?? new()
            }).ToList() ?? new(),
            Metadata = args.Metadata ?? new()
        }, cancellationToken);

        return BuildToolSuccess(result);
    }

    private async Task<object> ExecuteEvaluateToolCallAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpEvaluateToolCallArguments>(argumentsElement);
        var result = await _orchestrator.EvaluateToolCallAsync(new ToolCallEvaluationRequest
        {
            TenantContext = BuildTenantContext(args.Tenant),
            RequestedTools = args.RequestedTools?.Select(tool => new ToolCallDescriptor
            {
                ToolName = tool.ToolName,
                Parameters = tool.Parameters?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new()
            }).ToList() ?? new(),
            Metadata = args.Metadata ?? new()
        }, cancellationToken);

        return BuildToolSuccess(result);
    }

    private async Task<object> ExecuteEvaluateOutputAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpEvaluateOutputArguments>(argumentsElement);
        var result = await _orchestrator.EvaluateOutputAsync(new OutputEvaluationRequest
        {
            TenantContext = BuildTenantContext(args.Tenant),
            InputExecutionId = args.InputExecutionId,
            ModelOutput = args.ModelOutput,
            OutputSchemaJson = args.OutputSchemaJson,
            Metadata = args.Metadata ?? new()
        }, cancellationToken);

        return BuildToolSuccess(result);
    }

    private async Task<object> ExecuteEvaluateFullAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpEvaluateFullArguments>(argumentsElement);
        var result = await _orchestrator.EvaluateFullAsync(new FullEvaluationRequest
        {
            TenantContext = BuildTenantContext(args.Tenant),
            UserPrompt = args.UserPrompt,
            SystemPrompt = args.SystemPrompt,
            ModelOutput = args.ModelOutput,
            DataSources = args.DataSources?.Select(source => new SourceDescriptor
            {
                SourceId = source.SourceId,
                SourceType = source.SourceType,
                TenantId = source.TenantId,
                TrustLevel = source.TrustLevel,
                Uri = source.Uri,
                Metadata = source.Metadata ?? new()
            }).ToList() ?? new(),
            RequestedTools = args.RequestedTools?.Select(tool => new ToolCallDescriptor
            {
                ToolName = tool.ToolName,
                Parameters = tool.Parameters?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new()
            }).ToList() ?? new(),
            OutputSchemaJson = args.OutputSchemaJson,
            Metadata = args.Metadata ?? new()
        }, cancellationToken);

        return BuildToolSuccess(result);
    }

    private async Task<object> ExecuteGetToolRegistryAsync(JsonElement argumentsElement, CancellationToken cancellationToken)
    {
        var args = DeserializeArguments<McpToolRegistryArguments>(argumentsElement);
        var result = await _toolRegistryService.GetRegistryAsync(BuildTenantContext(args.Tenant), cancellationToken);
        return BuildToolSuccess(result);
    }

    private object ExecuteGetManifest()
        => BuildToolSuccess(BuildManifest(HttpContext.Request));

    private static object BuildToolSuccess(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = json
                }
            },
            structuredContent = payload,
            isError = false
        };
    }

    private static object BuildToolError(string message)
        => new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = message
                }
            },
            isError = true
        };

    private static object BuildJsonRpcResult(JsonNode id, object result)
        => new
        {
            jsonrpc = "2.0",
            id,
            result
        };

    private static object BuildJsonRpcError(JsonNode? id, int code, string message)
        => new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        };

    private static JsonNode? CloneNode(JsonElement element)
        => element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : JsonNode.Parse(element.GetRawText());

    private static T DeserializeArguments<T>(JsonElement argumentsElement)
    {
        if (argumentsElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ArgumentException("Tool arguments are required.");

        if (argumentsElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Tool arguments must be a JSON object.");

        var result = JsonSerializer.Deserialize<T>(argumentsElement.GetRawText(), JsonOptions);
        if (result is null)
            throw new ArgumentException("Tool arguments could not be parsed.");

        return result;
    }

    private TenantContext BuildTenantContext(McpTenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new ArgumentException("tenant.tenantId is required.");

        if (tenant.ApplicationId == Guid.Empty)
            throw new ArgumentException("tenant.applicationId is required.");

        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? tenant.UserId
            ?? User.Identity?.Name
            ?? "mcp-client";

        return new TenantContext
        {
            TenantId = tenant.TenantId,
            ApplicationId = tenant.ApplicationId,
            UserId = userId,
            SessionId = string.IsNullOrWhiteSpace(tenant.SessionId) ? Guid.NewGuid().ToString("N") : tenant.SessionId,
            CorrelationId = string.IsNullOrWhiteSpace(tenant.CorrelationId) ? HttpContext.TraceIdentifier : tenant.CorrelationId,
            Environment = string.IsNullOrWhiteSpace(tenant.Environment)
                ? (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "production")
                : tenant.Environment
        };
    }

    private IEnumerable<object> BuildTools()
    {
        yield return new
        {
            name = "guardrail.evaluate_input",
            title = "Evaluate Prompt Input",
            description = "Run the input guardrail pipeline for a prompt before it reaches a downstream model.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant", "userPrompt" },
                properties = new
                {
                    tenant = BuildTenantSchema(),
                    userPrompt = new { type = "string", description = "The end-user prompt to evaluate." },
                    systemPrompt = new { type = "string", description = "Optional system prompt supplied by the caller." },
                    dataSources = new
                    {
                        type = "array",
                        items = BuildSourceDescriptorSchema()
                    },
                    requestedTools = new
                    {
                        type = "array",
                        items = BuildToolCallSchema()
                    },
                    metadata = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" }
                    }
                }
            }
        };

        yield return new
        {
            name = "guardrail.evaluate_context",
            title = "Evaluate Retrieved Context",
            description = "Validate source trust, tenant boundaries, and data-source policy before retrieved context is sent to a model.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant", "dataSources" },
                properties = new
                {
                    tenant = BuildTenantSchema(),
                    dataSources = new
                    {
                        type = "array",
                        items = BuildSourceDescriptorSchema()
                    },
                    metadata = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" }
                    }
                }
            }
        };

        yield return new
        {
            name = "guardrail.evaluate_tool_call",
            title = "Evaluate Tool Call",
            description = "Validate proposed MCP/tool actions before execution, including deny lists and human-approval gates.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant", "requestedTools" },
                properties = new
                {
                    tenant = BuildTenantSchema(),
                    requestedTools = new
                    {
                        type = "array",
                        items = BuildToolCallSchema()
                    },
                    metadata = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" }
                    }
                }
            }
        };

        yield return new
        {
            name = "guardrail.evaluate_output",
            title = "Evaluate Model Output",
            description = "Run the output guardrail pipeline against generated model text before returning it to a user.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant", "modelOutput" },
                properties = new
                {
                    tenant = BuildTenantSchema(),
                    inputExecutionId = new { type = "string", format = "uuid", description = "Optional input evaluation execution id." },
                    modelOutput = new { type = "string", description = "Raw model output to validate." },
                    outputSchemaJson = new { type = "string", description = "Optional JSON schema the output should satisfy." },
                    metadata = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" }
                    }
                }
            }
        };

        yield return new
        {
            name = "guardrail.evaluate_full",
            title = "Evaluate Full Exchange",
            description = "Evaluate both the prompt and the model output in one call when both are already available.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant", "userPrompt", "modelOutput" },
                properties = new
                {
                    tenant = BuildTenantSchema(),
                    userPrompt = new { type = "string" },
                    systemPrompt = new { type = "string" },
                    modelOutput = new { type = "string" },
                    dataSources = new
                    {
                        type = "array",
                        items = BuildSourceDescriptorSchema()
                    },
                    requestedTools = new
                    {
                        type = "array",
                        items = BuildToolCallSchema()
                    },
                    outputSchemaJson = new { type = "string" },
                    metadata = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" }
                    }
                }
            }
        };

        yield return new
        {
            name = "guardrail.get_tool_registry",
            title = "Get Tool Registry",
            description = "Return the effective tenant/application tool registry, including blocked and approval-gated tools.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "tenant" },
                properties = new
                {
                    tenant = BuildTenantSchema()
                }
            }
        };

        yield return new
        {
            name = "guardrail.get_manifest",
            title = "Get Integration Manifest",
            description = "Return the guardrail server manifest with REST and MCP integration endpoints.",
            inputSchema = new
            {
                type = "object",
                additionalProperties = false
            }
        };
    }

    private static object BuildTenantSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "tenantId", "applicationId" },
            properties = new
            {
                tenantId = new { type = "string", format = "uuid" },
                applicationId = new { type = "string", format = "uuid" },
                userId = new { type = "string" },
                sessionId = new { type = "string" },
                correlationId = new { type = "string" },
                environment = new { type = "string" }
            }
        };

    private static object BuildSourceDescriptorSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "sourceId", "sourceType" },
            properties = new
            {
                sourceId = new { type = "string" },
                sourceType = new { type = "string" },
                tenantId = new { type = "string" },
                trustLevel = new
                {
                    type = "string",
                    @enum = new[] { "Untrusted", "External", "Internal", "Verified", "Privileged" }
                },
                uri = new { type = "string" },
                metadata = new
                {
                    type = "object",
                    additionalProperties = new { type = "string" }
                }
            }
        };

    private static object BuildToolCallSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "toolName" },
            properties = new
            {
                toolName = new { type = "string" },
                parameters = new
                {
                    type = "object",
                    additionalProperties = new { type = "string" }
                },
                callId = new { type = "string" },
                isDestructive = new { type = "boolean" }
            }
        };

    private bool IsOriginAllowed(string? originHeader)
    {
        if (string.IsNullOrWhiteSpace(originHeader))
            return true;

        if (!Uri.TryCreate(originHeader, UriKind.Absolute, out var origin))
            return false;

        if (origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            origin.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var configuredOrigins = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        return configuredOrigins.Contains(originHeader, StringComparer.OrdinalIgnoreCase);
    }

    private static object BuildManifest(HttpRequest request)
    {
        var baseUri = $"{request.Scheme}://{request.Host.Value}";
        return new
        {
            name = ServerName,
            version = ServerVersion,
            protocols = new
            {
                rest = new
                {
                    evaluateInput = $"{baseUri}/api/guardrail/evaluate-input",
                    evaluateContext = $"{baseUri}/api/guardrail/evaluate-context",
                    evaluateToolCall = $"{baseUri}/api/guardrail/evaluate-tool-call",
                    evaluateOutput = $"{baseUri}/api/guardrail/evaluate-output",
                    evaluateFull = $"{baseUri}/api/guardrail/evaluate-full",
                    toolRegistry = $"{baseUri}/api/tools/registry",
                    chatCompletions = $"{baseUri}/v1/chat/completions"
                },
                mcp = new
                {
                    endpoint = $"{baseUri}/mcp",
                    protocolVersion = ProtocolVersion,
                    tools = new[]
                    {
                        "guardrail.evaluate_input",
                        "guardrail.evaluate_context",
                        "guardrail.evaluate_tool_call",
                        "guardrail.evaluate_output",
                        "guardrail.evaluate_full",
                        "guardrail.get_tool_registry",
                        "guardrail.get_manifest"
                    }
                }
            },
            authentication = new
            {
                scheme = "Bearer",
                requiredHeaders = new[] { "Authorization" },
                tenantHeaders = new[] { "X-Tenant-Id", "X-Application-Id" }
            }
        };
    }
}

public sealed class McpTenantContext
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public string? CorrelationId { get; init; }
    public string? Environment { get; init; }
}

public sealed class McpEvaluateInputArguments
{
    public McpTenantContext Tenant { get; init; } = new();
    public string UserPrompt { get; init; } = string.Empty;
    public string? SystemPrompt { get; init; }
    public List<SourceDescriptorApiModel>? DataSources { get; init; }
    public List<ToolCallApiModel>? RequestedTools { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class McpEvaluateOutputArguments
{
    public McpTenantContext Tenant { get; init; } = new();
    public Guid? InputExecutionId { get; init; }
    public string ModelOutput { get; init; } = string.Empty;
    public string? OutputSchemaJson { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class McpEvaluateContextArguments
{
    public McpTenantContext Tenant { get; init; } = new();
    public List<SourceDescriptorApiModel>? DataSources { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class McpEvaluateToolCallArguments
{
    public McpTenantContext Tenant { get; init; } = new();
    public List<ToolCallApiModel>? RequestedTools { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class McpEvaluateFullArguments
{
    public McpTenantContext Tenant { get; init; } = new();
    public string UserPrompt { get; init; } = string.Empty;
    public string? SystemPrompt { get; init; }
    public string ModelOutput { get; init; } = string.Empty;
    public List<SourceDescriptorApiModel>? DataSources { get; init; }
    public List<ToolCallApiModel>? RequestedTools { get; init; }
    public string? OutputSchemaJson { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class McpToolRegistryArguments
{
    public McpTenantContext Tenant { get; init; } = new();
}
