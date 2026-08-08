"""Hosted STT -> affect -> LLM -> TTS backend for interrogation turns.

Cloud Run must remain pinned to one instance because session history, affect
baselines, and turn caps live in this process. Public requests are protected by
the shared client key in auth.py and the paid-turn budget in limits.py.
"""

import argparse
import asyncio
import base64
from dataclasses import replace
from functools import partial
import os
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager, suppress
from typing import Optional

import uvicorn
from fastapi import FastAPI, File, Form, UploadFile
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException
from starlette.middleware.gzip import GZipMiddleware

import audio_utils
import auth
import config
import fabrication
import features_classical
import limits
import llm
import prosody
import ser
import session_store
import stt
import tts

config.validate()

_ser_pool = ThreadPoolExecutor(max_workers=1)
_vendor_pool = ThreadPoolExecutor(max_workers=2)
# The §7 judge gets its own worker rather than sharing _vendor_pool. A judge
# stuck on the 20s LLM HTTP timeout then cannot starve the next turn's reply or
# TTS of a worker, and one worker is self-limiting in the right direction: a
# still-running judge makes the next turn's judge queue, that turn's grace
# window expires, and it simply reports nothing.
_judge_pool = ThreadPoolExecutor(max_workers=1)

# Conversation history per session, keyed by the GUID Unity mints at scene
# start. See session_store.py for why in-memory is correct here.
_session_store = session_store.InMemorySessionStore(
    config.SIDECAR_MAX_SESSIONS,
    config.SESSION_IDLE_TTL_SECONDS,
)
_prosody_registry = prosody.ProsodyRegistry(
    max_sessions=config.SIDECAR_MAX_SESSIONS,
    reference_turns=config.PROSODY_BASELINE_TURNS,
    minimum_confidence=config.PROSODY_MIN_CONFIDENCE,
)
_turn_limiter = limits.TurnLimiter(
    config.MAX_TURNS_PER_SESSION,
    config.MAX_TURNS_PER_DAY,
)
_models_loaded = False
_prosody_model_available = False
_prosody_load_error = "loading" if config.PROSODY_ENABLED else "disabled"
_session_reset_generations: dict[str, int] = {}
_active_session_turns: dict[str, int] = {}
# The current phase briefing per session (see the /turn handler's
# scene_instruction handling below). Deliberately NOT part of `history` —
# it is re-applied fresh to every turn rather than stored as a history
# entry, so it can never desynchronize the strict user/model alternation
# `history` must maintain.
_scene_instructions: dict[str, str] = {}

_BUDGET_ENDINGS = {
    "session_turn_limit_reached": "We're done for tonight. The station will follow up.",
    "daily_turn_budget_exhausted": "We're done for tonight. The station will follow up.",
}


class ClientInputError(ValueError):
    """A validated request problem that is safe to return to the local client."""


async def _reap_idle_sessions() -> None:
    interval_seconds = min(60.0, max(1.0, config.SESSION_IDLE_TTL_SECONDS / 2.0))
    while True:
        await asyncio.sleep(interval_seconds)
        _expire_idle_sessions()


@asynccontextmanager
async def lifespan(_app: FastAPI):
    global _models_loaded, _prosody_model_available, _prosody_load_error
    print("[Sidecar] Loading affect model...")
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
    reaper_task = asyncio.create_task(_reap_idle_sessions())
    try:
        yield
    finally:
        reaper_task.cancel()
        with suppress(asyncio.CancelledError):
            await reaper_task


app = FastAPI(
    title="Interrogation Sidecar",
    lifespan=lifespan,
    docs_url=None,
    redoc_url=None,
    openapi_url=None,
)

# The reply body is base64 PCM, and base64 spends 8 bits per 6 bits of payload —
# so roughly a quarter of what we ship is encoding overhead that gzip gets back
# almost for free, plus whatever the PCM itself compresses. Measured 8 Aug the
# download was 216KB and 565ms of a 3.4s turn. Inert for clients that don't send
# Accept-Encoding: gzip, so it can never break an older build. minimum_size
# keeps it off the small JSON error bodies, where framing would cost more than
# it saves.
app.add_middleware(GZipMiddleware, minimum_size=1024)


