using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardrail.API.Controllers;

[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public sealed class ManifestController : ControllerBase
{
    private const string ProtocolVersion = "2025-11-25";
    private const string ServerName = "enterprise-ai-guardrail-platform";
    private const string ServerVersion = "1.0.0";

    [HttpGet("/.well-known/ai-guardrail.json")]
    [HttpGet("/api/integrations/manifest")]
    public IActionResult GetManifest()
    {
        var baseUri = $"{Request.Scheme}://{Request.Host.Value}";

        return Ok(new
        {
            name = ServerName,
            version = ServerVersion,
            description = "Multi-tenant guardrail gateway with REST and MCP compatibility surfaces.",
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
                    transport = "streamable-http-jsonrpc",
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
                tenantHeaders = new[] { "X-Tenant-Id", "X-Application-Id", "X-Session-Id" }
            }
        });
    }
}
