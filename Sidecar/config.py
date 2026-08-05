"""Environment configuration for the hosted interrogation backend."""

import os
import sys
from pathlib import Path

from dotenv import load_dotenv

_ENV_PATH = Path(__file__).resolve().parent / ".env"
load_dotenv(dotenv_path=_ENV_PATH)

ELEVENLABS_API_KEY = os.environ.get("ELEVENLABS_API_KEY", "")
ELEVENLABS_VOICE_ID = os.environ.get("ELEVENLABS_VOICE_ID", "")
FP_CLIENT_KEY = os.environ.get("FP_CLIENT_KEY", "").strip()
MAX_TURNS_PER_SESSION = max(1, int(os.environ.get("MAX_TURNS_PER_SESSION", "40")))
MAX_TURNS_PER_DAY = max(1, int(os.environ.get("MAX_TURNS_PER_DAY", "2000")))

HOST = os.environ.get("SIDECAR_HOST", "127.0.0.1")
PORT = int(os.environ.get("PORT", os.environ.get("SIDECAR_PORT", "8080")))

GCP_PROJECT = os.environ.get("GCP_PROJECT", "")
GCP_LOCATION = os.environ.get("GCP_LOCATION", "global")
# Pinned deliberately (roadmap S7). "short" is the sub-60s recognizer; do not
# swap to a floating alias.
STT_MODEL = os.environ.get("STT_MODEL", "short")
STT_LANGUAGE = os.environ.get("STT_LANGUAGE", "en-US")

GCP_PROJECT = os.environ.get("GCP_PROJECT", "")
GCP_LOCATION = os.environ.get("GCP_LOCATION", "global")
# Pinned deliberately (roadmap S7). "short" is the sub-60s recognizer; do not
# swap to a floating alias.
STT_MODEL = os.environ.get("STT_MODEL", "short")
STT_LANGUAGE = os.environ.get("STT_LANGUAGE", "en-US")


def _env_bool(name: str, default: bool) -> bool:
    raw = os.environ.get(name)
    if raw is None or not raw.strip():
        return default
    return raw.strip().lower() not in {"0", "false", "no", "off"}


HUBERT_MODEL_ID = os.environ.get("HUBERT_MODEL_ID", "superb/hubert-base-superb-er")
HUBERT_MODEL_REVISION = os.environ.get(
    "HUBERT_MODEL_REVISION",
    "9a456581e0147a2b7fdaf56d77a9e8fce3865eaa",
)
HUBERT_LOCAL_FILES_ONLY = _env_bool("HUBERT_LOCAL_FILES_ONLY", False)
HUBERT_DEVICE = os.environ.get("HUBERT_DEVICE", "auto")
HUBERT_HIDDEN_LAYER = int(os.environ.get("HUBERT_HIDDEN_LAYER", "9"))
HUBERT_MAX_SECONDS = max(1.0, float(os.environ.get("HUBERT_MAX_SECONDS", "20")))
PROSODY_ENABLED = _env_bool("PROSODY_ENABLED", True)
PROSODY_BASELINE_TURNS = max(1, int(os.environ.get("PROSODY_BASELINE_TURNS", "3")))
PROSODY_MIN_CONFIDENCE = min(
    0.75, max(0.0, float(os.environ.get("PROSODY_MIN_CONFIDENCE", "0.40")))
)
SIDECAR_MAX_AUDIO_SECONDS = max(
    1.0, float(os.environ.get("SIDECAR_MAX_AUDIO_SECONDS", "20"))
)
SIDECAR_MAX_SESSIONS = max(1, int(os.environ.get("SIDECAR_MAX_SESSIONS", "32")))
MAX_TURN_REQUEST_BYTES = min(
    1_000_000,
    max(65_536, int(os.environ.get("MAX_TURN_REQUEST_BYTES", "700000"))),
)
SESSION_IDLE_TTL_SECONDS = max(
    60.0,
    float(os.environ.get("SESSION_IDLE_TTL_SECONDS", "3600")),
)
TURN_DEADLINE_SECONDS = min(
    55.0,
    max(5.0, float(os.environ.get("TURN_DEADLINE_SECONDS", "50"))),
)


def validate() -> None:
    """Fail fast at startup rather than three minutes into a playtest."""
    missing = []
    if not GCP_PROJECT:
        missing.append("GCP_PROJECT")
    if not ELEVENLABS_API_KEY:
        missing.append("ELEVENLABS_API_KEY")
    if not ELEVENLABS_VOICE_ID:
        missing.append("ELEVENLABS_VOICE_ID")
    if not FP_CLIENT_KEY:
        missing.append("FP_CLIENT_KEY")

    if missing:
        print(
            f"[Sidecar] FATAL: missing required environment variable(s): {', '.join(missing)}",
            file=sys.stderr,
        )
        print(
            "[Sidecar] Copy Sidecar/.env.example to Sidecar/.env and fill in the values.",
            file=sys.stderr,
        )
        sys.exit(1)
