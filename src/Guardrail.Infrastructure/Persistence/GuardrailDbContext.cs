using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guardrail.Infrastructure.Persistence;

public sealed class GuardrailDbContext : DbContext
{
    public GuardrailDbContext(DbContextOptions<GuardrailDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationVersion> ApplicationVersions => Set<ApplicationVersion>();
    public DbSet<PolicyProfile> PolicyProfiles => Set<PolicyProfile>();
    public DbSet<PolicyRule> PolicyRules => Set<PolicyRule>();
    public DbSet<ToolPolicy> ToolPolicies => Set<ToolPolicy>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<DataBoundaryProfile> DataBoundaryProfiles => Set<DataBoundaryProfile>();
    public DbSet<GuardrailExecution> GuardrailExecutions => Set<GuardrailExecution>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskSignal> RiskSignals => Set<RiskSignal>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<HumanReviewCase> HumanReviewCases => Set<HumanReviewCase>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationDataset> EvaluationDatasets => Set<EvaluationDataset>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<RedactionResult> RedactionResults => Set<RedactionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite does not support schemas — only apply for PostgreSQL
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            modelBuilder.HasDefaultSchema("guardrail");

        ConfigureTenant(modelBuilder.Entity<Tenant>());
        ConfigureApplication(modelBuilder.Entity<Application>());
        ConfigureApplicationVersion(modelBuilder.Entity<ApplicationVersion>());
        ConfigurePolicyProfile(modelBuilder.Entity<PolicyProfile>());
        ConfigurePolicyRule(modelBuilder.Entity<PolicyRule>());
        ConfigureToolPolicy(modelBuilder.Entity<ToolPolicy>());
        ConfigureModelProfile(modelBuilder.Entity<ModelProfile>());
        ConfigureDataBoundaryProfile(modelBuilder.Entity<DataBoundaryProfile>());
        ConfigureGuardrailExecution(modelBuilder.Entity<GuardrailExecution>());
        ConfigureRiskAssessment(modelBuilder.Entity<RiskAssessment>());
        ConfigureRiskSignal(modelBuilder.Entity<RiskSignal>());
        ConfigureAuditEvent(modelBuilder.Entity<AuditEvent>());
        ConfigureIncident(modelBuilder.Entity<Incident>());
        ConfigureHumanReviewCase(modelBuilder.Entity<HumanReviewCase>());
        ConfigureEvaluationRun(modelBuilder.Entity<EvaluationRun>());
        ConfigureEvaluationDataset(modelBuilder.Entity<EvaluationDataset>());
        ConfigureEvaluationResult(modelBuilder.Entity<EvaluationResult>());
        ConfigureRedactionResult(modelBuilder.Entity<RedactionResult>());
    }

