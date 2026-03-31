import os
import json
import uuid
from datetime import datetime


def seed_database(db, settings):
    from .database import PolicyProfile

    seed_path = settings.policy_seed_path

    # Also try a local relative path if the configured absolute path doesn't exist
    if not os.path.exists(seed_path):
        # Try resolving relative to the project root (two levels up from this file)
        project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        alt_path = os.path.join(project_root, "policies", "samples")
        if os.path.exists(alt_path):
            seed_path = alt_path
        else:
            return

    for fname in os.listdir(seed_path):
        if not fname.endswith(".json"):
            continue

        fpath = os.path.join(seed_path, fname)
        try:
            with open(fpath, encoding="utf-8") as f:
                data = json.load(f)
        except (json.JSONDecodeError, OSError):
            continue

        tenant_id = str(data.get("tenantId", ""))
        application_id = str(data.get("applicationId", ""))
        name = data.get("name", "")

        # Skip if already exists
        exists = (
            db.query(PolicyProfile)
            .filter_by(name=name, tenant_id=tenant_id, application_id=application_id)
            .first()
        )
        if exists:
            continue

        profile = PolicyProfile(
            id=str(uuid.uuid4()),
            name=name,
            tenant_id=tenant_id,
            application_id=application_id,
            scope=data.get("scope", "Application"),
            domain=data.get("domain", ""),
            description=data.get("description", ""),
            is_active=True,
            version=1,
            policy_json=json.dumps(data.get("policy", {})),
            effective_from=data.get("effectiveFrom", "2026-01-01T00:00:00Z"),
            created_at=datetime.utcnow().isoformat(),
        )
        db.add(profile)

    db.commit()
