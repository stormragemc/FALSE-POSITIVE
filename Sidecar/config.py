"""Environment configuration for the hosted interrogation backend."""

import os
import sys
from pathlib import Path

from dotenv import load_dotenv

_ENV_PATH = Path(__file__).resolve().parent / ".env"
load_dotenv(dotenv_path=_ENV_PATH)

ELEVENLABS_API_KEY = os.environ.get("ELEVENLABS_API_KEY", "")
# Maksim — "Raw, unpolished, deep", Russian accent. Defaulted in source rather
# than left to .env so the officer sounds the same on every machine without
# anyone hand-copying an undocumented ID; a Voice Library ID is public, not a
# secret (the API key above still is). Casting rationale and the sharing-terms
# check are in docs/superpowers/specs/2026-08-07-spassky-voice-and-delivery-design.md.
ELEVENLABS_VOICE_ID = os.environ.get("ELEVENLABS_VOICE_ID", "6sXsAlJKKBf265ucBSRt")
# eleven_multilingual_v2 was cast by ear for Maksim's Russian accent (spec
# 2026-08-07) and stays the committed default. It is also the slowest option:
# measured 925ms against eleven_flash_v2_5's ~215ms on the same line. Left as an
# env var so the trade can be A/B'd on a live build — ELEVENLABS_MODEL_ID=
# eleven_flash_v2_5 — without a code change or a re-cast of the voice.
ELEVENLABS_MODEL_ID = os.environ.get("ELEVENLABS_MODEL_ID", "eleven_multilingual_v2")
# The rate ElevenLabs renders at and the rate Unity is handed, kept equal so a
# reply is never resampled on the way through. 24000 is the quality default:
# part of the officer's accent sits in sibilance above 8kHz, which 16000 cannot
# represent, and the voice was cast by ear. 16000 is the latency option — it
# drops the reply body by a third, ~185ms of download on a 216KB line — and is
# the right trade on a thin connection. Values outside what ElevenLabs renders
# natively fall back to the default rather than forcing a resample.
TTS_SAMPLE_RATE = int(os.environ.get("TTS_SAMPLE_RATE", "24000"))
if TTS_SAMPLE_RATE not in {16000, 22050, 24000, 44100}:
    TTS_SAMPLE_RATE = 24000
FP_CLIENT_KEY = os.environ.get("FP_CLIENT_KEY", "").strip()
MAX_TURNS_PER_SESSION = max(1, int(os.environ.get("MAX_TURNS_PER_SESSION", "40")))
MAX_TURNS_PER_DAY = max(1, int(os.environ.get("MAX_TURNS_PER_DAY", "2000")))
SIDECAR_STT_TIMEOUT_SECONDS = max(
    1.0, float(os.environ.get("SIDECAR_STT_TIMEOUT_SECONDS", "20"))
)
SIDECAR_LLM_TIMEOUT_SECONDS = max(
    1.0, float(os.environ.get("SIDECAR_LLM_TIMEOUT_SECONDS", "20"))
)

HOST = os.environ.get("SIDECAR_HOST", "127.0.0.1")
PORT = int(os.environ.get("PORT", os.environ.get("SIDECAR_PORT", "8080")))

GCP_PROJECT = os.environ.get("GCP_PROJECT", "")
GCP_LOCATION = os.environ.get("GCP_LOCATION", "global")
# Pinned deliberately (roadmap S7); do not swap to a floating alias. "long" over
# "short" because "short" cleans disfluencies for readability, and this pipeline
# needs them: the four IEMOCAP classes have no nervous bucket, so a verbal "uh"
# reaches the interrogator only through the transcript. It is also voiced, so the
# pacing gates in features_classical.py cannot see it either. Measured 6 Aug,
# "short" dropped the filler from "Um, I was at the store" and returned an empty
# transcript for a halting 8.5s clip on three consecutive attempts; "long" kept
# the fillers, at the same latency.
STT_MODEL = os.environ.get("STT_MODEL", "long")
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
# Bounds HuBERT only — the classical features read the whole utterance
# (app.py:_flag_hubert_window). Lowered from 20, which matched
# SIDECAR_MAX_AUDIO_SECONDS and therefore never truncated anything: measured
# 8 Aug on a 9.6s answer, HuBERT took 1934ms against STT's 1336ms and was the
# slowest stage of the entire turn, and its cost grows with input length, so a
# rambling 20s answer paid ~4s. Emotion is a whole-utterance impression that the
# first 8s carries; latency is not.
HUBERT_MAX_SECONDS = max(1.0, float(os.environ.get("HUBERT_MAX_SECONDS", "8")))
PROSODY_ENABLED = _env_bool("PROSODY_ENABLED", True)
PROSODY_BASELINE_TURNS = max(1, int(os.environ.get("PROSODY_BASELINE_TURNS", "3")))
PROSODY_MIN_CONFIDENCE = min(
    0.75, max(0.0, float(os.environ.get("PROSODY_MIN_CONFIDENCE", "0.40")))
)
# Echoes the affect block llm.py embeds back in the /turn response, for the
# test bench. Off by default and deliberately not part of the Unity DTO: this is
# prompt text, and the client key is documented as a speed bump rather than a
# security boundary, so it should not ride the production wire format.
DEBUG_AFFECT_CONTEXT = _env_bool("SIDECAR_DEBUG_AFFECT_CONTEXT", False)
SIDECAR_MAX_AUDIO_SECONDS = max(
    1.0, float(os.environ.get("SIDECAR_MAX_AUDIO_SECONDS", "20"))
)
SIDECAR_MAX_SESSIONS = max(1, int(os.environ.get("SIDECAR_MAX_SESSIONS", "32")))
# The client sends one of these per interrogation phase (see docs/GAME_COMPLETION_PLAN.md
# A7) — generous enough for a phase prompt plus the witness-knowledge briefing, small
# enough that it can't be used to smuggle a large payload into history.
#
# Raised from 6000 for the §7 trap baits. Measured before that change, P2's
# instruction was already 4860 of 6000 worst case (case_file.txt 1864 +
# phase_p2_recall.txt 1690 + the 16-row knowledge block 1300), and the six bait
# lines plus their follow-ups do not fit in the remaining 1140.
SIDECAR_MAX_SCENE_INSTRUCTION_CHARS = max(
    256, int(os.environ.get("SIDECAR_MAX_SCENE_INSTRUCTION_CHARS", "9000"))
)
# docs/STORY_SCRIPT.md §7. Off makes every turn report no unsupported details,
# which the client already treats as a normal playthrough — so this is a kill
# switch that needs no code change if the judge ever misbehaves in a demo.
TRAP_JUDGE_ENABLED = _env_bool("TRAP_JUDGE_ENABLED", True)
# How long the turn will wait for the judge AFTER text-to-speech has returned.
# The judge starts alongside the officer's reply and normally finishes long
# before TTS does, so this is the tail case only. It is deliberately short: a
# turn must never become slow, or fail, because a scoring signal was late.
TRAP_JUDGE_GRACE_SECONDS = max(
    0.0, float(os.environ.get("TRAP_JUDGE_GRACE_SECONDS", "1.5"))
)
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