@app.middleware("http")
async def require_client_key(request, call_next):
    if request.url.path == "/health":
        return await call_next(request)
    provided_key = request.headers.get(auth.CLIENT_KEY_HEADER)
    if not auth.is_authorized(provided_key, config.FP_CLIENT_KEY):
        result = _empty_response()
        result["error"] = "unauthorized"
        return JSONResponse(status_code=401, content=result)
    if request.url.path == "/turn":
        if not await _cache_bounded_request_body(request):
            result = _empty_response()
            result["error"] = "request too large"
            return JSONResponse(status_code=413, content=result)
    return await call_next(request)


async def _cache_bounded_request_body(request) -> bool:
    """Read at most one turn body, then replay it to FastAPI's parser.

    Checking ``request.body()`` after the fact still lets an attacker make the
    process allocate an arbitrarily large body. Content-Length gives us a free
    early rejection for normal requests; counting stream chunks covers clients
    that use chunked transfer encoding or lie about the declared size.
    """
    declared_length = request.headers.get("content-length")
    if declared_length is not None:
        try:
            if int(declared_length) > config.MAX_TURN_REQUEST_BYTES:
                return False
        except (TypeError, ValueError):
            # Let the streaming counter and the multipart parser handle a
            # malformed declaration without trusting it for allocation.
            pass

    chunks: list[bytes] = []
    total_bytes = 0
    async for chunk in request.stream():
        if len(chunk) > config.MAX_TURN_REQUEST_BYTES - total_bytes:
            return False
        chunks.append(chunk)
        total_bytes += len(chunk)

    # Starlette's middleware request replays this cached body to the route's
    # multipart parser when call_next() runs.
    request._body = b"".join(chunks)
    return True

def _empty_response() -> dict:
    return {
        "ok": False, "error": "",
        "session_ended": False,
        "transcript": "", "emotion": "", "emotion_confidence": 0.0,
        "reply_text": "", "audio_b64": "",
        "audio_sample_rate": 0, "audio_channels": 0,
        "stt_ms": 0, "ser_ms": 0, "llm_ms": 0, "tts_ms": 0, "total_ms": 0,
        "prosody": prosody.ProsodySignal().to_dict(),
        # Trap ids from docs/STORY_SCRIPT.md §7 — details this witness stated as
        # memory but was not in a position to observe. Empty is the common case
        # and is indistinguishable, by design, from the judge not having run.
        "fabrications": [],
    }


def _invalid_request_response(path: str, status_code: int) -> JSONResponse:
    if path == "/turn":
        result = _empty_response()
        result["error"] = "invalid request"
    else:
        result = {"ok": False, "error": "invalid request"}
    return JSONResponse(status_code=status_code, content=result)


@app.exception_handler(RequestValidationError)
async def validation_error(request, _exc):
    return _invalid_request_response(request.url.path, 422)


@app.exception_handler(StarletteHTTPException)
async def http_error(request, exc):
    if request.url.path in {"/turn", "/session/reset"} and exc.status_code in {400, 422}:
        return _invalid_request_response(request.url.path, exc.status_code)
    return JSONResponse(status_code=exc.status_code, content={"detail": exc.detail})


def _validate_session_id(session_id: str) -> str:
    normalized = (session_id or "").strip()
    if not normalized or len(normalized) > 128 or any(ord(char) < 32 for char in normalized):
        raise ClientInputError("session_id must contain 1-128 printable characters")
    return normalized


