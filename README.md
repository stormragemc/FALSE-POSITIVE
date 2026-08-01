# FALSE POSITIVE

**An AI-powered psychological mystery game.**
*The detective can hear your fear. It cannot tell why you are afraid.*

You witness a crime through brief, chaotic, incomplete glimpses. Then you are interrogated —
hands-free, by voice, with no dialogue options — by an autonomous AI detective. You can tell
the truth, hide your uncertainty, protect someone, or lie. The detective reads two channels at
once: an LLM tracking meaning, timeline and consistency, and HuBERT-based speech
representations reading vocal affect. It never claims to know that you are lying. It only knows
that something moved you — and fear might be guilt, faulty memory, or the pressure of being
falsely accused.

Submission for the **Garena AI Build Challenge 2026** (theme: *Reimagine Digital Entertainment
Experiences*). Shortlisted; deliverables due **9 Aug 2026**.

---

## Status — 1 Aug 2026

A **working vertical slice exists**: Unity client plus a local Python sidecar running the full
voice loop — microphone → speech-to-text → speech emotion recognition → LLM reply → text-to-speech
→ playback with lip sync.

What is proven, and what is merely wired but not yet observed running, is tracked precisely in
[`docs/UNITY_CLIENT.md`](docs/UNITY_CLIENT.md). That distinction is deliberate and load-bearing:
the brief judges the prototype live, so nothing here claims more than it does.

