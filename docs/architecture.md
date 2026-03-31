# Architecture

## System Overview

The Enterprise AI Guardrail Platform is implemented as a **Python FastAPI gateway** that evaluates AI traffic before and after model execution. The platform applies layered guardrails:

1. Input evaluation — content safety + prompt injection detection
2. Policy enforcement — tenant/application-scoped business rules
3. Risk aggregation — weighted scoring across 6 dimensions
4. Model invocation — HuggingFace Inference API (only if input passes)
5. Output evaluation — re-scan model response before it reaches the user
6. Audit — every decision is recorded

```
Your App → [Input Guardrail] → HuggingFace LLM → [Output Guardrail] → User
                ↓                                        ↓
           Block / Allow                          Block / Redact / Allow
           Audit Trail                              Audit Trail
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| API framework | FastAPI (Python 3.12) |
| Data validation | Pydantic v2 + Pydantic Settings |
| ORM / DB | SQLAlchemy 2 — SQLite (demo), PostgreSQL (prod) |
| Async HTTP | httpx |
| LLM inference | HuggingFace Inference API (Together router) |
| Static UI | Vanilla HTML/JS served via `StaticFiles` |
| Container | Docker (python:3.12-slim, port 7860) |

---

## Repository Structure

```
app/
  __init__.py         # package marker
  main.py             # FastAPI app, all HTTP routes, startup hook
  config.py           # Pydantic BaseSettings (reads env vars)
  database.py         # SQLAlchemy engine + ORM models
  providers.py        # ContentSafetyProvider, PromptShieldProvider
  risk_engine.py      # WeightedRiskEngine — 6-dimension scoring
  policy_engine.py    # resolve_policy(), evaluate_policy()
  hf_client.py        # call_hf() — async HuggingFace chat completions
  seed.py             # seed_database() — loads JSON policy files on startup

policies/
  samples/            # 5 seeded policy JSON files

evaluations/
  datasets/           # 48-case red-team test suite (JSON)

src/
  Guardrail.API/
    wwwroot/          # Demo UI (index.html) + Admin UI (admin.html)

docs/
  architecture.md     # this file
  implementation-plan.md
  guardrail-examples.md
```

---

## Component Responsibilities

### `app/main.py` — API Layer

- Defines all HTTP routes (see API Reference below).
- Resolves tenant/application from `X-Tenant-Id` / `X-Application-Id` headers.
- Orchestrates the 3-step pipeline: input guardrail → model call → output guardrail.
- Serves static UI files from `wwwroot/`.

### `app/config.py` — Settings

Reads configuration from environment variables via `pydantic_settings.BaseSettings`:

| Env var | Default | Purpose |
|---|---|---|
| `HF_TOKEN` | `""` | HuggingFace API token |
| `HF_MODEL_ID` | `Qwen/Qwen2.5-7B-Instruct-Turbo` | Model for demo chat |
| `HF_MAX_TOKENS` | `512` | Max tokens per response |
| `DATABASE_URL` | `sqlite:///./guardrail.db` | Database connection string |
| `POLICY_SEED_PATH` | `/app/policies/samples` | Path to policy JSON files |
| `SEED_ON_STARTUP` | `true` | Seed policies on startup |

### `app/database.py` — Persistence

Two ORM models persisted to SQLite/PostgreSQL:

- **`PolicyProfile`** — tenant/application-scoped guardrail rules (forbidden phrases, thresholds, domain restrictions)
- **`AuditEvent`** — one row per guardrail evaluation (decision, score, flags, timestamp)

`Base.metadata.create_all()` runs on startup — no migration tool required for SQLite.

### `app/providers.py` — Safety Providers

#### `ContentSafetyProvider`
Heuristic classifier covering 10 threat categories:

| Category | Score | Severity |
|---|---|---|
| Jailbreak | 0.92 | Critical |
| SocialEngineering | 0.90 | Critical |
| CredentialPhishing | 0.90 | Critical |
| CodeInterpreterAbuse | 0.92 | Critical |
| Violence | 0.85 | High |
| SelfHarm | 0.90 | High |
| Sexual | 0.70 | Medium |
| Hate | 0.85 | High |
| DestructiveAction | 0.95 | Critical |
| DataExfiltration | 0.90 | Critical |

Plus regex-based PII/PHI detection (email, SSN pattern, MRN pattern).

Uses word-boundary regex (`\b`) for single-word markers to avoid false positives (e.g. "skills" not matching "kill").

#### `PromptShieldProvider`
Detects prompt injection and jailbreak markers in both:
- **Direct injection** — user prompt contains an override marker
- **Indirect injection** — documents/model output passed as context contain markers

35+ marker strings covering DAN variants, instruction override, role override, credential exfiltration, and paraphrase patterns.

### `app/risk_engine.py` — Weighted Risk Scoring

Six dimensions combine into a single normalized 0–100 score:

| Dimension | Weight | Source |
|---|---|---|
| Content Safety | 30% | `ContentSafetyProvider` |
| Privacy (PII/PHI) | 25% | `ContentSafetyProvider` (PII/PHI flags) |
| Prompt Injection | 20% | `PromptShieldProvider` |
| Business Rules | 10% | `policy_engine.evaluate_policy()` |
| Action Safety | 10% | DestructiveAction / DataExfiltration flags |
| Output Quality | 5% | Reserved (currently 0) |

