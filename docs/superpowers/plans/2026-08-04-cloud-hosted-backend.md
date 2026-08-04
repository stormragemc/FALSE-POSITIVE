# Cloud-Hosted Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the FALSE POSITIVE Python sidecar off the player's machine onto a pinned Google Cloud Run service, so the Unity game can ship on itch.io as a plain binary that talks to a URL.

**Architecture:** The FastAPI app keeps its shape. STT swaps from local `faster-whisper` to Google Cloud Speech-to-Text v2; Gemini moves from an AI Studio key to Vertex AI so GCP credits pay for it; HuBERT and ElevenLabs stay in place. Session state stays in RAM, which is only correct because the service is pinned to exactly one instance (`min-instances=1, max-instances=1`). The same container image runs on a laptop, preserving an offline demo fallback.

**Tech Stack:** Python 3.12 · FastAPI · uvicorn · `google-cloud-speech` v2 · `google-genai` (Vertex backend) · `transformers` + `torch` (CPU wheel) · ElevenLabs · Docker · Cloud Run · Secret Manager · Unity 6 (`6000.5.6f1`)

**Spec:** [`docs/superpowers/specs/2026-08-04-cloud-hosted-backend-design.md`](../specs/2026-08-04-cloud-hosted-backend-design.md)

## Global Constraints

- **The 47 existing tests must stay green after every commit.** Run from `Sidecar/`: `python3 -m pytest tests/ -q`. Use `python3`, never `python` — `python` is not on PATH on this machine.
- **`tests/test_app_failure_isolation.py` stubs `app.py`'s entire import graph by hand.** It builds a fake `config` module with a *fixed attribute list* (lines 63–75), a fake `stt` module (lines 89–93), a `_FakeFastAPI` class (lines 20–29), and tears down `cls.app._stt_pool` (line 139). **Any new `config.X` that `app.py` reads, any change to `stt`'s signature, any new FastAPI method `app.py` calls, and any removal of `_stt_pool` breaks this file.** Update it in the same commit as the change that breaks it. This is the single most common way to break the suite.
- **No credentials in the repo.** `.gitignore` already blocks `.env`, `*.pem`, `*.key`. Never commit a real key, never print one to stdout, never echo `.env` contents.
- **Pin every model id.** Roadmap item S7. No floating aliases for STT, Gemini, or HuBERT.
- **Auth fails closed.** A missing or empty expected key denies every request; it never falls open to "no key configured, allow all".
- **Commit messages:** conventional commits, one line, no body, no trailers. e.g. `feat(sidecar): add turn cost limiter`.
- **Honesty rule** (`docs/CONCEPT.md`): no claim in README, deck, or docs may contradict the deployed system. Task 9 exists because this migration makes several current claims false.
- **Do not touch the critical path** — A5, A6, `DetectiveAction`, notebook panel. Out of scope.

---

## File Structure

**New Python modules** (each one thing, each unit-testable without a network or a model):

| File | Responsibility |
|---|---|
| `Sidecar/session_store.py` | Owns conversation history storage + LRU eviction. The seam a Firestore implementation drops into later. |
| `Sidecar/auth.py` | One pure predicate: is this client key valid? |
| `Sidecar/limits.py` | Per-session and per-day turn admission counting. |

**Rewritten:** `Sidecar/stt.py` (same public contract, Google backend).

**New non-Python:** `Sidecar/Dockerfile`, `Sidecar/.dockerignore`, `docs/PRIVACY.md`.

**Modified:** `Sidecar/app.py`, `config.py`, `llm.py`, `requirements.txt`, `.env.example`, `tests/test_app_failure_isolation.py`, `Assets/_Project/Scripts/Core/InterrogationConfig.cs`, `Assets/_Project/Scripts/Core/SidecarProcessLauncher.cs`, `Assets/_Project/Scripts/Net/InterrogationSidecarClient.cs`, `README.md`, `Sidecar/README.md`, `docs/CONCEPT.md`, `docs/ROADMAP.md`.

---

### Task 1: Session store behind an interface

**Why:** `app.py` keeps history in a module-level `OrderedDict`. That is correct on a pinned instance and wrong on any other topology. Putting it behind an interface now means the Firestore swap is a new class, not surgery on the turn handler.

**Files:**
- Create: `Sidecar/session_store.py`
- Create: `Sidecar/tests/test_session_store.py`
- Modify: `Sidecar/app.py:42`, `app.py:105-123`, `app.py:203-210`
- Modify: `Sidecar/tests/test_app_failure_isolation.py:143`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `InMemorySessionStore(max_sessions: int)` with methods
  `history(session_id: str) -> list[dict]`,
  `commit(session_id: str, history: list[dict]) -> list[str]` (returns evicted session ids),
  `reset(session_id: str) -> None`,
  `clear() -> None`.
  Tasks 4, 5 and 9 reference this type by name.

- [ ] **Step 1: Write the failing test**

Create `Sidecar/tests/test_session_store.py`:

```python
"""The session store is the seam a networked store drops into later."""

import unittest

from session_store import InMemorySessionStore


class InMemorySessionStoreTests(unittest.TestCase):
    def test_unknown_session_reads_as_empty_without_creating_it(self):
        store = InMemorySessionStore(max_sessions=4)

        self.assertEqual(store.history("nobody"), [])
        self.assertEqual(store.commit("real", [{"role": "user", "content": "hi"}]), [])
        self.assertEqual(len(store.history("real")), 1)

    def test_commit_replaces_history_and_refreshes_recency(self):
        store = InMemorySessionStore(max_sessions=2)
        store.commit("a", [{"role": "user", "content": "1"}])
        store.commit("b", [{"role": "user", "content": "2"}])
        # Touching "a" must make "b" the least-recently-used, not "a".
        store.commit("a", [{"role": "user", "content": "1"}, {"role": "user", "content": "3"}])

        evicted = store.commit("c", [{"role": "user", "content": "4"}])

        self.assertEqual(evicted, ["b"])
        self.assertEqual(len(store.history("a")), 2)
        self.assertEqual(store.history("b"), [])

    def test_reset_forgets_one_session_and_clear_forgets_all(self):
        store = InMemorySessionStore(max_sessions=4)
        store.commit("a", [{"role": "user", "content": "1"}])
        store.commit("b", [{"role": "user", "content": "2"}])

        store.reset("a")
        self.assertEqual(store.history("a"), [])
        self.assertEqual(len(store.history("b")), 1)

        store.clear()
        self.assertEqual(store.history("b"), [])

    def test_reset_of_unknown_session_is_not_an_error(self):
        store = InMemorySessionStore(max_sessions=4)
        store.reset("never-existed")  # must not raise


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd Sidecar && python3 -m pytest tests/test_session_store.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'session_store'`

- [ ] **Step 3: Write the implementation**

Create `Sidecar/session_store.py`:

```python
"""Conversation-history storage for the interrogation backend.

The backend runs pinned to a single Cloud Run instance, so in-memory state is
correct. This interface exists so that assumption is replaceable: a Firestore
implementation with the same four methods drops in without touching app.py.
"""

from collections import OrderedDict


class InMemorySessionStore:
    """History keyed by the GUID Unity mints at scene start, LRU-evicted."""

    def __init__(self, max_sessions: int):
        self._max_sessions = max(1, int(max_sessions))
        self._sessions: OrderedDict[str, list[dict]] = OrderedDict()

    def history(self, session_id: str) -> list[dict]:
        """Never creates the session — a read of an unknown id is empty."""
        return self._sessions.get(session_id, [])

    def commit(self, session_id: str, history: list[dict]) -> list[str]:
        """Store history and mark the session most-recently-used.

        Returns the ids evicted to stay under the cap, so the caller can drop
        the matching prosody state in the same breath.
        """
        self._sessions.pop(session_id, None)
        self._sessions[session_id] = history
        evicted: list[str] = []
        while len(self._sessions) > self._max_sessions:
            evicted_id, _history = self._sessions.popitem(last=False)
            evicted.append(evicted_id)
        return evicted

    def reset(self, session_id: str) -> None:
        self._sessions.pop(session_id, None)

    def clear(self) -> None:
        self._sessions.clear()
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd Sidecar && python3 -m pytest tests/test_session_store.py -q`
Expected: PASS, 4 tests

- [ ] **Step 5: Wire `app.py` to the store**

In `Sidecar/app.py`, add `import session_store` to the import block (alphabetical, after `prosody`).

Replace line 42:

```python
_sessions: OrderedDict[str, list[dict]] = OrderedDict()
```

with:

```python
_session_store = session_store.InMemorySessionStore(config.SIDECAR_MAX_SESSIONS)
```

Remove the now-unused `from collections import OrderedDict` at line 12.

Replace `_history_for` and `_commit_history` (lines 105–123) with:

```python
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
```

In `session_reset` (line 208), replace `_sessions.pop(session_id, None)` with
`_session_store.reset(session_id)`.

- [ ] **Step 6: Fix the hand-stubbed app test**

`tests/test_app_failure_isolation.py:143` reads `self.app._sessions.clear()`, which no longer exists. Change that line to:

```python
        self.app._session_store.clear()
```

- [ ] **Step 7: Run the full suite**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 51 tests (47 existing + 4 new)

- [ ] **Step 8: Commit**

```bash
git add Sidecar/session_store.py Sidecar/tests/test_session_store.py Sidecar/app.py Sidecar/tests/test_app_failure_isolation.py
git commit -m "refactor(sidecar): put session history behind a store interface"
```

---

### Task 2: STT — faster-whisper to Google Cloud Speech-to-Text

**Why:** Local Whisper is the reason the container would need multi-GB weights and the reason `_stt_pool` is a single-worker CPU bottleneck. Google STT is a network call billed to the GCP credits.

**Files:**
- Rewrite: `Sidecar/stt.py`
- Create: `Sidecar/tests/test_stt_adapter.py`
- Modify: `Sidecar/app.py:37`, `app.py:63`, `app.py:256-278`
- Modify: `Sidecar/config.py`
- Modify: `Sidecar/requirements.txt`
- Modify: `Sidecar/tests/test_app_failure_isolation.py:66-75`, `:89-93`, `:137-140`

**Interfaces:**
- Consumes: `InMemorySessionStore` from Task 1 (unchanged, just present).
- Produces: `async stt.transcribe(pcm16_le_bytes: bytes) -> tuple[str, int]` returning `(text, elapsed_ms)`. **The signature changes from sync-float32 to async-bytes.** `stt.load()` is deleted. Task 6 relies on `stt` importing no torch.

- [ ] **Step 1: Add the config the adapter needs**

In `Sidecar/config.py`, after the `PORT` line (line 21), add:

```python
GCP_PROJECT = os.environ.get("GCP_PROJECT", "")
GCP_LOCATION = os.environ.get("GCP_LOCATION", "global")
# Pinned deliberately (roadmap S7). "short" is the sub-60s recognizer; do not
# swap to a floating alias.
STT_MODEL = os.environ.get("STT_MODEL", "short")
STT_LANGUAGE = os.environ.get("STT_LANGUAGE", "en-US")
```

In `validate()`, add `GCP_PROJECT` to the required set — insert before the `ELEVENLABS_API_KEY` check:

```python
    if not GCP_PROJECT:
        missing.append("GCP_PROJECT")
```

- [ ] **Step 2: Write the failing test**

Create `Sidecar/tests/test_stt_adapter.py`. It fakes the Google client so the suite still runs offline:

```python
"""STT adapter contract: the Google backend must keep stt.py's promises.

Google's client is faked so this test needs no network and no credentials —
the same property features_classical relies on.
"""

import asyncio
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import patch


def _fake_speech_modules(recognize):
    """Build the minimal google.cloud.speech_v2 surface stt.py imports."""

    class _AudioEncoding:
        LINEAR16 = "LINEAR16"

    class _ExplicitDecodingConfig:
        AudioEncoding = _AudioEncoding

        def __init__(self, **kwargs):
            self.kwargs = kwargs

    cloud_speech = ModuleType("google.cloud.speech_v2.types.cloud_speech")
    cloud_speech.ExplicitDecodingConfig = _ExplicitDecodingConfig
    cloud_speech.RecognitionConfig = lambda **kwargs: SimpleNamespace(**kwargs)
    cloud_speech.RecognizeRequest = lambda **kwargs: SimpleNamespace(**kwargs)

    class _SpeechAsyncClient:
        def __init__(self, *_args, **_kwargs):
            pass

        async def recognize(self, request):
            return await recognize(request)

    speech_v2 = ModuleType("google.cloud.speech_v2")
    speech_v2.SpeechAsyncClient = _SpeechAsyncClient
    types_module = ModuleType("google.cloud.speech_v2.types")
    types_module.cloud_speech = cloud_speech

    return {
        "google.cloud.speech_v2": speech_v2,
        "google.cloud.speech_v2.types": types_module,
        "google.cloud.speech_v2.types.cloud_speech": cloud_speech,
    }


def _result(transcript):
    return SimpleNamespace(alternatives=[SimpleNamespace(transcript=transcript)])


def _load_stt(recognize):
    import importlib
    from pathlib import Path
    import importlib.util

    path = Path(__file__).resolve().parents[1] / "stt.py"
    spec = importlib.util.spec_from_file_location("sidecar_stt_under_test", path)
    module = importlib.util.module_from_spec(spec)
    stubs = dict(_fake_speech_modules(recognize))
    stubs["config"] = ModuleType("config")
    stubs["config"].GCP_PROJECT = "test-project"
    stubs["config"].GCP_LOCATION = "global"
    stubs["config"].STT_MODEL = "short"
    stubs["config"].STT_LANGUAGE = "en-US"
    with patch.dict(sys.modules, stubs):
        spec.loader.exec_module(module)
    return module


class SttAdapterTests(unittest.TestCase):
    def test_joins_result_alternatives_into_one_transcript(self):
        async def recognize(_request):
            return SimpleNamespace(results=[_result(" I was "), _result("at home. ")])

        stt = _load_stt(recognize)
        text, elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "I was at home.")
        self.assertGreaterEqual(elapsed_ms, 0)

    def test_silence_returns_empty_string_not_an_error(self):
        async def recognize(_request):
            return SimpleNamespace(results=[])

        stt = _load_stt(recognize)
        text, _elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "")

    def test_results_without_alternatives_are_skipped(self):
        async def recognize(_request):
            return SimpleNamespace(results=[SimpleNamespace(alternatives=[]), _result("ok")])

        stt = _load_stt(recognize)
        text, _elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "ok")

    def test_api_failure_propagates_so_the_turn_handler_can_catch_it(self):
        async def recognize(_request):
            raise RuntimeError("google says no")

        stt = _load_stt(recognize)
        with self.assertRaises(RuntimeError):
            asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

    def test_request_targets_the_configured_project_with_linear16_at_16k(self):
        captured = {}

        async def recognize(request):
            captured["request"] = request
            return SimpleNamespace(results=[])

        stt = _load_stt(recognize)
        asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        request = captured["request"]
        self.assertEqual(
            request.recognizer,
            "projects/test-project/locations/global/recognizers/_",
        )
        self.assertEqual(request.config.model, "short")
        self.assertEqual(request.config.language_codes, ["en-US"])
        decoding = request.config.explicit_decoding_config
        self.assertEqual(decoding.kwargs["sample_rate_hertz"], 16000)
        self.assertEqual(decoding.kwargs["audio_channel_count"], 1)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd Sidecar && python3 -m pytest tests/test_stt_adapter.py -q`
