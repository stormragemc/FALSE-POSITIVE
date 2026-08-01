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
