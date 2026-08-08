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

## Status — 6 Aug 2026

A **local vertical slice has previously run**: Unity microphone → speech-to-text → speech
emotion recognition → LLM reply → text-to-speech → playback with lip sync. The cloud migration
code now includes Google STT, Vertex Gemini, client-key auth, turn caps, and a Dockerfile. The
105-test offline suite passes, and the container boots with HuBERT ready and rejects an
unauthenticated turn.

**Deployed 6 Aug 2026.** The backend runs on Cloud Run at
`https://false-positive-backend-465469192069.us-central1.run.app`, with budgets, secrets and
runtime IAM in place. The credentialed chain is verified end to end — STT, affect, LLM and TTS
in 2351 ms — but by `curl` with a synthesized WAV, not from the game. **Still in flight:** the
committed Unity asset keeps the hosted URL and client key blank until Task 8 sets them through
the Inspector, so nothing has run from the client yet. Status:
[`docs/ROADMAP.md` §9](docs/ROADMAP.md#9-distribution-hosted-backend-migration-record).

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
│  UNITY CLIENT                │        │  BACKEND — Google Cloud Run        │
│  Assets/_Project/            │        │  Sidecar/ · FastAPI · pinned to 1  │
│                              │ HTTPS  │                                    │
│  • first-person room, sit/   │◄──────►│  POST /turn  ← client key required │
│    stand camera              │  POST  │    ├─ Google Cloud STT   ──► API   │
│  • mic capture + VAD         │ /turn  │    ├─ hubert-base-superb-er        │
│  • cop lip sync + idle anim  │        │    │    affect + change  IN-PROC   │
│  • debug overlay (F1)        │        │    ├─ Gemini 3.6 Flash   ──► Vertex│
│  ships the client key ───────┼───────►│    └─ ElevenLabs TTS     ──► API   │
│  no vendor api keys          │        │  vendor keys in Secret Manager     │
└──────────────────────────────┘        └────────────────────────────────────┘
```

**Why a separate backend rather than everything in Unity:** the speech models are PyTorch and
have no honest path into a Unity build, and a vendor API key embedded in a game binary is
extractable in minutes. The backend is the sole holder of those credentials.

**Why it is hosted rather than local.** Until 4 Aug this same process ran on the player's own
machine over `127.0.0.1`. That made a downloadable build impossible — a stranger needed Python,
gigabytes of model weights, and two API keys of their own. Moving it to Cloud Run removes all
three, and **the cost is that player audio now leaves the machine**. That trade was made
deliberately; it is disclosed in [`docs/PRIVACY.md`](docs/PRIVACY.md) and recorded in
[`docs/ROADMAP.md` §9](docs/ROADMAP.md#9-distribution-hosted-backend-migration-record).

**The service is pinned to exactly one instance** (`--min-instances 1 --max-instances 1`).
Session history and the prosody baseline live in that process's memory, so a second instance
would make the detective forget the interrogation mid-scene and reset the affect baseline. The
flag is load-bearing, not a cost tweak.

**HuBERT is the emotion model here, not the speech recogniser.** Worth stating plainly, since
"HuBERT" is easy to read as ASR. Transcription is Google Cloud Speech-to-Text's job; HuBERT reads
tone, not words.
The checkpoint's full four-class distribution is combined with deterministic timing, pause,
pitch, energy, uncertainty, and hidden-state-change measurements. A session-local early reference
makes calibrated change more useful than an isolated label. Failed turns are not committed to that
reference, low-quality readings are suppressed, witness text is isolated from sensor context, the
model is optional at runtime, and the LLM receives only a bounded soft impression. It is never
framed as a lie detector. See the primary-source research and reviewed design in
[`docs/HUBERT_ORCHESTRATION_PLAN.md`](docs/HUBERT_ORCHESTRATION_PLAN.md).

## Setup

There are three ways to run this, depending on what you are trying to do.

### 1. Play the hosted game (after deployment)

Once Task 7 is complete, **nothing is installed on the player's machine**: the build talks to
the hosted backend over HTTPS. At the current checkpoint the hosted URL/key are intentionally
blank, so use the local Docker path below for backend testing.

### 2. Run the backend locally, in Docker

The demo fallback — and the thing to reach for if the venue's network is untrustworthy or the
Cloud Run service is off. Same image that runs in production.

```bash
cd Sidecar
docker build -t false-positive-backend:dev .
gcloud auth application-default login     # once

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

Then point Unity at it: clear `backendBaseUrl` on `Assets/_Project/Config/InterrogationConfig.asset`
so it falls back to `sidecarHost`/`sidecarPort`, or set it to `http://127.0.0.1:8080`, and set
`backendClientKey` to `local-dev-key`.

The first build bakes the HuBERT checkpoint into the image and takes a few minutes. It is not
hung.

### 3. Develop on the backend

```bash
cd Sidecar
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
gcloud auth application-default login     # Google STT and Vertex use ADC, not an API key
cp .env.example .env                      # then fill it in
python3 -m pytest tests/ -q               # offline: no network, no keys, no model download
```

`ffmpeg` must be on PATH — it is the MP3 decode fallback for ElevenLabs plans that do not grant
PCM output. Full steps, including the ElevenLabs free-tier gotcha that looks like a broken
pipeline but isn't: [`Sidecar/README.md`](Sidecar/README.md).

### Opening the Unity project

Unity **6000.5.6f1**. Open **`Assets/_Project/Scenes/_Persistent.unity`** and press Play — this
is the entry scene (build index 0) and the one `_Persistent`/`GameFlowDirector`/the whole flow
boot from. Pressing Play from any other scene still works: `PersistentSceneBootstrap` loads
`_Persistent` additively before anything else runs. The client checks `GET /health` on the
configured backend once the first live interrogation turn is reached — the menu, memory scenes,
and cutscenes need no backend at all.

## How to play

The whole game is playable end to end without any dialogue options — you speak, the officer
listens. Only P2 and P3's officer replies actually need the backend; everything else (menu,
both memory scenes, every cutscene, P1's scripted prompt, M1's yell gate) is local already.