Expected: FAIL — `stt.py` still imports `faster_whisper` and `transcribe` is not a coroutine.

- [ ] **Step 4: Rewrite `stt.py`**

Replace the whole file:

```python
"""Speech-to-text via Google Cloud Speech-to-Text v2.

Authenticates as the runtime service account through Application Default
Credentials — there is no API key for this vendor. Locally, run
`gcloud auth application-default login` once.

Audio arrives as the same LINEAR16 16kHz mono bytes Unity uploads, so no
decode or resample happens here; app.py has already normalized the buffer.
"""

import time

from google.cloud import speech_v2
from google.cloud.speech_v2.types import cloud_speech

import config

# Utterances are capped at 20s by the client's VAD, so the synchronous
# short-form recognizer is the right call — streaming would complicate the
# turn boundary for no latency win at this length.
_SAMPLE_RATE = 16000

_client: speech_v2.SpeechAsyncClient | None = None


def _get_client() -> speech_v2.SpeechAsyncClient:
    global _client
    if _client is None:
        _client = speech_v2.SpeechAsyncClient()
    return _client


def _recognizer_path() -> str:
    # The trailing "_" means "no stored recognizer, use the inline config".
    return f"projects/{config.GCP_PROJECT}/locations/{config.GCP_LOCATION}/recognizers/_"


async def transcribe(pcm16_le_bytes: bytes) -> tuple[str, int]:
    """pcm16_le_bytes: mono little-endian PCM16 at 16kHz. Returns (text, elapsed_ms).

    Raises on API failure. app.py's /turn handler converts that into a failed
    turn the client can retry, exactly as a local STT crash used to.
    """
    client = _get_client()
    t0 = time.perf_counter()

    request = cloud_speech.RecognizeRequest(
        recognizer=_recognizer_path(),
        config=cloud_speech.RecognitionConfig(
            explicit_decoding_config=cloud_speech.ExplicitDecodingConfig(
                encoding=cloud_speech.ExplicitDecodingConfig.AudioEncoding.LINEAR16,
                sample_rate_hertz=_SAMPLE_RATE,
                audio_channel_count=1,
            ),
            language_codes=[config.STT_LANGUAGE],
            model=config.STT_MODEL,
        ),
        content=pcm16_le_bytes,
    )

    response = await client.recognize(request=request)

    parts = [
        result.alternatives[0].transcript.strip()
        for result in response.results
        if result.alternatives
    ]
    text = " ".join(part for part in parts if part).strip()
    ms = int((time.perf_counter() - t0) * 1000)
    return text, ms
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd Sidecar && python3 -m pytest tests/test_stt_adapter.py -q`
Expected: PASS, 5 tests

- [ ] **Step 6: Update `requirements.txt`**

Replace the STT block:

```
# STT (local, free)
faster-whisper>=1.0.3
```

with:

```
# STT (Google Cloud Speech-to-Text v2, service-account auth)
google-cloud-speech>=2.27
```

- [ ] **Step 7: Wire `app.py` to the async adapter**

Delete line 37 (`_stt_pool = ThreadPoolExecutor(max_workers=1)`). Keep `_ser_pool`.

In `lifespan` (line 63), delete `stt.load()`. The line above it prints "Loading models…"; change that print to:

```python
    print("[Sidecar] Loading affect model (first run downloads it)...")
```

In the `/turn` handler, replace lines 263–278 with:

```python
            loop = asyncio.get_running_loop()
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
```

- [ ] **Step 8: Fix the hand-stubbed app test**

Three edits to `tests/test_app_failure_isolation.py`:

Add the new config attributes to the fake config module (after `PORT=8765,` on line 74):

```python
            GCP_PROJECT="test-project",
            GCP_LOCATION="global",
            STT_MODEL="short",
            STT_LANGUAGE="en-US",
```

Replace the `stt` stub (lines 89–93) — it must now be an async callable taking bytes:

```python
        async def fake_transcribe(_pcm_bytes):
            return "fixture transcript", 5

        stt = _module("stt", transcribe=fake_transcribe)
```

Replace `tearDownClass` (lines 137–140) — `_stt_pool` is gone:

```python
    @classmethod
    def tearDownClass(cls):
        cls.app._ser_pool.shutdown(wait=True)
```

Also add `float32_to_pcm16_bytes` to the `audio_utils` stub (after line 113), since `/turn` now calls it:

```python
            float32_to_pcm16_bytes=lambda audio: (
                (np.clip(audio, -1.0, 1.0) * 32767.0).astype("<i2").tobytes()
            ),
```

- [ ] **Step 9: Run the full suite**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 56 tests

- [ ] **Step 10: Commit**

```bash
git add Sidecar/stt.py Sidecar/config.py Sidecar/requirements.txt Sidecar/app.py Sidecar/tests/
git commit -m "feat(sidecar): move stt to google cloud speech-to-text"
```

---

### Task 3: Gemini — AI Studio key to Vertex AI

**Why:** `llm.py:88` builds an AI Studio client from `GEMINI_API_KEY`. That bills a personal key, not the team's $300 of GCP credits. Vertex also authenticates by service account, so a long-lived secret disappears.

**Files:**
- Modify: `Sidecar/llm.py:85-89`
- Modify: `Sidecar/config.py`
- Modify: `Sidecar/.env.example`
- Modify: `Sidecar/tests/test_config.py`

**Interfaces:**
- Consumes: `config.GCP_PROJECT`, `config.GCP_LOCATION` from Task 2.
- Produces: no signature change. `llm.generate_reply` keeps its existing keyword arguments and `(reply_text, elapsed_ms)` return.

- [ ] **Step 1: Write the failing test**

Append to `Sidecar/tests/test_config.py`, inside `ConfigTests`:

```python
    def test_gemini_api_key_is_gone_so_no_stale_secret_is_expected(self):
        self.assertFalse(
            hasattr(config, "GEMINI_API_KEY"),
            "Vertex authenticates by service account; a GEMINI_API_KEY constant "
            "left behind invites someone to reintroduce a key-based client.",
        )

    def test_missing_gcp_project_is_a_startup_failure(self):
        path = Path(__file__).resolve().parents[1] / "config.py"
        spec = importlib.util.spec_from_file_location("sidecar_config_validate_test", path)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(os.environ, {"GCP_PROJECT": ""}):
            spec.loader.exec_module(module)

        with self.assertRaises(SystemExit):
            module.validate()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd Sidecar && python3 -m pytest tests/test_config.py -q`
