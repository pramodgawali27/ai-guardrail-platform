# Enterprise AI Guardrail Platform - Product Architecture Strategy

## Product Positioning

The platform should be positioned as an organization-wide AI security control plane that every AI client can call before prompts, context, model outputs, and tool actions are allowed to proceed.

The current repository already has the correct foundation:

- `Guardrail.API` exposes REST, MCP, OpenAI-compatible chat, manifests, audit, human review, policy, tools, and evaluation endpoints.
- `Guardrail.Application` keeps API use cases isolated behind commands, queries, and validators.
- `Guardrail.Core` owns tenant, policy, risk, audit, review, tool, evaluation, and data-boundary domain models.
- `Guardrail.Infrastructure` provides EF Core persistence, policy resolution, safety providers, prompt shield providers, firewalls, observability, and evaluation workers.
- `policies/samples` and `evaluations/datasets` already provide a seed corpus for product demos, regression tests, and policy examples.
- `deploy/k8s`, `infra/azure`, Docker, and Compose already provide early deployment paths.

The product goal is to turn this from a guardrail API into a full enterprise guardrail fabric:

```text
AI Client / Agent / App
        |
        |  SDK, MCP, HTTP Middleware, Gateway, or OpenAI-compatible proxy
        v
Enterprise Guardrail Gateway
        |
        +--> Identity, tenant, app, environment, data boundary
        +--> Input prompt and context inspection
        +--> Prompt injection and jailbreak defense
        +--> Sensitive data, secret, PII, PHI, PCI, IP, and source-code leakage controls
        +--> Tool and action firewall
        +--> Model output validation, redaction, citation, and schema checks
        +--> Human review and approval workflow
        +--> Audit, evidence, metrics, incidents, and evaluations
        +--> Threat intelligence and continuous policy improvement
```

## Design Principles

1. Default-deny for sensitive actions. Allow common chat flows, but require explicit policy for tools, data sources, outbound connectors, code execution, write actions, and external sharing.
2. Policy as product. Admins should manage rules using guided forms, templates, simulations, approvals, and version history instead of raw JSON only.
3. Multiple integration surfaces. Support REST, MCP, OpenAI-compatible proxy, SDK middleware, gateway sidecars, CI checks, and webhook connectors.
4. Evidence-first auditing. Every decision should have a correlation ID, policy version, risk signals, provider versions, redaction metadata, and review status.
5. Privacy by design. Store hashes, summaries, classifications, and encrypted evidence. Retain raw prompts only when explicitly configured.
6. Continuous evaluation. Every new rule, provider, model, prompt template, and threat feed update must pass regression and red-team suites before promotion.
7. Explainable controls. Admins and buyers should understand what was blocked, why, under which policy, and what changed over time.

## Competitive Baseline

The product should benchmark against the current enterprise guardrail market:

- NVIDIA NeMo Guardrails: programmable rails and runtime guardrails for LLM applications.
- Microsoft Azure AI Content Safety and Prompt Shields: managed content safety, jailbreak, protected material, and prompt attack detection.
- Lakera Guard: prompt injection, data leakage, and LLM application security controls.
- Protect AI, HiddenLayer, and related AI security vendors: broader AI security posture, model scanning, threat detection, and supply-chain controls.
- Open-source guardrail frameworks such as Guardrails AI, Llama Guard, Rebuff, Garak, and NeMo Guardrails.

To be sellable, this platform should compete on organization-wide integration, admin usability, policy lifecycle, audit evidence, MCP-native tool governance, and self-improving evaluations rather than only keyword detection.

Reference frameworks to align with:

- OWASP Top 10 for LLM Applications: prompt injection, sensitive information disclosure, supply-chain risk, excessive agency, insecure output handling, and overreliance.
- NIST AI RMF and the Generative AI Profile: governance, mapping, measurement, risk management, transparency, privacy, and security controls.
- Model Context Protocol security guidance: tool permissioning, origin validation, explicit consent, tenant isolation, and context-source trust.

## Target Architecture

### 1. Integration Layer

Keep the current REST, MCP, and OpenAI-compatible proxy surfaces, then add first-party connectors:

- TypeScript SDK for Node.js, VS Code extensions, internal web apps, Copilot-style agents, and serverless functions.
- Python SDK for data science, LangChain, LlamaIndex, CrewAI, AutoGen, and internal notebooks.
- .NET SDK for enterprise services and ASP.NET middleware.
- HTTP reverse proxy / Envoy filter for zero-code integration.
- MCP server package for Claude Desktop, Claude Code, Cursor, VS Code, and internal agent hosts.
- OpenAI-compatible proxy for apps that already use `/v1/chat/completions`.
- GitHub Action and CI scanner for prompts, policy bundles, eval results, and unsafe agent tool definitions.