def _validate_scene_instruction(scene_instruction: str) -> str:
    """Empty is the common case (most turns carry no phase change). Non-empty
    is trusted, unescaped context (see llm._to_contents / HISTORY_KIND_SCENE)
    written only by the game client (docs/GAME_COMPLETION_PLAN.md A7) — so it
    is validated at the boundary rather than escaped like witness speech."""
    normalized = (scene_instruction or "").strip()
    if not normalized:
        return ""
    if len(normalized) > config.SIDECAR_MAX_SCENE_INSTRUCTION_CHARS:
        raise ClientInputError(
            f"scene_instruction exceeds {config.SIDECAR_MAX_SCENE_INSTRUCTION_CHARS} characters"
        )
    if any(ord(char) < 32 and char not in "\n\t" for char in normalized):
        raise ClientInputError("scene_instruction must not contain control characters")
    if llm.contains_reserved_marker(normalized):
        raise ClientInputError("scene_instruction must not contain a reserved context marker")
    return normalized


def _history_for(session_id: str) -> list[dict]:
    return _session_store.history(session_id)


def _last_officer_question(history: list[dict]) -> str:
    """The officer's most recent line, read before this turn is committed.

    The §7 judge needs it because the traps are baited as questions: after "Did
    you hear glass?", a witness answering "Yes" has claimed something they could
    not have observed, and the word "yes" alone does not say so.
    """
    for message in reversed(history):
        if message.get("role") == "assistant":
            return (message.get("content") or "").strip()
    return ""


def _drain(task) -> None:
    """Retrieve a late task's exception so it is never reported as unretrieved."""
    task.add_done_callback(
        lambda finished: None if finished.cancelled() else finished.exception()
    )


async def _collect_fabrications(task, deadline: float) -> list[str]:
    """Take the judge's ids if they are ready, and wait only briefly if not.

    The judge was started alongside the officer's reply and has had the whole
    reply plus text-to-speech to finish in, so this is the tail case. A turn
    must never be slowed, and must certainly never time out, over a scoring
    signal: anything not ready in the grace window is dropped and the turn
    reports nothing caught.
    """
    if task is None:
        return []

    loop = asyncio.get_running_loop()
    grace = min(
        config.TRAP_JUDGE_GRACE_SECONDS,
        max(0.0, deadline - loop.time()),
    )
    done, _ = await asyncio.wait({task}, timeout=grace)
    if task not in done:
        print("[Sidecar] unsupported-detail judge was not ready in time; none recorded.")
        _drain(task)
        return []

    try:
        trap_ids, judge_ms = task.result()
    except Exception as e:
        print(f"[Sidecar] unsupported-detail judge failed: {e}")
        return []

    if trap_ids:
        print(f"[Sidecar] unsupported details: {', '.join(trap_ids)} ({judge_ms}ms)")
    return trap_ids


def _begin_session_turn(session_id: str) -> int:
    _active_session_turns[session_id] = _active_session_turns.get(session_id, 0) + 1
    return _session_reset_generations.get(session_id, 0)


def _finish_session_turn(session_id: str) -> None:
    remaining = _active_session_turns.get(session_id, 1) - 1
    if remaining > 0:
        _active_session_turns[session_id] = remaining
        return
    _active_session_turns.pop(session_id, None)
    if not _session_store.history(session_id):
        _session_reset_generations.pop(session_id, None)


def _forget_reset_generation_if_inactive(session_id: str) -> None:
    if not _active_session_turns.get(session_id):
        _session_reset_generations.pop(session_id, None)


def _expire_idle_sessions() -> None:
    for expired_id in _session_store.expire_idle():
        _prosody_registry.reset(expired_id)
        _turn_limiter.forget(expired_id)
        _forget_reset_generation_if_inactive(expired_id)
        _scene_instructions.pop(expired_id, None)


async def _await_before_deadline(awaitable, deadline: float):
    remaining = deadline - asyncio.get_running_loop().time()
    if remaining <= 0:
        close = getattr(awaitable, "close", None)
        if close is not None:
            close()
        raise TimeoutError
    return await asyncio.wait_for(awaitable, timeout=remaining)