Expected: FAIL — `GEMINI_API_KEY` still exists on the module.

- [ ] **Step 3: Remove the key from config**

In `Sidecar/config.py`, delete line 16 (`GEMINI_API_KEY = ...`). In `validate()`, delete the two lines:

```python
    if not GEMINI_API_KEY:
        missing.append("GEMINI_API_KEY")
```

- [ ] **Step 4: Point the client at Vertex**

In `Sidecar/llm.py`, replace `_get_client` (lines 85–89):

```python
def _get_client() -> genai.Client:
    """Vertex backend: billed to the project's GCP credits and authenticated
    by the runtime service account, so no API key exists to leak. Locally, run
    `gcloud auth application-default login` once."""
    global _client
    if _client is None:
        _client = genai.Client(
            vertexai=True,
            project=config.GCP_PROJECT,
            location=config.GCP_LOCATION,
        )
    return _client
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd Sidecar && python3 -m pytest tests/test_config.py -q`
Expected: PASS

- [ ] **Step 6: Update `.env.example`**

Delete the `GEMINI_API_KEY` line. Add, with a comment:

```
# Google Cloud project that bills Vertex AI (Gemini) and Speech-to-Text.
# Both authenticate via Application Default Credentials — no API key.
# Locally: gcloud auth application-default login
GCP_PROJECT=
GCP_LOCATION=global
```

- [ ] **Step 7: Confirm the model is available on Vertex in this region**

`llm.py:MODEL` is `gemini-3.6-flash`. Vertex model availability is regional and its ids are not always identical to AI Studio's.

Ask the user to run: `! gcloud ai models list --region=us-central1 2>&1 | head -40`

If `gemini-3.6-flash` is not served in `global`, set `GCP_LOCATION` to a region that serves it and record the pinned id in a comment above `MODEL`. **Do not silently substitute a different model** — the detective's behaviour is tuned to this one.

- [ ] **Step 8: Run the full suite and commit**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 58 tests

```bash
git add Sidecar/llm.py Sidecar/config.py Sidecar/.env.example Sidecar/tests/test_config.py
git commit -m "feat(sidecar): bill gemini through vertex ai instead of an api key"
```

---

### Task 4: Client authentication and removal of the debug endpoint

**Why:** The service is about to be internet-reachable with two paid vendors behind it. `/debug/last_turn` (`app.py:213`) currently returns the last player's transcript to any caller.

**Files:**
- Create: `Sidecar/auth.py`
- Create: `Sidecar/tests/test_auth.py`
- Modify: `Sidecar/app.py:48`, `:84`, `:213-215`, `:351-352`, `:361-362`
- Modify: `Sidecar/config.py`
- Modify: `Sidecar/.env.example`
- Modify: `Sidecar/tests/test_app_failure_isolation.py:20-29`, `:63-75`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `auth.is_authorized(supplied: str, expected: str) -> bool`, and the header name constant `auth.CLIENT_KEY_HEADER = "x-fp-client-key"`. Task 8's Unity client sends this exact header.

- [ ] **Step 1: Write the failing test**

Create `Sidecar/tests/test_auth.py`:

```python
"""The client key is a speed bump, not a wall — but it must fail closed."""

import unittest

import auth


class AuthTests(unittest.TestCase):
    def test_matching_key_is_authorized(self):
        self.assertTrue(auth.is_authorized("s3cret", "s3cret"))

    def test_wrong_key_is_rejected(self):
        self.assertFalse(auth.is_authorized("wrong", "s3cret"))

    def test_missing_key_is_rejected(self):
        self.assertFalse(auth.is_authorized("", "s3cret"))
        self.assertFalse(auth.is_authorized(None, "s3cret"))

    def test_unconfigured_server_denies_everything_rather_than_falling_open(self):
        # A deploy that forgot the secret must reject traffic, not serve it.
        self.assertFalse(auth.is_authorized("anything", ""))
        self.assertFalse(auth.is_authorized("", ""))

    def test_header_name_is_lowercase_for_case_insensitive_lookup(self):
        self.assertEqual(auth.CLIENT_KEY_HEADER, auth.CLIENT_KEY_HEADER.lower())


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd Sidecar && python3 -m pytest tests/test_auth.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'auth'`

- [ ] **Step 3: Write `auth.py`**

```python
"""Shared-secret gate for the public backend.

The key ships inside the Unity build and is therefore extractable by anyone
who cares to look. It stops drive-by traffic against a public URL; it is not
a security boundary. The real cost protection is limits.py.
"""

import hmac

CLIENT_KEY_HEADER = "x-fp-client-key"


def is_authorized(supplied: str | None, expected: str) -> bool:
    """Fails closed: an unconfigured server rejects every request."""
    if not expected:
        return False
    return hmac.compare_digest(supplied or "", expected)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd Sidecar && python3 -m pytest tests/test_auth.py -q`
Expected: PASS, 5 tests

- [ ] **Step 5: Add the config value**

In `Sidecar/config.py`, near the other secrets:

```python
FP_CLIENT_KEY = os.environ.get("FP_CLIENT_KEY", "")
```

and add it to `validate()`'s required set:

```python
    if not FP_CLIENT_KEY:
        missing.append("FP_CLIENT_KEY")
```

In `.env.example`, add:

```
# Shared secret the Unity build sends as X-FP-Client-Key. Generate with:
#   python3 -c "import secrets; print(secrets.token_urlsafe(32))"
FP_CLIENT_KEY=
```

- [ ] **Step 6: Register the middleware in `app.py`**

Add `import auth` to the imports. After `app = FastAPI(...)` (line 84):

```python
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
```

- [ ] **Step 7: Delete the debug endpoint and its global**

Delete lines 213–215 (`@app.get("/debug/last_turn")` and `debug_last_turn`). Delete `_last_turn_debug: dict = {}` (line 48). Delete the four lines that write to it — the pair at 351–352 and the pair at 361–362.

- [ ] **Step 8: Teach the fake FastAPI about middleware**

`_FakeFastAPI` in `tests/test_app_failure_isolation.py` has no `middleware` method, so `app.py` will now fail to import under test. Add to the class (after `post`, line 28):

```python
    def middleware(self, _kind):
        return lambda function: function
```

Add `FP_CLIENT_KEY="test-key"` to the fake config module's attribute list.

- [ ] **Step 9: Run the full suite**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 63 tests

- [ ] **Step 10: Commit**

```bash
git add Sidecar/auth.py Sidecar/tests/test_auth.py Sidecar/app.py Sidecar/config.py Sidecar/.env.example Sidecar/tests/test_app_failure_isolation.py
git commit -m "feat(sidecar): require a client key and drop the debug transcript endpoint"
```

---

### Task 5: Turn cost ceiling

**Why:** Every turn spends Google STT, Vertex, and ElevenLabs money. One stuck client looping `/turn` can drain $300 and an ElevenLabs quota unattended. Counting in memory is correct here for the same reason session state is: one instance.

**Files:**
- Create: `Sidecar/limits.py`
- Create: `Sidecar/tests/test_limits.py`
- Modify: `Sidecar/app.py`
- Modify: `Sidecar/config.py`
- Modify: `Sidecar/.env.example`
- Modify: `Sidecar/tests/test_app_failure_isolation.py:63-75`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `limits.TurnLimiter(max_per_session: int, max_per_day: int)` with
  `admit(session_id: str, now: float) -> str` (returns `""` when allowed, else a reason slug) and
  `forget(session_id: str) -> None`.
  Task 9's docs reference the reason slugs `session_turn_limit_reached` and `daily_turn_budget_exhausted`.

