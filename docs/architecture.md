# Architecture

## System Overview

The Enterprise AI Guardrail Platform is implemented as a centralized ASP.NET Core gateway that evaluates AI traffic before and after model execution. The platform applies layered guardrails:

1. Channel and API guardrails
2. Input guardrails
3. Context and data guardrails
4. Model and tool guardrails
5. Output guardrails
6. Audit, monitoring, and evaluation guardrails

The current implementation uses PostgreSQL for multi-tenant persistence, Redis-ready caching, Azure-compatible provider abstractions, OpenTelemetry/Serilog for observability, and background processing for evaluation suites.

## Solution Structure

`src/Guardrail.Core`
- Domain entities, enums, value objects, and cross-layer abstractions.

`src/Guardrail.Application`
- MediatR commands and queries for evaluation, audit lookup, and policy resolution.
- FluentValidation pipeline behavior for request validation.

`src/Guardrail.Infrastructure`
- EF Core `GuardrailDbContext`
- Repository implementations
- JSON policy engine
- Weighted risk engine
- Tool firewall and context firewall
- Output validator
- Azure Content Safety and Prompt Shields adapters with heuristic fallback
- Guardrail orchestration service
- Evaluation background queue and worker
- Startup initialization and seed loading

`src/Guardrail.API`
- Public REST API
- Swagger/OpenAPI
- Auth, middleware, rate limiting, health, version

`tests/Guardrail.UnitTests`
- Risk, policy, tool, and context unit tests

`tests/Guardrail.IntegrationTests`
- API integration test with in-memory EF Core

## Domain Model

Core persisted entities:

- `Tenant`
- `Application`
- `ApplicationVersion`
- `PolicyProfile`
- `PolicyRule`
- `ToolPolicy`
- `ModelProfile`
- `DataBoundaryProfile`
- `GuardrailExecution`
- `RiskAssessment`
- `RiskSignal`
- `AuditEvent`
- `Incident`
- `HumanReviewCase`
- `EvaluationRun`
- `EvaluationDataset`
- `EvaluationResult`
- `RedactionResult`

Key value objects and contracts:

- `TenantContext`
- `ConstraintSet`
- `SourceDescriptor`
- `RiskScore`
- `GuardrailEvaluationResult`
- `EffectivePolicy`

## Multi-Tenant Model

The platform uses a shared PostgreSQL database with tenant discriminators on tenant-owned tables. Isolation is enforced by:

- tenant and application identifiers on executions, audits, review cases, evaluation runs, and application policy profiles
- tenant-aware policy resolution
- tenant-aware context firewall validation
- tenant-aware audit filtering
- soft-delete query filters at the EF Core layer

This is the practical default for enterprise SaaS because it minimizes operational overhead while still supporting strong application-layer isolation.

## Policy Model

Policies are JSON-backed, versioned, and scope-aware. The effective policy is produced by merging:

1. Global baseline
2. Tenant policy
3. Application policy

Merged policy concerns:

- risk thresholds
- tool allow and deny lists
- approval-required tools
- source type and trust boundaries
- redaction requirements
- citation and disclaimer requirements
- forbidden phrases
- risk weights

Seeded policy examples live in `policies/samples/`.

## Risk Engine

The weighted risk engine computes six dimensions:

- content safety
- privacy
- prompt injection
- business or policy
- tool or action
- output quality

It produces:

- raw per-dimension scores
- weighted total
- normalized 0-100 score
- final decision
- rationale
- recommended constraints

Decision outcomes:

- `Allow`
- `AllowWithConstraints`
- `Redact`
- `Escalate`
- `Block`

## Request Lifecycle

### Input evaluation

1. API request received
2. Tenant and application context resolved from headers and claims
3. Effective policy loaded from policy engine
4. Content safety provider scans prompt
5. Prompt shield provider scans prompt and attached source metadata
6. Policy engine evaluates business rules
7. Tool firewall validates requested tools
8. Context firewall validates source boundaries
9. Risk engine calculates decision
10. Execution, risk assessment, signals, and audit event are persisted
11. Optional human review case is created

### Output evaluation

1. Effective policy loaded
2. Output validator applies schema, disclaimer, citation, and redaction checks
3. Content safety provider scans sanitized output
4. Policy engine evaluates output-facing rules
5. Risk engine calculates final decision
6. Existing execution is updated or a new one is created
7. Redaction result, risk signals, and audit event are persisted

## Database Design

The relational model is implemented in `src/Guardrail.Infrastructure/Persistence/GuardrailDbContext.cs`.

Important tables and indexes:

- `policy_profiles`: scoped lookup index on tenant, application, domain, activity, and effective time
- `guardrail_executions`: unique correlation ID, tenant/application/time index
- `audit_events`: tenant/application/time, correlation ID, incident indexes
- `human_review_cases`: tenant/application/status/time index
- `evaluation_runs`: tenant/status/time index
- `evaluation_results`: unique `(run_id, case_id)`
- `tool_policies`: unique `(tenant_id, application_id, tool_name)`

JSON-heavy fields are stored as `jsonb` in PostgreSQL for flexible policy and metadata persistence.

## Security Design

Current safeguards:

- JWT bearer auth in production
- local development auth bypass via `Auth:DisableAuth=true`
- role-based authorization policies for admin, read, and evaluate actions
- correlation IDs and structured error handling
- rate limiting
- no raw prompt persistence in audit or execution storage
- SHA-256 request and output hashing
- cross-tenant context blocking
- approval-required tool model

Threats explicitly addressed:

- prompt injection and jailbreak attempts
- cross-tenant data access
- risky tool invocation
- sensitive data leakage in outputs
- logging leakage through safe summaries only

## Observability

The platform includes:

- Serilog structured logs
- correlation ID middleware
- OpenTelemetry tracing for ASP.NET Core and HTTP clients
- metrics for evaluated requests, blocked requests, escalations, redactions, latency, and risk score
- `/api/health`
- `/api/version`

## Evaluation Framework

Evaluation runs are persisted and processed asynchronously by:

- `EvaluationBackgroundQueue`
- `EvaluationService`
- `EvaluationWorker`
- `EvaluationRunProcessor`

Seed datasets live in `evaluations/datasets/`. The baseline suite covers:

- normal summaries
- prompt injection
- PHI leakage
- risky tool requests

## Deployment

Concrete deployment assets:

- `src/Guardrail.API/Dockerfile`
- `docker-compose.yml`
- `deploy/k8s/*.yaml`
- `infra/azure/main.bicep`

## Migration Strategy

The codebase is EF Core-first. Local startup uses:

- `Migrate()` when migrations exist
- `EnsureCreated()` when bootstrapping locally without generated migrations

Recommended production workflow:

1. Generate explicit migrations from `GuardrailDbContext`
2. Validate against a staging PostgreSQL instance
3. Apply via CI/CD before or during deployment rollout

## Extension Points

Designed extension seams:

- `IContentSafetyProvider`
- `IPromptShieldProvider`
- `IPolicyEngine`
- `IRiskEngine`
- `IContextFirewall`
- `IToolFirewall`
- `IOutputValidator`
- `IEvaluationService`

This is sufficient to add alternate model vendors, moderation providers, or more advanced hallucination detection without rewriting the API surface.