def _commit_history(
    session_id: str,
    history: list[dict],
    user_text: str,
    reply_text: str,
    is_scene_kind: bool,
) -> None:
    """`is_scene_kind` is true whenever `user_text` is a stage direction
    rather than real witness speech — the true session opener AND an
    audio-less phase-transition turn both qualify, so that on replay
    `_to_contents` never re-wraps either of them as quoted witness speech.
    """
    history_kind = llm.HISTORY_KIND_SCENE if is_scene_kind else llm.HISTORY_KIND_WITNESS
    history.append({"role": "user", "content": user_text, "kind": history_kind})
    history.append({"role": "assistant", "content": reply_text})
    for evicted_id in _session_store.commit(session_id, history):
        _prosody_registry.reset(evicted_id)
        _turn_limiter.forget(evicted_id)
        _forget_reset_generation_if_inactive(evicted_id)
        _scene_instructions.pop(evicted_id, None)


def _analyze_affect(audio_f32: object):
    try:
        features = features_classical.extract(audio_f32, 16000)
    except Exception as exc:
        print(
            f"[Sidecar] Classical audio features unavailable ({type(exc).__name__}); "
            "continuing turn."
        )
        features = _flag_hubert_window(
            _unavailable_features(audio_f32, "feature_extraction_failed")
        )
        return (
            features,
            None,
            "feature_extraction_failed",
        )
    features = _flag_hubert_window(features)
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


def _flag_hubert_window(features):
    """Mark turns where the emotion label saw only the head of the utterance.

    The two analysers deliberately read different windows. The classical DSP is
    numpy and costs ~37ms on a full 20s buffer, so it gets everything and its
    duration, pauses and speech ratio describe the whole answer. HuBERT is a
    transformer whose cost grows with input length — at 9.6s it was the single
    slowest stage of the turn, above even STT — so ser.py bounds it to
    HUBERT_MAX_SECONDS on its own. This flag records that asymmetry for the
    debug overlay; it does not suppress speech rate, which is derived from the
    transcript and the classical features and is therefore still whole-utterance.
    """
    if features.duration_seconds <= config.HUBERT_MAX_SECONDS + 1e-4:
        return features
    return replace(
        features,
        flags=tuple(dict.fromkeys((*features.flags, "affect_window_truncated"))),
    )