- [ ] **Step 1: Write the failing test**

Create `Sidecar/tests/test_limits.py`:

```python
"""Cost ceiling. Counts attempts, not successes — a retry loop still spends
money at the vendors, so admission is where the cap belongs."""

import unittest

from limits import TurnLimiter


class TurnLimiterTests(unittest.TestCase):
    def test_turns_under_both_caps_are_admitted(self):
        limiter = TurnLimiter(max_per_session=3, max_per_day=10)

        self.assertEqual(limiter.admit("a", 0.0), "")
        self.assertEqual(limiter.admit("a", 1.0), "")

    def test_session_cap_blocks_that_session_only(self):
        limiter = TurnLimiter(max_per_session=2, max_per_day=100)
        limiter.admit("a", 0.0)
        limiter.admit("a", 0.0)

        self.assertEqual(limiter.admit("a", 0.0), "session_turn_limit_reached")
        self.assertEqual(limiter.admit("b", 0.0), "")

    def test_daily_cap_blocks_everyone(self):
        limiter = TurnLimiter(max_per_session=100, max_per_day=2)
        limiter.admit("a", 0.0)
        limiter.admit("b", 0.0)

        self.assertEqual(limiter.admit("c", 0.0), "daily_turn_budget_exhausted")

    def test_daily_counter_rolls_over_but_session_counter_does_not(self):
        limiter = TurnLimiter(max_per_session=100, max_per_day=1)
        limiter.admit("a", 0.0)
        self.assertEqual(limiter.admit("b", 0.0), "daily_turn_budget_exhausted")

        tomorrow = 86400.0 + 10.0
        self.assertEqual(limiter.admit("b", tomorrow), "")

    def test_forget_clears_a_session_count_for_session_reset(self):
        limiter = TurnLimiter(max_per_session=1, max_per_day=100)
        limiter.admit("a", 0.0)
        self.assertEqual(limiter.admit("a", 0.0), "session_turn_limit_reached")

        limiter.forget("a")

        self.assertEqual(limiter.admit("a", 0.0), "")

    def test_a_blocked_turn_does_not_consume_daily_budget(self):
        limiter = TurnLimiter(max_per_session=1, max_per_day=5)
        limiter.admit("a", 0.0)
        limiter.admit("a", 0.0)  # blocked by the session cap

        # Four of the five daily turns must remain for other players.
        for _ in range(4):
            self.assertEqual(limiter.admit("b", 0.0), "")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd Sidecar && python3 -m pytest tests/test_limits.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'limits'`

- [ ] **Step 3: Write `limits.py`**

```python
"""Turn admission caps, so a runaway client cannot drain the project budget.

Counts on admission rather than on success: a client retrying a failing turn
still pays Google, Vertex, and ElevenLabs on every attempt.

In-memory counters are correct only because the service is pinned to one
Cloud Run instance. If that ever changes, this moves to a shared counter at
the same time the session store does.
"""

_SECONDS_PER_DAY = 86400.0


class TurnLimiter:
    def __init__(self, max_per_session: int, max_per_day: int):
        self._max_per_session = max(1, int(max_per_session))
        self._max_per_day = max(1, int(max_per_day))
        self._session_counts: dict[str, int] = {}
        self._day_index: int | None = None
        self._day_count = 0

    def admit(self, session_id: str, now: float) -> str:
        """Returns "" if the turn may proceed, else a machine-readable reason.

        Counts the turn when it is admitted; a rejected turn costs nothing and
        is not counted against anyone else's budget.
        """
        day_index = int(now // _SECONDS_PER_DAY)
        if day_index != self._day_index:
            self._day_index = day_index
            self._day_count = 0

        if self._session_counts.get(session_id, 0) >= self._max_per_session:
            return "session_turn_limit_reached"
        if self._day_count >= self._max_per_day:
            return "daily_turn_budget_exhausted"

        self._session_counts[session_id] = self._session_counts.get(session_id, 0) + 1
        self._day_count += 1
        return ""

    def forget(self, session_id: str) -> None:
        self._session_counts.pop(session_id, None)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd Sidecar && python3 -m pytest tests/test_limits.py -q`
Expected: PASS, 6 tests

- [ ] **Step 5: Add config**

In `Sidecar/config.py`:

```python
MAX_TURNS_PER_SESSION = max(1, int(os.environ.get("MAX_TURNS_PER_SESSION", "40")))
MAX_TURNS_PER_DAY = max(1, int(os.environ.get("MAX_TURNS_PER_DAY", "2000")))
```

In `.env.example`:

```
# Cost ceiling. A turn spends Google STT + Vertex + ElevenLabs, so these are
# budget controls, not gameplay tuning.
MAX_TURNS_PER_SESSION=40
MAX_TURNS_PER_DAY=2000
```

- [ ] **Step 6: Enforce in `app.py`**

Add `import limits` to the imports. Next to `_session_store`:

```python
_turn_limiter = limits.TurnLimiter(
    max_per_session=config.MAX_TURNS_PER_SESSION,
    max_per_day=config.MAX_TURNS_PER_DAY,
)
```

Add an exception type beside `ClientInputError`:

```python
class TurnBudgetError(RuntimeError):
    """The turn was refused to protect the project budget, not because the
    request was malformed. Distinct from ClientInputError so it can answer 429."""
```

In the `/turn` handler, immediately after `session_id = _validate_session_id(session_id)`:

```python
        budget_reason = _turn_limiter.admit(session_id, time.time())
        if budget_reason:
            raise TurnBudgetError(budget_reason)
```

In the `except` block, replace the status-code line so budget refusals answer 429:

```python
        is_input_error = isinstance(e, ClientInputError)
        is_budget_error = isinstance(e, TurnBudgetError)
        if is_input_error or is_budget_error:
            result["error"] = str(e)
        else:
            result["error"] = "turn pipeline failed; retry the utterance"
        status_code = 429 if is_budget_error else (400 if is_input_error else 500)
        return JSONResponse(status_code=status_code, content=result)
```

In `session_reset`, add `_turn_limiter.forget(session_id)` beside the store reset.

In `_commit_history`, drop the limiter count for evicted sessions too:

```python
    for evicted_id in _session_store.commit(session_id, history):
        _prosody_registry.reset(evicted_id)
        _turn_limiter.forget(evicted_id)
```

- [ ] **Step 7: Update the fake config module**

Add to `tests/test_app_failure_isolation.py`'s fake config:

```python
            MAX_TURNS_PER_SESSION=40,
            MAX_TURNS_PER_DAY=2000,
```

- [ ] **Step 8: Run the full suite**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 69 tests

- [ ] **Step 9: Commit**

```bash
git add Sidecar/limits.py Sidecar/tests/test_limits.py Sidecar/app.py Sidecar/config.py Sidecar/.env.example Sidecar/tests/test_app_failure_isolation.py
git commit -m "feat(sidecar): cap turns per session and per day to bound cost"
```

---

### Task 6: Container — and the checkpoint that gates everything after it

**Why:** This is the step where the design proves itself. If a full interrogation does not run inside the container on a laptop, nothing in Task 7 is worth attempting.

