# Interrogation Sidecar

A local FastAPI process that does everything the game's voice loop needs
outside Unity: speech-to-text, speech emotion recognition, the officer's LLM
reply, and the officer's TTS voice. Unity never holds an API key — both keys
live only in this folder's `.env`, and this process only ever binds to
`127.0.0.1`.

See [`../docs/HUBERT_ORCHESTRATION_PLAN.md`](../docs/HUBERT_ORCHESTRATION_PLAN.md)
for the primary-source HuBERT research, model limits, reviewed architecture,
and reliability policy. This file contains the practical setup/run steps.

## One-time setup

1. Install **Python 3.10–3.12** and make sure it's on PATH.
2. Install **ffmpeg** and make sure it's on PATH (used to decode MP3 if your
   ElevenLabs plan doesn't grant PCM output directly — see `tts.py`).
3. Copy `.env.example` to `.env` and fill in:
   - `GEMINI_API_KEY` — from aistudio.google.com/apikey. The model is
     `gemini-3.6-flash` (the `MODEL` constant at the top of `llm.py`);
     current per-token pricing is at ai.google.dev/pricing.
   - `ELEVENLABS_API_KEY` — from elevenlabs.io. The free tier is enough for
     light testing only; a paid plan (Starter or above) is realistically
     needed for actual play.
   - `ELEVENLABS_VOICE_ID` — **not just any stock voice**: ElevenLabs' free
     tier only grants API access to voices you created yourself, which
     excludes the Voice Library *and* the "premade" voices (Adam, Rachel,
     Bill, …) shown in most walkthroughs — calling `/turn` with one of
     those returns `402 payment_required`. Run `python tools/probe_tts.py`
     (after the venv/deps below are set up) to find a voice ID your account
     can actually use over the API — it makes a real `convert()` call per
     candidate rather than trusting the visible voice list, since a voice
     being visible isn't the same as it being usable.
     **Already probed on this account**: `XrExE9yKIg1WjnnlVkGX` (Matilda) is
     one of 21 usable voices found, and is independently verified end-to-end
     via `tools/probe_full_turn.py` (real LLM → TTS → PCM audio, no
     fallback). It's also a female voice, matching Detective Mara Voss (the
     officer persona in `llm.py`). Set
     `ELEVENLABS_VOICE_ID=XrExE9yKIg1WjnnlVkGX` unless you'd rather pick a
     different one from the probe's full list.

Everything else — speech-to-text (`faster-whisper`) and emotion recognition
(`superb/hubert-base-superb-er`) — runs **fully locally, no key, no cost**.
Both models download to the Hugging Face cache the first time the sidecar
starts, which is why first launch can take a few minutes.

### Affect orchestration controls

The default HuBERT checkpoint predicts only neutral, happy, angry, and sad and
is not a fear or deception detector. The sidecar retains its full probability
distribution and combines it with uncertainty, pauses, energy/pitch variation,
hidden-state change, response onset, and an in-memory early-session reference.
Raw cosine distance remains debug data; actionable change is normalized to the
spread of that player's early reference turns. Only a successfully synthesized
turn is committed, preventing TTS retries from double-counting it. Low-quality
readings are visible in debug data but suppressed from Gemini, and witness text
is escaped into a separate trust block so it cannot imitate the sensor marker.
HuBERT and the classical features inspect the same `HUBERT_MAX_SECONDS` prefix;
Whisper still receives the complete bounded utterance for transcription.
When a turn exceeds that affect window, its full duration remains observable,
the signal is flagged `affect_window_truncated`, and session speech-rate
comparison is suppressed rather than mixing full-transcript words with a
partial acoustic window.

All controls are optional and documented in `.env.example`. The quickest
rollback is `PROSODY_ENABLED=false`: Whisper, Gemini, and TTS continue normally
without loading HuBERT. `/health` reports whether affect is enabled and loaded,
the exact checkpoint, device, orchestration version, and a bounded load-error
category. A HuBERT load or inference failure never cancels a dialogue turn.
`HUBERT_DEVICE=auto` chooses CUDA when present and CPU otherwise. MPS is an
explicit opt-in because tested Apple builds can terminate during model warm-up
instead of raising a recoverable Python exception.
`PROSODY_MIN_CONFIDENCE` is capped at `0.75`, matching the maximum confidence
the conservative policy can emit.
An overridden `HUBERT_MODEL_ID` must expose exactly the neutral, happy, angry,
and sad labels; startup rejects incompatible classifiers instead of silently
zeroing Unity's fixed four-label debug DTO.

## Running it

