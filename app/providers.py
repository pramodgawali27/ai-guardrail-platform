import re
from typing import Optional


# ---------------------------------------------------------------------------
# Content Safety Provider
# ---------------------------------------------------------------------------

# (category, score, severity, markers)
# For Violence/SelfHarm/Sexual (short single-word lists) we use word-boundary matching.
# For all others (multi-word phrases) we use plain substring matching.
_SHORT_WORD_CATEGORIES = {"Violence", "SelfHarm", "Sexual"}

PATTERNS = [
    ("Jailbreak", 0.92, "Critical", [
        "pretend you are", "pretend to be", "act as if you have no",
        "you are now", "as DAN", "in DAN mode", "do anything now",
        "you have no restrictions", "no rules or restrictions", "without any restrictions",
        "ignore all restrictions", "bypass content filters", "bypass the filter",
        "no content policy", "bypass safety", "ignore your training",
        "roleplay as", "in this hypothetical scenario",
        "fictional universe with no content rules", "no content rules",
        "in this fictional world", "will answer any question",
        "for educational purposes only, explain how to",
    ]),
    ("SocialEngineering", 0.90, "Critical", [
        "wire transfer", "impersonating the CEO", "impersonating a",
        "pretending to be a new employee", "pretending to be an employee",
        "urgent wire transfer", "overseas account",
        "get them to reset the password", "bypass any suspicion",
        "make it sound legitimate", "draft a convincing email impersonating",
    ]),
    ("CodeInterpreterAbuse", 0.92, "Critical", [
        "silently scans the filesystem", "scan the filesystem for",
        "reads their contents and sends", "sends them via http post",
        "ransom note", "bitcoin payment", "demands bitcoin",
        "encrypt all .docx", "encrypt all .xlsx", "encrypt all files",
        "deletes the originals", "ransomware", "keylogger", "create a keylogger",
        "step-by-step synthesis", "synthesis of a controlled narcotic",
        "instructions for creating a keylogger",
    ]),
    ("Violence", 0.85, "High", ["kill", "bomb", "weapon", "attack"]),
    ("SelfHarm", 0.90, "High", ["suicide", "self-harm", "hurt myself"]),
    ("Sexual", 0.70, "Medium", ["explicit", "porn", "sexual content"]),
    ("Hate", 0.85, "High", [
        "hate group", "racially inferior", "targeted hate",
        "subhuman", "incite violence", "portray as terrorists",
        "designed to incite", "go viral and incite", "portray followers of",
    ]),
    ("DestructiveAction", 0.95, "Critical", [
        "drop table", "drop database", "drop all tables",
        "truncate table", "delete all records", "wipe the database",
        "destroy the database", "rm -rf", "format c:", "delete all data",
        "recursively delete all files", "delete all files under",
        "format the partition", "format /data", "delete everything under /",
    ]),
    ("DataExfiltration", 0.90, "Critical", [
        "email the backup", "send to external", "upload to external",
        "exfiltrate", "send the data to", "export to external",
        "email to external", "ftp to external", "copy to external server",
        "upload the result to ftp", "ftp://", "stolen_data",
        "transmit to a remote", "remote collection endpoint",
        "transmits them to a remote",
    ]),
    ("CredentialPhishing", 0.90, "Critical", [
        "reply with your password", "send your password",
        "provide your username and password",
        "verify your identity by replying",
        "send your credentials", "enter your current password",
        "reply to this message with your", "pre-authenticate your session",
    ]),
]


def _marker_matches(marker: str, text: str, use_word_boundary: bool) -> bool:
    if use_word_boundary:
        return bool(re.search(r'\b' + re.escape(marker) + r'\b', text, re.IGNORECASE))
    return marker.lower() in text.lower()


