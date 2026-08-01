# Interrogation Sidecar

A local FastAPI process that does everything the game's voice loop needs
outside Unity: speech-to-text, speech emotion recognition, the officer's LLM
reply, and the officer's TTS voice. Unity never holds an API key — both keys
live only in this folder's `.env`, and this process only ever binds to
`127.0.0.1`.

See the implementation plan (`C:\Users\Giorg\.claude\plans\devise-a-great-and-gentle-wolf.md`)
for the full architecture and reasoning; this file is just the practical
setup/run steps.

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

```
curl http://127.0.0.1:8765/health

curl -F "audio=@sample.pcm" -F "sample_rate=16000" -F "session_id=test" ^
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
| `GET` | `/health` | `{status, models_loaded, version}` — also the launch gate |
| `POST` | `/turn` | The main pipeline call — see above |
| `POST` | `/session/reset` | Clears one session's conversation history |
| `GET` | `/debug/last_turn` | Dumps the last `/turn` response, for curl debugging |

## Troubleshooting

- **"missing required environment variable" and the process exits immediately**
  — `.env` doesn't exist or is missing a key. Copy `.env.example` to `.env`.
- **First `/turn` call is very slow** — models are still downloading/loading;
  wait for the "Models loaded. Ready." console line before testing.
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