    private static void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : BaseEntity
    {
        entity.ToTable(tableName);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.CreatedBy).HasMaxLength(256);
        entity.Property(x => x.UpdatedBy).HasMaxLength(256);
        entity.HasQueryFilter(x => !x.IsDeleted);
    }

    private static void ConfigureTenant(EntityTypeBuilder<Tenant> entity)
    {
        ConfigureBase(entity, "tenants");
        entity.Property(x => x.TenantId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.ComplianceProfile).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AllowedRegions)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.Property(x => x.Settings)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<Dictionary<string, string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<Dictionary<string, string>>());
        entity.HasIndex(x => x.TenantId).IsUnique();
        entity.HasIndex(x => x.IsActive);
    }

    private static void ConfigureApplication(EntityTypeBuilder<Application> entity)
    {
        ConfigureBase(entity, "applications");
        entity.Property(x => x.ApplicationId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Domain).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ApiKey).HasMaxLength(512).IsRequired();
        entity.Property(x => x.Settings)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<Dictionary<string, string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<Dictionary<string, string>>());
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId }).IsUnique();
        entity.HasIndex(x => new { x.TenantId, x.IsActive });
    }

    private static void ConfigureApplicationVersion(EntityTypeBuilder<ApplicationVersion> entity)
    {
        ConfigureBase(entity, "application_versions");
        entity.Property(x => x.VersionNumber).HasMaxLength(50).IsRequired();
        entity.Property(x => x.ConfigurationSnapshot).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.ApplicationId, x.VersionNumber }).IsUnique();
    }

    private static void ConfigurePolicyProfile(EntityTypeBuilder<PolicyProfile> entity)
    {
        ConfigureBase(entity, "policy_profiles");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Domain).HasMaxLength(100);
        entity.Property(x => x.PolicyJson).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.Domain, x.IsActive, x.EffectiveFrom });
    }

    private static void ConfigurePolicyRule(EntityTypeBuilder<PolicyRule> entity)
    {
        ConfigureBase(entity, "policy_rules");
        entity.Property(x => x.RuleKey).HasMaxLength(200).IsRequired();
        entity.Property(x => x.RuleName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Conditions).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.Actions).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.PolicyProfileId, x.RuleKey }).IsUnique();
        entity.HasIndex(x => new { x.PolicyProfileId, x.Priority });
    }

    private static void ConfigureToolPolicy(EntityTypeBuilder<ToolPolicy> entity)
    {
        ConfigureBase(entity, "tool_policies");
        entity.Property(x => x.ToolName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ToolDescription).HasMaxLength(2000);
        entity.Property(x => x.ActionRisk).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.AllowedParameters).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.DeniedParameters).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.EnvironmentRestrictions)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.ToolName }).IsUnique();
    }

    private static void ConfigureModelProfile(EntityTypeBuilder<ModelProfile> entity)
    {
        ConfigureBase(entity, "model_profiles");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Provider).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ModelId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AllowedCapabilities)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.Property(x => x.RestrictedCapabilities)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.HasIndex(x => new { x.TenantId, x.IsDefault });
    }

    private static void ConfigureDataBoundaryProfile(EntityTypeBuilder<DataBoundaryProfile> entity)
    {
        ConfigureBase(entity, "data_boundary_profiles");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.AllowedSourceIds)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.Property(x => x.DeniedSourceIds)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.Property(x => x.TrustLevels)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<Dictionary<string, SourceTrustLevel>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<Dictionary<string, SourceTrustLevel>>());
        entity.HasIndex(x => x.TenantId);
    }

    private static void ConfigureGuardrailExecution(EntityTypeBuilder<GuardrailExecution> entity)
    {
        ConfigureBase(entity, "guardrail_executions");
        entity.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        entity.Property(x => x.SessionId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.RequestPayloadHash).HasMaxLength(128).IsRequired();
        entity.Property(x => x.InputRiskLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.OutputRiskLevel).HasConversion<string>().HasMaxLength(50);
        entity.Property(x => x.FinalDecision).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.HasIndex(x => x.CorrelationId).IsUnique();
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.ProcessedAt });
    }

    private static void ConfigureRiskAssessment(EntityTypeBuilder<RiskAssessment> entity)
    {
        ConfigureBase(entity, "risk_assessments");
        entity.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Rationale).HasMaxLength(4000).IsRequired();
        entity.Property(x => x.RecommendedConstraints).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => x.ExecutionId);
        entity.HasIndex(x => new { x.RiskLevel, x.Decision });
    }

    private static void ConfigureRiskSignal(EntityTypeBuilder<RiskSignal> entity)
    {
        ConfigureBase(entity, "risk_signals");
        entity.Property(x => x.SignalType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.AssessmentId);
    }

    private static void ConfigureAuditEvent(EntityTypeBuilder<AuditEvent> entity)
    {
        ConfigureBase(entity, "audit_events");
        entity.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EventCategory).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.SafePayloadSummary).HasMaxLength(4000).IsRequired();
        entity.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(50);
        entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Tags)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<Dictionary<string, string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<Dictionary<string, string>>());
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.CreatedAt });
        entity.HasIndex(x => x.CorrelationId);
        entity.HasIndex(x => x.IsIncident);
    }

    private static void ConfigureIncident(EntityTypeBuilder<Incident> entity)
    {
        ConfigureBase(entity, "incidents");
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.AssignedTo).HasMaxLength(256);
        entity.Property(x => x.Resolution).HasMaxLength(4000);
        entity.Property(x => x.Tags)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.HasIndex(x => new { x.TenantId, x.Status });
    }

    private static void ConfigureHumanReviewCase(EntityTypeBuilder<HumanReviewCase> entity)
    {
        ConfigureBase(entity, "human_review_cases");
        entity.Property(x => x.ReviewReason).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.SafeContextSummary).HasMaxLength(4000).IsRequired();
        entity.Property(x => x.AssignedTo).HasMaxLength(256);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.ReviewNotes).HasMaxLength(4000);
        entity.Property(x => x.ReviewedBy).HasMaxLength(256);
        entity.Property(x => x.FinalDecision).HasConversion<string>().HasMaxLength(50);
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.Status, x.CreatedAt });
    }

    private static void ConfigureEvaluationRun(EntityTypeBuilder<EvaluationRun> entity)
    {
        ConfigureBase(entity, "evaluation_runs");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.SummaryJson).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }

    private static void ConfigureEvaluationDataset(EntityTypeBuilder<EvaluationDataset> entity)
    {
        ConfigureBase(entity, "evaluation_datasets");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Version).HasMaxLength(50).IsRequired();
        entity.Property(x => x.DatasetJson).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.Tags)
            .HasColumnType("jsonb")
            .HasConversion(JsonValueConversion.CreateConverter<List<string>>())
            .Metadata.SetValueComparer(JsonValueConversion.CreateComparer<List<string>>());
        entity.HasIndex(x => new { x.TenantId, x.Name, x.Version }).IsUnique();
    }

    private static void ConfigureEvaluationResult(EntityTypeBuilder<EvaluationResult> entity)
    {
        ConfigureBase(entity, "evaluation_results");
        entity.Property(x => x.CaseId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.CaseName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.InputSummary).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.ExpectedDecision).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.ActualDecision).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(x => x.Notes).HasMaxLength(2000);
        entity.HasIndex(x => new { x.RunId, x.CaseId }).IsUnique();
    }

    private static void ConfigureRedactionResult(EntityTypeBuilder<RedactionResult> entity)
    {
        ConfigureBase(entity, "redaction_results");
        entity.Property(x => x.OriginalHash).HasMaxLength(128).IsRequired();
        entity.Property(x => x.RedactedContentHash).HasMaxLength(128).IsRequired();
        entity.Property(x => x.RedactionDetails).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.Strategy).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.HasIndex(x => x.ExecutionId);
    }
}