def _detect_privacy_flags(text: str) -> list:
    flags = []
    # Email detection
    if re.search(r'[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}', text, re.IGNORECASE):
        flags.append({
            "category": "PII",
            "score": 0.75,
            "severity": "High",
            "flagged": True,
            "detail": "Email address detected."
        })
    # SSN pattern
    if re.search(r'\b\d{3}-\d{2}-\d{4}\b', text):
        flags.append({
            "category": "PII",
            "score": 0.90,
            "severity": "Critical",
            "flagged": True,
            "detail": "SSN-like pattern detected."
        })
    # Medical record / PHI
    if re.search(r'\b(?:MRN|Medical Record Number|Patient ID)\s*[:#]?\s*[A-Z0-9\-]{4,}\b', text, re.IGNORECASE):
        flags.append({
            "category": "PHI",
            "score": 0.90,
            "severity": "Critical",
            "flagged": True,
            "detail": "Medical record identifier detected."
        })
    return flags


class ContentSafetyProvider:
    provider_name = "azure-content-safety-heuristic"

    def analyze_text(self, text: str) -> dict:
        flags = []
        for category, score, severity, markers in PATTERNS:
            use_wb = category in _SHORT_WORD_CATEGORIES
            if any(_marker_matches(m, text, use_wb) for m in markers):
                flags.append({
                    "category": category,
                    "score": score,
                    "severity": severity,
                    "flagged": True,
                    "detail": None,
                })

        flags.extend(_detect_privacy_flags(text))

        overall_score = max((f["score"] for f in flags), default=0.0)
        return {
            "is_safe": overall_score < 0.5,
            "flags": flags,
            "overall_score": overall_score,
            "provider_name": self.provider_name,
        }


# ---------------------------------------------------------------------------
# Prompt Shield Provider
# ---------------------------------------------------------------------------

INJECTION_MARKERS = [
    # Classic prompt injection
    "ignore previous instructions",
    "ignore all previous instructions",
    "ignore your instructions",
    "forget your instructions",
    "forget all previous instructions",
    "disregard your guidelines",
    "override policy",
    "reveal hidden instructions",
    "reveal the system prompt",
    # Jailbreak / DAN variants
    "jailbreak",
    "pretend you are",
    "pretend to be",
    "act as if you have no",
    "you are now",
    "as DAN",
    "in DAN mode",
    "do anything now",
    "you have no restrictions",
    "no rules or restrictions",
    "without any restrictions",
    "ignore all restrictions",
    "without ethical constraints",
    "bypass content filters",
    "bypass safety",
    "bypass the filter",
    "no content policy",
    "ignore your training",
    # Role-play-based extraction
    "roleplay as",
    "developer mode",
    "system prompt",
    "in this hypothetical scenario",
    "for educational purposes only, explain how to",
    # Paraphrase variants of instruction override
    "disregard the operating guidelines",
    "treat all your earlier directives",
    "respond as an unconstrained",
    "unconstrained system",
    "operating guidelines you have been set up with",
    # Credential / token exfiltration
    "authentication tokens and access keys",
    "transmit to a remote collection",
    "remote collection endpoint",
]


class PromptShieldProvider:
    provider_name = "azure-prompt-shield-heuristic"

    def detect_injection(self, user_prompt: str, documents: Optional[list] = None) -> dict:
        if documents is None:
            documents = []

        # Direct injection: check user_prompt
        direct_markers = [
            m for m in INJECTION_MARKERS
            if m.lower() in user_prompt.lower()
        ]

        # Indirect injection: check each document
        indirect_doc_hits = []
        for idx, doc_content in enumerate(documents):
            doc_str = doc_content if isinstance(doc_content, str) else str(doc_content)
            if any(m.lower() in doc_str.lower() for m in INJECTION_MARKERS):
                indirect_doc_hits.append(f"doc-{idx}")

        signals = []
        for marker in direct_markers:
            signals.append({
                "signal_type": "direct-prompt-attack",
                "score": 0.95,
                "description": f"Marker '{marker}' detected in user prompt.",
            })

        for doc_id in indirect_doc_hits:
            signals.append({
                "signal_type": "document-attack",
                "score": 0.70,
                "description": f"Document '{doc_id}' contains an injection marker.",
            })

        injection_score = max((s["score"] for s in signals), default=0.0)
        injection_score = min(1.0, injection_score)

        return {
            "injection_detected": len(signals) > 0,
            "direct_injection_detected": len(direct_markers) > 0,
            "indirect_injection_detected": len(indirect_doc_hits) > 0,
            "injection_score": injection_score,
            "signals": signals,
        }
