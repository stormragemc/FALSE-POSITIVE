"""Interrogation sidecar — the whole STT -> emotion -> LLM -> TTS pipeline
behind one HTTP endpoint per conversational turn.

Run manually with `run_sidecar.bat` (Windows) or `python app.py`, or let
Unity's SidecarProcessLauncher start it automatically. Binds to 127.0.0.1
only — this process holds two paid API keys and must not be LAN-reachable.
"""

import argparse
import asyncio
import base64
from dataclasses import replace
import os
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager
from typing import Optional

import uvicorn
from fastapi import FastAPI, File, Form, UploadFile
from fastapi.responses import JSONResponse

import audio_utils
import auth
import config
import features_classical
import llm
import limits
import prosody
import ser
import session_store
import stt
import tts

config.validate()

_ser_pool = ThreadPoolExecutor(max_workers=1)

# Conversation history per session, keyed by the GUID Unity mints at scene
# start. See session_store.py for why in-memory is correct here.
_session_store = session_store.InMemorySessionStore(config.SIDECAR_MAX_SESSIONS)
_turn_limiter = limits.TurnLimiter(
    max_per_session=config.MAX_TURNS_PER_SESSION,
    max_per_day=config.MAX_TURNS_PER_DAY,
)
_prosody_registry = prosody.ProsodyRegistry(
    max_sessions=config.SIDECAR_MAX_SESSIONS,
    reference_turns=config.PROSODY_BASELINE_TURNS,
    minimum_confidence=config.PROSODY_MIN_CONFIDENCE,
)
_models_loaded = False
_prosody_model_available = False
_prosody_load_error = "loading" if config.PROSODY_ENABLED else "disabled"
_session_reset_epoch = 0

_BUDGET_ENDINGS = {
    "session_turn_limit_reached": "We're done for tonight. The station will follow up.",
    "daily_turn_budget_exhausted": "We're done for tonight. The station will follow up.",
}


class ClientInputError(ValueError):
    """A validated request problem that is safe to return to the local client."""


class TurnBudgetError(RuntimeError):
    """The turn was refused to protect the project budget, not because the
    request was malformed. Distinct from ClientInputError so it can answer 429."""


@asynccontextmanager
async def lifespan(_app: FastAPI):
    global _models_loaded, _prosody_model_available, _prosody_load_error
    print("[Sidecar] Loading affect model (first run downloads it)...")
    if config.PROSODY_ENABLED:
        try:
            ser.load()
            _prosody_model_available = True
            _prosody_load_error = ""
        except Exception as exc:
            _prosody_model_available = False
            _prosody_load_error = type(exc).__name__
            print(
                f"[Sidecar] Affect model unavailable ({type(exc).__name__}); "
                "continuing in transcript-only mode."
            )
    else:
        _prosody_model_available = False
        _prosody_load_error = "disabled"
    _models_loaded = True
    print("[Sidecar] Models loaded. Ready.")
    yield


app = FastAPI(title="Interrogation Sidecar", lifespan=lifespan)

# /health stays open so uptime checks and the client's pre-flight probe work
# without shipping the key to anything that merely wants liveness.
_OPEN_PATHS = frozenset({"/health"})


@app.middleware("http")
async def require_client_key(request, call_next):
    if request.url.path not in _OPEN_PATHS:
        supplied = request.headers.get(auth.CLIENT_KEY_HEADER)
        if not auth.is_authorized(supplied, config.FP_CLIENT_KEY):
            return JSONResponse(
                status_code=401,
                content={"ok": False, "error": "unauthorized"},
            )
    return await call_next(request)


def _empty_response() -> dict:
    return {
        "ok": False, "error": "",
        "session_ended": False,
        "transcript": "", "emotion": "", "emotion_confidence": 0.0,
        "reply_text": "", "audio_b64": "",
        "audio_sample_rate": 0, "audio_channels": 0,
        "stt_ms": 0, "ser_ms": 0, "llm_ms": 0, "tts_ms": 0, "total_ms": 0,
        "prosody": prosody.ProsodySignal().to_dict(),
    }


def _validate_session_id(session_id: str) -> str:
    normalized = (session_id or "").strip()
    if not normalized or len(normalized) > 128 or any(ord(char) < 32 for char in normalized):
        raise ClientInputError("session_id must contain 1-128 printable characters")
    return normalized


def _history_for(session_id: str) -> list[dict]:
    return _session_store.history(session_id)


def _commit_history(
    session_id: str,
    history: list[dict],
    user_text: str,
    reply_text: str,
    is_opening: bool,
) -> None:
    history_kind = llm.HISTORY_KIND_SCENE if is_opening else llm.HISTORY_KIND_WITNESS
    history.append({"role": "user", "content": user_text, "kind": history_kind})
    history.append({"role": "assistant", "content": reply_text})
    for evicted_id in _session_store.commit(session_id, history):
        _prosody_registry.reset(evicted_id)
        _turn_limiter.forget(evicted_id)


