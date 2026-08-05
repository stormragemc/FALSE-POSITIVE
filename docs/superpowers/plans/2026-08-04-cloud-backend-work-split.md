# Cloud migration — who does what

**Three people, five days.** Companion to
[`2026-08-04-cloud-hosted-backend.md`](2026-08-04-cloud-hosted-backend.md) (the task-by-task
plan) and [the design spec](../specs/2026-08-04-cloud-hosted-backend-design.md).

Read the plan for *how*. Read this for *who*, *in what order*, and *what not to touch*.

---

## The rule that makes this work: exclusive file ownership

The nine tasks are not independent. `Sidecar/app.py` is modified by Tasks 1, 2, 4, 5 and 6;
`Sidecar/config.py` by Tasks 2, 3, 4 and 5. Hand those to three people at once and the week
goes into merge conflicts instead of the game.

So the split is **by file, not by task**. Nobody edits a file another stream owns. If you need
a change in someone else's file, message them — do not reach in.

| Stream | Owner | Owns, exclusively | Never touches |
|---|---|---|---|
| **A — Backend core** | **Marcel** | `Sidecar/app.py`, `config.py`, `stt.py`, `llm.py`, `session_store.py`, `requirements.txt`, `.env.example`, `Dockerfile`, `.dockerignore`, `tests/test_app_failure_isolation.py`, `tests/test_config.py`, `tests/test_stt_adapter.py`, `tests/test_session_store.py` | `Assets/`, `docs/`, `auth.py`, `limits.py` |
| **B — Security & cloud** | **Vinay** | `Sidecar/auth.py`, `Sidecar/limits.py`, `tests/test_auth.py`, `tests/test_limits.py`, **the GCP project itself** | Everything else in `Sidecar/` |
| **C — Client & docs** | **Ananda** | `Assets/**`, `docs/**`, `README.md`, `Sidecar/README.md` | All of `Sidecar/*.py` |

Assigned 4 Aug 2026.

**Checkpoint — 5 Aug 2026 (`cloud/client-docs`):** Marcel's pushed Tasks 1–3
have been integrated. Auth, limits, their FastAPI wiring, Docker packaging, and
the blank/disabled Unity asset handoff are implemented and offline-tested on
this branch. The remaining blocking work is the credentialed container gate,
GCP budget/project setup, Cloud Run deployment, and then inserting the deployed
URL/client key plus a Unity play-test. No HuBERT work is waiting on Marcel.

There is **zero overlap**. That is the entire trick.

## Streams

### A — Backend core — Marcel

**Tasks 1, 2, 3, 6.** The vendor swaps and the container. This is the critical path — the
longest chain and the one everything else waits on.

1. Task 1 — session store behind an interface
2. Task 2 — STT to Google Cloud
3. Task 3 — Gemini to Vertex
4. *Wire in B's `auth.py` and `limits.py`* (Task 4 steps 5–9, Task 5 steps 5–8)
5. Task 6 — Dockerfile, and the local verification gate

**A owns all `app.py` wiring, including for B's modules.** The plan writes those wiring steps
out verbatim, so A does not need to have written `auth.py` to integrate it. This is what keeps
`app.py` conflict-free.

> **A's tripwire:** `tests/test_app_failure_isolation.py` hand-stubs `app.py`'s whole import
> graph — a fake `config` with a fixed attribute list, a fake `stt`, a `_FakeFastAPI` with no
> `middleware` method, and a teardown that shuts down `_stt_pool`. Four tasks break it. Every
> break has its exact fix written into the plan step that causes it. When 47 tests explode for
> no apparent reason, this file is why.

### B — Security & cloud — Vinay

**Tasks 4 and 5 (module halves), then Task 7.** Thematically coherent: B owns the things that
stop a public endpoint becoming a bill.

> **This closes a documented gap.** `CONCEPT.md`'s 1 Aug evening amendment has carried
> ⚠ *"Needs a named budget owner"* against the Gemini vendor change since it was written.
> Vinay owning the GCP project, its billing account and the budget alert **is** that owner.
> Worth ratifying explicitly rather than leaving it implied by this table.

1. Task 4 steps 1–5 — write `auth.py` + `tests/test_auth.py`
2. Task 5 steps 1–5 — write `limits.py` + `tests/test_limits.py`
3. Hand both to A with the config constants they need (`FP_CLIENT_KEY`,
   `MAX_TURNS_PER_SESSION`, `MAX_TURNS_PER_DAY`) — **A adds them to `config.py`**, B does not
4. **In parallel, from hour one:** GCP account prep — Task 7 steps 1–5. None of it needs the
   image to exist.
5. Task 7 steps 6–9 — deploy, IAM, verify, record latency

Both of B's modules are pure, dependency-free and fully unit-tested. Neither imports `config`,
`torch`, or anything network — they can be written and finished before A has merged anything.

> **B does Task 7 step 3 — the budget alert — before anything can spend money.** $300 with an
> always-on pinned instance and no alert is the standard way to wake up to an empty balance.
> Not last, first.

### C — Client & docs — Ananda

