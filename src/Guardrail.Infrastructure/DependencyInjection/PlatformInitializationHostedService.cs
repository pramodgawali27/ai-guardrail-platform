using System.Text.Json;
using System.Text.Json.Serialization;
using Guardrail.Core.Domain.Entities;
using Guardrail.Infrastructure.Evaluation;
using Guardrail.Infrastructure.Persistence;
using Guardrail.Infrastructure.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardrail.Infrastructure.DependencyInjection;

public sealed class PlatformInitializationHostedService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly GuardrailPlatformOptions _platformOptions;
    private readonly ILogger<PlatformInitializationHostedService> _logger;

    static PlatformInitializationHostedService()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public PlatformInitializationHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<GuardrailPlatformOptions> platformOptions,
        ILogger<PlatformInitializationHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _platformOptions = platformOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GuardrailDbContext>();

        if (_platformOptions.ApplyDatabaseOnStartup)
        {
            var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
            if (isSqlite)
            {
                // SQLite demo mode: create schema directly from model (no migration files needed)
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("SQLite demo database initialized via EnsureCreated.");
            }
            else
            {
                var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pending.Any())
                    await dbContext.Database.MigrateAsync(cancellationToken);
            }
        }

        if (_platformOptions.SeedDataOnStartup)
        {
            await SeedPoliciesAsync(dbContext, cancellationToken);
            await SeedEvaluationDatasetsAsync(dbContext, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private async Task SeedPoliciesAsync(GuardrailDbContext dbContext, CancellationToken cancellationToken)
    {
        var policyDirectory = ResolvePath(_platformOptions.PolicySeedPath);
        if (!Directory.Exists(policyDirectory))
        {
            _logger.LogWarning("Policy seed directory {PolicyDirectory} was not found. Skipping seed.", policyDirectory);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(policyDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var policyFile = JsonSerializer.Deserialize<SeedPolicyFile>(await File.ReadAllTextAsync(file, cancellationToken), JsonOptions);
            if (policyFile is null)
                continue;

            var exists = await dbContext.PolicyProfiles.AnyAsync(
                x =>
                    x.Name == policyFile.Name &&
                    x.TenantId == policyFile.TenantId &&
                    x.ApplicationId == policyFile.ApplicationId,
                cancellationToken);

            if (exists)
                continue;

            var profile = PolicyProfile.Create(
                policyFile.Name,
                policyFile.Scope,
                policyFile.EffectiveFrom,
                policyFile.TenantId,
                policyFile.ApplicationId,
                policyFile.Domain,
                policyFile.Description,
                JsonSerializer.Serialize(policyFile.Policy, JsonOptions),
                policyFile.ParentProfileId,
                policyFile.EffectiveTo,
                "seed");

            dbContext.PolicyProfiles.Add(profile);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedEvaluationDatasetsAsync(GuardrailDbContext dbContext, CancellationToken cancellationToken)
    {
        var datasetDirectory = ResolvePath(_platformOptions.EvaluationSeedPath);
        if (!Directory.Exists(datasetDirectory))
        {
            _logger.LogWarning("Evaluation seed directory {DatasetDirectory} was not found. Skipping seed.", datasetDirectory);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(datasetDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var datasetFile = JsonSerializer.Deserialize<SeedEvaluationDatasetFile>(await File.ReadAllTextAsync(file, cancellationToken), JsonOptions);
            if (datasetFile is null)
                continue;

            var exists = await dbContext.EvaluationDatasets.AnyAsync(
                x => x.TenantId == datasetFile.TenantId && x.Name == datasetFile.Name && x.Version == datasetFile.Version,
                cancellationToken);

            if (exists)
                continue;

            var dataset = EvaluationDataset.Create(
                datasetFile.TenantId,
                datasetFile.Name,
                datasetFile.Category,
                JsonSerializer.Serialize(datasetFile.Cases, JsonOptions),
                datasetFile.Cases.Count,
                datasetFile.Version,
                datasetFile.Description,
                datasetFile.Tags,
                "seed");

            dbContext.EvaluationDatasets.Add(dataset);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ResolvePath(string configuredPath)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
}

internal sealed class SeedEvaluationDatasetFile
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public List<string> Tags { get; set; } = new();
    public List<Guardrail.Core.Abstractions.EvaluationCaseDefinition> Cases { get; set; } = new();
}
