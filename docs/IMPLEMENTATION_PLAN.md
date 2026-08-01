# FALSE POSITIVE — implementation plan (PoC)

> **For agentic workers:** read [`AGENTS.md`](../AGENTS.md) first. There is an identity gate:
> if the human driving you has not said **who they are** and **which workstream they are on**,
> ask and do nothing else. Do not start cross-workstream integration until the gate in
> [§9](#9-the-integration-gate) opens.

**Goal:** by **8 Aug 2026**, a proof-of-concept that a judge can play live — witness a crime in
incomplete glimpses, be interrogated hands-free by voice, and reach an outcome shaped by what
they said and how they said it.

**Architecture:** a Unity client owns the experience (crime, mic, detective's voice, outcome). A
local Python sidecar owns every model call. HuBERT runs locally inside the sidecar; everything
else is OpenAI. The two channels of the pitch — *meaning* and *affect* — are separate services
inside the sidecar that meet only at the detective's turn, so the split is visible in the
architecture, not just the deck.

**Tech stack:** Unity 6 (C#) · Python 3.11 + FastAPI + WebSockets · PyTorch + HuggingFace
`transformers` (HuBERT) · OpenAI API (`gpt-5.6-terra`, `gpt-5.6-sol`, `gpt-4o-transcribe`,
`gpt-4o-mini-tts`).

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
| **Marcel** | Prosody / HuBERT | The affect channel. HuBERT + prosodic features → `ProsodySignal` | `service/prosody/` |
| **Bong** | Detective agent | The opponent. Goal policy, prompts, tactic selection, consistency analyst | `service/detective/`, `prompts/` |
| **Vinay** | AI security | Key handling, prompt-injection defence, output safety, cost limits, voice privacy, red-team report | `service/security/`, `docs/SECURITY.md`, `docs/PRIVACY.md` |
| **Ado** | Core service + delivery | Sidecar skeleton, session orchestration, case data, consistency tracker, exception handling, tests, docs, CI. **Agent-executable** — see [§8](#8-ados-backlog-agent-executable) | `service/core/`, `service/cases/`, `docs/`, `tests/` |

**Ado does not touch `unity/`.** Per instruction. If Unity work is blocking, it goes to Giorgi.

**Branches carry the owner's name:** `<name>/<short-kebab-summary>` — `giorgi/mic-capture-ptt`,
`marcel/hubert-baseline`, `bong/tactic-selection`, `vinay/output-safety-filter`,
`ado/session-orchestrator`. An agent that needs a new branch creates it under the name of the
person it is working for, and says so. Full rules in [`AGENTS.md`](../AGENTS.md).

---

## 2. Architecture

```
┌─────────────────────────────────┐         ┌──────────────────────────────────────────┐
│  UNITY CLIENT  (Giorgi)         │         │  SIDECAR  service/  (Ado)                │
│                                 │         │  FastAPI · localhost:8765                │
│  • crime sequence (3 glimpses)  │  WS     │                                          │
│  • mic capture 16 kHz mono PCM  │◄───────►│  ┌────────────────────────────────────┐  │
│  • push-to-talk                 │ JSON +  │  │ session orchestrator (Ado)         │  │
│  • detective audio playback     │ binary  │  └───┬──────────┬──────────┬──────────┘  │
│  • detective's notebook panel   │         │      │          │          │             │
│  • outcome screen               │         │      ▼          ▼          ▼             │
│  • visible failure states       │         │  ┌────────┐ ┌────────┐ ┌──────────────┐  │
└─────────────────────────────────┘         │  │PROSODY │ │TRANSCR.│ │ DETECTIVE    │  │
                                            │  │Marcel  │ │  Ado   │ │ Bong         │  │
   ships NO api key ────────────────────►   │  │HuBERT  │ │gpt-4o- │ │gpt-5.6-terra │  │
                                            │  │ LOCAL  │ │transcr.│ │+ analyst sol │  │
                                            │  └────────┘ └────────┘ └──────┬───────┘  │
                                            │       │                       │          │
                                            │       └──── ProsodySignal ────┘          │
                                            │                               ▼          │
                                            │                        ┌────────────┐    │
                                            │  security layer        │ TTS        │    │
                                            │  (Vinay) wraps all ───►│gpt-4o-mini │    │
                                            │  egress + all output   │   -tts     │    │
                                            └────────────────────────┴────────────┘────┘
                                                            │
                                                            ▼  OpenAI API
```

### 2.1 Why a sidecar and not "all in Unity"

Three reasons, in order of weight:

1. **HuBERT is PyTorch.** There is no honest way to run it inside Unity in eight days. The pitch
   names HuBERT; the sidecar is what makes that claim true rather than aspirational.
2. **The API key must never ship in the game build.** A key embedded in a Unity binary is
   extractable in minutes. Key lives in the sidecar's environment only. (G2, and Vinay's first
   task.)
3. **Five people can work in parallel** behind one frozen WebSocket contract.

### 2.2 Why a pipeline and not the Realtime API — **needs team sign-off**

OpenAI's `gpt-realtime-2.1` would give lower latency and a more natural interruption feel. We are
**not** using it for the PoC. The reasoning, so it can be argued with:

| | Pipeline (chosen) | Realtime API |
|---|---|---|
| HuBERT gets raw audio | Yes, one obvious fork point | Yes, but audio path is owned by the SDK |
| Two channels visible in architecture | **Yes — this is the pitch** | Blurred inside one model |
| Parallel work by 5 people | Each stage independently testable | One integration everyone waits on |
| Visible exception handling (scored) | Per-stage, easy to surface | Harder to decompose |
| Cost | ~$0.015/min TTS + text tokens | $32/$64 per M audio in/out tokens |
| Latency | ~2.0–3.5 s per turn | ~0.5 s |
| Unity integration | Plain WebSocket | WebRTC, fiddly in Unity |

Latency is the real cost. Mitigations, all of which are also good drama: stream TTS (first chunk
lands in ~300–600 ms), start speaking on the first complete sentence, and give the detective a
diegetic beat — a pen tap, a page turn — while it thinks. A detective who pauses before answering
is not a bug.

**Fallback path, labelled:** if the turn loop lands above 4 s on the demo machine by Day 6, the
labelled stretch is `gpt-realtime-2.1-mini` for the utterance only, with prosody and consistency
still on the pipeline. Do not start this before Day 6.

### 2.3 Model choices

Every API call goes to OpenAI on team credits. HuBERT is the sole exception and runs locally.

| Role | Model | Why this one |
|---|---|---|
| Detective turn (in the loop, latency-critical) | `gpt-5.6-terra` | Balanced intelligence/cost tier. It is on the critical path of every turn; `sol` is too slow to sit there. |
| Consistency analyst (async, latency-tolerant) | `gpt-5.6-sol` | Consistency is *the* determining factor of the outcome (CONCEPT §4). This runs off the critical path after each answer, so we pay for the best reasoning available. |
| Transcription | `gpt-4o-transcribe` | Materially better word error rate than `whisper-1` on accents, background noise and variable speaking speed. Our players are a multinational team demoing in a noisy room — this is the exact failure mode it fixes. |
| Detective's voice | `gpt-4o-mini-tts` | Streaming, ~$0.015/min, and it accepts **prompt-based control of tone, pace and delivery** — so the detective's shift from sympathetic to cold is expressed in the voice, driven by the same model output that chose the tactic. Genuine product fit, not just the cheap option. |
| Prosody / affect | `facebook/hubert-base-ls960` (local) | The pitch names HuBERT. Runs on CPU. **Marcel: confirm the checkpoint licence and add it to the README table before first commit (G4).** |

**Degradation ladder** (Vinay owns, Ado wires): `gpt-5.6-terra` → `gpt-5.6-luna` on timeout or
429 → scripted holding line, visibly labelled in the notebook as a fallback. Never a silent
failure.

> Model IDs verified against OpenAI's model list on 1 Aug 2026. Re-verify at
> `developers.openai.com/api/docs/models` before the freeze; if any ID has moved, the README
> disclosure table (G4) has to move with it.

---

## 3. Frozen contracts

**These are frozen at the end of Day 1 and are the reason five people can work at once.** Changing
one after Day 1 requires telling all five owners in the group chat. Everything below is version
`v1`; every payload carries `"v": 1`.

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

### 3.4 WebSocket protocol — Giorgi ↔ Ado

`ws://localhost:8765/session`. Text frames are JSON; binary frames are raw 16-bit PCM, 16 kHz,
mono.

**Client → server**

| Event | Payload | Meaning |
|---|---|---|
| `session.start` | `{"case_id": "case_01_the_stairwell"}` | Begin |
| `calibration.audio` | binary PCM | Baseline capture (§4.1) |
| `utterance.begin` | `{"utterance_id": "u_007"}` | Push-to-talk pressed |
| `utterance.audio` | binary PCM | Streamed while held |
| `utterance.end` | `{"utterance_id": "u_007"}` | Released |
| `utterance.abort` | `{"reason": "mic_lost" \| "too_short" \| "player_cancelled"}` | Client-side failure |

**Server → client**

| Event | Payload | Meaning |
|---|---|---|
| `transcript.partial` | `{"text": "..."}` | Shown as ghost text |
| `transcript.final` | `{"utterance_id","text"}` | Locked in |
| `detective.thinking` | `{"beat": "page_turn"}` | Cover the latency |
| `detective.action` | `DetectiveAction` | Notebook + subtitle |
| `detective.audio` | binary MP3 chunks | Streamed playback |
| `detective.audio.end` | `{}` | Playback complete |
| `state.update` | `{"pressure","turn","consistency_score"}` | HUD |
| `session.fault` | `{"stage","code","message","recovery"}` | **Visible failure — see §6** |
| `session.end` | `{"ending_id","summary"}` | Outcome screen |

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

| # | Task | Files | Done when |
|---|---|---|---|
| A1 | Sidecar skeleton + health endpoint | `service/main.py`, `service/config.py`, `pyproject.toml` | `GET /health` returns `{"ok":true,"models":{...}}` listing the four model IDs from `models.yaml`; `pytest tests/test_health.py` passes |
| A2 | `.env.example` + config loader | `.env.example`, `service/config.py` | Only variable *names* committed (G2); loader raises a named error listing the missing var, never a stack trace |
| A3 | Contract models as code | `service/contracts.py` | Pydantic models for all four §3 schemas; round-trip test on the exact JSON in this document; **test asserting no deception-like key exists** (G6) |
| A4 | WebSocket session endpoint | `service/core/session.py` | Every §3.4 event accepted/emitted; unknown event → `session.fault` not a crash; `tests/test_protocol.py` covers all twelve |
| A5 | Case data model + loader | `service/cases/schema.py`, `service/cases/case_01_the_stairwell.yaml` | Case loads from YAML, validates, exposes ground truth / glimpses / topics / traps; invalid case fails loudly at startup |
| A6 | Consistency tracker | `service/core/consistency.py` | Given a transcript, extracts claims, detects contradictions, produces `CaseState`; fixture test of a known 8-turn contradictory transcript scores below 0.5, a clean one above 0.8 |
| A7 | Transcript replay harness | `tests/replay.py`, `tests/fixtures/*.jsonl` | Runs a whole session from a fixture with **no mic and no Unity**; this is how the other four test their work |
| A8 | Turn orchestrator | `service/core/orchestrator.py` | Forks audio to prosody and transcription concurrently, joins, calls detective, streams TTS; a stage failure produces the §6 fault, never an exception to the client |
| A9 | Fault handling F1–F6 | `service/core/faults.py` | Each fault forced by a test; each produces `session.fault` with a `recovery` string |
| A10 | Structured session logging | `service/core/telemetry.py` | Per-turn JSONL: latencies per stage, tokens, cost, tactic chosen. **No audio, no transcript text** unless `FP_LOG_TRANSCRIPTS=1` (Vinay reviews) |
| A11 | Cost meter | `service/core/cost.py` | Running $ per session, exposed on `/health` and in the notebook; hard cap ends the session in-fiction |
| A12 | README: setup + architecture + disclosure | `README.md`, `docs/SETUP.md` | Required by the brief (G4). Table complete with licences. Clean-machine tested by someone who did not write it |
| A13 | CI: tests + secret scan | `.github/workflows/ci.yml` | `pytest` + `gitleaks` on every push; secret scan failing blocks the merge (G2) |

**Dependency order:** A1 → A2 → A3 → (A4, A5 parallel) → A6 → A7 → A8 → A9 → (A10–A13 parallel).
A7 unblocks everyone else; get to it early.

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
| HuBERT adds nothing the transcript did not already say | Marcel demonstrates the money shot by D4: identical words, two deliveries, two different tactics. If it cannot be demonstrated, we say so honestly rather than claiming it | Marcel |
| Detective is a branch table wearing an LLM costume | Tactic comes from model output; notebook exposes the reasoning; G9 is a review item on every PR | Bong |
| Player talks the detective out of its role via voice | Transcript is data, never instruction; hardened system prompt; red-team on D6 | Vinay |
| Unity is one person | Ado's harness means everything except the client is testable without Unity; Giorgi is never the bottleneck for four people | Giorgi |
| Demo machine has no network / noisy room | Push-to-talk not VAD; cached case data; test on the actual machine D7 | All |
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