def _analyze_affect(audio_f32: object, full_duration_seconds: float | None = None):
    try:
        features = features_classical.extract(audio_f32, 16000)
    except Exception as exc:
        print(
            f"[Sidecar] Classical audio features unavailable ({type(exc).__name__}); "
            "continuing turn."
        )
        features = _with_full_duration(
            _unavailable_features(audio_f32, "feature_extraction_failed"),
            full_duration_seconds,
        )
        return (
            features,
            None,
            "feature_extraction_failed",
        )
    features = _with_full_duration(features, full_duration_seconds)
    if not config.PROSODY_ENABLED:
        return features, None, "prosody_disabled"
    if not _prosody_model_available:
        return features, None, "hubert_load_failed"
    try:
        return features, ser.analyze(audio_f32), ""
    except Exception as exc:
        print(f"[Sidecar] Affect inference unavailable ({type(exc).__name__}); continuing turn.")
        return features, None, "hubert_inference_failed"


def _unavailable_features(audio_f32: object, reason: str):
    try:
        duration_seconds = max(0.0, len(audio_f32) / 16000.0)
    except (TypeError, AttributeError):
        duration_seconds = 0.0
    return features_classical.ClassicalFeatures(
        duration_seconds=round(duration_seconds, 4),
        speech_ratio=0.0,
        long_pause_count=0,
        pitch_variability=0.0,
        energy_variability=0.0,
        clipping_ratio=0.0,
        rms=0.0,
        flags=(reason,),
    )


def _with_full_duration(features, full_duration_seconds: float | None):
    if full_duration_seconds is None or full_duration_seconds <= features.duration_seconds + 1e-4:
        return features
    flags = tuple(dict.fromkeys((*features.flags, "affect_window_truncated")))
    return replace(
        features,
        duration_seconds=round(max(0.0, full_duration_seconds), 4),
        flags=flags,
    )


@app.get("/health")
def health():
    prosody_error = _prosody_load_error
    if config.PROSODY_ENABLED and not _models_loaded and not _prosody_model_available:
        prosody_error = "loading"
    return {
        "status": "ok" if _models_loaded else "loading",
        "models_loaded": _models_loaded,
        "version": "0.2.0",
        "prosody": {
            "enabled": config.PROSODY_ENABLED,
            "available": _prosody_model_available,
            "model_id": config.HUBERT_MODEL_ID,
            "device": ser.device(),
            "orchestration_version": prosody.ORCHESTRATION_VERSION,
            "error": prosody_error,
        },
    }


@app.post("/session/reset")
async def session_reset(session_id: str = Form(...)):
    global _session_reset_epoch
    session_id = (session_id or "").strip()
    _session_reset_epoch += 1
    _session_store.reset(session_id)
    _turn_limiter.forget(session_id)
    _prosody_registry.reset(session_id)
    return {"ok": True}