**Tasks 8 and 9.** Fully parallel from hour one; blocked only at the very end on two values.

1. Task 8 steps 1–4 — the Unity code changes. **These need nothing from A or B.** The header
   name (`X-FP-Client-Key`) and the config field shape are both specified in the plan.
2. Task 9 steps 1–3 — `PRIVACY.md`, the `CONCEPT.md` corrections. Prose, no dependencies.
3. *Blocked* on B's deploy for two values: the Cloud Run URL and the client key
4. Task 8 steps 5–6 — paste both into `InterrogationConfig.asset`, play-test
5. Task 9 steps 4–7 — `ROADMAP.md` §9 rewrite, README restructure, the grep check

> **Coordinate with Giorgi on the Unity half.** `CONCEPT.md` records him as the client owner,
> and he is not on this migration — so Task 8 lands in a file he owns day to day. Two things
> specifically: `InterrogationConfig.asset` must be edited through the Unity Inspector (hand
> editing the YAML corrupts its type metadata), and the play-test in step 6 needs the project
> actually running. A ten-minute handoff beats a corrupted asset.

---

## Sequence and the two blocking points

There are only **two** moments where a stream waits on another. Everything else is parallel.

```
        Day 1 (4 Aug)         Day 2 (5 Aug)          Day 3 (6 Aug)
A   ├── Task 1, Task 2 ──────► Task 3, wire B ──────► Task 6 ──┐
                                                                │ image
B   ├── auth.py, limits.py ──► GCP prep, secrets ──────────────►├─► Task 7 deploy ──┐
    │                                                           │                    │ URL + key
C   ├── Task 8 code ─────────► PRIVACY.md, CONCEPT ────────────►┘                    ├─► asset, play-test
                                                                                     └─► ROADMAP, README
```

**Block 1:** A cannot finish Task 6 until B's two modules are merged. Mitigated by B writing
them first — they are ~40 lines each and should land on day one.

**Block 2:** C cannot fill in `InterrogationConfig.asset` until B's deploy produces a URL and a
client key. Everything else in C's stream is done by then.

**Target: migration finished 6 Aug.** That leaves 7–8 Aug for the consistency tracker (A6) and
`DetectiveAction` — the features the pitch is actually about. If this slips past the 6th, cut
scope in the migration, not in the game.

## Branching and merge order

Currently everyone is on `main`. With three streams that will not hold.

```bash
git checkout -b cloud/backend      # A
git checkout -b cloud/security     # B
git checkout -b cloud/client-docs  # C
```

**Merge order: B → A → C.** B's files are all new, so they merge without conflict at any time.
A merges after, so the wiring lands against modules already on `main`. C touches no shared file
and can merge whenever.

Before every push, on every branch:

```bash
cd Sidecar && python3 -m pytest tests/ -q
```

**Green or it does not go to `main`.** Use `python3`, not `python` — `python` is not on PATH.

Rebase on `main` before pushing rather than merging `main` in; the history stays readable and
`app.py` conflicts surface once instead of twice.

Commit messages: conventional commits, one line, no body, no trailers.

## What each stream needs that the repo cannot give them

| Stream | Needs |
|---|---|
| A | Docker Desktop installed. `gcloud auth application-default login` for the Task 6 container run. The ElevenLabs key and voice id in a local `.env`. |
| B | **Owner access to the GCP project and its billing account.** `gcloud` was not installed on the machine this plan was written on — `brew install --cask google-cloud-sdk` first. |
| C | Unity 6 (`6000.5.6f1`) with the project open. `InterrogationConfig.asset` must be edited through the Inspector — hand-editing the YAML corrupts its type metadata. |

## Things all three of you should know

**The pinned instance is load-bearing.** `--max-instances 1` is not a cost tweak. Session
history and the prosody baseline live in the process's RAM; a second instance means the
detective forgets the interrogation mid-scene and the affect system — which measures *change
from how the player sounded at the start* — resets to nothing. Whoever redeploys, keep the flag.

**A redeploy drops every live session.** Deploy between playtests, never during one.

**ElevenLabs is not on the GCP credits.** Free tier is ~10k characters/month, a detective line
is ~200 characters — roughly 50 lines per month across all players. Fine for the 9 Aug demo,
not survivable for a public itch.io launch. `tts.synthesize()` is a single function with a
stable contract, so swapping to Google Cloud TTS is a one-file change *if* we hit the wall.
Nobody builds that now.

**Turn the Cloud Run service off after judging.** Always-on costs money whether anyone is
playing or not.

**The privacy claim inverts.** Several files currently promise the player their voice never
leaves their machine. After deploy that is false, and `CONCEPT.md`'s honesty rule makes fixing
it a submission blocker rather than cleanup. That is Task 9, and it is C's — but if anyone
spots a doc claiming on-device audio, say so.

## If you want it faster than this

The honest answer is that A is the critical path and splitting it further costs more in
coordination than it saves. If A is genuinely blocked, the next cut is Task 3 (Vertex) — it
touches only `llm.py` and `config.py`, is about fifteen lines, and can go to B *provided A has
not started editing `config.py` yet*. Agree that before either of you begins, not after.
