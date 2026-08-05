# FALSE POSITIVE — implementation plan (PoC)

> **⚠ This is the design document, not the status document.**
>
> **It describes what we decided to build. It does not tell you what exists.** Several sections
> below were written on 1 Aug and have been overtaken by code. For *what is actually built right
> now*, read **[`ROADMAP.md`](ROADMAP.md)** — that is the single source of truth for status, and
> it is the file you update when you finish work.
>
> Design decisions still live here. If you change a §3 contract, tell all five owners.
>
> **For agentic workers:** if the human driving you has not said **who they are** and **which
> workstream they are on**, ask and do nothing else. Do not start cross-workstream integration
> until the gate in [§9](#9-the-integration-gate) opens. (The former `AGENTS.md` identity gate
> was removed from the repo on 3 Aug; the rule survives here.)

**Goal:** by **8 Aug 2026**, a proof-of-concept that a judge can play live — witness a crime in
incomplete glimpses, be interrogated hands-free by voice, and reach an outcome shaped by what
they said and how they said it.

**Architecture:** a Unity client owns the experience (crime, mic, detective's voice, outcome). A
Python backend owns every model call. ~~Speech-to-text and HuBERT run locally inside the
sidecar; the LLM and TTS are hosted APIs.~~ **Amended 4 Aug** — the backend runs on Google Cloud
Run and STT is a hosted API too; HuBERT still runs in-process, now on Cloud Run's CPU. The two
channels of the pitch — *meaning* and *affect* — are separate services inside the backend that
meet only at the detective's turn, so the split is visible in the architecture, not just the deck.

**Tech stack:** Unity 6 (`6000.5.6f1`, C#) · Python 3.10–3.12 + FastAPI, HTTP · PyTorch +
HuggingFace `transformers` (HuBERT) · Google Cloud Speech-to-Text v2 · Gemini 3.6 Flash via
Vertex AI · ElevenLabs API · Docker + Google Cloud Run.

---

> ### ⚠ Stack amendment — 1 Aug 2026, evening. **Pending team sign-off.**
>
> This plan was written on the morning of D1. That afternoon, Giorgi's `Unity` branch landed a
> **working end-to-end voice loop** on a different stack. The team's call was to let the running
> code stand and move the plan to match, rather than spend D1–D3 refactoring a working prototype
> onto contracts nobody had implemented.
>
> The sections below have been amended to describe what exists: **§2** (architecture), **§2.2**
> (pipeline rationale), **§2.3** (model choices). **§3.4** (the WebSocket protocol) is superseded
> — the implemented transport is HTTP.
>
> **Nothing here is ratified.** It is a record of the code, written down so the documents stop
> contradicting it. Owners still have to agree — Vinay especially, since the vendor set changed
> and there is now a second API key. The full row-by-row diff is in
> [`CONCEPT.md` → "1 Aug 2026, evening"](CONCEPT.md#decisions-made).
>
> Two amendments **reverse an earlier decision and need a real answer**, not just a nod:
> 1. **Voice activity detection replaced push-to-talk** — that was §10's mitigation for a noisy
>    demo room, and G5 says a stage that only works in a quiet room is worth zero.
> 2. **`superb/hubert-base-superb-er` (4 classes + confidence) replaced the 13-field
>    `ProsodySignal`** in §3.1. The pitch's HuBERT claim survives; the *richness* the detective
>    was designed around does not. **Marcel's call.**

---

> ### ⚠ Stack amendment — 4 Aug 2026. **The backend moved to the cloud.**
>
> The whole Python sidecar now runs on **Google Cloud Run**, so the shipped build is a plain
> binary that talks to a URL and needs no Python, no model downloads, and no keys of the player's
> own. This is what makes an itch.io release possible at all.
>
> What it changes in this document:
> - **STT is Google Cloud Speech-to-Text v2**, not local `faster-whisper`. §2's "no audio egress"
>   argument and the §2.3 transcription row are struck through below.
> - **Gemini is reached through Vertex AI**, authenticated as the runtime service account. The
>   long-lived `GEMINI_API_KEY` is gone; Vinay owns the project and its billing.
> - **The build now ships one key** — a shared client key gating the public URL. §2.1's "ships no
>   key" argument is amended, not abandoned: no *vendor* key ships, and the client key is
>   extractable by design.
> - **The privacy position inverted.** Player audio leaves the machine. Disclosed to players in
>   [`PRIVACY.md`](PRIVACY.md) and recorded in [`ROADMAP.md` §9](ROADMAP.md#9-distribution-hosted-backend-migration-record).
>
> Design: [`superpowers/specs/2026-08-04-cloud-hosted-backend-design.md`](superpowers/specs/2026-08-04-cloud-hosted-backend-design.md).

---

## Global constraints

Every task inherits these. They come from [`CHALLENGE_BRIEF.md`](CHALLENGE_BRIEF.md) and
[`CONCEPT.md`](CONCEPT.md) and are not negotiable at task level.

| # | Constraint | Source |
|---|---|---|
| G1 | **Submission 9 Aug 2026.** Code freeze **8 Aug 23:59**. Nothing is modified after the email goes out. | Brief |
| G2 | **The repo is public.** No keys, no `.env`, no personal data, no voice recordings of real people. | Brief |
| G3 | **Prompts and agent configs are a graded deliverable** — they live in `prompts/` as files, never as inline string literals. | Brief |
| G4 | **Every third-party library, model, dataset and API is disclosed** in the README table with its licence, at the moment it is introduced. | Brief |
| G5 | **It must run end-to-end in a live setting.** A stage that only works in a recording is worth zero. | Brief, Build Quality |
| G6 | **The system detects affect, never lies.** No truth meter, no "deception" field in any schema, no "lie detected" copy anywhere. | CONCEPT non-negotiable |
| G7 | **Voice only.** No dialogue-option menus, ever. | CONCEPT non-negotiable |
| G8 | **No replay of the crime.** Imperfect memory is the mechanic. | CONCEPT non-negotiable |
| G9 | **The detective's behaviour comes from model output**, not a branch table. | CONCEPT non-negotiable |
| G10 | Mocks, fallbacks and pre-recorded assets are **labelled as such** in the same breath as the claim. | CONCEPT honesty rule |

---

## 1. Team and ownership

Five people. One owner per box. The owner is the only person who merges into their directory.

| Person | Workstream | Owns | Primary directory |
|---|---|---|---|
| **Giorgi** | Unity client | The playable experience: crime sequence, mic capture, detective playback, notebook, outcome, all visible failure states | `unity/` |
| **Marcel** | Prosody / HuBERT | The affect channel. HuBERT + prosodic features → `ProsodySignal`. **Owner** — Bong and Ado assist, see §1.1 | `service/prosody/` |
| **Bong** | Detective agent | The opponent. Goal policy, prompts, tactic selection, consistency analyst. Also assists on prosody | `service/detective/`, `prompts/`, `service/prosody/` |
| **Vinay** | AI security | Key handling, prompt-injection defence, output safety, cost limits, voice privacy, red-team report | `service/security/`, `docs/SECURITY.md`, `docs/PRIVACY.md` |
| **Ado** | Core service + delivery | Sidecar skeleton, session orchestration, case data, consistency tracker, exception handling, tests, docs, CI. Also assists on prosody. **Agent-executable** — see [§8](#8-ados-backlog-agent-executable) | `service/core/`, `service/cases/`, `docs/`, `tests/`, `service/prosody/` |

**Ado does not touch `unity/`.** Per instruction. If Unity work is blocking, it goes to Giorgi.

**⚠ Paths as built.** The directories above are the *plan's* names. The merged tree puts the Unity
project and the sidecar at the repo root, because relocating a working project on D1 is churn.
**Ownership is unchanged** — only the paths move. Read the table through this map:

| Plan says | On disk | Owner |
|---|---|---|
| `unity/` | `Assets/`, `Packages/`, `ProjectSettings/` (repo root) | Giorgi |
| `service/core/` | `Sidecar/app.py`, `Sidecar/audio_utils.py` | Ado |
| `service/prosody/` | `Sidecar/ser.py` | Marcel (shared — §1.1) |
| `service/detective/` | `Sidecar/llm.py` | Bong |
| `prompts/` | **does not exist yet** — the detective's persona is an inline literal in `Sidecar/llm.py`. **This is a live G3 violation on a graded deliverable.** Bong extracts it; see §8. | Bong |
| `service/cases/`, `service/security/`, `tests/` | do not exist yet | Ado / Vinay / Ado |

`docs/` is unchanged and is Ado's. [`AGENTS.md`](../AGENTS.md) still lists the plan's paths; it
needs the same map before anyone drives an agent off it.

**Branches carry the owner's name:** `<name>/<short-kebab-summary>` — `giorgi/mic-capture-ptt`,
`marcel/hubert-baseline`, `bong/tactic-selection`, `vinay/output-safety-filter`,
`ado/session-orchestrator`. An agent that needs a new branch creates it under the name of the
person it is working for, and says so. Full rules in [`AGENTS.md`](../AGENTS.md).

### 1.1 `service/prosody/` is shared — three people, one owner

The affect channel is the highest-risk part of the build and the one that most needs to be
working by D4, so **Bong and Ado can both build inside `service/prosody/`**. Marcel remains the
owner. That means:

- **Marcel owns `ProsodySignal` (§3.1).** Bong and Ado implement against it; they do not change
  its shape. A field added or removed is Marcel's call and gets announced to all five.
- **Marcel reviews and merges** everything landing in `service/prosody/`, including work from the
  other two. Branch under your own name (`bong/...`, `ado/...`), not Marcel's.
- **Split by file, not by line.** Marcel takes the HuBERT path — checkpoint loading, hidden-state
  extraction, `hubert_instability`, `hubert_baseline_distance`. The classical signal-processing
  features (F0, energy, timing, pauses) and the test rig are separable work that Bong or Ado can
  own end-to-end without touching Marcel's files. See A14 and A15.
- **Bong's angle is the consumer's.** He is the one person who knows what the detective actually
  needs from the signal, so if a field reads well in the schema but is useless in a prompt, he
  should be the one to say so — before D4, not after.

If two of you are about to edit the same file, that is the moment to talk rather than merge.

---

## 2. Architecture

**As built, amended 4 Aug 2026.** Solid boxes exist and run. `┄` marks what is planned and not
yet written. The backend box moved from the player's machine to Cloud Run on 4 Aug; everything
inside it is otherwise the same code.

```
┌─────────────────────────────────┐         ┌──────────────────────────────────────────┐
│  UNITY CLIENT  (Giorgi)         │         │  BACKEND  Sidecar/  (Ado)                │
│  Assets/_Project/               │         │  FastAPI on Google Cloud Run             │
│                                 │  HTTPS  │  pinned --min/--max-instances 1          │
│  • mic capture 16 kHz mono      │  POST   │  ┌────────────────────────────────────┐  │
│  • voice activity detection     │  /turn  │  │ client-key gate → turn limiter     │  │
│    ⚠ was push-to-talk           │────────►│  │ → turn handler                     │  │
│  • detective audio playback     │  wav +  │  └───┬──────────┬──────────┬──────────┘  │
│  • cop lip sync + idle anim     │  key    │      │          │          │             │
│  • debug overlay (F1)           │◄────────│      ▼          ▼          ▼             │
│  ┄ crime sequence (3 glimpses)  │  mp3 +  │  ┌────────┐ ┌────────┐ ┌──────────────┐  │
│  ┄ detective's notebook panel   │  text   │  │AFFECT  │ │  STT   │ │ DETECTIVE    │  │
│  ┄ outcome screen               │         │  │Marcel  │ │  Ado   │ │ Bong         │  │
│  ┄ visible failure states       │         │  │hubert- │ │Google  │ │Gemini 3.6    │  │
└─────────────────────────────────┘         │  │superb  │ │STT v2  │ │Flash         │  │
                                            │  │-er     │ │ short  │ │ ──►Vertex AI │  │
   ships NO vendor key ─────────────────►   │  │IN-PROC │ │ ──►API │ │┄ analyst     │  │
   ⚠ ships the client key, extractable      │  └────────┘ └────────┘ └──────┬───────┘  │
                                            │       │                       │          │
                                            │       └── label + conf ───────┘          │
                                            │       ⚠ not ProsodySignal     ▼          │
                                            │                        ┌────────────┐    │
                                            │  ┄ security layer      │ TTS        │    │
                                            │  ┄ (Vinay) wraps ────► │ ElevenLabs │    │
                                            │  ┄ egress + output     │   ───►API  │    │
                                            └────────────────────────┴────────────┘────┘
                                                            │
                        Secret Manager ──► env              ▼  Google STT · Vertex · ElevenLabs
```

~~**Player audio never leaves the machine.** Both local models consume the waveform; only the
*transcript* and the *emotion label* go to Gemini, and only the reply *text* goes to ElevenLabs.
That is a stronger privacy position than this plan originally had — say it plainly in the deck.~~

**Superseded 4 Aug.** Player audio is uploaded to the hosted backend over HTTPS, where Google
STT transcribes it and HuBERT reads it for tone in-process. It is held for the length of one turn
and never written to disk. Gemini and ElevenLabs still see only *text*. **The deck must not claim
on-device privacy.** What it may claim is the true and narrower version, which is in
[`PRIVACY.md`](PRIVACY.md): nothing is stored, nothing trains a model, and there is no account.

### 2.1 Why a sidecar and not "all in Unity"

Three reasons, in order of weight:

1. **HuBERT is PyTorch.** There is no honest way to run it inside Unity in eight days. The pitch
   names HuBERT; the backend is what makes that claim true rather than aspirational. (Whisper was
   the second half of this argument until 4 Aug; STT is now a network call.)
2. **The *vendor* API keys must never ship in the game build.** A key embedded in a Unity binary
   is extractable in minutes. Since 4 Aug they live in Secret Manager and are injected into the
   Cloud Run container; the ElevenLabs key is the only one left, because Google is reached
   through the runtime service account. (G2, and Vinay's first task.)
   ⚠ **Amended 4 Aug:** the build now ships *one* key, the client key that gates the public URL.
   It is extractable, that is understood, and it is a speed bump rather than a boundary — the
   in-process turn caps are only a checkpoint guard. Durable per-client limits
   and provider-side hard quotas are still required to bound the public bill.
3. **Five people can work in parallel** behind one frozen HTTP contract.

This reasoning largely held. Reason 3 is untouched; reasons 1 and 2 survived the cloud migration
in amended form rather than being overturned.

### 2.2 Why a pipeline and not a speech-to-speech model — **settled by the implementation**

A speech-to-speech model (OpenAI's `gpt-realtime-2.1`, or Gemini's Live API) would give lower
latency and a more natural interruption feel. We are **not** using one. This was argued on D1
morning and then built the same way that afternoon, independently — which is about as strong a
confirmation as a design decision gets.

| | Pipeline (chosen, built) | Speech-to-speech |
|---|---|---|
| HuBERT gets raw audio | Yes, one obvious fork point | Yes, but the audio path is owned by the SDK |
| Two channels visible in architecture | **Yes — this is the pitch** | Blurred inside one model |
| Parallel work by 5 people | Each stage independently testable | One integration everyone waits on |
| Visible exception handling (scored) | Per-stage, easy to surface | Harder to decompose |
| Cost | ElevenLabs TTS + Gemini text tokens | Audio tokens in *and* out, ~an order of magnitude more |
| Latency | ~2.0–3.5 s per turn | ~0.5 s |
| Unity integration | Plain HTTP POST | WebRTC, fiddly in Unity |

Latency is the real cost, and it is now measurable rather than estimated — the sidecar reports
per-stage timings, and the F1 overlay shows them. Mitigations, all of which are also good drama:
stream TTS, start speaking on the first complete sentence, and give the detective a diegetic beat
— a pen tap, a page turn — while it thinks. A detective who pauses before answering is not a bug.

**Fallback path, labelled:** if the turn loop is still above 4 s on the demo machine by Day 6, the
labelled stretch is a speech-to-speech model for the *utterance only*, with affect and consistency
still on the pipeline. Do not start this before Day 6.

### 2.3 Model choices — ⚠ amended, pending sign-off

**This table describes what the code calls.** The morning-of-D1 version — all-OpenAI on team
credits — is preserved in [`CONCEPT.md`](CONCEPT.md#decisions-made) as superseded. ~~Two models
are **local**, so the player's audio never leaves the machine; two are **hosted APIs**, and they
are what the keys are for.~~ **Amended 4 Aug:** three of the four are hosted calls made from the
Cloud Run container. Only HuBERT runs in-process, and that process is now a server, not the
player's machine.

| Role | Model | Where | Why this one |
|---|---|---|---|
| Detective turn (in the loop, latency-critical) | `gemini-3.6-flash` | API, **via Vertex AI since 4 Aug** | Fast enough to sit on the critical path of every turn, cheap enough to iterate on all week, and long-context enough to carry the whole interrogation transcript as the consistency substrate. ⚠ **Reverses the all-OpenAI decision** — ~~needs a named budget owner~~ **Vinay, named 4 Aug**, who owns the GCP project the Vertex calls now bill to. |
| Consistency analyst (async, latency-tolerant) | *not yet implemented* | — | Consistency is *the* determining factor of the outcome (CONCEPT §4) and there is currently **nothing tracking it**. This is the single biggest gap in the build, and it is Ado's A6. |
| ~~Transcription~~ | ~~`faster-whisper`, `small.en`~~ | ~~**LOCAL**~~ | ~~Runs on the player's machine: no key, no per-minute cost, no audio egress.~~ **Superseded 4 Aug — see the row below.** |
| Transcription | Google Cloud Speech-to-Text v2, `short` recognizer | API | Removes multi-GB weights from the container and the single-worker CPU bottleneck from the turn loop, and bills to the GCP credits. It also answers the noisy-room and accented-speech risk the local model reopened — that was the original argument for a hosted ASR, and it wins again. Model id pinned deliberately (roadmap S7). |
| Detective's voice | ElevenLabs TTS | API | Best-in-class delivery for an interrogator, which is a character whose *voice* is most of the performance. ⚠ Second vendor, second key, and **not** on team credits. Free tier only grants API access to voices you created yourself — see `Sidecar/README.md`. |
| Affect | `superb/hubert-base-superb-er` | **IN-PROCESS** — baked into the Cloud Run image | The pitch names HuBERT, and this is HuBERT — fine-tuned on IEMOCAP for 4-class emotion recognition (neutral / happy / angry / sad) plus a confidence. Runs on CPU. ⚠ Far thinner than the 13-field `ProsodySignal` in §3.1: **Marcel's call** whether to enrich it with classical prosodic features or move the contract. ⚠ IEMOCAP carries a restrictive academic licence — the lineage is disclosed in the README and the terms still need checking (G4). |

**On the affect model's weakness — this is a feature, and the deck should say so.** The checkpoint
is roughly 0.68 accurate on its own benchmark, and the sidecar's own docstring calls it "a soft
impression, not ground truth". A frequently-wrong affect detector *is the thesis of the game.* The
title is `FALSE POSITIVE`. Do not quietly paper over the error rate; state it, and show the
detective acting on a reading that may be wrong. (G6, G10.)

**Degradation ladder** (Vinay owns, Ado wires) — **not built yet.** On Gemini timeout or 429:
retry once, then a scripted holding line, visibly labelled as a fallback. On TTS failure: show the
detective's line as text rather than dropping the turn. On empty transcription: the detective
reacts to the silence rather than the pipeline erroring. Never a silent failure — exception
handling is explicitly scored.

> Model IDs re-verified against the implementation on 1 Aug 2026. Re-verify against the vendors'
> live model lists before the freeze; if any ID has moved, the README disclosure table (G4) has to
> move with it.

---

## 3. Frozen contracts

**These are frozen at the end of Day 1 and are the reason five people can work at once.** Changing
one after Day 1 requires telling all five owners in the group chat. Everything below is version
`v1`; every payload carries `"v": 1`.

> **⚠ Status against the implementation — updated 4 Aug 2026.** Live status lives in
> [`ROADMAP.md`](ROADMAP.md); this table is a pointer, kept here because people read §3 directly.
>
> | | Status |
> |---|---|
> | **3.1 `ProsodySignal`** | ☑ **Implemented and no longer contested.** `Sidecar/prosody.py` emits every field below except `longest_pause_ms` and `utterance_id`, plus reliability/calibration metadata the contract did not anticipate. Design: [`HUBERT_ORCHESTRATION_PLAN.md`](HUBERT_ORCHESTRATION_PLAN.md). **Marcel's decision is resolved; Bong is unblocked.** |
> | **3.2 `DetectiveAction`** | ☐ Not implemented — `llm.py` returns plain dialogue text. Structured output is what makes the tactic *visible* (G9, and the notebook panel). **Bong's** to build, and it is on the critical path. |
> | **3.3 `CaseState`** | ☐ Not implemented — the case is a hardcoded premise string at `llm.py:35`. **Ado's** A5, critical path. |
> | **3.4 WebSocket protocol** | ❌ **Superseded.** Transport is HTTP `POST /turn`; see the amended §3.4. |

### 3.1 `ProsodySignal` — Marcel produces, Bong consumes

One per player utterance. Emitted by `service/prosody/`.

```jsonc
{
  "v": 1,
  "utterance_id": "u_007",
  "duration_ms": 4200,

  "onset_delay_ms": 1450,        // silence between the question ending and speech starting
  "speech_ratio": 0.62,          // voiced frames / total frames
  "long_pause_count": 2,         // pauses > 600 ms inside the utterance
  "longest_pause_ms": 910,

  "speech_rate_delta": -0.18,    // vs this player's calibration baseline, z-normalised
  "pitch_variability": 0.71,     // 0..1, normalised against baseline
  "energy_variability": 0.44,    // 0..1

  "hubert_instability": 0.66,    // mean frame-to-frame cosine distance, hidden layer 9, 0..1
  "hubert_baseline_distance": 0.52, // distance from the player's calibration centroid, 0..1

  "arousal": 0.68,               // 0..1 derived composite
  "tension": 0.73,               // 0..1 derived composite
  "confidence_in_signal": 0.81,  // 0..1 — how much this reading should be trusted

  "flags": ["long_onset_delay", "elevated_tension"]
}
```

Rules that are not stylistic:

- **There is no `deception`, `truthfulness`, `lie_probability` or equivalent field, and there
  never will be one.** (G6.) Vinay adds a schema test that fails the build if such a key appears.
- `confidence_in_signal` is the exception-handling hook. Under 1.5 s of audio, heavy clipping, or
  a failed baseline → it drops below `0.4`, and the detective is instructed to not press on
  prosody at all. This is how we avoid the system confidently reading noise.
- All the `*_delta`, `*_variability` and `*_distance` values are **relative to that player's own
  baseline**, captured during calibration (§4.1). Absolute prosody across different humans is
  meaningless; relative prosody is the honest version of the claim.

### 3.2 `DetectiveAction` — Bong produces, Ado and Giorgi consume

The structured output of the detective's turn.

```jsonc
{
  "v": 1,
  "tactic": "confront_contradiction",
  "target_claim_ids": ["c_003", "c_009"],
  "utterance": "You said the car was blue. Ten minutes ago it was 'too dark to tell'. Which was it?",
  "delivery": {
    "tone": "cold",              // warm | neutral | cold | weary | sharp
    "pace": "slow",              // slow | measured | fast
    "emphasis": ["blue", "dark"]
  },
  "internal_note": "1.4 s onset delay and tension 0.73 on the vehicle question, plus a hard contradiction on colour. Pressing the contradiction, not the affect.",
  "pressure_delta": 1,           // -2..+2
  "ends_interrogation": false
}
```

`tactic` enum — the only legal values:
`open_question` · `press_uncertainty` · `confront_contradiction` · `set_trap` ·
`feign_sympathy` · `change_subject` · `silence` · `accuse` · `close`

- The model **chooses** the tactic. There is no `if contradiction then confront` in our code.
  (G9.) Bong's prompt describes goals and pressure; it does not enumerate branches.
- `internal_note` is rendered in the detective's notebook panel. It is the single strongest
  "genuine AI-driven behaviour" evidence a judge can see, and it must never assert that the
  player lied — Vinay's output filter enforces this on the string.

### 3.3 `CaseState` — Ado owns, everyone reads

```jsonc
{
  "v": 1,
  "session_id": "s_01H...",
  "case_id": "case_01_the_stairwell",
  "turn": 7,
  "pressure": 3,                 // 0..10
  "claims": [
    {
      "id": "c_003", "topic": "vehicle_colour", "value": "blue",
      "turn": 2, "verbatim": "it was blue, I think", "hedged": true,
      "status": "contradicted"   // asserted | hedged | contradicted | retracted | corroborated
    }
  ],
  "contradictions": [
    { "claim_a": "c_003", "claim_b": "c_009", "topic": "vehicle_colour",
      "severity": 0.8, "turn": 9, "note": "colour asserted then denied as unobservable" }
  ],
  "unexplored_topics": ["second_voice", "time_of_arrival"],
  "consistency_score": 0.62      // 0..1 — drives the ending
}
```

### 3.4 HTTP protocol — Giorgi ↔ Ado — ⚠ **amended; supersedes the WebSocket design**

The WebSocket event protocol previously specified here was never built. Giorgi implemented
request/response over HTTP instead, and it works, so **HTTP is the contract**. Recorded here as
built, from `Sidecar/app.py`.

~~`http://127.0.0.1:8765`~~ **The Cloud Run HTTPS URL, from `InterrogationConfig.backendBaseUrl`**
(4 Aug; the loopback address remains the fallback for local development). One request per turn,
`multipart/form-data` in, JSON out.

**Every endpoint except `GET /health` requires the header `x-fp-client-key`** (4 Aug). A missing
or wrong key returns `401`; a server with no key configured rejects everything rather than
falling open.

| Endpoint | Request | Response |
|---|---|---|
| `GET /health` | — (no key required) | `{"status": "ok" \| "loading", "models_loaded": bool, "version": "0.3.0"}` — Unity polls this before the first turn. ~~and auto-launches the sidecar if nothing answers~~ **`autoLaunchSidecar` is off since 4 Aug; a failed probe is now an error message, not a launch.** |
| `POST /session/reset` | `session_id` | `{"ok": true}` — clears that session's turn history and prosody reference; paid-turn accounting is retained |
| `POST /turn` | `session_id`, `sample_rate` (default 16000), `audio` (16-bit PCM mono; **omit to make the detective open the interrogation**) | the turn payload below. `429` when a turn cap is hit, with `error` set to `session_turn_limit_reached` or `daily_turn_budget_exhausted` (4 Aug) |
| ~~`GET /debug/last_turn`~~ | — | ~~the last turn payload, success or failure. Feeds the F1 overlay~~ **Deleted 4 Aug** — it returned the last player's transcript to any caller, which is indefensible on a public URL. The F1 overlay reads the live `/turn` response instead. |

**Turn payload** — the same keys on success and failure, so the client has one shape to parse:

```jsonc
{
  "ok": true,
  "error": "",                  // populated, and HTTP 500, on any stage failure
  "transcript": "...",          // Google Cloud Speech-to-Text v2
  "emotion": "neutral",         // 4-class: neutral | happy | angry | sad
  "emotion_confidence": 0.61,
  "reply_text": "...",          // Gemini
  "audio_b64": "...",           // base64 PCM of the ElevenLabs speech
  "audio_sample_rate": 24000,
  "audio_channels": 1,
  "stt_ms": 0, "ser_ms": 0, "llm_ms": 0, "tts_ms": 0, "total_ms": 0
}
```

**Two things worth keeping.** STT and affect run *concurrently* on the same buffer via
`asyncio.gather` — they are independent reads, and serialising them would waste the cheaper of the
two. And the per-stage timings are already in the payload, which makes §2.2's latency argument
measurable instead of asserted.

**What HTTP costs us, to be honest about it.** No `transcript.partial` ghost text, no
`detective.thinking` beat, and no streamed audio — the player waits on one blocking response with
no feedback. That is a real UX regression against the WebSocket design and it will be visible on
stage. The cheap fix is client-side: Giorgi plays a diegetic beat (pen tap, page turn) while the
request is in flight. **Do not reopen the transport decision to get streaming back** unless the
turn loop is still unacceptable at D6.

**Still to add to this contract** (Ado, and the reason A5/A6 exist): `case_id` on the session,
`DetectiveAction` in place of bare `reply_text`, a consistency score in the payload, and a
structured `fault` object with `{stage, code, message, recovery}` so failures are *visible* rather
than a 500 with a stack-trace string (§6).

---

## 4. The experience being built

### 4.1 The loop

1. **Cold open — the crime.** Three glimpses, ~8 s total. Deliberately incomplete: a stairwell,
   a raised voice, a figure leaving. The player never sees the whole thing and **never gets a
   replay** (G8).
2. **Calibration, in-fiction.** The detective asks for name and where they were tonight. This is
   the neutral baseline for §3.1 — and it plays as the start of an interrogation, not a mic test.
   *If calibration fails, prosody runs in absolute mode with `confidence_in_signal` capped at
   0.5, and the notebook says so.*
3. **Interrogation.** 10–14 turns. Push-to-talk. Every answer forks: transcript → analyst;
   audio → HuBERT. Both meet at the detective's turn.
4. **Outcome.** Driven primarily by `consistency_score`, secondarily by pressure and by which
   topics stayed unexplored. Four endings (§4.3).

### 4.2 The case — `case_01_the_stairwell`

One case only. Ado authors it as data (`service/cases/case_01_the_stairwell.yaml`); it is not
hardcoded in the detective's prompt. It needs: ground truth of what happened, what the three
glimpses actually show, the cast, the topics the detective wants covered, and the traps
(details the player *cannot* have seen — if they claim them, that is a fabrication the detective
can catch).

The traps are the sharpest mechanic we have and they cost nothing to build: a player who
confidently describes something the glimpses never showed has told us something real about
themselves.

### 4.3 Endings

| Ending | Condition |
|---|---|
| `released_clean` | High consistency, no fabrications |
| `released_suspicious` | High consistency but low coverage — the detective never got enough |
| `held_for_questioning` | Contradictions unresolved |
| `charged` | Multiple hard contradictions or a caught fabrication |

The outcome screen shows the contradictions the detective found, **quoting the player back to
themselves.** It never says the player lied — it says what they said, and when. (G6.)

---

## 5. Definition of done

The PoC is done when, on a machine that is not the developer's, with no network cache and no
pre-recorded assets:

- [ ] `docs/SETUP.md` gets a stranger from clone to running in under 10 minutes.
- [ ] The crime plays, and there is no replay button anywhere.
- [ ] The player speaks; the detective answers in voice; **no menu is ever shown** (G7).
- [ ] Ten or more turns complete without a crash.
- [ ] The detective visibly changes tactic in response to a contradiction — reproducibly, in a
      scripted playtest script that a judge can follow.
- [ ] The detective visibly changes tactic in response to prosody alone (same words, hesitant
      delivery → different tactic). **This is the demo's money shot.**
- [ ] The notebook shows tactic + `internal_note` every turn.
- [ ] Each of the six faults in §6 can be triggered on purpose and each is handled visibly.
- [ ] A full session costs under $0.50 in OpenAI credits, measured.
- [ ] Nothing in the repo asserts, implies, or renders that the system detects lying (G6).
- [ ] README has setup, architecture, prompts location, and the complete third-party table (G4).
- [ ] `docs/PRIVACY.md` states what is recorded, where it goes, how long it is kept, what is
      deleted — and the game says so before the first recording.

---

## 6. Exception handling — graded, so it is a feature

The brief scores "appropriate human review or exception handling." Each of these gets a
**visible, in-fiction** response. Vinay specifies, Ado wires, Giorgi renders.

| # | Fault | Visible behaviour |
|---|---|---|
| F1 | ASR returns empty / silence | Detective: *"I didn't catch that. Say it again."* Turn is not consumed. |
| F2 | Player says nothing for 15 s | Detective presses the silence — `tactic: "silence"`. Silence is an answer, and the game treats it as one. |
| F3 | Model timeout or 429 | Degradation ladder (§2.3). Notebook shows the fallback, labelled. |
| F4 | Model returns unparseable output | One re-ask with the schema error appended; then a holding line, labelled. |
| F5 | Mic permission denied / device lost | Blocking in-fiction card: the interrogation cannot proceed without a voice. Retry button. |
| F6 | Sidecar unreachable | Title-screen state: "Interrogation service offline", with the exact command to start it. |

Rule: **no silent fallbacks.** Every degraded path says it is degraded (G10).

---

## 7. Schedule — 1 Aug to 9 Aug

Eight working days. The freeze is real: G1.

| Day | Date | Milestone | Who |
|---|---|---|---|
| **D1** | Sat 1 Aug | **Contracts frozen** (§3). Repo scaffolded. Everyone can `git pull` and start. Marcel picks his HuBERT approach and says which. | All |
| **D2** | Sun 2 Aug | Each workstream runs standalone against fakes. Ado's replay harness works. | All |
| **D3** | Mon 3 Aug | **Thin slice: text in → text out, end to end.** No audio yet. If this slips, cut scope, not the date. | Ado + Bong + Giorgi |
| **D4** | Tue 4 Aug | Audio both directions. Prosody live. First real voice interrogation. | All |
| **D5** | Wed 5 Aug | Detective adapts on both channels. Case content in. Endings fire. | Bong + Ado |
| **D6** | Thu 6 Aug | Hardening. All six faults handled. Security pass + red-team. Latency measured. | Vinay + all |
| **D7** | Fri 7 Aug | Content polish. Full playtest on a clean machine. Deck drafted. | All |
| **D8** | Sat 8 Aug | **Freeze 23:59.** Demo video shot from a real playthrough. Deck final. README + disclosure complete. | All |
| **D9** | Sun 9 Aug | Submit. **No code changes.** | Ado |

**D3 is the schedule's load-bearing day.** A text-only end-to-end slice on Monday means the
remaining five days are improvement rather than integration. If D3 is not green by Monday night,
cut the notebook panel, cut two endings, cut the crime cinematic to stills — but do not move D3.

---

## 8. Ado's backlog (agent-executable)

Written so a Claude Code agent can pick up any one of them cold. Each is: files, acceptance
criteria, and a test. TDD — the test is written and seen to fail before the implementation.

Per-task detail lives in `docs/superpowers/plans/` and is generated one workstream at a time; this
is the ordered backlog and its acceptance criteria.

**⚠ Rebased onto the merged tree, 1 Aug evening.** Three of these arrived for free in Giorgi's
sidecar, one is superseded, and the paths moved per §1. Status key: ☑ done · ◐ partial · ☐ open.

| # | Task | Files | Done when |
|---|---|---|---|
| ☑ A1 | Sidecar skeleton + health endpoint | `Sidecar/app.py` | **Done in the merge.** `GET /health` returns `{"status","models_loaded","version"}`. Remainder: it does not list the model IDs — add that, it is the cheapest possible G4 self-check |
| ☑ A2 | `.env.example` + config loader | `Sidecar/.env.example`, `Sidecar/config.py` | **Done in the merge.** Variable names only (G2); the sidecar fails fast with a named error naming the missing var |
| ☐ A3 | Contract models as code | `Sidecar/contracts.py` | Pydantic models for the §3 schemas; round-trip test on the exact JSON in this document; **test asserting no deception-like key exists** (G6). ⚠ Blocked on Marcel's §3.1 decision |
| ❌ A4 | ~~WebSocket session endpoint~~ | — | **Superseded.** Transport is HTTP and it works (§3.4). The part worth keeping moves into A9: an unknown or malformed request must produce a structured fault, not a 500 with a stack-trace string |
| ☐ A5 | Case data model + loader | `Sidecar/cases/schema.py`, `Sidecar/cases/case_01_the_stairwell.yaml` | Case loads from YAML, validates, exposes ground truth / glimpses / topics / traps; invalid case fails loudly at startup. ⚠ Today the case is a hardcoded premise string in `llm.py` — **coordinate with Bong**, this straddles the two boxes |
| ☐ A6 | **Consistency tracker** | `Sidecar/consistency.py` | Given a transcript, extracts claims, detects contradictions, produces `CaseState`; fixture test of a known 8-turn contradictory transcript scores below 0.5, a clean one above 0.8. **The biggest gap in the build — the pitch calls consistency the determining factor and nothing tracks it. Start here.** |
| ☐ A7 | Transcript replay harness | `tests/replay.py`, `tests/fixtures/*.jsonl` | Runs a whole session from a fixture with **no mic and no Unity**; this is how the other four test their work |
| ◐ A8 | Turn orchestrator | `Sidecar/app.py` | **Half done in the merge** — STT and affect already fork concurrently via `asyncio.gather` and rejoin before the LLM call. Remainder: consistency in the join, `DetectiveAction` out, and a stage failure producing a §6 fault rather than a bare 500 |
| ☐ A9 | Fault handling F1–F6 | `Sidecar/faults.py` | Each fault forced by a test; each produces a structured fault with a `recovery` string. ⚠ Today every failure is one 500 with `str(e)` — honest, but not *visible*, and exception handling is scored |
| ☐ A10 | Structured session logging | `Sidecar/telemetry.py` | Per-turn JSONL: latencies per stage, tokens, cost, tactic chosen. **No audio, no transcript text** unless `FP_LOG_TRANSCRIPTS=1` (Vinay reviews). Per-stage timings already exist in the turn payload — persist them |
| ☐ A11 | Cost meter | `Sidecar/cost.py` | Running $ per session, exposed on `/health` and in the notebook; hard cap ends the session in-fiction. ⚠ Now spans **two** vendors |
| ◐ A12 | README: setup + architecture + disclosure | `README.md` | **Rewritten in the merge** with the disclosure table filled in for the first time (G4). Remainder: the ⚠ rows — IEMOCAP, Avaturn, ffmpeg/soxr, Unity licence tier — need their licences confirmed, and it needs a clean-machine test by someone who did not write it |
| ☐ A13 | CI: tests + secret scan | `.github/workflows/ci.yml` | `pytest` + `gitleaks` on every push; secret scan failing blocks the merge (G2) |
| ☑ A14 | Classical prosody features (assisting Marcel) | `Sidecar/features_classical.py` | **Done 3 Aug.** F0, energy, timing, pause detection per §3.1 — pure NumPy, no model, no HuBERT import. Unit-tested against synthetic tones with known pitch and pause structure. Consumed by `prosody.py`, which closed the §3.1 gap |
| ☐ A15 | Prosody test rig + fixture generator (assisting Marcel) | `tests/prosody/`, `tests/fixtures/generate_audio.py` | Generates fixture audio **at test time** via ElevenLabs with contrasting delivery (flat/brisk vs hesitant/slow) — see the fixture rule below. Asserts the two produce measurably different signals. This is the D4 money shot as a test |

**Dependency order, rebased 4 Aug:** A1 ☑ → A2 ☑ → A14 ☑ → **A6** → A5 → A3 → A7 → A8 → A9 →
(A10–A13 parallel). A6 jumps the queue: it is the biggest gap, it is stack-independent, and the
contract it depended on is no longer contested. A15 sits outside the chain.

> **⚠ The security workstream (S1–S8) is not in this table.** It was added on 4 Aug and lives in
> [`ROADMAP.md` §5](ROADMAP.md#5-ai-security), owned by Vinay. Two of its items are on the
> never-cut list: **S1** (nothing currently filters model output before it is spoken aloud to a
> room) and **S2** (voice prompt injection is untested against an adversary). Several A-tasks
> fold into it — A3 into S3, A13 into S5, A11 into S6.

> **Fixture audio rule (G2).** No `.wav`/`.mp3` is ever committed, and no recording of a real
> person goes near this repo — `.gitignore` already blocks the extensions, and A15 must not work
> around it. The generator script is committed; the audio it produces is not. Synthetic TTS
> fixtures prove the pipeline reacts to delivery; they do **not** prove it reads real human
> affect. For that, the team records themselves locally, keeps it local, and reports what they
> saw. Do not let a green A15 stand in for the D4 demonstration (G10).

---

## 9. The integration gate

**Until this gate opens, no agent and no person wires workstreams together.** Each owner builds
their box against the §3 contracts and the A7 fakes.

The gate opens when **either**:

1. Every box in [§5 Definition of done](#5-definition-of-done) that belongs to a single
   workstream is checked, **or**
2. A member of the team says, in so many words, that the project is finished and it is time to
   wire everything up.

After the gate opens, the work changes character: cross-workstream integration, latency tuning,
content polish, the demo script, the deck, and the video. Not before. Premature integration with
five people and eight days is how the D3 slice gets missed.

---

## 10. Risks

| Risk | Mitigation | Owner |
|---|---|---|
| Turn latency makes it feel dead | Stream TTS, diegetic thinking beat, measure from D4 not D7; Realtime API is the labelled D6 escape hatch | Ado |
| HuBERT adds nothing the transcript did not already say | The money shot by D4: identical words, two deliveries, two different tactics. Three people on the channel (§1.1) so it is not one person's single point of failure, and A15 makes it a test rather than a hope. If it cannot be demonstrated, we say so honestly rather than claiming it | Marcel, with Bong + Ado |
| Detective is a branch table wearing an LLM costume | Tactic comes from model output; notebook exposes the reasoning; G9 is a review item on every PR | Bong |
| Player talks the detective out of its role via voice | Transcript is data, never instruction; hardened system prompt; red-team on D6 | Vinay |
| Unity is one person | Ado's harness means everything except the client is testable without Unity; Giorgi is never the bottleneck for four people | Giorgi |
| Demo machine has no network / noisy room | ⚠ **Mitigation currently broken.** The build uses VAD, not push-to-talk, so a noisy room can trigger turns the player did not intend — and G5 says a stage that only works in a quiet room is worth zero. Either add push-to-talk as an override, or prove VAD holds in a room with people talking. Decide by D5, test on the actual machine D7 | Giorgi, decided by all |
| Credits burn on a runaway loop | A11 hard cap per session | Vinay |

---

## 11. Open — still not decided

Do not resolve these alone. Raise, decide as a team, then record in
[`CONCEPT.md`](CONCEPT.md).

- **Pipeline vs Realtime API** (§2.2) — proposed, needs sign-off.
- **How HuBERT features are consumed** — Marcel's call by end of D1. Recommendation: hand-derived
  prosody + HuBERT hidden-state statistics, **no training**, for the PoC; a probe trained on a
  public emotion corpus is the stretch and brings a dataset licence obligation (G4).
- **Team name** — needed for the Drive folder before submission.
- **Who shoots the demo video**, and on which machine.