@app.post("/turn")
async def turn(
    session_id: str = Form(...),
    sample_rate: int = Form(16000),
    onset_delay_ms: int = Form(0),
    audio: Optional[UploadFile] = File(None),
):
    t_total0 = time.perf_counter()
    result = _empty_response()
    prosody_tracker = None
    pending_prosody_update = None
    turn_reset_epoch = _session_reset_epoch
    try:
        session_id = _validate_session_id(session_id)
        budget_reason = _turn_limiter.admit(session_id, time.time())
        if budget_reason:
            raise TurnBudgetError(budget_reason)
        if sample_rate < 8000 or sample_rate > 192000:
            raise ClientInputError("sample_rate must be between 8000 and 192000")
        onset_delay_ms = min(120000, max(0, int(onset_delay_ms)))

        raw_bytes = b""
        if audio is not None:
            maximum_bytes = int(config.SIDECAR_MAX_AUDIO_SECONDS * sample_rate * 2)
            raw_bytes = await audio.read(maximum_bytes + 1)
            if len(raw_bytes) > maximum_bytes:
                raise ClientInputError(
                    f"audio exceeds {config.SIDECAR_MAX_AUDIO_SECONDS:g} second limit"
                )
        is_opening = len(raw_bytes) == 0
        if not is_opening and len(raw_bytes) % 2:
            raise ClientInputError("audio must be aligned little-endian PCM16")
        history = _history_for(session_id)

        if is_opening:
            transcript, emotion, emotion_conf = "", "", 0.0
            stt_ms = ser_ms = 0
            prosody_signal = prosody.ProsodySignal(
                reliability_reason="opening_turn", flags=["opening_turn"]
            )
        else:
            audio_f32 = audio_utils.pcm16_bytes_to_float32(raw_bytes)
            if sample_rate != 16000:
                audio_f32 = audio_utils.resample_float32(audio_f32, sample_rate, 16000)
            affect_maximum_samples = int(round(config.HUBERT_MAX_SECONDS * 16000))
            affect_audio_f32 = audio_f32[:affect_maximum_samples]
            full_duration_seconds = len(audio_f32) / 16000.0

            loop = asyncio.get_running_loop()
            # Re-encode from the resampled buffer rather than reusing raw_bytes:
            # a client uploading at 48kHz would otherwise send 48kHz samples
            # labelled 16kHz, which Google accepts and transcribes as garbage.
            stt_bytes = audio_utils.float32_to_pcm16_bytes(audio_f32)
            # STT is now a network call and SER is CPU-bound, so they overlap
            # naturally — no thread pool needed on the STT side any more.
            stt_result, affect_result = await asyncio.gather(
                stt.transcribe(stt_bytes),
                loop.run_in_executor(
                    _ser_pool,
                    _analyze_affect,
                    affect_audio_f32,
                    full_duration_seconds,
                ),
                return_exceptions=True,
            )
            if isinstance(stt_result, BaseException):
                raise RuntimeError("speech transcription failed") from stt_result
            transcript, stt_ms = stt_result

            if isinstance(affect_result, BaseException):
                features = _unavailable_features(
                    affect_audio_f32, "affect_pipeline_failed"
                )
                features = _with_full_duration(features, full_duration_seconds)
                observation = None
                unavailable_reason = "affect_pipeline_failed"
            else:
                features, observation, unavailable_reason = affect_result
            emotion = observation.label if observation is not None else ""
            emotion_conf = observation.confidence if observation is not None else 0.0
            ser_ms = observation.elapsed_ms if observation is not None else 0
            prosody_tracker = _prosody_registry.preview(session_id)
            pending_prosody_update = {
                "features": features,
                "observation": observation,
                "transcript": transcript,
                "onset_delay_ms": onset_delay_ms,
                "unavailable_reason": unavailable_reason,
            }
            prosody_signal = prosody_tracker.update(
                features=features,
                observation=observation,
                transcript=transcript,
                onset_delay_ms=onset_delay_ms,
                unavailable_reason=unavailable_reason,
                commit=False,
            )

        reply_text, llm_ms = llm.generate_reply(
            history=history,
            transcript=transcript,
            emotion=emotion,
            confidence=emotion_conf,
            is_opening=is_opening,
            prosody_signal=prosody_signal,
        )

        pcm, rate, channels, tts_ms = tts.synthesize(reply_text)
        norm_pcm, norm_rate = audio_utils.normalize_to_canonical(pcm, rate, channels)

        # Only successful, playable turns become part of the session reference.
        # A failed TTS attempt may be retried with the same audio and must not be
        # counted twice in the baseline or temporal trend.
        if turn_reset_epoch == _session_reset_epoch:
            if prosody_tracker is not None and pending_prosody_update is not None:
                prosody_signal = prosody_tracker.update(**pending_prosody_update, commit=True)

            user_text = transcript if not is_opening else llm.OPENING_KICKOFF_TEXT
            _commit_history(session_id, history, user_text, reply_text, is_opening)
            if prosody_tracker is not None:
                _prosody_registry.commit(session_id, prosody_tracker)
        else:
            prosody_signal = replace(
                prosody_signal,
                flags=[*prosody_signal.flags, "session_reset_during_turn"],
            )

        total_ms = int((time.perf_counter() - t_total0) * 1000)

        result.update({
            "ok": True,
            "transcript": transcript,
            "emotion": emotion, "emotion_confidence": emotion_conf,
            "prosody": prosody_signal.to_dict(),
            "reply_text": reply_text,
            "audio_b64": base64.b64encode(norm_pcm).decode("ascii"),
            "audio_sample_rate": norm_rate, "audio_channels": 1,
            "stt_ms": stt_ms, "ser_ms": ser_ms, "llm_ms": llm_ms, "tts_ms": tts_ms,
            "total_ms": total_ms,
        })
        return result

    except Exception as e:
        cause = e.__cause__
        cause_text = f"; cause={type(cause).__name__}: {cause}" if cause else ""
        print(f"[Sidecar] /turn failed ({type(e).__name__}): {e}{cause_text}")
        is_input_error = isinstance(e, ClientInputError)
        is_budget_error = isinstance(e, TurnBudgetError)
        if is_input_error or is_budget_error:
            result["error"] = str(e)
        else:
            result["error"] = "turn pipeline failed; retry the utterance"
        if is_budget_error:
            result["session_ended"] = True
            result["reply_text"] = _BUDGET_ENDINGS.get(
                str(e), "We're done for tonight. The station will follow up."
            )
        status_code = 429 if is_budget_error else (400 if is_input_error else 500)
        return JSONResponse(status_code=status_code, content=result)


def _watch_parent(pid: int) -> None:
    """Self-exit if the Unity process that launched us disappears, so an
    Editor crash doesn't orphan a process holding the port."""
    try:
        import psutil
    except ImportError:
        print("[Sidecar] psutil not installed; --parent-pid watchdog disabled.")
        return
    while True:
        if not psutil.pid_exists(pid):
            print(f"[Sidecar] Parent process {pid} is gone. Exiting.")
            os._exit(0)
        time.sleep(2)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default=config.HOST)
    parser.add_argument("--port", type=int, default=config.PORT)
    parser.add_argument("--parent-pid", type=int, default=None)
    args = parser.parse_args()

    if args.parent_pid:
        threading.Thread(target=_watch_parent, args=(args.parent_pid,), daemon=True).start()

    uvicorn.run(app, host=args.host, port=args.port)