**Files:**
- Create: `Sidecar/Dockerfile`
- Create: `Sidecar/.dockerignore`
- Modify: `Sidecar/app.py:5-7`, `app.py:20`

**Interfaces:**
- Consumes: everything from Tasks 1–5.
- Produces: a local image tagged `false-positive-backend:dev` that serves `/health` and `/turn` on `$PORT`.

- [ ] **Step 1: Write `.dockerignore`**

Create `Sidecar/.dockerignore`:

```
.env
.venv/
venv/
__pycache__/
**/__pycache__/
*.py[cod]
tests/
tools/
.DS_Store
run_sidecar.bat
```

- [ ] **Step 2: Write the Dockerfile**

Create `Sidecar/Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1
FROM python:3.12-slim

# ffmpeg is not optional: tts.py falls back to decoding ElevenLabs MP3 through
# pydub when the account's plan refuses PCM output, and pydub shells out to
# ffmpeg. Without it TTS works until the day the PCM endpoint 402s, then fails
# in production only.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

ENV PYTHONUNBUFFERED=1 \
    HF_HOME=/opt/huggingface

WORKDIR /app

# CPU-only torch first. The default wheel drags ~2.5GB of CUDA that a Cloud Run
# CPU instance can never use.
COPY requirements.txt .
RUN pip install --no-cache-dir --index-url https://download.pytorch.org/whl/cpu torch \
    && pip install --no-cache-dir -r requirements.txt

COPY . .

# Bake the HuBERT checkpoint into the image. Downloading it at first request
# turns a cold start into a ~90 second first turn.
RUN python -c "\
from transformers import AutoFeatureExtractor, AutoModelForAudioClassification; \
model_id='superb/hubert-base-superb-er'; \
AutoFeatureExtractor.from_pretrained(model_id); \
AutoModelForAudioClassification.from_pretrained(model_id)"

# Cloud Run injects PORT and requires binding 0.0.0.0.
ENV PORT=8080
CMD exec uvicorn app:app --host 0.0.0.0 --port ${PORT}
```

- [ ] **Step 3: Correct the stale docstring in `app.py`**

Lines 5–7 currently claim the process "Binds to 127.0.0.1 only … and must not be LAN-reachable." It is about to be internet-reachable. Replace the module docstring:

```python
"""Interrogation backend — the whole STT -> emotion -> LLM -> TTS pipeline
behind one HTTP endpoint per conversational turn.

Deployed to Cloud Run pinned at a single instance: session history and prosody
state live in this process's memory, which is only correct while exactly one
instance exists. See docs/superpowers/specs/2026-08-04-cloud-hosted-backend-design.md.

Requests are gated by a shared client key (auth.py) and a turn budget
(limits.py), because this endpoint is public and every turn spends money at
three vendors.
"""
```

- [ ] **Step 4: Build the image**

Run: `cd Sidecar && docker build -t false-positive-backend:dev .`
Expected: a successful build. The HuBERT bake step takes a few minutes on first run.

If Docker is not installed, ask the user to run: `! brew install --cask docker` and start Docker Desktop.

- [ ] **Step 5: Run the container against real credentials**

The container needs ADC for Google, plus the two secrets. Ask the user to run:

```
! gcloud auth application-default login
```

Then run the container, mounting those credentials read-only:

```bash
docker run --rm -p 8080:8080 \
  -e GCP_PROJECT="$(gcloud config get-value project)" \
  -e GCP_LOCATION=global \
  -e FP_CLIENT_KEY=local-dev-key \
  -e ELEVENLABS_API_KEY="$ELEVENLABS_API_KEY" \
  -e ELEVENLABS_VOICE_ID="$ELEVENLABS_VOICE_ID" \
  -e GOOGLE_APPLICATION_CREDENTIALS=/adc/application_default_credentials.json \
  -v "$HOME/.config/gcloud:/adc:ro" \
  false-positive-backend:dev
```

- [ ] **Step 6: Verify the container end to end**

In a second terminal:

```bash
curl -s localhost:8080/health | python3 -m json.tool
```
Expected: `"status": "ok"`, `"models_loaded": true`, and a `prosody` block reporting `available: true` on `cpu`.

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8080/turn -F session_id=probe
```
Expected: `401` — the middleware is doing its job.

```bash
curl -s -X POST localhost:8080/turn \
  -H 'x-fp-client-key: local-dev-key' \
  -F session_id=probe | python3 -m json.tool
```
Expected: `"ok": true` with a non-empty `reply_text` and `audio_b64` — this is the officer's opening line, which exercises Vertex and ElevenLabs but not STT.

**This is the gate.** If any of the three fail, fix them before Task 7. Do not deploy a container that does not work locally.

- [ ] **Step 7: Commit**

```bash
git add Sidecar/Dockerfile Sidecar/.dockerignore Sidecar/app.py
git commit -m "build(sidecar): containerize the backend for cloud run"
```

---

### Task 7: Deploy to Cloud Run

**Why:** The actual migration. Everything before this was preparation.

**Files:** none in the repo — this task is infrastructure. Its deliverable is a URL and the commands recorded in `Sidecar/README.md` (Task 9 writes them up).

**Interfaces:**
- Consumes: the verified image from Task 6.
- Produces: the Cloud Run HTTPS URL, which Task 8 puts into `InterrogationConfig`.

- [ ] **Step 1: Confirm tooling and project**

`gcloud` was **not installed** on this machine when the spec was written. Ask the user to run:

```
! gcloud version || brew install --cask google-cloud-sdk
```

Then confirm the active project and that billing is the $300 credit account:

```
! gcloud config get-value project && gcloud beta billing projects describe "$(gcloud config get-value project)"
```

- [ ] **Step 2: Enable the APIs**

```bash
gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  speech.googleapis.com \
  aiplatform.googleapis.com \
  secretmanager.googleapis.com
```

- [ ] **Step 3: Set the budget alert before anything can spend**

Do this first, not last. Ask the user to create a budget on the billing account with thresholds at **$50, $150 and $250** of the $300, alerting their email:

```
! gcloud billing budgets create --billing-account="$(gcloud beta billing projects describe "$(gcloud config get-value project)" --format='value(billingAccountName)' | cut -d/ -f2)" --display-name="false-positive" --budget-amount=300USD --threshold-rule=percent=0.17 --threshold-rule=percent=0.5 --threshold-rule=percent=0.83
```

If that command is rejected for permissions, set it in the Cloud Console under Billing → Budgets & alerts. **Do not skip this step.**

- [ ] **Step 4: Store the secrets**

```bash
printf '%s' "$ELEVENLABS_API_KEY" | gcloud secrets create elevenlabs-api-key --data-file=-
printf '%s' "$ELEVENLABS_VOICE_ID" | gcloud secrets create elevenlabs-voice-id --data-file=-
python3 -c "import secrets; print(secrets.token_urlsafe(32))" | tr -d '\n' | gcloud secrets create fp-client-key --data-file=-
```

Read the client key back once — Task 8 needs it for the Unity config:

```bash
gcloud secrets versions access latest --secret=fp-client-key
```

Treat that value like a password. It goes into the Unity `.asset`, which is committed, so **it is not a secret from anyone who downloads the game**. That is understood and documented; it still must not be pasted into chat logs or the deck.

- [ ] **Step 5: Create the Artifact Registry repo and push**

```bash
gcloud artifacts repositories create false-positive --repository-format=docker --location=us-central1
gcloud auth configure-docker us-central1-docker.pkg.dev

