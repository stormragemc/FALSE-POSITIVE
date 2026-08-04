# FALSE POSITIVE — the concept

> **An AI-powered psychological mystery game.**
> *The detective can hear your fear. It cannot tell why you are afraid.*

This is the shortlisted pitch, recorded verbatim so that every agent and team member works
from the same text. Do not paraphrase it into something new; if the concept changes, edit
this file and say what changed.

---

## Problem Statement (How Might We)

There is an opportunity to reimagine digital entertainment through an experience that treats the player's own voice, testimony and performance under pressure as part of the gameplay.

How might we utilize multimodal AI to create gaming experiences that feel personal, responsive and different for every mystery and psychological enthusiast out there?

## Canonical pitch (as submitted)

> We wanna build a new way to interact with digital entertainment, a game experience that
> doesn't wait for players to select from scripted dialogue options, but listens, interprets
> and responds to them in real time. False Positive is a voice-driven psychological mystery
> game that places the player not in the role of the detective, but as an eyewitness and
> potential suspect. The player first witnesses a sudden crime through brief, chaotic and
> incomplete glimpses, forcing them to rely on their actual memory during a hands-free
> interrogation. They can tell the truth, conceal uncertainty, protect someone else or
> deliberately mislead the investigator, with every spoken response shaping how the case
> unfolds.
>
> The interrogation is led by an autonomous AI detective powered by a goal-oriented LLM and
> HuBERT-based speech representations. While the LLM analyses meaning, timeline and
> CONSISTENCY (biggest determining factor of investigation outcome) of the player's testimony,
> HuBERT extracts acoustic and prosodic signals such as hesitation, vocal tension, confidence,
> pauses and changes in cadence. The detective uses both layers to adapt its strategy, pressing
> harder when it detects uncertainty, revisiting contradictions, changing tone, setting verbal
> traps or becoming unexpectedly sympathetic. Rather than claiming to know whether the player
> is lying, the system only knows when something affects them emotionally, creating the central
> tension: fear may signal guilt, faulty memory or the pressure of being falsely accused.
>
> By making multimodal AI the game's primary opponent, False Positive creates unique,
> emotionally responsive interrogations while helping players practice composure, clear
> thinking and consistent communication under pressure. These skills are useful in interviews,
> presentations and other high-stakes situations.

---

## The load-bearing ideas

Everything below is a restatement of the pitch, in the form the build has to respect.

1. **The player is the witness, not the detective.** The genre inversion is the originality
   claim. Any design decision that quietly turns the player back into an investigator (giving
   them evidence panels, deduction boards, accusation menus) destroys the pitch.
2. **Memory is the real mechanic.** The crime is shown in brief, chaotic, incomplete glimpses
   on purpose. The player's imperfect recall is the game's source of difficulty — not a bug to
   be smoothed over with a replay button.
3. **Voice is the only input.** No scripted dialogue options. Hands-free. The moment we add a
   list of things to click, we are a different game.
4. **Consistency is the biggest determining factor of the outcome.** Stated explicitly in the
   pitch. The scoring must reflect this, and the detective must be seen to catch
   contradictions.
5. **Two channels, deliberately separated.**
   - **LLM** — meaning, timeline, consistency of the testimony.
   - **HuBERT** — acoustic/prosodic signal: hesitation, vocal tension, confidence, pauses,
     cadence change.
6. **The system never claims to detect lies.** It detects *affect*. This is the ethical spine
   and the central dramatic tension at once: fear may mean guilt, faulty memory, or the
   pressure of being falsely accused. The name is the thesis — a false positive.
7. **The detective is an opponent, not a narrator.** Goal-oriented: it presses on uncertainty,
   revisits contradictions, changes tone, sets verbal traps, or turns sympathetic.
8. **The secondary claim is transferable skill** — composure, clear thinking, and consistent
   communication under pressure (interviews, presentations, high-stakes conversation). Useful
   for the impact slide; must not be oversold into an ed-tech pitch.

## Non-negotiables (violating these breaks the pitch, not just the build)

- No dialogue-option menus, ever.
- No "lie detected" UI, verdict, or percentage-of-truth meter presented as truth detection.
- No perfect replay of the crime on demand.
- The detective's behaviour must come from model output, not a scripted branch table
  (Build Quality explicitly scores "genuine use of AI-driven behaviour").
- Voice data handling is stated plainly to the player and in the README.

## Decisions made

Recorded here as they are taken, with the date and who took them. Implementation detail lives in
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

| Date | Decision | Notes |
|---|---|---|
| 1 Aug 2026 | **Game engine: Unity.** | Team call. Godot was briefly considered and dropped. Giorgi owns the client. |
| 1 Aug 2026 | ~~**All API calls go to OpenAI**, on team credits.~~ | **Superseded — see 1 Aug (evening) below.** |
| 1 Aug 2026 | ~~**Model picks:** `gpt-5.6-terra`, `gpt-5.6-sol`, `gpt-4o-transcribe`, `gpt-4o-mini-tts`, `facebook/hubert-base-ls960`.~~ | **Superseded — see 1 Aug (evening) below.** None of these were implemented. |
| 1 Aug 2026 | **Unity client + local Python sidecar.** | Keeps the API key out of the shipped build, makes the speech models possible at all, and lets five people work in parallel. **Stands** — but the transport is HTTP, not WebSocket; see below. |