**Not yet built** — the parts that carry the pitch rather than the plumbing: consistency tracking
across the interrogation, structured detective tactics with a visible reasoning note, the case as
authored data, and the outcome/endings model. See
[`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md).

## Architecture

Two halves, one boundary between them.

```
┌──────────────────────────────┐        ┌────────────────────────────────────┐
│  UNITY CLIENT                │        │  SIDECAR — local Python, 127.0.0.1 │
│  Assets/_Project/            │        │  Sidecar/ · FastAPI · port 8765    │
│                              │  HTTP  │                                    │
│  • first-person room, sit/   │◄──────►│  POST /turn                        │
│    stand camera              │  POST  │    ├─ faster-whisper   STT  LOCAL  │
│  • mic capture + VAD         │ /turn  │    ├─ hubert-base-superb-er        │
│  • cop lip sync + idle anim  │        │    │    emotion         LOCAL      │
│  • debug overlay (F1)        │        │    ├─ Gemini 3.6 Flash  ──► API    │
│                              │        │    └─ ElevenLabs TTS    ──► API    │
│  ships NO api key ───────────┼───────►│  keys live only in Sidecar/.env    │
└──────────────────────────────┘        └────────────────────────────────────┘
```

**Why a sidecar rather than everything in Unity:** the speech models are PyTorch and have no
honest path into a Unity build; and an API key embedded in a game binary is extractable in
minutes. The sidecar binds `127.0.0.1` only and is the sole holder of credentials.

**HuBERT is the emotion model here, not the speech recogniser.** Worth stating plainly, since
"HuBERT" is easy to read as ASR. Transcription is Whisper's job; HuBERT reads tone, not words.
Its output is a 4-class label plus a confidence score, and it is framed to the LLM as a soft
impression rather than a fact — the detector is frequently wrong, and the prompt says so.

## Setup

Full steps, including the ElevenLabs free-tier gotcha that looks like a broken pipeline but
isn't: [`Sidecar/README.md`](Sidecar/README.md).

1. Python 3.10–3.12 and `ffmpeg` on PATH.
2. Copy `Sidecar/.env.example` to `Sidecar/.env` and fill in `GEMINI_API_KEY`,
   `ELEVENLABS_API_KEY`, `ELEVENLABS_VOICE_ID`. The sidecar fails fast with a named error if any
   is missing.
3. Run `Sidecar/run_sidecar.bat` — creates the venv, installs deps, serves on `127.0.0.1:8765`.
4. Open in Unity **6000.5.6f1**, load `Assets/_Project/Scenes/Interrogation.unity`, press Play.
   Unity auto-launches the sidecar if nothing answers `GET /health`.

First run downloads the Whisper and HuBERT checkpoints to the HuggingFace cache and can take
several minutes. It is not hung.

## Documentation

| Doc | What it is |
|---|---|
| [`docs/CHALLENGE_BRIEF.md`](docs/CHALLENGE_BRIEF.md) | The Garena case brief — deliverables, judging criteria and weights, rules, timeline. **Authoritative.** |
| [`docs/CONCEPT.md`](docs/CONCEPT.md) | The shortlisted pitch verbatim, the non-negotiables, decisions taken, and what is still open. |
| [`docs/DELIVERABLES.md`](docs/DELIVERABLES.md) | Live checklist for the three submission items and the submission mechanics. |
| [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) | Ownership, contracts between components, and the schedule to 9 Aug. |
| [`docs/UNITY_CLIENT.md`](docs/UNITY_CLIENT.md) | Client and pipeline build notes — cop rigging, lip sync, VAD, debugging, proven-vs-wired status. |
| [`Sidecar/README.md`](Sidecar/README.md) | Sidecar setup, endpoints, troubleshooting. |

## Third-party components

Disclosure of every third-party library, model, dataset and API is a **required** submission item.
Rows marked **⚠** still need their licence confirmed by the owner of that area before the freeze.

### Models, datasets and APIs

| Component | Type | Used for | Licence / terms |
|---|---|---|---|
| `superb/hubert-base-superb-er` | Model | Speech emotion recognition (4-class + confidence) | Apache-2.0 ⚠ confirm on the model card |
| **IEMOCAP** | Dataset | Training data behind the checkpoint above | ⚠ **Restrictive academic licence.** We use released weights, not the corpus — but G4 covers datasets, so the lineage must be disclosed and the terms checked |
| Whisper `small.en` | Model | Speech-to-text weights | MIT (OpenAI) |
| Gemini 3.6 Flash | API | Detective dialogue | Google APIs Terms of Service ⚠ confirm commercial-use terms |
| ElevenLabs TTS | API | Detective's voice | ElevenLabs commercial terms ⚠ confirm plan tier and any attribution requirement |

### Libraries and tools

| Component | Type | Used for | Licence |
|---|---|---|---|
| Unity 6 (`6000.5.6f1`) | Engine | Game client | Unity proprietary EULA ⚠ confirm licence tier for submission |
| FastAPI | Python lib | Sidecar HTTP server | MIT |
| Uvicorn | Python lib | ASGI server | BSD-3-Clause |
| faster-whisper | Python lib | Local STT runtime | MIT |
| HuggingFace `transformers` | Python lib | Model loading / inference | Apache-2.0 |
| PyTorch | Python lib | Inference backend | BSD-3-Clause |
| `google-genai` | Python lib | Gemini client | Apache-2.0 |
| `elevenlabs` | Python lib | TTS client | MIT |
| soundfile | Python lib | Audio I/O | BSD-3-Clause |
| `soxr` | Python lib | Resampling | ⚠ LGPL-2.1 via libsoxr — confirm |
| pydub | Python lib | MP3 decode fallback | MIT |
| ffmpeg | Binary | MP3 decode fallback | ⚠ LGPL-2.1+ or GPL depending on build |
| python-dotenv | Python lib | Config loading | BSD-3-Clause |
| psutil | Python lib | Sidecar process management | BSD-3-Clause |
| uLipSync | Unity package | Lip sync (installed, currently inert) | MIT |
| Unity URP, Input System, AI Navigation, Burst, Mathematics, Timeline, uGUI, Visual Scripting | Unity packages | Engine features | Unity Companion License ⚠ |
| Avaturn | Service | Source of the cop avatar (`cop.glb`, T1 export) | ⚠ Avaturn terms — confirm redistribution rights for a public repo |
| Blender | Tool | Headless rigging of the cop model | GPL-3.0 (build-time tool, not distributed) |

## Credentials

No API keys, passwords, or `.env` files are ever committed — the brief prohibits it explicitly.
Keys live only in `Sidecar/.env`, which is git-ignored. `Sidecar/.env.example` documents the
required variable *names* only.

## Voice data

The player's microphone audio is processed **entirely on the local machine** — Whisper and HuBERT
both run inside the sidecar, and the audio itself is never uploaded. Only the resulting *text*
transcript and the emotion label leave the machine, sent to the Gemini API to generate the
detective's reply; the reply text is then sent to ElevenLabs to be spoken. Audio is held in memory
for the duration of a turn and is not written to disk.