Recommended client contract:

```text
evaluate_input(prompt, system, context, requested_tools, tenant, app, user)
model_or_agent_call()
evaluate_tool_call(tool, args, target, data_classification)
execute_tool_if_allowed()
evaluate_output(output, schema, citations, tenant, app, user)
return_or_redact()
```

### 2. Policy Control Plane

Expand the existing `PoliciesController`, `PolicyProfile`, and JSON policy model into a full lifecycle:

- Draft, Active, Scheduled, Deprecated, Archived policy states.
- Versioned policies with immutable historical versions.
- Policy inheritance across global, tenant, department, app, environment, user group, and data domain.
- Approval workflow for high-risk changes.
- Policy simulation mode and shadow mode.
- Policy dry run against selected audit records and evaluation datasets.
- Rollback to prior version.
- Rule marketplace with templates for healthcare, finance, HR, legal, engineering, sales, support, and public chatbot use cases.
- Exception workflow with expiry, owner, reason, and audit trail.

Admin UI modules:

- Dashboard: risk trends, blocks, escalations, top apps, top users, top signals, false positive rate, incidents.
- Policy Builder: guided controls, JSON advanced mode, rule templates, inheritance preview.
- Simulator: test one prompt/output/tool call before publishing.
- Evaluation Lab: run red-team suites and compare current vs draft policy.
- Tool Firewall: allowed, denied, approval-required, parameter restrictions, data movement rules.
- Data Boundaries: source trust, region, tenant isolation, document classification, retention.
- Human Review Queue: approve, deny, redact, annotate, create incident, create rule from case.
- Audit Explorer: searchable decisions with evidence, correlation IDs, policy versions, and export.
- Threat Intelligence: new attacks, recommended rules, vendor/provider update notes.
- Tenant and App Registry: onboarding, API keys, auth scopes, quotas, environment separation.

### 3. Guardrail Enforcement Plane

The enforcement plane should evaluate five surfaces, not only prompt and output:

1. Input prompt: jailbreaks, prompt injection, unsafe requests, social engineering, malicious code intent, policy violations.
2. Retrieved context: indirect prompt injection, source trust, cross-tenant leakage, stale documents, classified data, untrusted URLs.
3. Tool call: excessive agency, destructive actions, parameter injection, data exfiltration, payment or account changes, privilege escalation.
4. Model output: unsafe content, PII leakage, secrets, hallucination markers, required citations, schema validity, unsafe instructions.
5. Memory and logs: retention limits, privacy scrubbing, tenant isolation, user deletion, incident preservation.

Core detectors:

- Prompt injection and jailbreak detector.
- Indirect prompt injection detector for retrieved documents and web pages.
- Sensitive data detector for PII, PHI, PCI, secrets, credentials, API keys, tokens, and internal identifiers.
- Business policy detector for forbidden subjects, regulated advice, competitor disclosure, HR/legal/finance constraints.
- Tool/action risk classifier for read/write/delete/export/external-send/code-execute/payment/admin actions.
- Data exfiltration detector for suspicious destinations, encoded data, bulk export, external URLs, and clipboard/file writes.
- Output schema and citation validator.
- Grounding and evidence validator for RAG responses.
- Toxicity, hate, self-harm, sexual, violence, CBRN, illegal activity, and child-safety classifier.
- Model and provider allowlist with environment-specific restrictions.

Decision model:

```text
Allow       - continue normally
AllowWarn   - continue, attach warning and audit signal
Redact      - transform sensitive content, continue
Transform   - rewrite to safe answer, continue
Escalate    - require human review or app-specific fallback
Approve     - require just-in-time human approval for a tool/action
Block       - stop request, return safe explanation
Quarantine  - preserve evidence, open incident, notify security
```

### 4. Risk Engine

Keep the existing weighted risk engine, but evolve it into a hybrid model:

- Rule-based signals for deterministic controls.
- ML/classifier signals for semantic risk.
- Similarity search against known attacks and safe examples.
- LLM-as-judge only for ambiguous cases, never as the sole blocker for high-risk actions.
- Tenant-specific risk weights.
- Confidence scores, false-positive feedback, and policy version impact.
- Provider health and fallback confidence.

Recommended risk dimensions:

- Content safety
- Privacy and sensitive data
- Prompt injection and jailbreak
- Indirect context injection
- Tool/action safety
- Data boundary and access control
- Business policy
- Output quality and schema compliance
- Grounding and citation quality
- Regulatory domain risk
- User, app, and environment trust

### 5. Audit, Evidence, and Compliance

The current audit entities are a good base. Make them compliance-grade:

