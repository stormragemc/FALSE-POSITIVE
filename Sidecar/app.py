"""Interrogation sidecar — the whole STT -> emotion -> LLM -> TTS pipeline
behind one HTTP endpoint per conversational turn.

Run manually with `run_sidecar.bat` (Windows) or `python app.py`, or let
Unity's SidecarProcessLauncher start it automatically. Binds to 127.0.0.1
only — this process holds two paid API keys and must not be LAN-reachable.
"""

import argparse
import asyncio
import base64
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
import config
import llm
import ser
import stt
import tts

config.validate()

_stt_pool = ThreadPoolExecutor(max_workers=1)
_ser_pool = ThreadPoolExecutor(max_workers=1)

# In-memory conversation history per session. Single-player, single machine —
# no persistence needed for v1. Keyed by the GUID Unity mints at scene start.
_sessions: dict[str, list[dict]] = {}
_last_turn_debug: dict = {}
_models_loaded = False


@asynccontextmanager
async def lifespan(_app: FastAPI):
    global _models_loaded
    print("[Sidecar] Loading models (first run downloads them — this can take a while)...")
    stt.load()
    ser.load()
    _models_loaded = True
    print("[Sidecar] Models loaded. Ready.")
    yield


app = FastAPI(title="Interrogation Sidecar", lifespan=lifespan)


def _empty_response() -> dict:
    return {
        "ok": False, "error": "",
        "transcript": "", "emotion": "", "emotion_confidence": 0.0,
        "reply_text": "", "audio_b64": "",
        "audio_sample_rate": 0, "audio_channels": 0,
        "stt_ms": 0, "ser_ms": 0, "llm_ms": 0, "tts_ms": 0, "total_ms": 0,
    }


@app.get("/health")
def health():
    return {"status": "ok" if _models_loaded else "loading", "models_loaded": _models_loaded, "version": "0.1.0"}


@app.post("/session/reset")
def session_reset(session_id: str = Form(...)):
    _sessions.pop(session_id, None)
    return {"ok": True}


@app.get("/debug/last_turn")
def debug_last_turn():
    return _last_turn_debug or {"ok": False, "error": "no turns yet"}


@app.post("/turn")
async def turn(
    session_id: str = Form(...),
    sample_rate: int = Form(16000),
    audio: Optional[UploadFile] = File(None),
):
    t_total0 = time.perf_counter()
    history = _sessions.setdefault(session_id, [])

    raw_bytes = b""
    if audio is not None:
        raw_bytes = await audio.read()
    is_opening = len(raw_bytes) == 0

    result = _empty_response()
    try:
        if is_opening:
            transcript, emotion, emotion_conf = "", "", 0.0
            stt_ms = ser_ms = 0
        else:
            audio_f32 = audio_utils.pcm16_bytes_to_float32(raw_bytes)
            if sample_rate != 16000:
                audio_f32 = audio_utils.resample_float32(audio_f32, sample_rate, 16000)

            loop = asyncio.get_running_loop()
            # STT and SER are independent reads of the same buffer — run them
            # concurrently rather than serially (plan section 0).
            (transcript, stt_ms), (emotion, emotion_conf, ser_ms) = await asyncio.gather(
                loop.run_in_executor(_stt_pool, stt.transcribe, audio_f32),
                loop.run_in_executor(_ser_pool, ser.classify, audio_f32),
            )

        reply_text, llm_ms = llm.generate_reply(
            history=history,
            transcript=transcript,
            emotion=emotion,
            confidence=emotion_conf,
            is_opening=is_opening,
        )

        pcm, rate, channels, tts_ms = tts.synthesize(reply_text)
        norm_pcm, norm_rate = audio_utils.normalize_to_canonical(pcm, rate, channels)

        user_text = transcript if not is_opening else llm.OPENING_KICKOFF_TEXT
        history.append({"role": "user", "content": user_text})
        history.append({"role": "assistant", "content": reply_text})

        total_ms = int((time.perf_counter() - t_total0) * 1000)

        result.update({
            "ok": True,
            "transcript": transcript,
            "emotion": emotion, "emotion_confidence": emotion_conf,
            "reply_text": reply_text,
            "audio_b64": base64.b64encode(norm_pcm).decode("ascii"),
            "audio_sample_rate": norm_rate, "audio_channels": 1,
            "stt_ms": stt_ms, "ser_ms": ser_ms, "llm_ms": llm_ms, "tts_ms": tts_ms,
            "total_ms": total_ms,
        })
        _last_turn_debug.clear()
        _last_turn_debug.update(result)
        return result

    except Exception as e:
        print(f"[Sidecar] /turn failed: {e}")
        result["error"] = str(e)
        _last_turn_debug.clear()
        _last_turn_debug.update(result)
        return JSONResponse(status_code=500, content=result)


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
