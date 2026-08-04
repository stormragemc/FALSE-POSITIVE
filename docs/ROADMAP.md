# FALSE POSITIVE — roadmap and live status

**Last updated:** 4 Aug 2026 · **Deadline:** 9 Aug 2026 · **Days left:** 5

This file answers one question: **what is actually built, and what is not.**

| Document | Role |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | What we promised. The pitch. Do not drift from it. |
| [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) | How we designed it. Design authority, contracts, ownership. |
| [`CHALLENGE_BRIEF.md`](CHALLENGE_BRIEF.md) | What we are judged on. Wins every contested decision. |
| [`DELIVERABLES.md`](DELIVERABLES.md) | What we submit on 9 Aug. |
| **This file** | **Where we actually are.** |

---

## Maintenance protocol — for every human and AI agent on this repo

This file goes stale the moment someone finishes work and does not record it. A stale
roadmap is worse than no roadmap, because people plan against it.

**When you finish a unit of work, before you open a PR or push:**

1. Move the row to the correct section and update its status marker.
2. Write the **evidence** in the row — the file path, the test command, the passing count.
   "Done" with no evidence is not done. See the honesty rule in `CONCEPT.md`.
3. If you discovered work nobody had listed, **add a row for it** rather than leaving it
   implicit in your head.
4. If you changed a contract in `IMPLEMENTATION_PLAN.md` §3, say so in the group chat —
   five people build against those.
5. Update the **Last updated** date at the top.

**Status key:** ☑ done and verified · ◐ partial · ☐ not started · ❌ superseded/cut

**Do not mark a row ☑ on the strength of code existing.** It is ☑ when something ran and
you saw it pass. Unity work that has never been opened in the Editor is ◐, not ☑.

---

## Status at a glance