- Immutable append-only audit events.
- Hash chains or tamper-evident event digests.
- Policy version, model version, provider version, detector version, and SDK version on every event.
- Configurable raw prompt retention: none, encrypted short-term, legal hold, or regulated archive.
- Evidence package export for incidents.
- SIEM integration through syslog, OpenTelemetry, Splunk HEC, Azure Monitor, Datadog, and webhooks.
- Audit search by tenant, app, user, session, correlation ID, decision, signal, policy version, and time range.
- Admin activity audit for every policy, rule, review, exception, and deployment change.

### 6. Self-Improving Guardrail Loop

The system should improve through governed automation, not uncontrolled live changes.

Pipeline:

```text
Threat feeds + public vulnerability sources + internal incidents + review feedback
        |
        v
Threat ingestion and normalization
        |
        v
Candidate attacks, rules, test cases, and detector prompts
        |
        v
Offline evaluation against regression + red-team + customer-specific safe traffic
        |
        v
Admin recommendation with explainability and expected impact
        |
        v
Approval, staged rollout, monitoring, rollback
```

Sources to ingest:

- OWASP LLM guidance updates.
- NIST and government AI security guidance.
- Vendor advisories from model providers and guardrail providers.
- Public prompt injection and jailbreak corpora.
- Internal incident reports and human review decisions.
- Failed evaluations and customer false-positive reports.
- CVEs and package advisories for model-serving, MCP, SDK, and plugin dependencies.

The system should generate:

- New red-team cases.
- New semantic attack embeddings.
- Candidate rule templates.
- Detector prompt updates.
- Recommended policy threshold changes.
- Release notes for admins.

Safety requirement: self-improvement must never auto-promote blocking policies into production without admin approval unless a tenant explicitly opts into emergency managed rules.

## Organization-Wide Deployment Strategy

### Deployment Modes

1. Central SaaS or internal platform service
   - Best for large organizations.
   - One guardrail control plane, many tenants/apps.
   - Shared policy templates and central audit.

2. Private cloud deployment
   - Kubernetes with PostgreSQL, Redis, object storage, and managed identity.
   - Recommended enterprise default.

3. Sidecar or local gateway
   - Runs close to sensitive apps.
   - Useful for low-latency, air-gapped, or data-residency workloads.

4. Embedded SDK mode
   - Lightweight checks in app process.
   - Should still call central policy and audit APIs.

5. Development desktop mode
   - Claude Desktop, Claude Code, VS Code, Cursor, and Copilot-style tools call MCP or local proxy.

### Reference Kubernetes Topology

```text
Ingress / API Gateway
        |
        v
Guardrail API pods
        |
        +--> PostgreSQL primary + replicas
        +--> Redis cache / rate limit / queue
        +--> Object storage for encrypted evidence packages
        +--> Evaluation worker pods
        +--> Threat intelligence worker pods
        +--> Admin UI static assets or separate frontend
        +--> OpenTelemetry collector
        +--> SIEM / alerting sink
```

### Enterprise Controls

- OIDC/SAML SSO with RBAC and tenant-scoped permissions.
- Service-to-service auth with JWT, mTLS, or workload identity.
- Separate keys per tenant and environment.
- Network policies and private endpoints.
- Per-tenant quotas and rate limits.
- Region-specific storage and data boundary policies.
- Blue/green or canary release for detector and policy changes.
- Disaster recovery runbook with database backups and policy export.

## API and Connector Strategy

### MCP

Current MCP support should evolve into:

- Streamable HTTP with session support where clients require it.
- Tool-list change notifications when policy changes.
- Dynamic tenant-aware tool registry.
- Tool annotations for destructive, external, write, read-only, approval-required, and data-export actions.
- Consent and approval workflow tools.
- Server-side origin validation, tenant validation, and explicit app registration.

Recommended MCP tools:

- `guardrail.evaluate_input`
- `guardrail.evaluate_context`
- `guardrail.evaluate_tool_call`
- `guardrail.evaluate_output`
- `guardrail.evaluate_full`
- `guardrail.get_tool_registry`
- `guardrail.request_human_approval`
- `guardrail.submit_review_feedback`
- `guardrail.get_policy_manifest`

### SDKs

Each SDK should provide:

- One-line middleware for input and output evaluation.
- Tool wrapper decorators.
- Retry, timeout, and fail-closed/fail-open configuration.
- Correlation ID propagation.
- Local PII pre-scrub option.
- Policy manifest cache.
- Structured errors and typed decisions.
- OpenTelemetry spans.

### OpenAI-Compatible Proxy

Expand from non-streaming to:

- Streaming chat completions with output chunk buffering and partial redaction.
- Model allowlist and provider routing.
- Failover model providers.
- Guardrail metadata in response headers and JSON body.
- Policy-controlled request and response logging.