**Flow:** Main Menu → mic consent card → mic calibration → **P1** (waking, "who are you?") →
**M1 — the cabin at night** (free-roam, fix the radio, call for Nick) → **P2** (recall, free
voice) → **M2 — the cabin at morning** (find the key, get outside, carry the body) → **P3**
(verdict — name a suspect, or don't) → one of four endings → the outcome card.

### Offline demo

The main menu has a second button, **Offline demo**, next to Play. It runs the identical
consent → calibration → P1 → … → outcome flow — the mic is still required, since P1's spoken
prompt and M1's yell gate are local-only either way — but P2 and P3 replace the live officer
with a fixed, pre-written Spassky script (14 authored lines, real ElevenLabs VO) instead of a
sidecar call. Use this to play or demo the full story with no backend running at all.

Offline demo is **honestly labelled, not a substitute for the real interrogation**: an
"OFFLINE — scripted interrogation" badge stays on screen throughout, the officer cannot react
to anything you actually say (there is no speech-to-text without the backend), and the ending
is always the David ending, since naming a suspect requires a transcript the offline path
doesn't have. Normal **Play** is unchanged and still needs the sidecar for P2/P3.

**Controls**

| Input | Action |
|---|---|
| WASD + mouse | Move / look |
| Hold **E** | Interact with whatever the crosshair is on |
| Speak | Answer the officer, or satisfy an on-screen speech prompt |
| Speak loudly | The one gate that requires volume — calling for Nick at the cabin door |
| **F1** | Debug overlay — turn state, VAD, story marks |
| **F2** | *(Editor / dev builds only)* Force-advance to the next phase, skipping its normal completion trigger |
| **Esc** | Settings / pause |

**What needs the backend:** only the officer's live replies in P2/P3 (P1 is local, see above).
If you press normal **Play** with the sidecar down, those two phases will not receive a reply —
after three failed turns in a row the console logs a clear pointer to Offline demo, but there is
still no in-fiction fault card yet (that's `A13`, Day-2 scope). Use **Offline demo** to avoid
this entirely.

**Honesty notes**, per `docs/GAME_COMPLETION_PLAN.md` §10's "never fake" rule:

- Every cutscene is the documented **cheap form** — a fade, a subtitle, and either recorded VO or
  a diegetic sound effect — not a Unity Timeline sequence, even though `com.unity.timeline` is in
  the manifest.
- The four endings are picked by a **client-side stopgap**: whichever suspect's name you say
  unambiguously in P3, or nobody's, falls to the David ending. The full credibility/fabrication/
  clue-citation rule in `docs/STORY_SCRIPT.md` §8 is Day-2 scope (`A10`).
- The outcome card is a fixed closing line, not yet the verbatim-quote version (`A11`).
- All character voice lines under `Assets/_Project/Art/Audio/VO/` are ElevenLabs-generated —
  synthetic, not recordings of real people.
- **Offline demo** plays a fixed script, not a reactive conversation — it exists so the full
  flow is playable with no backend, not as a claim that the interrogation mechanic works
  offline. It is labelled on screen for the entirety of P2/P3 so it is never mistaken for the
  real thing.

## Documentation

| Doc | What it is |
|---|---|
| [`docs/CHALLENGE_BRIEF.md`](docs/CHALLENGE_BRIEF.md) | The Garena case brief — deliverables, judging criteria and weights, rules, timeline. **Authoritative.** |
| [`docs/CONCEPT.md`](docs/CONCEPT.md) | The shortlisted pitch verbatim, the non-negotiables, decisions taken, and what is still open. |
| [`docs/DELIVERABLES.md`](docs/DELIVERABLES.md) | Live checklist for the three submission items and the submission mechanics. |
| [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) | Ownership, contracts between components, and the schedule to 9 Aug. |
| [`docs/HUBERT_ORCHESTRATION_PLAN.md`](docs/HUBERT_ORCHESTRATION_PLAN.md) | HuBERT primary-source research, model limits, orchestration design, failure policy, and test plan. |
| [`docs/UNITY_CLIENT.md`](docs/UNITY_CLIENT.md) | Client and pipeline build notes — cop rigging, lip sync, VAD, debugging, proven-vs-wired status. |
| [`docs/PRIVACY.md`](docs/PRIVACY.md) | What happens to the player's voice, in plain language. Required by the brief and by the concept's non-negotiables. |
| [`Sidecar/README.md`](Sidecar/README.md) | Backend setup, deploy commands, endpoints, troubleshooting. |

## Third-party components

Disclosure of every third-party library, model, dataset and API is a **required** submission item.
Rows marked **⚠** still need their licence confirmed by the owner of that area before the freeze.

### Models, datasets and APIs

| Component | Type | Used for | Licence / terms |
|---|---|---|---|
| `superb/hubert-base-superb-er` | Model | Four-class speech-affect observation plus hidden representations | Apache-2.0 (model card) |
| **IEMOCAP** | Dataset | Training data behind the checkpoint above | ⚠ **Restrictive academic licence.** We use released weights, not the corpus — but G4 covers datasets, so the lineage must be disclosed and the terms checked |
| Poly Haven asset set | 3D models and PBR textures | Interrogation desk, chairs, light, binder and room surfaces; cabin sofa and boots | CC0 1.0 — exact asset IDs, authors, source URLs and verified hashes in [`Assets/_Project/Art/PolyHaven/README.md`](Assets/_Project/Art/PolyHaven/README.md) |
| ~~Whisper `small.en`~~ | Model | ~~Speech-to-text weights~~ | **Removed 4 Aug** — replaced by Google Cloud Speech-to-Text |
| Google Cloud Speech-to-Text v2 | API | Speech-to-text (`short` recognizer, pinned) | Google Cloud Terms of Service ⚠ confirm commercial-use terms |
| Gemini 3.6 Flash (via Vertex AI) | API | Detective dialogue | Google Cloud Terms of Service ⚠ confirm commercial-use terms |
| ElevenLabs TTS | API | Detective's voice | ElevenLabs commercial terms ⚠ confirm plan tier and any attribution requirement |
| ElevenLabs Sound Generation | API | Ambient/diegetic SFX for cutscenes and the main menu storm bed (`Assets/_Project/Art/Audio/SFX/`) — synthetic, not field recordings | ElevenLabs commercial terms ⚠ confirm plan tier |
| Google Cloud Run | Service | Hosts the backend | Google Cloud Terms of Service |

### Libraries and tools

| Component | Type | Used for | Licence |
|---|---|---|---|
| Unity 6 (`6000.5.6f1`) | Engine | Game client | Unity proprietary EULA ⚠ confirm licence tier for submission |
| FastAPI | Python lib | Sidecar HTTP server | MIT |
| Uvicorn | Python lib | ASGI server | BSD-3-Clause |
| ~~faster-whisper~~ | Python lib | ~~Local STT runtime~~ | **Removed 4 Aug** — no longer a dependency |
| `google-cloud-speech` | Python lib | Speech-to-Text v2 client | Apache-2.0 |
| Docker | Tool | Container build for Cloud Run | Apache-2.0 (build-time tool, not distributed) |
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

No vendor API keys, passwords, or `.env` files are ever committed — the brief prohibits it
explicitly. Locally they live only in `Sidecar/.env`, which is git-ignored; in production they
live in Google Secret Manager. `Sidecar/.env.example` documents the required variable *names*
only. Google Speech-to-Text and Vertex AI have no API key at all — they authenticate as the
runtime service account.

**One deliberate deployment exception:** the shared client key will be committed in
`Assets/_Project/Config/InterrogationConfig.asset` when Task 7 produces it. It is blank at this
checkpoint. A shipped build must carry a copy, so it is extractable from any download: a speed
bump against drive-by traffic, **not a security boundary**. The in-process turn caps are only a
checkpoint guard; a public deployment still needs durable per-client limits and provider-side
hard quotas to bound cost across restarts.

## Voice data

Player-facing version, in plain language: [`docs/PRIVACY.md`](docs/PRIVACY.md). Summary:

**Player audio leaves the machine.** Each detected utterance is uploaded over HTTPS to the
hosted backend, where Google Cloud Speech-to-Text transcribes it and HuBERT reads it for tone.
This changed on 4 Aug 2026 — before that everything ran locally, and this section claimed the
opposite. The trade was made deliberately so the game can ship as a plain download; see
[`docs/ROADMAP.md` §9](docs/ROADMAP.md#9-distribution-hosted-backend-migration-record).

What has *not* changed:

- **Audio is never written to disk.** It exists in the backend's memory for the duration of one
  turn and is then discarded. There is no recording archive, and none of it is used for training.
- **Only text and a bounded affect impression reach Gemini.** Raw class vectors, embeddings and
  the early-session reference stay inside the backend process.
- **The transcript is held in memory only**, for the length of one interrogation, so the
  detective can catch contradictions. It is dropped on session reset, session eviction, or
  restart.
- **No account, no identity.** Sessions are keyed by a random GUID the client mints at scene
  start.
- **Nothing claims to detect deception.** The system measures affect and says so.

The remaining gap is the in-game notice before the first recording — the doc exists, the
in-product disclosure does not. Tracked as S8 in the roadmap.
