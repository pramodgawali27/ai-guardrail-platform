---
title: Enterprise AI Guardrail Platform
emoji: 🛡️
colorFrom: blue
colorTo: indigo
sdk: docker
pinned: false
license: mit
short_description: Multi-tenant AI safety, policy enforcement & audit gateway
---

# Enterprise AI Guardrail Platform

Production-grade, reusable, multi-tenant AI guardrail gateway built on .NET 9, ASP.NET Core, EF Core, PostgreSQL, Redis-ready caching, OpenTelemetry, Serilog, and Azure-compatible safety abstractions.

## What is implemented

- Input evaluation API
- Output evaluation API
- Full evaluation API
- Tenant and application scoped policy resolution
- JSON policy model with versioned policy profiles
- Weighted risk engine
- Prompt injection detection abstraction
- Content safety abstraction
- Tool firewall
- Context firewall
- Output validator with schema and redaction checks
- Audit trail persistence
- Human review case persistence and workflow endpoints
- Background evaluation runs with seeded datasets
- Docker, Compose, Kubernetes, and Azure Bicep starting assets

## Solution

`EnterpriseAiGuardrailPlatform.sln`

Projects:

- `src/Guardrail.Core`
- `src/Guardrail.Application`
- `src/Guardrail.Infrastructure`
- `src/Guardrail.API`
- `tests/Guardrail.UnitTests`
- `tests/Guardrail.IntegrationTests`

Detailed architecture notes are in `docs/architecture.md`.

## Seeded Samples

Seeded policy files:

- `policies/samples/global-enterprise-baseline.json`
- `policies/samples/plain-language-summary-policy.json`
- `policies/samples/enterprise-copilot-policy.json`
- `policies/samples/regulated-healthcare-policy.json`
- `policies/samples/internal-developer-assistant-policy.json`

Seeded tenant and application IDs used by samples:

- Tenant: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001`
- Plain Language Summary App: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001`
- Enterprise Copilot App: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002`
- Regulated Healthcare App: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003`
- Internal Developer Assistant App: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0004`

Seeded evaluation dataset:

- `evaluations/datasets/baseline-redteam-suite.json`

## Local Run

### Prerequisites

- .NET 9 SDK
- Docker Desktop or local PostgreSQL and Redis

### Option 1: Docker Compose

```bash
docker compose up --build
```

API:

- `http://localhost:8080/api/version`
- `http://localhost:8080/swagger`

### Option 2: Run from the SDK

Start PostgreSQL and Redis first, then:

```bash
dotnet run --project src/Guardrail.API/Guardrail.API.csproj
```

Development defaults:

- `Auth:DisableAuth=true`
- heuristic Azure provider fallback enabled
- database and seed bootstrap enabled

## Common Headers

The guardrail endpoints require:

- `X-Tenant-Id`
- `X-Application-Id`
- optional `X-Correlation-Id`
- optional `X-Session-Id`

## Example Request

```bash
curl -X POST http://localhost:8080/api/guardrail/evaluate-input \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001" \
  -H "X-Application-Id: bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001" \
  -d '{
    "userPrompt": "Summarize this update for a non-technical employee.",
    "systemPrompt": "You are an enterprise summary assistant."
  }'
```

## Test

```bash
dotnet test EnterpriseAiGuardrailPlatform.sln
```

## Deployment Assets

- Local containers: `docker-compose.yml`
- API container image: `src/Guardrail.API/Dockerfile`
- Kubernetes skeleton: `deploy/k8s/`
- Azure IaC skeleton: `infra/azure/main.bicep`

## Notes

- EF Core startup uses `EnsureCreated()` when no migrations exist yet.
- For production, add explicit migrations from `GuardrailDbContext` and apply them through CI/CD.
- Azure Content Safety and Prompt Shields adapters are implemented with HTTP integration points and heuristic fallback so the platform works locally without cloud credentials.