PROJECT="$(gcloud config get-value project)"
IMAGE="us-central1-docker.pkg.dev/${PROJECT}/false-positive/backend:v1"
cd Sidecar && docker build --platform linux/amd64 -t "$IMAGE" . && docker push "$IMAGE"
```

`--platform linux/amd64` matters on an Apple Silicon laptop — Cloud Run will not run an arm64 image.

- [ ] **Step 6: Deploy pinned**

```bash
gcloud run deploy false-positive-backend \
  --image "$IMAGE" \
  --region us-central1 \
  --allow-unauthenticated \
  --min-instances 1 \
  --max-instances 1 \
  --cpu 2 \
  --memory 4Gi \
  --timeout 120 \
  --set-env-vars "GCP_PROJECT=${PROJECT},GCP_LOCATION=global,SIDECAR_MAX_SESSIONS=200" \
  --set-secrets "ELEVENLABS_API_KEY=elevenlabs-api-key:latest,ELEVENLABS_VOICE_ID=elevenlabs-voice-id:latest,FP_CLIENT_KEY=fp-client-key:latest"
```

`--max-instances 1` is load-bearing, not a cost tweak. Removing it silently breaks session memory and the prosody baseline. `--allow-unauthenticated` is required because a game client cannot do IAM; the app's own middleware is the gate.

- [ ] **Step 7: Grant the runtime service account its roles**

```bash
SA="$(gcloud run services describe false-positive-backend --region us-central1 --format='value(spec.template.spec.serviceAccountName)')"
gcloud projects add-iam-policy-binding "$PROJECT" --member="serviceAccount:${SA}" --role=roles/speech.client
gcloud projects add-iam-policy-binding "$PROJECT" --member="serviceAccount:${SA}" --role=roles/aiplatform.user
```

- [ ] **Step 8: Verify the deployed service**

```bash
URL="$(gcloud run services describe false-positive-backend --region us-central1 --format='value(status.url)')"
curl -s "$URL/health" | python3 -m json.tool
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$URL/turn" -F session_id=probe
curl -s -X POST "$URL/turn" -H "x-fp-client-key: $(gcloud secrets versions access latest --secret=fp-client-key)" -F session_id=probe | python3 -m json.tool
```
Expected: `status: ok` · `401` · `ok: true`.

- [ ] **Step 9: Record the turn latency**

Run the authenticated opening-line call three times with `curl -w '%{time_total}\n'` and note the numbers. Compare against the local container. **If a turn exceeds ~4 seconds, that is a finding for the demo plan** — raise it rather than discovering it on stage.

There is no commit for this task; its output is the URL and the recorded latency.

---

### Task 8: Unity client points at the cloud

**Why:** `InterrogationConfig.cs:46` is the only place in the project that knows where the backend lives. This is why the migration is small.

**Files:**
- Modify: `Assets/_Project/Scripts/Core/InterrogationConfig.cs:14-24`, `:46`
- Modify: `Assets/_Project/Scripts/Net/InterrogationSidecarClient.cs:31-33`, `:82-84`, `:92`
- Modify: `Assets/_Project/Scripts/Core/SidecarProcessLauncher.cs:50-54`
- Modify: `Assets/_Project/Config/InterrogationConfig.asset` (via the Unity Inspector, not a text editor)

**Interfaces:**
- Consumes: `auth.CLIENT_KEY_HEADER` (`"x-fp-client-key"`) from Task 4; the Cloud Run URL and client key from Task 7.
- Produces: no new C# types.

- [ ] **Step 1: Add the cloud fields to `InterrogationConfig`**

Replace the `Sidecar connection` and `Sidecar launch` headers (lines 14–24):

```csharp
        [Header("Backend connection")]
        [Tooltip("Full https:// URL of the hosted backend. Leave empty to fall back to the local host/port below.")]
        public string backendBaseUrl = "";
        [Tooltip("Shared secret sent as X-FP-Client-Key. Extractable from a shipped build by design — it stops drive-by traffic, it is not a security boundary.")]
        public string backendClientKey = "";
        [Tooltip("Used only when backendBaseUrl is empty — local container or local python process.")]
        public string sidecarHost = "127.0.0.1";
        public int sidecarPort = 8765;
        [Tooltip("Per-request timeout. Generous because it now covers network latency as well as model work.")]
        public float requestTimeoutSeconds = 60f;

        [Header("Local sidecar launch (developers only)")]
        [Tooltip("Off for shipped builds. Only starts a local python process when backendBaseUrl is empty.")]
        public bool autoLaunchSidecar = false;
        [Tooltip("How long to poll /health before giving up (first run downloads models).")]
        public float sidecarLaunchTimeoutSeconds = 90f;
        public float sidecarHealthPollIntervalSeconds = 0.5f;
```

Replace line 46:

```csharp
        public string SidecarBaseUrl =>
            string.IsNullOrWhiteSpace(backendBaseUrl)
                ? $"http://{sidecarHost}:{sidecarPort}"
                : backendBaseUrl.TrimEnd('/');
```

- [ ] **Step 2: Send the auth header on both requests**

In `InterrogationSidecarClient.cs`, in `HealthRoutine` after `req.timeout = 5;`:

```csharp
            ApplyClientKey(req);
```

In `TurnRoutine` after `req.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);`:

```csharp
            ApplyClientKey(req);
```

Add the helper to the class:

```csharp
        private void ApplyClientKey(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(config.backendClientKey))
            {
                request.SetRequestHeader("X-FP-Client-Key", config.backendClientKey);
            }
        }
```

- [ ] **Step 3: Make the unreachable-backend message true**

Line 92 says "Voice service unreachable. Is the sidecar running?" — wrong advice for a player on itch.io. Replace:

```csharp
                onError?.Invoke("Could not reach the interrogation service. Check your connection and try again.");
```

This is roadmap fault **F6**, whose meaning changes from *launch failed* to *backend unreachable*.

- [ ] **Step 4: Fix the launcher's failure copy**

`SidecarProcessLauncher` health-checks first and calls `OnReady` if the backend answers, so it already behaves correctly against a cloud URL. Only its failure message is wrong. Replace lines 50–54:

```csharp
            if (!config.autoLaunchSidecar)
            {
                OnFailed?.Invoke(string.IsNullOrWhiteSpace(config.backendBaseUrl)
                    ? "Voice services are not running. Start the local backend, then press Play again."
                    : "Could not reach the interrogation service. Check your connection and try again.");
                yield break;
            }
```

- [ ] **Step 5: Set the values on the asset**

Open the Unity Editor, select `Assets/_Project/Config/InterrogationConfig.asset`, and in the Inspector set `backendBaseUrl` to the Cloud Run URL from Task 7 step 8 and `backendClientKey` to the value from Task 7 step 4. Leave `autoLaunchSidecar` unchecked.

Edit through the Inspector, not by hand — Unity `.asset` YAML carries type metadata that hand edits corrupt.

- [ ] **Step 6: Play-test**

Enter Play mode and run a full interrogation: opening line plays, speak a reply, the detective answers. Watch the Console for 401s (wrong key) and confirm `DebugOverlayUI` still shows the prosody fields.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Core/InterrogationConfig.cs Assets/_Project/Scripts/Net/InterrogationSidecarClient.cs Assets/_Project/Scripts/Core/SidecarProcessLauncher.cs Assets/_Project/Config/InterrogationConfig.asset
git commit -m "feat(client): point the game at the hosted backend"
```