## Admin-Friendly Policy Model

Move from raw JSON-first editing to a layered model:

```text
Organization Baseline
  -> Department Policy
    -> Application Policy
      -> Environment Override
        -> Temporary Exception
```

Policy categories:

- Content safety
- Data protection
- Prompt injection
- Context/RAG protection
- Tool/action permissions
- Output quality and citations
- Regulated-domain behavior
- Retention and audit
- Human review thresholds
- Provider and model restrictions

Admin workflow:

1. Choose template or existing policy.
2. Edit guided controls.
3. Preview inherited effective policy.
4. Simulate prompts, outputs, context, and tool calls.
5. Run evaluation suite.
6. Submit for approval if needed.
7. Promote to scheduled or active.
8. Monitor impact and rollback if necessary.

## Security Constraints Coverage

Minimum constraints for a sellable enterprise release:

- Prompt injection and jailbreak detection.
- Indirect prompt injection from documents, web pages, emails, tickets, and code comments.
- PII, PHI, PCI, secrets, credentials, access tokens, keys, and confidential terms.
- Cross-tenant data isolation.
- Region and data residency restrictions.
- User and app authorization checks.
- Tool allow/deny lists.
- Tool parameter restrictions.
- Approval gates for high-impact actions.
- External destination controls.
- File, clipboard, network, email, database, and shell command restrictions.
- SQL, shell, code execution, and destructive operation detection.
- RAG source trust scoring and citation requirement.
- Model output schema validation.
- Hallucination and unsupported claim handling for regulated apps.
- Audit and retention policy.
- Incident and legal hold support.
- Fail-open/fail-closed configuration by app criticality.
- Tamper-evident admin activity logging.

## Product Roadmap

### Phase 0 - Stabilize Current Foundation

- Keep .NET implementation as the source of truth.
- Mark `app/` Python prototype as legacy or move it under `archive/`.
- Add architecture decision records for REST, MCP, proxy, policy, audit, and provider design.
- Add endpoint-level OpenAPI examples.
- Add seed data and eval docs for quick demos.

### Phase 1 - Enterprise Admin MVP

- Policy lifecycle: draft, active, archived.
- Policy dry run and simulation endpoint.
- Evaluation run from admin UI.
- Human review queue UI.
- Audit explorer UI.
- Tenant/application onboarding UI.
- RBAC claims mapping documentation.

### Phase 2 - Integration Productization

- TypeScript, Python, and .NET SDKs.
- VS Code and GitHub Copilot extension guidance.
- Claude Desktop / Claude Code MCP setup templates.
- LangChain, LlamaIndex, Semantic Kernel, and AutoGen examples.
- OpenAI-compatible streaming proxy.
- Envoy/Nginx gateway deployment pattern.

### Phase 3 - Advanced Enforcement

- `evaluate_context` and `evaluate_tool_call` first-class APIs.
- Parameter-level tool policies.
- External destination and data export controls.
- Semantic attack similarity using vector search.
- Llama Guard or equivalent local classifier option.
- Grounding and citation verifier.

### Phase 4 - Continuous Intelligence

- Threat intelligence ingestion.
- Managed rule recommendations.
- Automated red-team generation.
- False-positive/false-negative feedback loop.
- Canary policy rollout.
- Drift detection across apps and tenants.

### Phase 5 - Enterprise Scale and Commercial Readiness

- Multi-region deployment.
- Tenant billing and usage metering.
- SIEM integrations.
- Compliance reports.
- Marketplace templates.
- SOC2-ready operational controls.
- High-availability reference architecture.

## Success Metrics

- Detection: attack block rate, false positive rate, false negative rate, category-level coverage.
- Operations: p95 latency, provider fallback rate, uptime, policy publish time, rollback time.
- Admin usability: time to create policy, time to investigate incident, simulation pass rate.
- Adoption: number of onboarded apps, SDK usage, MCP clients, proxy traffic.
- Governance: review SLA, exception expiry compliance, audit completeness, evaluation coverage.
- Business: demo conversion rate, enterprise pilot activation time, support ticket volume.

## Immediate Next Build Items

1. Add policy draft/active/archive status to the domain and admin UI.
2. Add policy dry-run endpoint that does not write audit records.
3. Add `evaluate_tool_call` and `evaluate_context` as first-class REST and MCP tools.
4. Add SDK starter packages for TypeScript and Python.
5. Add OpenAI-compatible streaming proxy support.
6. Add audit explorer filters and export.
7. Add evaluation comparison: current active policy vs draft policy.
8. Add tamper-evident audit event digest.
9. Add a managed-threat-rules ingestion worker in recommendation-only mode.
10. Add deployment guides for Kubernetes, Azure Container Apps, and local MCP desktop usage.
