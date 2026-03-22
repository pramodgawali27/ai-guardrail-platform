using Guardrail.Core.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Guardrail.Infrastructure.Evaluation;
using Guardrail.Infrastructure.Firewalls;
using Guardrail.Infrastructure.Observability;
using Guardrail.Infrastructure.Orchestration;
using Guardrail.Infrastructure.Persistence;
using Guardrail.Infrastructure.Policies;
using Guardrail.Infrastructure.Providers;
using Guardrail.Infrastructure.Repositories;
using Guardrail.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Guardrail.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GuardrailPlatformOptions>(configuration.GetSection("Guardrail"));
        services.Configure<AzureContentSafetyOptions>(configuration.GetSection("Providers:AzureContentSafety"));
        services.Configure<AzurePromptShieldOptions>(configuration.GetSection("Providers:AzurePromptShield"));

        var pgConnectionString = configuration.GetConnectionString("PostgreSql")
            ?? configuration.GetConnectionString("DefaultConnection");

        // Use SQLite when no real PostgreSQL connection string is provided.
        // This enables zero-dependency demo mode (HF Spaces, local dev without Docker).
        var useSqlite = string.IsNullOrWhiteSpace(pgConnectionString)
            || !pgConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
               && !pgConnectionString.Contains("postgresql://", StringComparison.OrdinalIgnoreCase)
               && !pgConnectionString.Contains("postgres://", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<GuardrailDbContext>(options =>
        {
            if (useSqlite)
            {
                // Demo / HF Spaces mode — no external DB required
                options.UseSqlite("Data Source=/tmp/guardrail_demo.db");
            }
            else
            {
                options.UseNpgsql(pgConnectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__efmigrations_history", "guardrail");
                });
            }
        });

        services.AddMemoryCache();

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "guardrail:";
            });
        }

        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        services.AddScoped<IHumanReviewRepository, HumanReviewRepository>();

        services.AddScoped<IPolicyEngine, JsonPolicyEngine>();
        services.AddScoped<IRiskEngine, WeightedRiskEngine>();
        services.AddScoped<IContextFirewall, DefaultContextFirewall>();
        services.AddScoped<IToolFirewall, DefaultToolFirewall>();
        services.AddScoped<IOutputValidator, DefaultOutputValidator>();
        services.AddScoped<IGuardrailOrchestrator, GuardrailOrchestrator>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<EvaluationRunProcessor>();
        services.AddSingleton<GuardrailMetrics>();

        services.AddHttpClient<AzureContentSafetyProvider>();
        services.AddHttpClient<AzurePromptShieldProvider>();
        services.AddScoped<IContentSafetyProvider>(sp => sp.GetRequiredService<AzureContentSafetyProvider>());
        services.AddScoped<IPromptShieldProvider>(sp => sp.GetRequiredService<AzurePromptShieldProvider>());

        services.Configure<HuggingFaceOptions>(configuration.GetSection("HuggingFace"));
        services.AddHttpClient<HuggingFaceInferenceClient>();

        services.AddSingleton<EvaluationBackgroundQueue>();
        services.AddHostedService<EvaluationWorker>();
        services.AddHostedService<PlatformInitializationHostedService>();

        return services;
    }
}
