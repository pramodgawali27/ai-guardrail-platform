import json
import os
import time
import uuid
from datetime import datetime

from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from sqlalchemy.orm import Session

from .config import get_settings
from .database import Base, engine, get_db
from .hf_client import call_hf
from .policy_engine import evaluate_policy, resolve_policy
from .providers import ContentSafetyProvider, PromptShieldProvider
from .risk_engine import evaluate_risk
from .seed import seed_database

app = FastAPI(title="AI Guardrail Platform")
settings = get_settings()
content_safety = ContentSafetyProvider()
prompt_shield = PromptShieldProvider()


# ---------------------------------------------------------------------------
# Startup
# ---------------------------------------------------------------------------

@app.on_event("startup")
def startup():
    Base.metadata.create_all(bind=engine)
    if settings.seed_on_startup:
        from .database import SessionLocal
        db = SessionLocal()
        try:
            seed_database(db, settings)
        finally:
            db.close()


# ---------------------------------------------------------------------------
# Utility helpers
# ---------------------------------------------------------------------------

def _build_result(tenant_id: str, application_id: str, risk_result: dict, policy_name: str) -> dict:
    r = risk_result
    return {
        "executionId": str(uuid.uuid4()),
        "correlationId": uuid.uuid4().hex,
        "decision": r["decision"],
        "riskLevel": r["risk_level"],
        "normalizedRiskScore": r["normalized_score"],
        "rationale": r["rationale"],
        "appliedPolicies": [policy_name],
        "detectedSignals": r["applied_signals"],
        "requiresHumanReview": r["decision"] == "Escalate",
        "isAllowed": r["decision"] in ("Allow", "AllowWithConstraints"),
        "isBlocked": r["decision"] == "Block",
        "evaluatedAt": datetime.utcnow().isoformat() + "Z",
        "durationMs": r.get("duration_ms", 0),
    }


def _policy_to_dict(p) -> dict:
    return {
        "id": p.id,
        "name": p.name,
        "tenantId": p.tenant_id,
        "applicationId": p.application_id,
        "scope": p.scope,
        "domain": p.domain or "",
        "description": p.description or "",
        "isActive": p.is_active,
        "version": p.version,
        "policy": json.loads(p.policy_json) if p.policy_json else {},
        "effectiveFrom": p.effective_from,
        "createdAt": p.created_at,
    }


# ---------------------------------------------------------------------------
# Health / version
# ---------------------------------------------------------------------------

@app.get("/api/version")
def version():
    return {
        "version": "1.0.0",
        "service": "Guardrail API",
        "buildDate": "2026-03-20T00:00:00Z",
    }


@app.get("/health")
def health():
    return {"status": "healthy"}


# ---------------------------------------------------------------------------
# Guardrail evaluation endpoints
# ---------------------------------------------------------------------------

