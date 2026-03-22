using Guardrail.Core.Abstractions;

namespace Guardrail.Infrastructure.Firewalls;

public sealed class DefaultToolFirewall : IToolFirewall
{
    public Task<ToolValidationResult> ValidateToolsAsync(ToolValidationRequest request, CancellationToken ct = default)
    {
        var result = new ToolValidationResult
        {
            AllToolsAllowed = true
        };

        foreach (var tool in request.RequestedTools)
        {
            var name = tool.ToolName.Trim();
            var denied = request.Policy.Configuration.DeniedTools.Contains(name, StringComparer.OrdinalIgnoreCase);
            var allowListConfigured = request.Policy.Configuration.AllowedTools.Count > 0;
            var allowedByList = request.Policy.Configuration.AllowedTools.Contains(name, StringComparer.OrdinalIgnoreCase);
            var approvalRequired = request.Policy.Configuration.ApprovalRequiredTools.Contains(name, StringComparer.OrdinalIgnoreCase);

            if (denied || (!request.Policy.Constraints.AllowToolUse && !allowListConfigured) || (allowListConfigured && !allowedByList))
            {
                result.AllToolsAllowed = false;
                result.DeniedTools.Add(name);
                continue;
            }

            if (approvalRequired || IsHighRiskTool(name))
            {
                result.ApprovalRequiredTools.Add(name);
            }

            result.AllowedTools.Add(name);
        }

        result.ToolRiskScore =
            result.DeniedTools.Count > 0
                ? 1.0m
                : result.ApprovalRequiredTools.Count > 0
                    ? 0.65m
                    : result.AllowedTools.Count == 0
                        ? 0m
                        : 0.10m;

        return Task.FromResult(result);
    }

    private static bool IsHighRiskTool(string toolName)
        => toolName.Contains("delete", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("export", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("publish", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("send", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("email", StringComparison.OrdinalIgnoreCase);
}