**Manually** (recommended while iterating on the Python side):

```
run_sidecar.bat
```

This creates/reuses a `.venv`, installs `requirements.txt`, and starts the
server on `127.0.0.1:8765`. Leave the console window open — Python
tracebacks show up there.

**Automatically**: Unity's `SidecarProcessLauncher` checks `GET /health`
first and starts this same script if nothing answers, so you can also just
press Play in the Editor with the sidecar not already running. A manually
started sidecar (the normal iteration workflow) is transparently reused
either way.

## Testing it in isolation (do this before touching Unity)

The orchestration unit suite uses generated signals and needs no API keys,
network access, or model download. Run this from `Sidecar/`:

```
python -m unittest discover -s tests -v
```

To inspect a real file through Whisper and the richer HuBERT observation (this
does download/load both local models):

```
python tools/probe_stt_ser.py tools/sample.pcm
```

```
curl http://127.0.0.1:8765/health

curl -H "X-FP-Client-Key: YOUR_FP_CLIENT_KEY" -F "audio=@sample.pcm" -F "sample_rate=16000" -F "session_id=test" ^
     http://127.0.0.1:8765/turn
```

`sample.pcm` must be raw 16-bit little-endian PCM, mono, 16kHz (no WAV
header) — `Sidecar/tools/sample.pcm` is exactly that (a short synthesized
utterance), ready to use directly in the curl command above. To make a new
one: record a short WAV and strip its 44-byte header, or
`ffmpeg -i sample.wav -f s16le -ar 16000 -ac 1 sample.pcm`.

Decode the returned `audio_b64` field back to a file and play it to confirm
the whole STT → emotion → LLM → TTS chain works end to end, and note the
`*_ms` timing fields — that number is what decides whether the pipeline's
overall latency is acceptable before any Unity work depends on it.

POST with no `audio` part (or an empty one) triggers the officer's scripted
opening line instead of running STT/SER — this is what Unity calls at scene
start.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Launch status plus nested HuBERT availability/model/device |
| `POST` | `/turn` | Main pipeline; accepts optional `onset_delay_ms`, returns additive `prosody` |
| `POST` | `/session/reset` | Clears conversation history and the prosody reference together |

`/health` is open for the Unity launch check. `/turn` and `/session/reset` require an
`X-FP-Client-Key` header that matches `FP_CLIENT_KEY` in the sidecar environment. A missing or
wrong key receives `401`. The service has no debug-transcript endpoint.

A reset also invalidates state commits from any turn that was already in
flight, so an old history/reference cannot reappear after the reset completes.

## Troubleshooting

- **"missing required environment variable" and the process exits immediately**
  — `.env` doesn't exist or is missing a key. Copy `.env.example` to `.env`.
- **First `/turn` call is very slow** — models are still downloading/loading;
  wait for the "Models loaded. Ready." console line before testing.
- **A turn's `prosody.available` is false** — inspect that `/turn` response's
  `prosody.reliability_reason`: `prosody_disabled`, `hubert_load_failed`,
  `hubert_inference_failed`, and `feature_extraction_failed` name the degraded
  stage. For startup state, `/health` uses `prosody.error`: `loading`,
  `disabled`, empty when ready, or the bounded exception class from a failed
  load.
- **TTS request fails with `402 payment_required`** — the configured voice
  isn't API-usable on your plan (see the `ELEVENLABS_VOICE_ID` note above).
  Run `python tools/probe_tts.py` to find one that is; `tts.py` now surfaces
  this as a clear one-line error naming the cause rather than a raw HTTP
  header dump.
- **TTS request fails some other way** — check the API key has quota
  remaining and hasn't expired.
- **`pydub`/ffmpeg errors** — ffmpeg isn't on PATH; this only matters if your
  ElevenLabs plan doesn't grant PCM output (see `tts.py`'s fallback path).
- **Install fails with an `OSError`/"No such file or directory" mentioning a
  very long path under `elevenlabs\...`** — Windows' default 260-character
  `MAX_PATH` limit, tripped by that package's generated file names combined
  with this project's already-deep folder path. `run_sidecar.bat` works
  around this by creating its venv at `%SystemDrive%\fpsc_venv` instead of
  `Sidecar\.venv` — a short, fixed path outside the project tree. If you'd
  rather keep the venv local, enable "Win32 long paths" once
  (Settings → search "long paths" → toggle on, or
  `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled = 1` via
  an elevated PowerShell, no reboot needed on current Windows builds) and
  change `VENV_DIR` in `run_sidecar.bat` back to `.venv`.