Decision thresholds:

| Score | Decision |
|---|---|
| ≥ 90 | Block |
| ≥ 80 | Escalate |
| ≥ 35 | AllowWithConstraints |
| < 35 | Allow |

### `app/policy_engine.py` — Policy Resolution

`resolve_policy()` queries `PolicyProfile` with priority:

1. Application-scoped policy (highest priority)
2. Tenant-scoped policy
3. Global baseline

`evaluate_policy()` checks forbidden phrases from the resolved policy against the prompt.

### `app/hf_client.py` — LLM Client

`call_hf(prompt, settings)` — async POST to `router.huggingface.co/together/v1/chat/completions`. Returns the assistant message text. Falls back to a static message when `HF_TOKEN` is not configured.

### `app/seed.py` — Database Seeding

`seed_database()` reads all `.json` files from `POLICY_SEED_PATH`, deserializes each as a `PolicyProfile`, and inserts any that don't already exist (idempotent on `name`).

---

## API Reference

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/version` | Service version |
| `GET` | `/health` | Health check |
| `POST` | `/api/guardrail/evaluate-input` | Evaluate a user prompt before sending to LLM |
| `POST` | `/api/guardrail/evaluate-output` | Evaluate model output before returning to user |
| `POST` | `/api/demo/chat` | Full 3-step pipeline (input → LLM → output) |
| `GET` | `/api/policies` | List all active policies |
| `GET` | `/api/policies/{tenant_id}/{application_id}` | Policies for a specific app |
| `POST` | `/api/policies` | Create a policy |
| `PUT` | `/api/policies/{policy_id}` | Update a policy |
| `DELETE` | `/api/policies/{policy_id}` | Soft-delete a policy |

Interactive docs available at `/docs` (Swagger UI) when running locally.

**Headers for evaluate-input / evaluate-output:**

```
X-Tenant-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001
X-Application-Id: bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002
```

---

## Request Lifecycle

### Input evaluation (`/api/guardrail/evaluate-input`)

1. Read `X-Tenant-Id` / `X-Application-Id` headers
2. `resolve_policy()` — fetch effective policy from DB
3. `ContentSafetyProvider.analyze_text(prompt)` — heuristic scan
4. `PromptShieldProvider.detect_injection(prompt)` — injection markers
5. `evaluate_policy(prompt, policy)` — forbidden phrases
6. `evaluate_risk(content, shield, policy_result)` — weighted aggregate
7. Return decision JSON

### Demo chat (`/api/demo/chat`)

Same as above for input, then if `isAllowed`:

1. `call_hf(prompt, settings)` — get AI response
2. `ContentSafetyProvider.analyze_text(ai_text)` — scan output
3. `PromptShieldProvider.detect_injection("", documents=[ai_text])` — indirect injection
4. `evaluate_policy(None, ai_text, policy)` — output policy
5. `evaluate_risk(...)` — output risk score
6. Return `{ inputGuardrail, modelResponse, outputGuardrail }`

---

## Multi-Tenant Model

Each `PolicyProfile` row has `tenant_id`, `application_id`, and `scope` (`Global` / `Tenant` / `Application`). Resolution priority is Application > Tenant > Global. One database, many tenants — isolation is enforced at the application layer.

---

## Security Controls

- **Auth:** disabled by default for demo (HuggingFace Spaces). For production, add FastAPI middleware for JWT/API key validation.
- **PII:** raw prompts are not retained beyond the request — only the decision and score are written to `AuditEvent`.
- **Rate limiting:** enforce via reverse proxy (nginx/Cloudflare) in production.
- **Secrets:** inject `HF_TOKEN` and `DATABASE_URL` via environment variables — never commit to source.

---

## Deployment

### HuggingFace Spaces (current)

The Space runs `uvicorn app.main:app --host 0.0.0.0 --port 7860` inside the Docker image. The `README.md` YAML frontmatter declares `app_port: 7860` so HF routes traffic correctly.

### Local Docker

```bash
docker build -t guardrail .
docker run -p 7860:7860 -e HF_TOKEN=hf_xxx guardrail
```

### Production (PostgreSQL)

```bash
DATABASE_URL=postgresql+psycopg2://user:pass@host/db \
HF_TOKEN=hf_xxx \
uvicorn app.main:app --host 0.0.0.0 --port 7860 --workers 4
```

---

## Extension Points

- **Add a new threat category:** add patterns to `ContentSafetyProvider.analyze_text()` in `app/providers.py`.
- **Replace heuristics with LlamaGuard:** implement an async `classify()` call in `providers.py` and wire it into `ContentSafetyProvider.analyze_text()` behind a feature flag.
- **Add semantic similarity:** embed prompts with `sentence-transformers/all-MiniLM-L6-v2` and query `pgvector` for nearest attack embeddings — see `docs/implementation-plan.md` Phase 2.
- **Add a new policy rule type:** extend the policy JSON schema and update `evaluate_policy()` in `app/policy_engine.py`.
