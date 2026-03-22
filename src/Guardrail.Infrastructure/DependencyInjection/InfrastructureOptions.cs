namespace Guardrail.Infrastructure.DependencyInjection;

public sealed class GuardrailPlatformOptions
{
    public bool SeedDataOnStartup { get; set; } = true;
    public bool ApplyDatabaseOnStartup { get; set; } = true;
    public int PolicyCacheMinutes { get; set; } = 15;
    public string PolicySeedPath { get; set; } = "../../../../policies/samples";
    public string EvaluationSeedPath { get; set; } = "../../../../evaluations/datasets";
}

public sealed class AzureContentSafetyOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string ApiVersion { get; set; } = "2024-09-01";
    public bool UseHeuristicFallback { get; set; } = true;
}

public sealed class AzurePromptShieldOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string ApiVersion { get; set; } = "2024-09-01";
    public bool UseHeuristicFallback { get; set; } = true;
}
