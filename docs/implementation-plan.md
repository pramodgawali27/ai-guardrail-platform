# Implementation Plan

## Principles

Every decision in this plan is guided by the following:

1. **No external dependency for safety decisions.** The heuristic fallbacks in `ContentSafetyProvider` and `PromptShieldProvider` demonstrate the right instinct; this plan extends it by making in-house ML models the primary path, not the fallback.
2. **Providers classify, risk engine aggregates.** Providers return raw scores and flags. `evaluate_risk()` aggregates them into a normalized score and decision. This separation is preserved in every phase.
3. **Evaluation is the continuous feedback loop.** All improvements must be measurable through the red-team test suite before they are deployed.
4. **Feature flags gate every phase.** New code paths are activated via environment variables, not code changes.
5. **Privacy by design.** Prompts are not persisted; only decisions, scores, and aggregated flags are stored.

---

## Phase 1: LlamaGuard Safety Classifier

### Goal

Replace `ContentSafetyProvider`'s static keyword heuristics with `meta-llama/LlamaGuard-3-8B`. LlamaGuard understands semantic context rather than keyword presence, and returns structured harm categories that map directly onto the existing flag model. After this phase, the heuristic provider becomes a fallback only.

### What changes in `app/providers.py`

Add `LlamaGuardProvider` class:

```python
class LlamaGuardProvider:
    """Calls LlamaGuard-3-8B via HuggingFace Inference API."""

    CATEGORY_MAP = {
        "S1": ("ViolentCrimes", 0.95),
        "S2": ("NonViolentCrimes", 0.85),
        "S3": ("SexCrimes", 0.95),
        "S4": ("ChildExploitation", 0.99),
        "S5": ("Defamation", 0.70),
        "S6": ("SpecializedAdvice", 0.65),
        "S7": ("PrivacyViolation", 0.85),
        "S8": ("IntellectualProperty", 0.65),
        "S9": ("CBRN", 0.95),
        "S10": ("HateSpeech", 0.85),
        "S11": ("SelfHarm", 0.90),
        "S12": ("SexualContent", 0.75),
        "S13": ("ElectionsInfluence", 0.70),
        "S14": ("CodeInterpreterAbuse", 0.92),
    }

    async def classify(self, text: str, settings) -> list[dict]:
        """Returns list of flag dicts compatible with ContentSafetyProvider output."""
        prompt = self._format_prompt(text)
        raw = await call_hf_raw(prompt, settings, max_tokens=64, temperature=0.0)
        return self._parse(raw)

    def _format_prompt(self, text: str) -> str:
        return (
            "[INST] Task: Check if there is unsafe content in the user message.\n"
            "<BEGIN CONVERSATION>\nUser: " + text + "\n<END CONVERSATION>\n"
            "Provide your safety assessment for the user message. "
            "Answer 'safe' or 'unsafe' followed by the violated categories. [/INST]"
        )

    def _parse(self, raw: str) -> list[dict]:
        raw = raw.strip()
        if raw.startswith("safe"):
            return []
        flags = []
        lines = raw.split("\n")
        if len(lines) >= 2:
            for code in lines[1].split(","):
                code = code.strip()
                if code in self.CATEGORY_MAP:
                    name, score = self.CATEGORY_MAP[code]
                    flags.append({"category": name, "score": score,
                                  "severity": "Critical" if score >= 0.90 else "High",
                                  "flagged": True})
        return flags
```

Wire into `ContentSafetyProvider.analyze_text()` behind `LLAMAGUARD_ENABLED` env var:

```python
LLAMAGUARD_ENABLED = os.getenv("LLAMAGUARD_ENABLED", "false").lower() == "true"

class ContentSafetyProvider:
    async def analyze_text_async(self, text: str, settings) -> dict:
        if LLAMAGUARD_ENABLED:
            flags = await LlamaGuardProvider().classify(text, settings)
            flags += self._detect_privacy_flags(text)   # always run regex PII
        else:
            flags = self._analyze_heuristically(text)
        ...
```

### Feature flag

```bash
LLAMAGUARD_ENABLED=true  # set in HF Space secrets
```

### Definition of Done

- `LlamaGuardProvider` returns correct flags for all 14 S-categories.
- Red-team suite achieves ≥ 97% pass rate with LlamaGuard enabled.
- Heuristic path still used when `LLAMAGUARD_ENABLED=false` (zero-dependency demo mode preserved).

---

