# Architecture

## System Overview

The Enterprise AI Guardrail Platform is a **.NET 9 multi-tenant guardrail gateway**. It can be used in three ways:

1. As a REST guardrail API for explicit pre/post model checks
2. As an MCP server over JSON-RPC HTTP for agent ecosystems
3. As an OpenAI-style chat proxy for applications that expect `/v1/chat/completions`

At its core, the platform evaluates prompts, model outputs, tool requests, and context sources against tenant/application policy before traffic reaches end users or downstream actions.

For the broader sellable product architecture, admin experience, connector strategy, self-improving threat loop, and deployment roadmap, see [Product Architecture Strategy](product-architecture-strategy.md).

```
Client / Agent / App
        |
        +--> REST Guardrail API
        +--> MCP Server (/mcp)
        +--> OpenAI-style Proxy (/v1/chat/completions)
                    |
                    v
         Guardrail Orchestrator
    +--------+--------+--------+--------+
    | Policy | Safety | Tools  | Output |
    | Engine | Scan   |/Context| Check  |
    +--------+--------+--------+--------+
                    |
                    v
         Audit / Review / Evaluation Data
                    |
                    v
            SQLite or PostgreSQL
```

## Solution Layout

```
src/
  Guardrail.API/            ASP.NET Core host, controllers, demo/admin UI
  Guardrail.Application/    MediatR commands, validators, behaviors
  Guardrail.Core/           Domain entities, enums, abstractions
  Guardrail.Infrastructure/ EF Core, policy engine, providers, firewalls
tests/
  Guardrail.UnitTests/
  Guardrail.IntegrationTests/
policies/samples/           Seed policy JSON files
evaluations/datasets/       Seed regression and red-team datasets
app/                        Legacy Python prototype retained for reference
```

## Request Paths

### 1. REST Guardrail API

- `POST /api/guardrail/evaluate-input`
- `POST /api/guardrail/evaluate-context`
- `POST /api/guardrail/evaluate-tool-call`
- `POST /api/guardrail/evaluate-output`
- `POST /api/guardrail/evaluate-full`
- `POST /api/tools/validate`
- `GET /api/tools/registry`

Use this path when your application already controls its own model invocation and wants the platform to make allow/block/redact/escalate decisions around it.

### 2. MCP Server

- `POST /mcp`
- `GET /mcp` returns `405` because SSE streams are not enabled

The MCP surface exposes these server tools:

- `guardrail.evaluate_input`
- `guardrail.evaluate_context`
- `guardrail.evaluate_tool_call`
- `guardrail.evaluate_output`
- `guardrail.evaluate_full`
- `guardrail.get_tool_registry`
- `guardrail.get_manifest`

This gives agent runtimes a standard JSON-RPC tool surface without requiring a custom SDK first.

### 3. OpenAI-style Proxy

- `POST /v1/chat/completions`
- `POST /api/proxy/chat/completions`

This route:

1. Evaluates the incoming transcript
2. Calls the configured HuggingFace router-backed model
3. Evaluates the generated output
4. Returns either:
   - a normal chat completion with guardrail metadata, or
   - a `403` error when the request/output is blocked or escalated

## Domain and Policy Model

The platform is policy-first and multi-tenant.

### Tenant resolution

REST requests use:

- `X-Tenant-Id`
- `X-Application-Id`
- optional `X-Session-Id`

MCP requests carry tenant context in tool arguments.

### Policy precedence

`JsonPolicyEngine` merges policy profiles in priority order:

1. Global
2. Tenant
3. Application

The resolved `EffectivePolicy` drives:

- forbidden phrases
- tool allow/deny/approval lists
- data source restrictions
- redaction and evidence requirements
- risk thresholds and weights

## Guardrail Pipeline

### Input evaluation

`GuardrailOrchestrator.EvaluateInputAsync()` performs:

1. Effective policy resolution
2. Content safety analysis
3. Prompt injection detection
4. Policy evaluation
5. Tool firewall validation
6. Context firewall validation
7. Weighted risk scoring
8. Audit persistence and optional review-case creation

### Output evaluation

`GuardrailOrchestrator.EvaluateOutputAsync()` performs:

1. Output validation and redaction
2. Content safety analysis of the final/redacted output
3. Indirect injection detection on model output
4. Policy evaluation against output content
5. Weighted risk scoring
6. Audit persistence and optional review-case creation

### Full evaluation

`EvaluateFullAsync()` is a convenience path for callers that already have both prompt and response available.

## Tool Registry

The tool registry is policy-backed and tenant/application-aware.

`PolicyBackedToolRegistryService` merges:

- the effective policy’s `allowedTools`, `deniedTools`, and `approvalRequiredTools`
- persisted `ToolPolicy` rows from the database
- environment restrictions

The registry response tells clients:

- whether a tool is allowed
- whether human approval is required
- declared parameter restrictions
- action risk classification
- whether the rule came only from policy or from policy + database metadata

This is the main discovery mechanism for downstream orchestrators that want to understand what actions an agent may attempt.

## Persistence and Startup

`GuardrailDbContext` stores:

- policy profiles and rules
- tool policies
- guardrail executions
- risk assessments and signals
- audit events
- human review cases
- evaluation datasets and results

Startup behavior is controlled by `Guardrail` settings:

- `ApplyDatabaseOnStartup`
- `SeedDataOnStartup`
- `PolicySeedPath`
- `EvaluationSeedPath`

SQLite is used by default when no PostgreSQL connection string is configured; PostgreSQL is used for production.

## Providers

### Content safety

`AzureContentSafetyProvider`

- uses Azure AI Content Safety when configured
- falls back to heuristic analysis when enabled

### Prompt injection

`AzurePromptShieldProvider`

- uses Azure Prompt Shields when configured
- falls back to heuristic marker detection when enabled

### Model proxy

`HuggingFaceInferenceClient`

- calls the HuggingFace router-compatible chat completions endpoint
- powers the demo UI and OpenAI-style proxy route

## Observability and Operations

The API host includes:

- Serilog console and rolling file logs
- OpenTelemetry tracing
- correlation ID middleware
- rate limiting
- health checks
- JWT auth or development auth bypass

## Discovery and Integration Metadata

Anonymous discovery endpoints:

- `GET /.well-known/ai-guardrail.json`
- `GET /api/integrations/manifest`

These advertise:

- REST endpoints
- MCP endpoint and protocol version
- expected auth scheme
- tenant/application header requirements

## Current Gaps

The platform is materially stronger now, but a few product gaps still remain:

1. The MCP endpoint is JSON-RPC compatible, but does not yet provide SSE streams or server-initiated notifications.
2. The OpenAI-style proxy currently supports non-streaming chat completions only.
3. The legacy `app/` Python prototype remains in-repo for reference and should eventually be archived or moved to avoid future drift.
4. There is not yet a first-party SDK layer for tenant bootstrap, auth, and client-side retries.
