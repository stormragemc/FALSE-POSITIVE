# Working in this repo

This is a competition submission for the **Garena AI Build Challenge 2026**, due **9 Aug 2026**.
Read these before doing anything:

1. [`docs/CHALLENGE_BRIEF.md`](docs/CHALLENGE_BRIEF.md) — what we are judged on. Authoritative.
2. [`docs/CONCEPT.md`](docs/CONCEPT.md) — the pitch, the non-negotiables, and the open decisions.
3. [`docs/DELIVERABLES.md`](docs/DELIVERABLES.md) — what still has to exist by the deadline.

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

- Commit messages: conventional commits, one line. `feat(detective): …`, `fix: …`, `docs: …`.
- Do not resolve an item from the "Open decisions" list in `docs/CONCEPT.md` unilaterally —
  raise it, then record the decision in that file when it is made.
