"""Environment configuration for the interrogation sidecar.

Both API keys live only here — this process's environment — and are never sent
to or stored by the Unity client. See Sidecar/.env.example for the template.
"""

import os
import sys
from pathlib import Path

from dotenv import load_dotenv

_ENV_PATH = Path(__file__).resolve().parent / ".env"
load_dotenv(dotenv_path=_ENV_PATH)

GEMINI_API_KEY = os.environ.get("GEMINI_API_KEY", "")
ELEVENLABS_API_KEY = os.environ.get("ELEVENLABS_API_KEY", "")
ELEVENLABS_VOICE_ID = os.environ.get("ELEVENLABS_VOICE_ID", "")

HOST = os.environ.get("SIDECAR_HOST", "127.0.0.1")
PORT = int(os.environ.get("SIDECAR_PORT", "8765"))


def _env_bool(name: str, default: bool) -> bool:
    raw = os.environ.get(name)
    if raw is None or not raw.strip():
        return default
    return raw.strip().lower() not in {"0", "false", "no", "off"}


HUBERT_MODEL_ID = os.environ.get("HUBERT_MODEL_ID", "superb/hubert-base-superb-er")
HUBERT_DEVICE = os.environ.get("HUBERT_DEVICE", "auto")
HUBERT_HIDDEN_LAYER = int(os.environ.get("HUBERT_HIDDEN_LAYER", "9"))
HUBERT_MAX_SECONDS = max(1.0, float(os.environ.get("HUBERT_MAX_SECONDS", "20")))
PROSODY_ENABLED = _env_bool("PROSODY_ENABLED", True)
PROSODY_BASELINE_TURNS = max(1, int(os.environ.get("PROSODY_BASELINE_TURNS", "3")))
PROSODY_MIN_CONFIDENCE = min(
    0.75, max(0.0, float(os.environ.get("PROSODY_MIN_CONFIDENCE", "0.40")))
)
SIDECAR_MAX_AUDIO_SECONDS = max(
    1.0, float(os.environ.get("SIDECAR_MAX_AUDIO_SECONDS", "30"))
)
SIDECAR_MAX_SESSIONS = max(1, int(os.environ.get("SIDECAR_MAX_SESSIONS", "32")))


def validate() -> None:
    """Fail fast at startup rather than three minutes into a playtest."""
    missing = []
    if not GEMINI_API_KEY:
        missing.append("GEMINI_API_KEY")
    if not ELEVENLABS_API_KEY:
        missing.append("ELEVENLABS_API_KEY")
    if not ELEVENLABS_VOICE_ID:
        missing.append("ELEVENLABS_VOICE_ID")

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
