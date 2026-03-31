WEIGHTS = {
    "content": 0.30,
    "privacy": 0.25,
    "injection": 0.20,
    "business": 0.10,
    "action": 0.10,
    "quality": 0.05,
}

BLOCK_THRESHOLD = 0.9
ESCALATION_THRESHOLD = 0.8
CONTENT_RISK_THRESHOLD = 0.7
PRIVACY_RISK_THRESHOLD = 0.6
INJECTION_RISK_THRESHOLD = 0.5

_ACTION_CATEGORIES = {"DestructiveAction", "DataExfiltration", "CodeInterpreterAbuse", "CredentialPhishing"}
_PRIVACY_CATEGORIES = {"PII", "PHI"}


def evaluate_risk(content_result: dict, shield_result: dict, policy_result: dict) -> dict:
    flags = content_result.get("flags", [])

    # Content risk: max score of non-privacy flags
    content_risk = max(
        (f["score"] for f in flags if f.get("category") not in _PRIVACY_CATEGORIES),
        default=0.0,
    )
    content_risk = min(1.0, content_risk)

    # Privacy risk: max score of PII/PHI flags
    privacy_risk = max(
        (f["score"] for f in flags if f.get("category") in _PRIVACY_CATEGORIES),
        default=0.0,
    )
    privacy_risk = min(1.0, privacy_risk)

    # Injection risk
    injection_risk = min(1.0, shield_result.get("injection_score", 0.0))

    # Business / policy risk
    business_risk = min(1.0, policy_result.get("policy_risk_score", 0.0))

    # Action risk: max score of destructive/exfil/etc. flags
    action_risk = max(
        (f["score"] for f in flags if f.get("category") in _ACTION_CATEGORIES),
        default=0.0,
    )
    action_risk = min(1.0, action_risk)

    quality_risk = 0.0

    weighted = (
        content_risk * WEIGHTS["content"]
        + privacy_risk * WEIGHTS["privacy"]
        + injection_risk * WEIGHTS["injection"]
        + business_risk * WEIGHTS["business"]
        + action_risk * WEIGHTS["action"]
        + quality_risk * WEIGHTS["quality"]
    )
    weighted = min(1.0, weighted)

    normalized = min(100.0, round(weighted * 100, 2))

    # Decision logic
    if (
        content_risk >= BLOCK_THRESHOLD
        or privacy_risk >= BLOCK_THRESHOLD
        or injection_risk >= BLOCK_THRESHOLD
        or weighted >= BLOCK_THRESHOLD
    ):
        decision = "Block"
        risk_level = "Critical"
    elif (
        content_risk >= CONTENT_RISK_THRESHOLD
        or privacy_risk >= PRIVACY_RISK_THRESHOLD
        or injection_risk >= INJECTION_RISK_THRESHOLD
        or weighted >= ESCALATION_THRESHOLD
    ):
        decision = "Escalate"
        risk_level = "High"
    elif weighted >= 0.35 or policy_result.get("has_violations", False):
        decision = "AllowWithConstraints"
        risk_level = "Medium" if weighted >= 0.2 else "Low"
    else:
        decision = "Allow"
        risk_level = "None" if normalized == 0 else "Low"

    # Build signal list
    signals = []
    for f in flags:
        if f.get("flagged"):
            signals.append(f"{f['category']}:{f['score']:.2f}")
    for s in shield_result.get("signals", []):
        signals.append(f"{s['signal_type']}:{s['score']:.2f}")
    for v in policy_result.get("violations", []):
        signals.append(f"{v['rule_key']}:{v['score']:.2f}")

    rationale = (
        f"overall={normalized:.2f}; "
        f"content={content_risk:.2f}; "
        f"privacy={privacy_risk:.2f}; "
        f"injection={injection_risk:.2f}; "
        f"business={business_risk:.2f}; "
        f"action={action_risk:.2f}; "
        f"quality={quality_risk:.2f}; "
        f"decision={decision}"
    )

    return {
        "decision": decision,
        "normalized_score": normalized,
        "risk_level": risk_level,
        "rationale": rationale,
        "applied_signals": signals,
    }