## Phase 2: Semantic Similarity / Paraphrase Detection

### Goal

Catch paraphrase attacks — prompts that express the same dangerous intent as known attack patterns but use different vocabulary (e.g. "erase all records from the prod DB" instead of "DROP TABLE"). Semantic similarity against a curated vector store of known attacks closes that gap.

### Embedding provider

```python
import httpx

async def embed(text: str, token: str) -> list[float]:
    """Embed via sentence-transformers/all-MiniLM-L6-v2 (384-dim)."""
    resp = await httpx.AsyncClient().post(
        "https://router.huggingface.co/hf-inference/models/"
        "sentence-transformers/all-MiniLM-L6-v2",
        headers={"Authorization": f"Bearer {token}"},
        json={"inputs": text},
    )
    resp.raise_for_status()
    return resp.json()  # list of 384 floats
```

### pgvector table (PostgreSQL only)

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE attack_embeddings (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL,
    category    VARCHAR(100) NOT NULL,
    label       VARCHAR(10) NOT NULL,     -- 'attack' | 'safe'
    case_id     VARCHAR(100),
    embedding   vector(384) NOT NULL,
    created_at  TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX ON attack_embeddings
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
```

### Similarity check in `app/risk_engine.py`

```python
SIMILARITY_THRESHOLD = float(os.getenv("SIMILARITY_THRESHOLD", "0.85"))

async def check_semantic_similarity(text: str, db, settings) -> float:
    """Returns max similarity to known attack embeddings. 0.0 on SQLite."""
    if "sqlite" in str(settings.database_url):
        return 0.0
    vec = await embed(text, settings.hf_token)
    row = db.execute(
        "SELECT 1 - (embedding <=> :v) AS sim FROM attack_embeddings "
        "WHERE label='attack' ORDER BY embedding <=> :v LIMIT 1",
        {"v": str(vec)}
    ).fetchone()
    return float(row.sim) if row else 0.0
```

### Seed embeddings on startup

In `app/seed.py`, after seeding policies:

```python
async def seed_attack_embeddings(db, settings):
    """Embed all Block cases from evaluation datasets."""
    for path in glob.glob(f"{settings.evaluation_seed_path}/*.json"):
        cases = json.load(open(path))
        for case in cases:
            if case.get("expectedDecision") == "Block":
                vec = await embed(case["userPrompt"], settings.hf_token)
                # upsert into attack_embeddings ...
```

### Feature flag

```bash
SEMANTIC_SIMILARITY_ENABLED=true
SIMILARITY_THRESHOLD=0.85
```

### Definition of Done

- Paraphrase attack cases achieve ≥ 85% detection.
- SQLite path short-circuits with no error (zero-dependency demo preserved).

---

## Phase 3: LLM-as-Judge for Ambiguous Cases

### Goal

Grey-zone scores (normalized 35–80) get a second-pass reasoning evaluation from an LLM judge instead of defaulting to Escalate, reducing human review queue volume by ≥ 30%.

### Judge call in `app/risk_engine.py`

```python
JUDGE_ENABLED = os.getenv("LLM_JUDGE_ENABLED", "false").lower() == "true"
GREY_ZONE_LOW = float(os.getenv("GREY_ZONE_LOW", "35"))
GREY_ZONE_HIGH = float(os.getenv("GREY_ZONE_HIGH", "80"))

JUDGE_SYSTEM = """You are a safety judge. Given a user prompt and detected risk signals,
return ONLY this JSON:
{"decision": "Allow|Escalate|Block", "reasoning": "<max 100 words>",
 "confidence": 0.0-1.0, "is_definitive": true|false}"""

async def judge_grey_zone(prompt: str, signals: list, score: float, settings) -> dict | None:
    if not JUDGE_ENABLED or not (GREY_ZONE_LOW <= score <= GREY_ZONE_HIGH):
        return None
    user_msg = f"Prompt: {prompt}\nSignals: {signals}\nCurrent score: {score}"
    raw = await call_hf(user_msg, settings, system_prompt=JUDGE_SYSTEM)
    try:
        return json.loads(raw)
    except Exception:
        return None  # safe default: keep original decision
```

### Feature flag

```bash
LLM_JUDGE_ENABLED=true
GREY_ZONE_LOW=35
GREY_ZONE_HIGH=80
```

### Definition of Done

- Judge is invoked only for scores in the grey zone.
- On JSON parse failure, original decision is preserved (no regression).
- Human review Escalate rate drops ≥ 30% in shadow comparison.

---

## Phase 4: Policy Admin API Enhancements

### Goal

Enable operators to stage policy changes, dry-run them against real prompts, and promote from Draft → Active — all without redeployment.

### New endpoints in `app/main.py`

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/admin/policies/{id}/duplicate` | Clone policy as Draft |
| `POST` | `/api/admin/policies/dry-run` | Evaluate prompt against a policy without writing audit records |
| `PUT` | `/api/admin/policies/{id}/promote` | Promote Draft → Active, archive previous Active |

### Policy versioning addition to `PolicyProfile`

```python
class PolicyProfile(Base):
    ...
    status = Column(String, default="Active")   # Draft | Active | Archived
    draft_of = Column(String, nullable=True)    # FK to parent policy id
```

Dry-run endpoint calls the same `evaluate_risk()` pipeline but does not write to `AuditEvent`.

### Feature flag

```bash
POLICY_ADMIN_ENABLED=true
```

---

## Phase 5: Auto-Improvement Feedback Loop

### Goal

Human reviewer decisions on escalated cases feed back into the semantic similarity store and regression suite automatically.

### Flow

```
Prompt flagged for Escalate
    │
    ▼
AuditEvent written (decision=Escalate, requiresHumanReview=True)
    │
    ▼
Reviewer labels via Admin UI: Allow (false positive) | Block (confirmed attack)
    │
    ├── Allow  → embed prompt → insert attack_embeddings label='safe'
    └── Block  → embed prompt → insert attack_embeddings label='attack'
    │
    ▼ (weekly background job)
EmbeddingRefreshJob — processes all unlabeled AuditEvents
    │
    ▼ (nightly)
RegressionJob — runs full 48-case suite, alerts if accuracy drops
```

### Background jobs in `app/main.py`

```python
from contextlib import asynccontextmanager
import asyncio

@asynccontextmanager
async def lifespan(app):
    asyncio.create_task(nightly_regression_job())
    asyncio.create_task(weekly_embedding_refresh())
    yield

async def nightly_regression_job():
    while True:
        await asyncio.sleep(24 * 3600)
        # run eval suite, create alert if accuracy < 0.95
        ...

async def weekly_embedding_refresh():
    while True:
        await asyncio.sleep(7 * 24 * 3600)
        # embed new labeled cases, upsert into attack_embeddings
        ...
```

### Metrics exposed via `/api/metrics`

| Metric | Description |
|---|---|
| `block_accuracy` | TP / (TP + FN) from last regression run |
| `allow_accuracy` | TN / (TN + FP) from last regression run |
| `false_positive_rate` | FP / total from last regression run |
| `embedding_count` | Total rows in `attack_embeddings` |

### Feature flag

```bash
REGRESSION_JOB_ENABLED=true
EMBEDDING_REFRESH_ENABLED=true
BLOCK_ACCURACY_THRESHOLD=0.95
ALLOW_ACCURACY_THRESHOLD=0.98
```

---

## Regression Thresholds

| Metric | Threshold |
|---|---|
| Block accuracy | ≥ 95% |
| Allow accuracy | ≥ 98% |
| Overall pass rate | ≥ 95% |

Current baseline (Phase 0, heuristic-only): **97.9% overall, 96.2% block accuracy, 0% false positive rate** on the 48-case red-team suite.

---

## Feature Flag Summary

| Flag (env var) | Default | Controls |
|---|---|---|
| `LLAMAGUARD_ENABLED` | `false` | Phase 1: LlamaGuard as primary classifier |
| `SEMANTIC_SIMILARITY_ENABLED` | `false` | Phase 2: pgvector paraphrase detection |
| `SIMILARITY_THRESHOLD` | `0.85` | Phase 2: cosine similarity cutoff |
| `LLM_JUDGE_ENABLED` | `false` | Phase 3: LLM-as-judge for grey zone |
| `GREY_ZONE_LOW` | `35` | Phase 3: lower grey zone bound |
| `GREY_ZONE_HIGH` | `80` | Phase 3: upper grey zone bound |
| `POLICY_ADMIN_ENABLED` | `false` | Phase 4: draft/promote policy lifecycle |
| `REGRESSION_JOB_ENABLED` | `false` | Phase 5: nightly regression job |
| `EMBEDDING_REFRESH_ENABLED` | `false` | Phase 5: weekly embedding refresh |
