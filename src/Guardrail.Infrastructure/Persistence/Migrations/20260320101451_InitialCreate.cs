using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardrail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "guardrail");

            migrationBuilder.CreateTable(
                name: "application_versions",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeployedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfigurationSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SafePayloadSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    IsIncident = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_boundary_profiles",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AllowedSourceIds = table.Column<string>(type: "jsonb", nullable: false),
                    DeniedSourceIds = table.Column<string>(type: "jsonb", nullable: false),
                    TrustLevels = table.Column<string>(type: "jsonb", nullable: false),
                    MaxDocumentsPerRequest = table.Column<int>(type: "integer", nullable: false),
                    CrossTenantAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_boundary_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_datasets",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CaseCount = table.Column<int>(type: "integer", nullable: false),
                    DatasetJson = table.Column<string>(type: "jsonb", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_results",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExpectedDecision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActualDecision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    IsFalsePositive = table.Column<bool>(type: "boolean", nullable: false),
                    IsFalseNegative = table.Column<bool>(type: "boolean", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NormalizedScore = table.Column<decimal>(type: "numeric", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalCases = table.Column<int>(type: "integer", nullable: false),
                    PassedCases = table.Column<int>(type: "integer", nullable: false),
                    FailedCases = table.Column<int>(type: "integer", nullable: false),
                    FalsePositives = table.Column<int>(type: "integer", nullable: false),
                    FalseNegatives = table.Column<int>(type: "integer", nullable: false),
                    AverageLatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    SummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guardrail_executions",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestPayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutputRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FinalDecision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExecutionDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardrail_executions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "human_review_cases",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SafeContextSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FinalDecision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_human_review_cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_profiles",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric", nullable: false),
                    AllowedCapabilities = table.Column<string>(type: "jsonb", nullable: false),
                    RestrictedCapabilities = table.Column<string>(type: "jsonb", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_profiles",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParentProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_rules",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Conditions = table.Column<string>(type: "jsonb", nullable: false),
                    Actions = table.Column<string>(type: "jsonb", nullable: false),
                    OverrideAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "redaction_results",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactedContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactionsApplied = table.Column<int>(type: "integer", nullable: false),
                    RedactionDetails = table.Column<string>(type: "jsonb", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redaction_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    PrivacyRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    InjectionRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    BusinessPolicyRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    ActionRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    OutputQualityRiskScore = table.Column<decimal>(type: "numeric", nullable: false),
                    WeightedTotalScore = table.Column<decimal>(type: "numeric", nullable: false),
                    NormalizedScore = table.Column<decimal>(type: "numeric", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecommendedConstraints = table.Column<string>(type: "jsonb", nullable: false),
                    SignalCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_signals",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ComplianceProfile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaxRequestsPerMinute = table.Column<int>(type: "integer", nullable: false),
                    AllowedRegions = table.Column<string>(type: "jsonb", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tool_policies",
                schema: "guardrail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToolName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToolDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActionRisk = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedParameters = table.Column<string>(type: "jsonb", nullable: false),
                    DeniedParameters = table.Column<string>(type: "jsonb", nullable: false),
                    EnvironmentRestrictions = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_versions_ApplicationId_VersionNumber",
                schema: "guardrail",
                table: "application_versions",
                columns: new[] { "ApplicationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_TenantId_ApplicationId",
                schema: "guardrail",
                table: "applications",
                columns: new[] { "TenantId", "ApplicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_TenantId_IsActive",
                schema: "guardrail",
                table: "applications",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_CorrelationId",
                schema: "guardrail",
                table: "audit_events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_IsIncident",
                schema: "guardrail",
                table: "audit_events",
                column: "IsIncident");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_TenantId_ApplicationId_CreatedAt",
                schema: "guardrail",
                table: "audit_events",
                columns: new[] { "TenantId", "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_data_boundary_profiles_TenantId",
                schema: "guardrail",
                table: "data_boundary_profiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_datasets_TenantId_Name_Version",
                schema: "guardrail",
                table: "evaluation_datasets",
                columns: new[] { "TenantId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_results_RunId_CaseId",
                schema: "guardrail",
                table: "evaluation_results",
                columns: new[] { "RunId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_TenantId_Status_CreatedAt",
                schema: "guardrail",
                table: "evaluation_runs",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guardrail_executions_CorrelationId",
                schema: "guardrail",
                table: "guardrail_executions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guardrail_executions_TenantId_ApplicationId_ProcessedAt",
                schema: "guardrail",
                table: "guardrail_executions",
                columns: new[] { "TenantId", "ApplicationId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_human_review_cases_TenantId_ApplicationId_Status_CreatedAt",
                schema: "guardrail",
                table: "human_review_cases",
                columns: new[] { "TenantId", "ApplicationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_TenantId_Status",
                schema: "guardrail",
                table: "incidents",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_model_profiles_TenantId_IsDefault",
                schema: "guardrail",
                table: "model_profiles",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_profiles_TenantId_ApplicationId_Domain_IsActive_Effe~",
                schema: "guardrail",
                table: "policy_profiles",
                columns: new[] { "TenantId", "ApplicationId", "Domain", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_rules_PolicyProfileId_Priority",
                schema: "guardrail",
                table: "policy_rules",
                columns: new[] { "PolicyProfileId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_rules_PolicyProfileId_RuleKey",
                schema: "guardrail",
                table: "policy_rules",
                columns: new[] { "PolicyProfileId", "RuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_redaction_results_ExecutionId",
                schema: "guardrail",
                table: "redaction_results",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_ExecutionId",
                schema: "guardrail",
                table: "risk_assessments",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_RiskLevel_Decision",
                schema: "guardrail",
                table: "risk_assessments",
                columns: new[] { "RiskLevel", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_signals_AssessmentId",
                schema: "guardrail",
                table: "risk_signals",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive",
                schema: "guardrail",
                table: "tenants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_TenantId",
                schema: "guardrail",
                table: "tenants",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_policies_TenantId_ApplicationId_ToolName",
                schema: "guardrail",
                table: "tool_policies",
                columns: new[] { "TenantId", "ApplicationId", "ToolName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_versions",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "data_boundary_profiles",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "evaluation_datasets",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "evaluation_results",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "evaluation_runs",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "guardrail_executions",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "human_review_cases",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "incidents",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "model_profiles",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "policy_profiles",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "policy_rules",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "redaction_results",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "risk_assessments",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "risk_signals",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "guardrail");

            migrationBuilder.DropTable(
                name: "tool_policies",
                schema: "guardrail");
        }
    }
}
