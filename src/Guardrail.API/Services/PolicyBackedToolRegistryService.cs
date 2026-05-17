using System.Text.Json;
using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardrail.API.Services;

public interface IToolRegistryService
{
    Task<ToolRegistrySnapshot> GetRegistryAsync(TenantContext context, CancellationToken cancellationToken = default);
}

public sealed class ToolRegistrySnapshot
{
    public string PolicyName { get; init; } = string.Empty;
    public int PolicyVersion { get; init; }
    public bool AllowToolUse { get; init; }
    public IReadOnlyList<ToolRegistryItem> Tools { get; init; } = Array.Empty<ToolRegistryItem>();
}

public sealed class ToolRegistryItem
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsAllowed { get; init; }
    public bool RequiresApproval { get; init; }
    public string ActionRisk { get; init; } = ActionRiskLevel.Low.ToString();
    public string PolicySource { get; init; } = "effective-policy";
    public IReadOnlyList<string> AllowedParameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeniedParameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EnvironmentRestrictions { get; init; } = Array.Empty<string>();
}

public sealed class PolicyBackedToolRegistryService : IToolRegistryService
{
    private readonly IPolicyEngine _policyEngine;
    private readonly GuardrailDbContext _dbContext;

    public PolicyBackedToolRegistryService(IPolicyEngine policyEngine, GuardrailDbContext dbContext)
    {
        _policyEngine = policyEngine;
        _dbContext = dbContext;
    }

    public async Task<ToolRegistrySnapshot> GetRegistryAsync(TenantContext context, CancellationToken cancellationToken = default)
    {
        var policy = await _policyEngine.ResolveEffectivePolicyAsync(context, cancellationToken);

        var toolPolicies = await _dbContext.ToolPolicies
            .AsNoTracking()
            .Where(x => x.TenantId == context.TenantId && (x.ApplicationId == null || x.ApplicationId == context.ApplicationId))
            .OrderBy(x => x.ApplicationId.HasValue ? 1 : 0)
            .ThenBy(x => x.ToolName)
            .ToListAsync(cancellationToken);

        var mergedPolicies = new Dictionary<string, ToolPolicy>(StringComparer.OrdinalIgnoreCase);
        foreach (var toolPolicy in toolPolicies)
            mergedPolicies[toolPolicy.ToolName] = toolPolicy;

        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var toolName in policy.Configuration.AllowedTools)
            toolNames.Add(toolName);
        foreach (var toolName in policy.Configuration.DeniedTools)
            toolNames.Add(toolName);
        foreach (var toolName in policy.Configuration.ApprovalRequiredTools)
            toolNames.Add(toolName);
        foreach (var toolName in mergedPolicies.Keys)
            toolNames.Add(toolName);

        var items = toolNames
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(name => BuildItem(name, policy, mergedPolicies.GetValueOrDefault(name), context.Environment))
            .ToList();

        return new ToolRegistrySnapshot
        {
            PolicyName = policy.ProfileName,
            PolicyVersion = policy.Version,
            AllowToolUse = policy.Constraints.AllowToolUse,
            Tools = items
        };
    }

    private static ToolRegistryItem BuildItem(
        string toolName,
        EffectivePolicy policy,
        ToolPolicy? toolPolicy,
        string? environment)
    {
        var allowListConfigured = policy.Configuration.AllowedTools.Count > 0;
        var deniedByPolicy = policy.Configuration.DeniedTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);
        var allowedByPolicy = !deniedByPolicy &&
                              ((policy.Constraints.AllowToolUse && !allowListConfigured) ||
                               (allowListConfigured && policy.Configuration.AllowedTools.Contains(toolName, StringComparer.OrdinalIgnoreCase)));
        var requiresApproval = policy.Configuration.ApprovalRequiredTools.Contains(toolName, StringComparer.OrdinalIgnoreCase)
                               || toolPolicy?.RequiresApproval == true
                               || IsHighRiskTool(toolName);

        var allowedByDatabase = toolPolicy?.IsAllowed ?? true;
        var environmentAllowed = IsEnvironmentAllowed(toolPolicy?.EnvironmentRestrictions, environment);
        var isAllowed = allowedByPolicy && allowedByDatabase && environmentAllowed;

        return new ToolRegistryItem
        {
            Name = toolName,
            Description = toolPolicy?.ToolDescription ?? BuildFallbackDescription(toolName, requiresApproval, isAllowed),
            IsAllowed = isAllowed,
            RequiresApproval = isAllowed && requiresApproval,
            ActionRisk = ResolveActionRisk(toolPolicy, requiresApproval, isAllowed).ToString(),
            PolicySource = ResolveSource(toolPolicy),
            AllowedParameters = ParseJsonArray(toolPolicy?.AllowedParameters),
            DeniedParameters = ParseJsonArray(toolPolicy?.DeniedParameters),
            EnvironmentRestrictions = toolPolicy?.EnvironmentRestrictions ?? new List<string>()
        };
    }

    private static bool IsEnvironmentAllowed(IReadOnlyCollection<string>? restrictions, string? environment)
    {
        if (restrictions is null || restrictions.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(environment))
            return false;

        return restrictions.Contains(environment, StringComparer.OrdinalIgnoreCase);
    }

    private static ActionRiskLevel ResolveActionRisk(ToolPolicy? toolPolicy, bool requiresApproval, bool isAllowed)
    {
        if (toolPolicy is not null)
            return toolPolicy.ActionRisk;

        if (!isAllowed)
            return ActionRiskLevel.High;

        return requiresApproval ? ActionRiskLevel.Medium : ActionRiskLevel.Low;
    }

    private static string ResolveSource(ToolPolicy? toolPolicy)
        => toolPolicy is null ? "effective-policy" : "effective-policy+database";

    private static IReadOnlyList<string> ParseJsonArray(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildFallbackDescription(string toolName, bool requiresApproval, bool isAllowed)
    {
        var normalizedName = string.Join(
            ' ',
            toolName
                .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

        var title = string.IsNullOrWhiteSpace(normalizedName)
            ? toolName
            : char.ToUpperInvariant(normalizedName[0]) + normalizedName[1..];

        if (!isAllowed)
            return $"{title} is currently blocked by the active tenant/application policy.";

        if (requiresApproval)
            return $"{title} is available, but requires elevated approval before invocation.";

        return $"{title} is available under the active tenant/application policy.";
    }

    private static bool IsHighRiskTool(string toolName)
        => toolName.Contains("delete", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("export", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("publish", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("send", StringComparison.OrdinalIgnoreCase)
           || toolName.Contains("email", StringComparison.OrdinalIgnoreCase);
}
