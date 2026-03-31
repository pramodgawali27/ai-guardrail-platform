import json
from typing import Optional
from sqlalchemy.orm import Session


_DEFAULT_POLICY = {
    "id": None,
    "name": "system-default",
    "tenant_id": "",
    "application_id": "",
    "scope": "Global",
    "domain": "",
    "forbidden_phrases": [],
    "policy_data": {},
}


def _policy_profile_to_dict(profile) -> dict:
    """Convert a PolicyProfile ORM object to a plain dict used internally."""
    try:
        policy_data = json.loads(profile.policy_json) if profile.policy_json else {}
    except (json.JSONDecodeError, TypeError):
        policy_data = {}

    forbidden_phrases = policy_data.get("forbiddenPhrases", [])

    return {
        "id": profile.id,
        "name": profile.name,
        "tenant_id": profile.tenant_id,
        "application_id": profile.application_id,
        "scope": profile.scope,
        "domain": profile.domain or "",
        "forbidden_phrases": forbidden_phrases,
        "policy_data": policy_data,
    }


def resolve_policy(tenant_id: str, application_id: str, db: Session) -> dict:
    """
    Resolve the most specific active policy for the given tenant / application.
    Priority: Application-level > Tenant-level > Global.
    Falls back to a safe default if nothing is found.
    """
    from .database import PolicyProfile  # avoid circular import at module level

    # 1. Application-level match
    profile = (
        db.query(PolicyProfile)
        .filter(
            PolicyProfile.tenant_id == tenant_id,
            PolicyProfile.application_id == application_id,
            PolicyProfile.is_active == True,
        )
        .first()
    )
    if profile:
        return _policy_profile_to_dict(profile)

    # 2. Tenant-level match (no specific application)
    profile = (
        db.query(PolicyProfile)
        .filter(
            PolicyProfile.tenant_id == tenant_id,
            PolicyProfile.application_id == "",
            PolicyProfile.is_active == True,
        )
        .first()
    )
    if profile:
        return _policy_profile_to_dict(profile)

    # 3. Global policy (no tenant, no application)
    profile = (
        db.query(PolicyProfile)
        .filter(
            PolicyProfile.tenant_id == "",
            PolicyProfile.application_id == "",
            PolicyProfile.is_active == True,
        )
        .first()
    )
    if profile:
        return _policy_profile_to_dict(profile)

    return dict(_DEFAULT_POLICY)


def evaluate_policy(
    user_prompt: Optional[str],
    model_output: Optional[str],
    policy: dict,
) -> dict:
    """
    Check the combined text against the policy's forbidden phrases.
    Returns violations, policy_risk_score, has_violations.
    """
    forbidden_phrases = policy.get("forbidden_phrases", [])

    # Aggregate all text to check
    parts = [p for p in [user_prompt, model_output] if p]
    aggregate_text = "\n".join(parts)

    violations = []
    seen = set()
    for phrase in forbidden_phrases:
        phrase_lower = phrase.lower()
        if phrase_lower in seen:
            continue
        seen.add(phrase_lower)
        if phrase_lower in aggregate_text.lower():
            violations.append({
                "rule_key": f"forbidden-phrase:{phrase}",
                "rule_name": "Forbidden phrase detected",
                "description": f"Content matched restricted phrase '{phrase}'.",
                "severity": "High",
                "score": 0.75,
            })

    score = max((v["score"] for v in violations), default=0.0)
    score = min(1.0, score)

    return {
        "has_violations": len(violations) > 0,
        "violations": violations,
        "policy_risk_score": score,
    }
