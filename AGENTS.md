# Working in this repo

This is a competition submission for the **Garena AI Build Challenge 2026**, due **9 Aug 2026**.
Read these before doing anything:

1. [`docs/CHALLENGE_BRIEF.md`](docs/CHALLENGE_BRIEF.md) — what we are judged on. Authoritative.
2. [`docs/CONCEPT.md`](docs/CONCEPT.md) — the pitch, the non-negotiables, and the open decisions.
3. [`docs/DELIVERABLES.md`](docs/DELIVERABLES.md) — what still has to exist by the deadline.
4. [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) — who owns what, the frozen
   contracts, and the schedule.

---

## Before you touch anything: the identity gate

Five people work in this repo — **Vinay, Bong, Giorgi, Marcel, Ado** — and each owns a different
part of the build. An agent that starts editing without knowing which of them it is working for
will write into someone else's box.

**If the person driving you has not told you their name and what they are working on, ask them —
and do nothing else.** No file reads beyond this document, no edits, no commands, no plan. Just:

> Which of you is this — Vinay, Bong, Giorgi, Marcel or Ado — and which workstream are you on?

Wait for the answer. Do not guess from git config, from the branch name, or from what the last
session was doing. Do not offer to "start with something safe in the meantime." One question,
then stop.

Once you have the answer, work only inside that person's directory:

| Person | Workstream | Directory |
|---|---|---|
| Giorgi | Unity client | `unity/` |
| Marcel | Prosody / HuBERT — **owner** | `service/prosody/` |
| Bong | Detective agent, and assisting on prosody | `service/detective/`, `prompts/`, `service/prosody/` |
| Vinay | AI security | `service/security/`, `docs/SECURITY.md`, `docs/PRIVACY.md` |
| Ado | Core service, docs, tests, CI, and assisting on prosody | `service/core/`, `service/cases/`, `docs/`, `tests/`, `service/prosody/` |

Ado does not work in `unity/` — that is Giorgi's, always.

If the task you have been given belongs to someone else's directory, say so and stop. The fix is
for the two humans to talk, not for you to reach across.

**`service/prosody/` is the one shared directory.** Marcel owns it; Bong and Ado build in it too.
If you are working for Bong or Ado inside `service/prosody/`:

- Do **not** change the shape of `ProsodySignal`. It is Marcel's contract and four other things
  depend on it. If the task seems to require a schema change, stop and say so.
- Do **not** edit the HuBERT files — checkpoint loading, hidden-state extraction,
  `hubert_instability`, `hubert_baseline_distance`. Those are Marcel's. The classical features and
  the test rig are the shared surface.
- Branch under the name of the person you are working for, never `marcel/...`, and say in the
  summary that Marcel has to review it.

## Branches

If the work needs a new branch, **create one with the owner's name in it** and say that you did:

```
<name>/<short-kebab-summary>
```

`giorgi/mic-capture-ptt` · `marcel/hubert-baseline` · `bong/tactic-selection` ·
`vinay/output-safety-filter` · `ado/session-orchestrator`

Never open a branch under a name that is not the person you are working for. If you are unsure
whether the work warrants a branch, ask.

## The integration gate

Build your own box against the frozen contracts in
[`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) §3. **Do not wire workstreams
together, tune the whole pipeline, or polish the final product** until one of these is true:

1. The single-workstream boxes in the plan's Definition of Done (§5) are checked off, **or**
2. A member of the team tells you the project is finished and it is time to wire everything up.

Until then, cross-workstream integration is out of scope no matter how obvious it looks. After
the gate opens, the job changes: integration, latency, polish, the demo script, the deck.

---

## What is being graded

- **Problem–Solution Fit — 40%.** The largest block. Clarity of the problem and of who it is
  for beats feature count.
- **Build Quality — 30%.** The prototype must run **end-to-end in a live setting**. Reliability,
  *genuine* AI-driven behaviour, sound model/tool/data integration, and visible exception
  handling.
- **Originality — 30%.** The genre inversion (player as witness, AI as opponent) is the claim.

## Rules that constrain the code

- **This repo is public.** Anything committed is readable by anyone. No credentials, no
  personal data, no voice recordings of real people.
- **No credentials in the repo.** No keys, passwords, or `.env` files — stated prohibition in
  the brief. `.env.example` with variable names only.
- **Prompts and agent configurations are a required deliverable.** Keep the detective's system
  prompt, goal policy, and model configs in version-controlled files, not inline string
  literals buried in application code.
- **Every third-party library, model, dataset and API must be disclosed**, with licence terms
  respected. Add to the table in the README when introduced.
- **Static mock-ups are explicitly insufficient.** A hardcoded fake of a stage is only
  acceptable as a labelled fallback behind a real path.
- **We process voice.** Be explicit in code and docs about what is recorded, where it is sent,
  how long it is kept, and what is deleted.

## Design lines you may not cross

From [`docs/CONCEPT.md`](docs/CONCEPT.md) — these are the pitch, not preferences:

- The player is the **witness**, never the detective.
- **Voice only.** No dialogue-option menus.
- No perfect replay of the crime on demand — imperfect memory is the mechanic.
- The system detects **affect, not lies**. No truth meter, no "lie detected" verdict presented
  as fact.
- **Consistency** is the biggest determining factor in the outcome.
- The detective's strategy comes from model output, not a scripted branch table.

## Honesty

Claims in the deck, the README, and the demo video must be true of the code as it exists at
submission. Label mocks, pre-recorded samples, and fallbacks as what they are. Judges run the
prototype live; a claim that dies on stage costs more than the feature was worth.

## Conventions

- Working conventions for agents — skills, planning gates, review gates, commit format — are in
  [`docs/AGENT_SETUP.md`](docs/AGENT_SETUP.md). Read it. Where it conflicts with this file, this
  file wins.
- Commit messages: conventional commits, one line. `feat(detective): …`, `fix: …`, `docs: …`.
- Do not resolve an item from the "Open decisions" list in `docs/CONCEPT.md` unilaterally —
  raise it, then record the decision in that file when it is made.