@app.post("/api/guardrail/evaluate-input")
def evaluate_input(
    body: dict,
    x_tenant_id: str = Header(default="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
    x_application_id: str = Header(default="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002"),
    db: Session = Depends(get_db),
):
    start = time.time()
    user_prompt = body.get("userPrompt", "")
    system_prompt = body.get("systemPrompt", "")

    policy = resolve_policy(x_tenant_id, x_application_id, db)
    full_text = f"{system_prompt}\n{user_prompt}" if system_prompt else user_prompt

    content_result = content_safety.analyze_text(full_text)
    shield_result = prompt_shield.detect_injection(user_prompt)
    policy_result = evaluate_policy(user_prompt, None, policy)
    risk_result = evaluate_risk(content_result, shield_result, policy_result)
    risk_result["duration_ms"] = int((time.time() - start) * 1000)

    return _build_result(x_tenant_id, x_application_id, risk_result, policy["name"])


@app.post("/api/guardrail/evaluate-output")
def evaluate_output_endpoint(
    body: dict,
    x_tenant_id: str = Header(default="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
    x_application_id: str = Header(default="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002"),
    db: Session = Depends(get_db),
):
    start = time.time()
    model_output = body.get("modelOutput", "")
    if not model_output:
        raise HTTPException(status_code=400, detail="Model output cannot be empty.")

    policy = resolve_policy(x_tenant_id, x_application_id, db)
    content_result = content_safety.analyze_text(model_output)
    # Scan model output for indirect injection
    shield_result = prompt_shield.detect_injection("", documents=[model_output])
    policy_result = evaluate_policy(None, model_output, policy)
    risk_result = evaluate_risk(content_result, shield_result, policy_result)
    risk_result["duration_ms"] = int((time.time() - start) * 1000)

    return _build_result(x_tenant_id, x_application_id, risk_result, policy["name"])


# ---------------------------------------------------------------------------
# Demo chat endpoint
# ---------------------------------------------------------------------------

@app.post("/api/demo/chat")
async def demo_chat(body: dict, db: Session = Depends(get_db)):
    user_prompt = body.get("userPrompt", body.get("prompt", ""))
    app_context = body.get("applicationContext", body.get("appType", "enterprise-copilot"))

    # Map app context to fixed demo IDs (matching the seeded policies)
    app_id_map = {
        "plain-language":    "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001",
        "enterprise-copilot": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002",
        "healthcare":         "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003",
        "developer":          "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0004",
    }
    tenant_id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"
    application_id = app_id_map.get(app_context, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002")

    policy = resolve_policy(tenant_id, application_id, db)

    # Step 1: Input guardrail
    content_result = content_safety.analyze_text(user_prompt)
    shield_result = prompt_shield.detect_injection(user_prompt)
    policy_result = evaluate_policy(user_prompt, None, policy)
    input_risk = evaluate_risk(content_result, shield_result, policy_result)
    input_guardrail = _build_result(tenant_id, application_id, input_risk, policy["name"])

    # Step 2: AI response (only if allowed)
    model_response = {"content": "", "model": settings.hf_model_id, "blocked": True}
    output_guardrail = None

    if input_guardrail["isAllowed"]:
        ai_text = await call_hf(user_prompt, settings)
        model_response = {
            "content": ai_text,
            "model": settings.hf_model_id,
            "blocked": False,
        }

        # Step 3: Output guardrail
        out_content = content_safety.analyze_text(ai_text)
        out_shield = prompt_shield.detect_injection("", documents=[ai_text])
        out_policy = evaluate_policy(None, ai_text, policy)
        out_risk = evaluate_risk(out_content, out_shield, out_policy)
        output_guardrail = _build_result(tenant_id, application_id, out_risk, policy["name"])
    else:
        output_guardrail = {
            "decision": "Block",
            "normalizedRiskScore": 0,
            "riskLevel": "None",
            "detectedSignals": [],
            "appliedPolicies": [policy["name"]],
            "isAllowed": False,
            "isBlocked": True,
            "rationale": "Input was blocked",
            "evaluatedAt": datetime.utcnow().isoformat() + "Z",
        }

    return {
        "inputGuardrail": input_guardrail,
        "modelResponse": model_response,
        "outputGuardrail": output_guardrail,
        "correlationId": input_guardrail["correlationId"],
    }


# ---------------------------------------------------------------------------
# Policies CRUD
# ---------------------------------------------------------------------------

@app.get("/api/policies/{tenant_id}/{application_id}")
def get_policies_by_app(tenant_id: str, application_id: str, db: Session = Depends(get_db)):
    from .database import PolicyProfile
    policies = (
        db.query(PolicyProfile)
        .filter(
            PolicyProfile.tenant_id == tenant_id,
            PolicyProfile.application_id == application_id,
            PolicyProfile.is_active == True,
        )
        .all()
    )
    return [_policy_to_dict(p) for p in policies]


@app.get("/api/policies")
def get_all_policies(db: Session = Depends(get_db)):
    from .database import PolicyProfile
    policies = db.query(PolicyProfile).filter(PolicyProfile.is_active == True).all()
    return [_policy_to_dict(p) for p in policies]


@app.post("/api/policies")
def create_policy(body: dict, db: Session = Depends(get_db)):
    from .database import PolicyProfile

    profile = PolicyProfile(
        id=str(uuid.uuid4()),
        name=body.get("name", ""),
        tenant_id=body.get("tenantId", ""),
        application_id=body.get("applicationId", ""),
        scope=body.get("scope", "Application"),
        domain=body.get("domain", ""),
        description=body.get("description", ""),
        is_active=True,
        version=1,
        policy_json=json.dumps(body.get("policy", {})),
        effective_from=body.get("effectiveFrom", datetime.utcnow().isoformat() + "Z"),
        created_at=datetime.utcnow().isoformat(),
    )
    db.add(profile)
    db.commit()
    db.refresh(profile)
    return _policy_to_dict(profile)


@app.put("/api/policies/{policy_id}")
def update_policy(policy_id: str, body: dict, db: Session = Depends(get_db)):
    from .database import PolicyProfile

    profile = db.query(PolicyProfile).filter_by(id=policy_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Policy not found")

    if "policy" in body:
        profile.policy_json = json.dumps(body["policy"])
    if "name" in body:
        profile.name = body["name"]
    profile.version += 1
    db.commit()
    db.refresh(profile)
    return _policy_to_dict(profile)


@app.delete("/api/policies/{policy_id}")
def delete_policy(policy_id: str, db: Session = Depends(get_db)):
    from .database import PolicyProfile

    profile = db.query(PolicyProfile).filter_by(id=policy_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Policy not found")

    profile.is_active = False
    db.commit()
    return {"success": True}


# ---------------------------------------------------------------------------
# Static file serving  (wwwroot)
# ---------------------------------------------------------------------------

_HERE = os.path.dirname(os.path.abspath(__file__))
_PROJECT_ROOT = os.path.dirname(_HERE)

# Priority 1: Docker path  /app/wwwroot
# Priority 2: local dev    <project_root>/src/Guardrail.API/wwwroot
# Priority 3: flat         <project_root>/wwwroot
_WWWROOT_CANDIDATES = [
    "/app/wwwroot",
    os.path.join(_PROJECT_ROOT, "src", "Guardrail.API", "wwwroot"),
    os.path.join(_PROJECT_ROOT, "wwwroot"),
]

_WWWROOT = next((p for p in _WWWROOT_CANDIDATES if os.path.isdir(p)), None)

if _WWWROOT:
    app.mount("/", StaticFiles(directory=_WWWROOT, html=True), name="static")