@app.get("/health")
def health():
    prosody_error = _prosody_load_error
    if config.PROSODY_ENABLED and not _models_loaded and not _prosody_model_available:
        prosody_error = "loading"
    return {
        "status": "ok" if _models_loaded else "loading",
        "models_loaded": _models_loaded,
        "version": "0.3.0",
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
    try:
        session_id = _validate_session_id(session_id)
    except ClientInputError as exc:
        return JSONResponse(
            status_code=400,
            content={"ok": False, "error": str(exc)},
        )
    _expire_idle_sessions()
    if _active_session_turns.get(session_id):
        _session_reset_generations[session_id] = (
            _session_reset_generations.get(session_id, 0) + 1
        )
    else:
        _session_reset_generations.pop(session_id, None)
    _session_store.reset(session_id)
    _prosody_registry.reset(session_id)
    _scene_instructions.pop(session_id, None)
    return {"ok": True}


@app.post("/turn")
async def turn(
    session_id: str = Form(...),
    sample_rate: int = Form(16000),
    onset_delay_ms: int = Form(0),
    audio: Optional[UploadFile] = File(None),
    # Appended last, after `audio`, so every existing positional call site
    # (tests included) that predates this field keeps working unchanged.
    scene_instruction: str = Form(""),
):
    t_total0 = time.perf_counter()
    loop = asyncio.get_running_loop()
    deadline = loop.time() + config.TURN_DEADLINE_SECONDS
    result = _empty_response()
    prosody_tracker = None
    pending_prosody_update = None
    tracked_session_id = None
    turn_reset_generation = 0
    judge_task = None
    try:
        session_id = _validate_session_id(session_id)
        scene_instruction = _validate_scene_instruction(scene_instruction)
        _expire_idle_sessions()
        if sample_rate < 8000 or sample_rate > 192000:
            raise ClientInputError("sample_rate must be between 8000 and 192000")
        onset_delay_ms = min(120000, max(0, int(onset_delay_ms)))

        raw_bytes = b""
        if audio is not None:
            maximum_bytes = int(config.SIDECAR_MAX_AUDIO_SECONDS * sample_rate * 2)
            raw_bytes = await _await_before_deadline(
                audio.read(maximum_bytes + 1),
                deadline,
            )
            if len(raw_bytes) > maximum_bytes:
                raise ClientInputError(
                    f"audio exceeds {config.SIDECAR_MAX_AUDIO_SECONDS:g} second limit"
                )
        has_audio = len(raw_bytes) > 0
        if has_audio and len(raw_bytes) % 2:
            raise ClientInputError("audio must be aligned little-endian PCM16")

        admitted, limit_reason = _turn_limiter.admit(session_id)
        if not admitted:
            result["error"] = limit_reason
            result["session_ended"] = True
            result["reply_text"] = _BUDGET_ENDINGS.get(
                limit_reason,
                "We're done for tonight. The station will follow up.",
            )
            return JSONResponse(status_code=429, content=result)

        tracked_session_id = session_id
        turn_reset_generation = _begin_session_turn(session_id)
        history = _history_for(session_id)
        # True ONLY for the very first turn of a brand-new session — an
        # audio-less turn later in the same session (a phase transition,
        # where the officer speaks first with no witness utterance to
        # react to) is NOT a session opening, and must not replay the
        # "you've just sat down" kickoff line every time a phase changes.
        is_session_opening = not has_audio and not history

        # The active phase briefing is NOT stored inside `history` — every
        # entry appended to `history` must be part of a strictly alternating
        # user/model sequence (Gemini's `contents` array requires this;
        # bolting an unpaired extra "user" entry in front of the turn's own
        # user content would permanently corrupt every future turn's replay
        # of this session). Instead it lives in its own tiny side-store,
        # keyed by session_id like `_active_session_turns`, and is folded
        # into `turn_texts` as an extra PART of the current (always
        # correctly paired) turn — see llm.generate_reply. A client sends it
        # once per phase; the server re-applies it on every subsequent turn
        # until the next phase sends a new one.
        if scene_instruction:
            _scene_instructions[session_id] = scene_instruction
        active_scene_instruction = _scene_instructions.get(session_id, "")

        if not has_audio:
            transcript, emotion, emotion_conf = "", "", 0.0
            stt_ms = ser_ms = 0
            prosody_signal = prosody.ProsodySignal(
                reliability_reason="opening_turn", flags=["opening_turn"]
            )
        else:
            audio_f32 = audio_utils.pcm16_bytes_to_float32(raw_bytes)
            if sample_rate != 16000:
                audio_f32 = audio_utils.resample_float32(audio_f32, sample_rate, 16000)
            # Re-encode from the resampled buffer rather than reusing raw_bytes:
            # a client uploading at 48kHz would otherwise send 48kHz samples
            # labelled 16kHz, which Google accepts and transcribes as garbage.
            stt_bytes = audio_utils.float32_to_pcm16_bytes(audio_f32)
            # STT is now a network call and SER is CPU-bound, so they overlap
            # naturally — no thread pool needed on the STT side any more.
            stt_result, affect_result = await _await_before_deadline(
                asyncio.gather(
                    stt.transcribe(stt_bytes),
                    loop.run_in_executor(
                        _ser_pool,
                        _analyze_affect,
                        audio_f32,
                    ),
                    return_exceptions=True,
                ),
                deadline,
            )
            if isinstance(stt_result, BaseException):
                raise RuntimeError("speech transcription failed") from stt_result
            transcript, stt_ms = stt_result

            if isinstance(affect_result, BaseException):
                features = _flag_hubert_window(
                    _unavailable_features(audio_f32, "affect_pipeline_failed")
                )
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

        # Debug echo of the affect block llm.py is about to embed. Captured here,
        # pre-commit, so it reflects the signal the model actually saw rather than
        # the post-commit signal that lands in the response. Opening turns carry no
        # affect block at all, mirroring the is_opening branch in generate_reply.
        affect_prompt_context = (
            ("" if is_session_opening
             else prosody_signal.prompt_context(config.PROSODY_MIN_CONFIDENCE))
            if config.DEBUG_AFFECT_CONTEXT
            else None
        )

        # Started BEFORE the reply is awaited, so it overlaps the officer's own
        # call and text-to-speech and costs no wall-clock time (docs/STORY_SCRIPT
        # §7). It is collected after TTS rather than awaited here — see
        # _collect_fabrications — so that a slow judge cannot eat the turn
        # deadline. Skipped entirely when there is nothing to judge: an opening
        # turn, a phase transition, or a silent recording.
        if has_audio and transcript.strip() and active_scene_instruction:
            judge_task = asyncio.ensure_future(
                loop.run_in_executor(
                    _judge_pool,
                    partial(
                        fabrication.judge,
                        scene_instruction=active_scene_instruction,
                        officer_question=_last_officer_question(history),
                        transcript=transcript,
                    ),
                )
            )

        reply_text, llm_ms = await _await_before_deadline(
            loop.run_in_executor(
                _vendor_pool,
                partial(
                    llm.generate_reply,
                    history=history,
                    transcript=transcript,
                    emotion=emotion,
                    confidence=emotion_conf,
                    is_opening=is_session_opening,
                    has_audio=has_audio,
                    prosody_signal=prosody_signal,
                    scene_instruction=active_scene_instruction,
                ),
            ),
            deadline,
        )

        pcm, rate, channels, tts_ms = await _await_before_deadline(
            loop.run_in_executor(
                _vendor_pool,
                tts.synthesize,
                reply_text,
            ),
            deadline,
        )
        norm_pcm, norm_rate = audio_utils.normalize_to_canonical(
            pcm, rate, channels, config.TTS_SAMPLE_RATE
        )

        fabrications = await _collect_fabrications(judge_task, deadline)
        judge_task = None

        # Only successful, playable turns become part of the session reference.
        # A failed TTS attempt may be retried with the same audio and must not be
        # counted twice in the baseline or temporal trend.
        if turn_reset_generation == _session_reset_generations.get(session_id, 0):
            if prosody_tracker is not None and pending_prosody_update is not None:
                prosody_signal = prosody_tracker.update(**pending_prosody_update, commit=True)

            if is_session_opening:
                user_text = llm.OPENING_KICKOFF_TEXT
            elif not has_audio:
                user_text = llm.PHASE_CONTINUATION_TEXT
            else:
                user_text = transcript
            is_scene_kind = is_session_opening or not has_audio
            _commit_history(session_id, history, user_text, reply_text, is_scene_kind)
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
            "fabrications": fabrications,
        })
        if affect_prompt_context is not None:
            result["affect_prompt_context"] = affect_prompt_context
        return result

    except TimeoutError:
        result["error"] = "turn timed out; retry the utterance"
        return JSONResponse(status_code=504, content=result)
    except Exception as e:
        cause = e.__cause__
        cause_text = f"; cause={type(cause).__name__}: {cause}" if cause else ""
        print(f"[Sidecar] /turn failed ({type(e).__name__}): {e}{cause_text}")
        is_input_error = isinstance(e, ClientInputError)
        result["error"] = str(e) if is_input_error else "turn pipeline failed; retry the utterance"
        return JSONResponse(status_code=400 if is_input_error else 500, content=result)
    finally:
        # A turn that failed after starting the judge (a TTS error, a timeout)
        # leaves it in flight. Let it run to completion and drop the result
        # rather than cancelling a vendor call mid-request.
        if judge_task is not None:
            _drain(judge_task)
        if tracked_session_id is not None:
            _finish_session_turn(tracked_session_id)


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