### 1 Aug 2026, evening — stack reconciled with the implementation ⚠ PENDING SIGN-OFF

Giorgi's `Unity` branch landed a working end-to-end voice loop on D1 using a different stack from
the one recorded that morning. The team's choice was to **let the working code stand and move the
plan to match**, rather than spend D1–D3 refactoring a running prototype onto contracts nobody had
implemented yet.

**These rows are not ratified.** They record what the code does so the documents stop contradicting
it. Each still needs the owners' assent — Vinay in particular, since the vendor set changed.

| Decision | Notes |
|---|---|
| **LLM: Gemini 3.6 Flash**, replacing `gpt-5.6-terra` / `gpt-5.6-sol`. | ⚠ Reverses "all API calls go to OpenAI, on team credits." Needs a named budget owner. Detective dialogue only — there is no consistency analyst yet. |
| **STT: `faster-whisper` (`small.en`), local**, replacing `gpt-4o-transcribe`. | Runs on the player's machine, no key, no cost. Player audio never leaves the machine — this is *stronger* than the original privacy position, not weaker. |
| **TTS: ElevenLabs**, replacing `gpt-4o-mini-tts`. | ⚠ Second vendor, second key, off team credits. Free tier only grants API access to voices you created — see `Sidecar/README.md`. |
| **Affect: `superb/hubert-base-superb-er`, local** — a 4-class emotion classifier, replacing the 13-field `ProsodySignal` derived from `facebook/hubert-base-ls960`. | Still HuBERT, so the pitch's claim holds. But it is a much thinner signal than the contract specified. ⚠ **Marcel's call** whether to enrich it. ⚠ Trained on **IEMOCAP**, which carries a restrictive academic licence — disclosure obligation, see README. |
| **Transport: HTTP `POST /turn`**, replacing the WebSocket protocol in the plan's §3.4. | Request/response per turn. Arrived at independently and it works; §3.4 is superseded. |
| **Input: voice activity detection, not push-to-talk.** | ⚠ Reverses the plan's §10 mitigation for noisy demo rooms. Needs a decision before the demo machine is chosen. |
| **Repo layout: Unity project and `Sidecar/` at the repo root**, not `unity/` and `service/`. | Relocating a working project on D1 is churn; the plan's paths move instead. |

## Open decisions

These are **not decided yet**. Do not treat any of them as settled, and do not silently pick
one — raise it. Engineering status for each lives in [`ROADMAP.md`](ROADMAP.md).

- **⚠ Distribution: itch.io vs judge-only local build.** Raised 4 Aug. The sidecar is a *local*
  Python process needing Python, multi-GB model downloads, and the player's own two API keys —
  so today's build **cannot be published as a playable itch.io download**. Hosting it on GCP
  fixes that but inverts the privacy claim and puts every player's API cost on us. Full analysis
  and three options: [`ROADMAP.md` §9](ROADMAP.md#9-distribution-the-backend-is-local).
- **Gemini billing: AI Studio key vs GCP/Vertex.** Nothing in the repo currently touches GCP;
  `llm.py` builds an AI Studio client from a `GEMINI_API_KEY`. If the team's credits are GCP
  credits, the current code does not bill against them. Names the budget owner the 1 Aug
  amendment asked for.
- **Pipeline (ASR → LLM → TTS) vs the Realtime API** for the voice loop. The plan proposes the
  pipeline and argues it in §2.2; it needs team sign-off, and `gpt-realtime-2.1-mini` stays as a
  labelled fallback if latency is unacceptable by D6.
- **Voice activity detection vs push-to-talk.** VAD shipped, which reversed §10's mitigation for
  a noisy demo room. Recommend adding push-to-talk as an override rather than replacing VAD.
- Whether prosody arrives as a signal the LLM sees, or as a separate policy that steers it.
- The case itself: crime, cast, ground truth, and what "the case unfolds" resolves into.
- How consistency is tracked and scored across the interrogation.
- Endings / outcome model.
- Latency budget and the fallback when a response is slow or ASR fails.
- **Team name** — required for the Drive folder before submission.

### Resolved since this list was written

- ~~**How HuBERT features are consumed**~~ — **decided and built, 3 Aug.** Hand-derived prosodic
  features plus HuBERT hidden-state statistics, no training, session-relative rather than
  absolute. This is the recommendation the plan made in §11. See
  [`HUBERT_ORCHESTRATION_PLAN.md`](HUBERT_ORCHESTRATION_PLAN.md); the §3.1 `ProsodySignal`
  contract is now implemented rather than contested.

## Honesty rule

Any claim in the deck, README, or demo must be true of the code that exists at the time of
submission. If something is a mock, a pre-recorded sample, or a fallback path, label it as one
in the same breath. Judges will run the prototype in a live setting; a claim that dies on stage
costs more than the feature was worth.