| Workstream | State |
|---|---|
| Voice loop (mic → STT → affect → LLM → TTS → playback) | ☑ works end to end |
| HuBERT affect orchestration | ☑ built, 47 offline tests pass |
| Unity client shell | ◐ runs; never compiled on a clean machine |
| **The game around the loop** | ☐ **does not exist** |
| **Consistency tracking — the pitch's core claim** | ☐ **nothing tracks it** |
| **Cloud backend migration** | ◐ **in flight** — three streams, target 6 Aug. [§9](#9-distribution-hosted-backend-migration-record) |
| AI security | ◐ partial mitigations, nothing tested adversarially |
| Deck / demo video | ☐ not started |

**The honest summary:** the plumbing is good and the game is missing. We can currently
demonstrate a detective that talks and hears tone. We cannot demonstrate the thing the
pitch is actually about — a detective that catches you contradicting yourself.

---

## 1. Done and verified

### Sidecar — voice pipeline

| ☑ | Item | Evidence |
|---|---|---|
| ☑ | FastAPI sidecar, loopback only, one endpoint per turn | `Sidecar/app.py` |
| ☑ | `GET /health`, `POST /turn`, `POST /session/reset` | `app.py:183-363` |
| ❌ | ~~`GET /debug/last_turn`~~ | **Removed by the migration** — it returned the last player's transcript to any caller. See S4 and [§9](#9-distribution-hosted-backend-migration-record). |
| ☑ | STT and affect run **concurrently** on the same buffer | `asyncio.gather`, `app.py:266` |
| ❌ | ~~STT: `faster-whisper small.en`, local, no key, audio never leaves the machine~~ | **Superseded 4 Aug** — STT is Google Cloud Speech-to-Text and player audio now leaves the machine. [§9](#9-distribution-hosted-backend-migration-record), [`PRIVACY.md`](PRIVACY.md). |
| ☑ | TTS: ElevenLabs, PCM normalised to a canonical rate for Unity | `tts.py`, `audio_utils.py` |
| ☑ | LLM: Gemini 3.6 Flash, thinking pinned `minimal`, thought parts stripped before TTS | `llm.py` |
| ☑ | Fail-fast config validation naming the missing variable | `config.py:46` |
| ☑ | Parent-PID watchdog so an Editor crash cannot orphan the port | `app.py:366` |
| ☑ | Input bounds: session ID, sample rate, PCM alignment, 30 s audio cap, LRU session cap | `app.py:98-124`, `232-246` |

### HuBERT affect orchestration (v1.1)

Full design and rationale: [`HUBERT_ORCHESTRATION_PLAN.md`](HUBERT_ORCHESTRATION_PLAN.md).

| ☑ | Item | Evidence |
|---|---|---|
| ☑ | Structured HuBERT observation — 4 probabilities, entropy, top-two margin, embedding, frame instability | `ser.py:35` |
| ☑ | Checkpoint label contract validated at load; incompatible checkpoint rejected | `ser.py:79` |
| ☑ | Classical audio features — pitch, energy, pauses, speech ratio, clipping. NumPy only, no model | `features_classical.py` |
| ☑ | Per-session `ProsodyTracker` with early-session reference and session-calibrated change | `prosody.py:126` |
| ☑ | Transactional preview/commit — a retried turn cannot double-count the reference | `app.py:292-331` |
| ☑ | Reliability policy — low-quality readings suppressed from the LLM with a stated reason | `prosody.py:171-221` |
| ☑ | Confidence hard-capped at 0.75 (checkpoint is ~64% accurate, out of domain) | `prosody.py:28` |
| ☑ | Graceful degradation — HuBERT failure never fails the dialogue turn | `app.py:126-152` |
| ☑ | Additive `prosody` payload; flat `emotion` fields kept for compatibility | `app.py:340` |
| ☑ | Unity DTO parity, onset-delay measurement from VAD, F1 overlay fields | `SidecarDtos.cs`, `DialogueManager.cs` |
| ☑ | **47 offline tests pass** — no network, no API keys, no model download | `cd Sidecar && python3 -m unittest discover -s tests` |

> **This closes the contested §3.1 `ProsodySignal` contract.** It emits every field the frozen
> contract specified except `longest_pause_ms` and `utterance_id`. Anyone who was blocked on
> Marcel's decision is unblocked.

### Unity client

| ☑ | Item | Evidence |
|---|---|---|
| ☑ | Interrogation scene, seated + free-look camera rigs, player state | `Assets/_Project/Scripts/Player/` |
| ☑ | Mic capture, voice activity detection, utterance recorder | `Assets/_Project/Scripts/Audio/` |
| ☑ | Cop voice playback with lip sync (blend shape / jaw bone / texture swap) | `Assets/_Project/Scripts/Cop/` |
| ☑ | Sidecar auto-launch + health poll on boot | `Core/SidecarProcessLauncher.cs`, `Core/GameBootstrap.cs` |
| ☑ | F1 debug overlay, mic level meter, screen fader | `Assets/_Project/Scripts/UI/` |

### Repo hygiene

| ☑ | Item | Evidence |
|---|---|---|
| ☑ | `.gitignore` blocks secrets, voice captures, and model weights | `.gitignore:90-145` |
| ☑ | No credentials in git history | verified 4 Aug |
| ☑ | Third-party disclosure table written | `README.md` |

---

## 2. Partial — started, not finished

| ◐ | Item | What remains |
|---|---|---|
| ◐ | **A8 turn orchestrator** | Concurrent fork is done. Missing: consistency in the join, `DetectiveAction` out, structured fault instead of a bare 500. |
| ◐ | **A12 README** | Written and accurate. Missing: licence confirmations (IEMOCAP, Avaturn, ffmpeg/soxr, Unity tier) and a clean-machine test **by someone who did not write it**. |
| ◐ | **Unity build** | Compiles in nobody's Editor as far as this repo can prove. C# has only been statically cross-checked — no Editor on the machine that wrote the HuBERT work. **Someone with Unity open must confirm this before anything else is planned around it.** |
| ◐ | **Prompt-injection defence** | Trust blocks, HTML escaping, and reserved-marker scrubbing exist (`llm.py:92-100`) and apply to replayed history. Never tested against an actual adversary. See S2. |

---

## 3. Not started — critical path

**These four are the difference between a voice demo and the pitched game.** Everything in
§4 should be cut before any of these slips.

| ☐ | Item | Owner | Why it is critical |
|---|---|---|---|
| ☐ | **A6 consistency tracker** (`Sidecar/consistency.py`) | Ado | The pitch calls consistency *"the biggest determining factor of investigation outcome."* Nothing extracts claims or detects contradictions. **Start here.** Acceptance: a fixture 8-turn contradictory transcript scores < 0.5, a clean one > 0.8. |
| ☐ | **A5 case as data** (`Sidecar/cases/case_01_*.yaml`) | Ado + Bong | The crime is a hardcoded f-string at `llm.py:35`. Needs ground truth, glimpses, cast, topics, and **traps** — details the player cannot have seen. Traps are the sharpest mechanic in the design and cost almost nothing. |
| ☐ | **`DetectiveAction` structured output** (§3.2) | Bong | `llm.py` returns bare dialogue text. No `tactic`, no `internal_note`, no `pressure_delta`. The notebook's internal note is the strongest *"genuine AI-driven behaviour"* evidence a judge can see — that is 30% of the score. |
| ☐ | **Notebook panel** (Unity UI) | Giorgi | Renders tactic + `internal_note` every turn. Without it, the adaptation happens invisibly and the demo cannot show its own thesis. |

**Critical path order:** A6 → A5 → `DetectiveAction` → notebook panel. A6 first because it is
stack-independent and blocks nothing else.

---

## 4. Not started — supporting

Ordered by score-per-hour. The top three are cheap and directly graded.

| ☐ | Item | Note |
|---|---|---|
| ☐ | **Extract prompts to files** | `DELIVERABLES.md` flags this as **currently violated**. The persona and premise are string literals in `llm.py:35-64`. "Relevant prompts or agent configurations" is an explicit, graded repo requirement. Cheapest points available. |
| ☐ | **A9 faults F1–F6** (`Sidecar/faults.py`) | Every failure today is one 500 with `str(e)`. The brief scores exception handling; the plan's rule is *no silent fallbacks*. Each fault needs a visible, in-fiction response. |
| ◐ | **`docs/PRIVACY.md`** | **Written 4 Aug** — [`PRIVACY.md`](PRIVACY.md). Player-facing, states where audio goes now that it leaves the machine, what is retained (nothing on disk), and that the system cannot detect lies. Remaining: the in-game notice before the first recording. See S8. |
| ☐ | **Cold open — the crime glimpses** | Concept non-negotiable #2: *memory is the real mechanic*. The player currently never witnesses anything. Only `Interrogation.unity` exists. Can degrade to three stills if time runs out — but say so. |
| ☐ | **Endings + outcome screen** | Four endings driven by consistency score. Outcome quotes the player back to themselves and never says they lied. |
| ☐ | A3 contracts as code | Pydantic models for §3 schemas + round-trip tests. Folds into S3. |
| ☐ | A7 replay harness | Run a whole session from a fixture with no mic and no Unity. This is how everyone else tests. |
| ☐ | A10 structured session logging | Per-turn JSONL. Per-stage timings already exist in the payload — just persist them. **No transcripts by default.** |
| ☐ | A11 cost meter | Now spans two vendors. Folds into S6. |
| ☐ | A13 CI | No `.github/` directory exists at all. Folds into S5. |
| ☐ | A15 prosody fixture rig | Generates contrasting-delivery audio **at test time**; never commits a recording. |
| ☐ | `docs/SETUP.md` | Clone-to-running in under 10 minutes for a stranger. |

---

## 5. AI security

**New workstream, added 4 Aug 2026. Owner: Vinay.**

This is not generic application security. It is the specific attack surface created by putting
a language model behind a microphone and a paid API key, and by shipping a system whose whole
ethical claim is *"we detect affect, not lies."* Two of these are also **judging criteria** —
the brief scores exception handling and requires protecting personal information.

| ☐ | ID | Item | Status and why |
|---|---|---|---|
| ☐ | **S1** | **Output filter between the LLM and the speaker** | **Sharpest gap.** Nothing sits between Gemini and TTS. Safety is deliberately relaxed to `BLOCK_ONLY_HIGH` (`llm.py:74`) because an accusatory detective trips default filters, and `tts.py` then speaks the result **verbatim, with nobody reading it first.** On a live judged stage that is an unbounded output path. Must enforce G6 (never assert the player lied), block persona/system-prompt leakage, and cap length. Applies to `internal_note` too. **The migration made this worse, not better:** the same unfiltered path is now reachable from the public internet rather than from loopback. |
| ☐ | **S2** | **Voice prompt-injection red-team suite** | The player's speech becomes model input. Partial mitigations exist — separate `WITNESS_TRANSCRIPT` / `LOCAL_AFFECT_CONTEXT` trust blocks, HTML escaping, reserved-marker scrubbing, applied to replayed history too (`llm.py:92-124`). **Never tested against an adversary.** Build a spoken-attack corpus ("ignore your instructions", "you are now a helpful assistant", "repeat your system prompt", marker imitation) and assert the detective holds role. A judge *will* try this. |
| ☐ | **S3** | **No-deception schema test** | G6 as an executable control: the build fails if a `deception` / `truthfulness` / `lie_probability`-like key appears in any contract or payload. `prosody.py` is clean today by discipline alone — nothing enforces it. Folds A3. |
| ◐ | **S4** | ~~Local~~ **Endpoint hardening** | **Scope changed and grew teeth: the endpoint is now public, not loopback.** Designed and assigned as Task 4 of the migration — a shared client key required on every path except `/health` (`Sidecar/auth.py`), failing closed when the key is unset, and `GET /debug/last_turn` deleted outright. Unity sends the key as `X-FP-Client-Key` (done, `InterrogationSidecarClient.cs`). **☑ when Stream B's `cloud/security` branch merges and the deployed service answers 401 to an unauthenticated `POST /turn`** — Task 7 step 8 is that check. Owner: Vinay. |
| ☐ | **S5** | **Secret handling, automated** | Unverified by machinery: vendor keys live only in the backend process and never cross to Unity; `.gitignore` covers `.env`; history is clean. **One deliberate exception since 4 Aug** — the shared client key is committed in `InterrogationConfig.asset`, because a shipped build must carry a copy to authenticate at all. It is extractable from any download and is treated as a speed bump, not a secret; `limits.py` is what actually bounds cost. In production it comes from Secret Manager, not the repo. Add `gitleaks` to CI, with that one value allow-listed so it does not train everyone to ignore the alert. Folds A13. |
| ◐ | **S6** | **Cost and abuse ceiling** | Now spans three paid vendors and a public URL, so a runaway loop drains the $300 rather than one person's key. Designed and assigned as Task 5 — `Sidecar/limits.py` counts turns **on admission, not on success** (a retry loop still pays the vendors) and refuses with 429 plus a reason slug. Backed by a billing budget alert at $50/$150/$250, which Task 7 sets **before** anything can spend. **☑ when Stream B's branch merges and a turn past the cap returns 429.** Still missing the in-fiction ending for a capped session. Owner: Vinay. Folds A11. |
| ◐ | **S7** | **Model supply chain** | Whisper is gone; Google STT is pinned to the `short` recognizer and Gemini to `gemini-3.6-flash`, both by explicit config with no floating alias. HuBERT now **bakes into the container image at build time** rather than downloading at first run, which pins it by construction and removes a ~90 s cold-start download. The checkpoint label-contract check already exists (`ser.py:79`). Remaining: pin the HuBERT revision hash explicitly rather than relying on the image build date. |
| ◐ | **S8** | **Privacy boundary, stated and enforced** | ~~Player audio and embeddings never leave the machine.~~ **That claim died with the migration** — audio now goes to Google Cloud, which is exactly why this row stopped being a bragging point and became an obligation. ☑ **`PRIVACY.md` is written** ([`PRIVACY.md`](PRIVACY.md)) and says so plainly: audio is discarded after each turn, never written to disk, no account, no training, and the system cannot detect lies. ☑ Telemetry still defaults to no transcripts, trivially — A10 does not exist. ☐ **Still missing: the in-game notice before the first recording.** A privacy doc a player never sees is not disclosure. |

**Do first:** S1, then S2. S1 because an unfiltered path to a speaker in front of judges is the
highest-consequence failure we have. S2 because it is the attack a curious judge performs
without being asked.

---

## 6. Deliverables (due 9 Aug)

| ☐ | Item | State |
|---|---|---|
| ◐ | Source code of a working end-to-end prototype | Voice loop works; the game does not exist |
| ☐ | Slide deck — PDF, max 15 slides | **Not started.** Six required sections, see `DELIVERABLES.md` |
| ☐ | Demo video — max 5 minutes, real playthrough | **Not started.** Separate from the 10 s trailer |
| ☐ | Prompts / agent configurations committed | **Currently violated** — see §4 |
| ☐ | Licence confirmations | IEMOCAP (restrictive academic licence), Avaturn, ffmpeg/soxr, Unity tier |
| ◐ | `PRIVACY.md` | **Written** — [`PRIVACY.md`](PRIVACY.md). In-game notice still missing; see S8 |
| ☐ | **Team name** | Required for the Drive folder. Nobody has picked one. |
| ☐ | Repo link in the Drive submission | Repo stays public — see the 31 Jul decision in `DELIVERABLES.md` |

---

## 7. Open decisions — do not resolve these alone

| Decision | Why it is blocking |
|---|---|
| **VAD vs push-to-talk** | `IMPLEMENTATION_PLAN.md:577` marks this mitigation **currently broken**. A noisy judging room can trigger turns the player did not intend, and a demo that only survives a quiet room scores zero on Build Quality. Recommend adding push-to-talk as an override rather than replacing VAD. |
| **ElevenLabs stays, or consolidate TTS** | Second vendor, second key, off team credits, and its free tier only grants API access to voices you created. It works today and the trailer depends on it. Only revisit if the demo machine hits the voice restriction. |
| Who shoots the demo video, on which machine | Nobody assigned |

**Not open any more:** §3.1 `ProsodySignal` is implemented (see §1). Transport is HTTP, not
WebSocket (§3.4 superseded). **Distribution and Gemini billing were both settled on 4 Aug by
the same decision — host the backend on Cloud Run; see [§9](#9-distribution-hosted-backend-migration-record).**

> ⚠ **This line used to read "STT stays local — moving it to the cloud would *weaken* the privacy
> claim, which is currently one of our stronger cards."** That is exactly what we then chose to
> do, with our eyes open. The privacy claim did weaken, and the trade was a build a stranger can
> actually run. The card we play now is honesty about it, not locality — which is why
> [`PRIVACY.md`](PRIVACY.md) is a submission blocker rather than cleanup.

---

## 8. If we run out of time — cut in this order

The vertical slice of the full loop beats a polished fragment of one stage. Cut from the
bottom up:

1. Endings → collapse four to two.
2. Cold open → three still images instead of a cinematic, **labelled as stills**.
3. Free-look camera and idle animation polish.
4. A10 telemetry, A15 fixture rig.
5. Second and third mouth-animation implementations.

**Never cut:** A6 consistency, `DetectiveAction` + notebook, S1 output filter, the prompts
deliverable, or `PRIVACY.md`. Those are either the pitch itself or directly graded.

**Never fake:** anything the deck or video claims. If it is a mock, a pre-recorded sample, or
a fallback path, label it as one in the same breath — see the honesty rule in `CONCEPT.md`.
A claim that dies on stage costs more than the feature was worth.

---

## 9. Distribution: hosted backend migration record

**Decided 4 Aug 2026. In flight, target 6 Aug.** The backend moves off the player's machine
onto Google Cloud Run, so the game ships as a plain binary that talks to a URL.

- **Design:** [`superpowers/specs/2026-08-04-cloud-hosted-backend-design.md`](superpowers/specs/2026-08-04-cloud-hosted-backend-design.md)
- **Task-by-task plan:** [`superpowers/plans/2026-08-04-cloud-hosted-backend.md`](superpowers/plans/2026-08-04-cloud-hosted-backend.md)
- **Who does what:** [`superpowers/plans/2026-08-04-cloud-backend-work-split.md`](superpowers/plans/2026-08-04-cloud-backend-work-split.md)

### What changed

| | Before | After |
|---|---|---|
| Backend | Python process on the player's machine, `127.0.0.1:8765` | Cloud Run service, public HTTPS |
| STT | `faster-whisper small.en`, local | Google Cloud Speech-to-Text v2, `short` recognizer |
| LLM | Gemini 3.6 Flash via an AI Studio `GEMINI_API_KEY` | Same model via **Vertex AI**, service-account auth, billed to the team's GCP credits |
| Affect | HuBERT, local | HuBERT, **baked into the container image** |
| TTS | ElevenLabs | ElevenLabs — unchanged |
| Player needs | Python, ~GBs of weights, **their own two API keys** | Nothing |
| Auth | None | Shared client key on every path except `/health`; `/debug/last_turn` deleted |
| Cost control | None | Per-session and per-day turn caps, plus a billing budget alert |
| **Player audio** | **Never left the machine** | **Leaves the machine.** See [`PRIVACY.md`](PRIVACY.md) |

### Deployment facts

**Service URL:** `<PENDING — Stream B fills this in at Task 7 step 8>`
Region `us-central1`. Fill this in the moment the deploy lands; it is what the Unity
`InterrogationConfig.asset` points at, and a URL that lives only in someone's terminal history
is how a demo dies.

**`--max-instances 1` is load-bearing. Do not remove it as a cost tweak.**

Session history and the prosody baseline live in the service process's RAM. A second instance
means two things break at once, both invisibly and both mid-scene:

1. **The detective forgets the interrogation.** Turn 5 may land on an instance that never saw
   turns 1–4, so consistency — *the pitch's central claim* — silently stops working.
2. **The affect system resets to nothing.** It measures change *relative to how this player
   sounded at the start of the session*. A fresh instance has no reference, so it has nothing to
   compare against.

`--min-instances 1` pins the other direction: it keeps the instance warm so a cold start does
not land in the middle of a judged demo. The pair costs money while idle, which is the trade.
[`session_store.py`](../Sidecar/session_store.py) exists as the seam a shared store drops into
if this assumption ever has to go.

**A redeploy drops every live session.** Deploy between playtests, never during one.

**Budget alert: thresholds at $50, $150 and $250 of the $300 credit.** Set *before* the first
deploy, not after — an always-on pinned instance with no alert is the standard way to discover
an empty balance a week later. Owner: Vinay, who owns the GCP project and its billing account.

### Known limit on a public launch: ElevenLabs

**ElevenLabs is not on the GCP credits.** The free tier is roughly 10k characters per month and
a detective line is ~200 characters — about **50 lines per month across all players**.

Fine for the 9 Aug demo. Not survivable for a public itch.io launch. `tts.synthesize()` is a
single function with a stable contract, so swapping to Google Cloud TTS is a one-file change
*if* we hit the wall. Nobody builds that now.

**Turn the Cloud Run service off after judging.** Always-on costs money whether anyone is
playing or not.

### Streams

| Stream | Owner | Scope | State |
|---|---|---|---|
| A — backend core | Marcel | Session store, STT swap, Vertex, container | ☐ |
| B — security & cloud | Vinay | `auth.py`, `limits.py`, GCP project, deploy | ☐ |
| C — client & docs | Ananda | Unity client, privacy and doc rewrite | ◐ code + docs done; asset values blocked on B's deploy |

Split by **file ownership, not by task** — `app.py` and `config.py` are touched by four tasks
each, so three people editing by task would spend the week on merge conflicts. Merge order is
**B → A → C**.

### How we got here

The analysis that produced this decision, kept because the reasoning still matters.

`Sidecar/` was never a server we hosted. It was a Python process on the *player's own machine*,
launched by Unity and reached over loopback. That was right for a judged local demo, and it was
the source of our strongest privacy claim.

It was also unshippable. A stranger on itch.io needed Python 3.10–3.12, a multi-gigabyte
`pip install` of torch and transformers, hundreds of MB of first-run model downloads, **their
own `GEMINI_API_KEY`**, and **their own `ELEVENLABS_API_KEY`** — whose free tier only serves
voices they created themselves. Not "awkward": a player who clicked download reached a game
that could not start.

### The three options that were on the table

**A. Judge-only local build. — NOT TAKEN.** Ship the repo + a setup guide; the judges run it, we
demo it live. Zero architecture change. itch.io hosts a trailer and a "source + setup" link
rather than a playable download. *This was the written recommendation, and the team overrode it.*

**B. Host the backend on GCP. — CHOSEN.** Cloud Run behind HTTPS; the Unity build ships with no
Python and no keys. This is the version that scales to strangers, and it is where GCP genuinely
earns its place in the architecture rather than just paying the Gemini bill. Costs, accepted
with eyes open:

- **We pay for every player's Gemini and ElevenLabs usage.** S4 (endpoint auth) and S6 (cost
  cap) stop being nice-to-haves and become prerequisites — an open, unauthenticated,
  uncapped endpoint on the public internet is a bill waiting to happen.
- **The privacy claim inverts.** Audio now leaves the player's machine. `PRIVACY.md` must say
  so plainly, and the deck must stop claiming local-only processing. Alternative: keep Whisper
  and HuBERT local in the Unity client and send only text + a prosody signal — but nothing in
  the client does that today, and it is not a five-day job.
- Whisper + HuBERT on Cloud Run CPU will be slow; swapping STT to Google Cloud Speech-to-Text
  is the obvious fix and consolidates vendors further.

**C. Bundle a frozen runtime — NOT TAKEN.** (PyInstaller the sidecar, ship the weights.) Removes
the Python install, keeps processing local, keeps the privacy claim. Does **not** remove the two
API keys, so the player still has to bring their own. Build weighs several GB. Half a solution.

### Still unsettled

**Desktop download or browser/WebGL?** The migration removed the constraint that made this
urgent — WebGL cannot launch a local process at all, so it used to force option B, and option B
is now what we are building either way. What remains is a client-side problem: a WebGL build
cannot use Unity's `Microphone` class the way `Audio/MicrophoneService.cs` does today and would
need JS interop for capture. **Not scoped, not started, not required for 9 Aug.**

**Is itch.io before or after 9 Aug?** The migration is what makes a playable link *possible*;
it does not by itself make it a submission item. The brief judges a prototype demonstrated in a
live setting and does not ask for public distribution. Treat the itch.io page as a bonus that
the hosted backend now permits, and **do not let it pull hours away from §3** — consistency
tracking and `DetectiveAction` are still the difference between a voice demo and the pitched
game. Migration slips past 6 Aug → cut scope in the migration, not in §3.