---

### Task 9: Documentation the migration makes false

**Why:** `docs/CONCEPT.md`'s honesty rule makes this blocking for submission, not cleanup. Several files currently promise the player that their voice never leaves their machine. After Task 7 that is false, and a claim that dies in front of judges costs more than the feature was worth.

**Files:**
- Create: `docs/PRIVACY.md`
- Modify: `Sidecar/config.py:1-5`
- Modify: `README.md`
- Modify: `Sidecar/README.md`
- Modify: `docs/CONCEPT.md` (the 1 Aug STT row, ~line 109; the open-decisions list, ~lines 121-129)
- Modify: `docs/ROADMAP.md` §9

**Interfaces:**
- Consumes: reason slugs from Task 5, header name from Task 4, the URL from Task 7.
- Produces: nothing code-facing.

- [ ] **Step 1: Write `docs/PRIVACY.md`**

Required by concept non-negotiable *"Voice data handling is stated plainly to the player and in the README."* It must state, in plain language a player would understand:

- The microphone records only while the player is speaking, in short utterances.
- Each utterance is sent over HTTPS to the game's backend on Google Cloud.
- There it is transcribed by Google Cloud Speech-to-Text and analysed by a local-to-the-server HuBERT model for **emotional tone only**.
- **No recording is written to disk or retained** — audio exists in memory for the duration of one turn.
- The transcript is held in memory for the length of the interrogation so the detective can remember what was said, and is discarded when the session ends or the server restarts.
- The detective's reply text is sent to ElevenLabs to be spoken.
- **The system does not and cannot detect lies.** It detects affect. Name the false-positive premise explicitly — it is the game's thesis, and saying it here is both honest and on-brand.
- Nothing is used for training.

- [ ] **Step 2: Correct `config.py`'s docstring**

Lines 3–4 say *"Both API keys live only here … never sent to or stored by the Unity client."* Half of that is now obsolete. Replace the docstring:

```python
"""Environment configuration for the interrogation backend.

Google STT and Vertex authenticate as the runtime service account, so the only
secrets here are the ElevenLabs key and the shared client key. Neither is sent
to the Unity client.

The client key is the exception to that rule by design: the Unity build carries
a copy so it can authenticate, which means it is extractable from a shipped
game. It bounds drive-by traffic; limits.py bounds cost. See
Sidecar/.env.example for the template.
"""
```

- [ ] **Step 3: Correct `docs/CONCEPT.md`**

The 1 Aug STT row claims local Whisper and *"Player audio never leaves the machine — this is stronger than the original privacy position, not weaker."* Strike it through in the established `~~...~~` style used elsewhere in that table, and add a superseding row:

```markdown
| **STT: Google Cloud Speech-to-Text**, replacing local `faster-whisper`. Decided 4 Aug. | Player audio now leaves the machine. Deliberate — it is what makes an itch.io release possible. Disclosed in [`PRIVACY.md`](PRIVACY.md). |
```

In the open-decisions list, move **itch.io distribution** and **Gemini billing** out of "not decided" into the "Resolved since this list was written" section, pointing at the spec.

- [ ] **Step 4: Rewrite `docs/ROADMAP.md` §9**

§9 currently presents the local backend as an open question with three options. Replace it with the migration record: the decision, the date, the Cloud Run URL, the pinned-instance constraint and *why* removing `--max-instances 1` breaks the game, the budget-alert thresholds, and the ElevenLabs free-tier ceiling as the known limit on a public launch.

Update the AI-security rows: **S1** unchanged and still the sharpest gap, **S4** now done, **S6** now done, **S8** now satisfied by `PRIVACY.md`.

Follow the maintenance protocol already written at the top of that file: update the row, write the evidence, bump the date. Do not mark a row ☑ on the strength of code existing.

- [ ] **Step 5: Rewrite the setup instructions**

`README.md` and `Sidecar/README.md` both walk a reader through installing Python and running a local sidecar. Restructure around three modes, matching the spec:

1. **Play the game** — nothing to install; the build talks to the hosted backend.
2. **Run the backend locally in Docker** — the demo fallback; the `docker run` from Task 6 step 5.
3. **Develop on the backend** — venv, `pip install -r requirements.txt`, `gcloud auth application-default login`, `.env` from `.env.example`.

Record the Task 7 deploy commands in `Sidecar/README.md` so a redeploy is not archaeology. Note that `faster-whisper` is gone and that `GEMINI_API_KEY` is no longer used.

- [ ] **Step 6: Check no doc still claims on-device audio**

```bash
grep -rniE "never leaves|on-device|stays on your (machine|computer)|local(ly)? (only|transcri)" README.md docs/ Sidecar/README.md
```
Expected: no hits that assert audio stays local. Any survivor is a claim that will be read by a judge.

- [ ] **Step 7: Run the full suite and commit**

Run: `cd Sidecar && python3 -m pytest tests/ -q`
Expected: PASS, 69 tests

```bash
git add docs/PRIVACY.md docs/CONCEPT.md docs/ROADMAP.md README.md Sidecar/README.md Sidecar/config.py
git commit -m "docs: state the hosted backend's privacy boundary"
```

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
|---|---|
| §1 target architecture | 6, 7 |
| §2 pinned session state, `SessionStore` escape hatch | 1, 7 (`--max-instances 1`) |
| §3 STT → Google | 2 |
| §4 Vertex | 3 |
| §5 auth, `/debug/last_turn` removal, cost cap, budget alert, binding, secrets | 4, 5, 6, 7 |
| §6 container, ffmpeg, CPU torch, baked weights | 6 |
| §7 Unity | 8 |
| §8 documentation | 9 |
| §9 testing | tests in 1–5; manual verification in 6 step 6, 7 step 8, 8 step 6; latency in 7 step 9 |
| §10 order of work | task order matches |

**Two spec items deliberately not given their own task:** per-IP rate limiting (spec §5 calls Cloud Armor the better answer and puts it out of scope this week) and the Firestore store (spec §2, explicitly "designed in but not built" — Task 1 provides the seam). Both stay in the roadmap.

**Known gap:** the plan does not add a test asserting `--max-instances 1`, because that is deployment configuration rather than code. Task 7 step 6 and the Task 9 §9 rewrite both call out why removing it breaks the game; that is the mitigation.

**Type consistency:** `InMemorySessionStore.commit` returns `list[str]` and Task 5 iterates it — consistent. `stt.transcribe` is async-bytes in Task 2 and awaited that way in `app.py` and in the Task 2 step 8 stub — consistent. `auth.CLIENT_KEY_HEADER` is `"x-fp-client-key"` lowercase for the middleware's case-insensitive lookup; Unity sends `"X-FP-Client-Key"` in Task 8, which HTTP header semantics make equivalent — intentional, noted here so it does not read as a mismatch. `TurnLimiter.admit` returns `""` for allowed in both `limits.py` and the `app.py` call site — consistent.

**Test count arithmetic:** 47 → 51 (Task 1) → 56 (Task 2) → 58 (Task 3) → 63 (Task 4) → 69 (Task 5). Tasks 6–9 add no automated tests.
