# FALSE POSITIVE

**An AI-powered psychological mystery game.**
*The detective can hear your fear. It cannot tell why you are afraid.*

You witness a crime through brief, chaotic, incomplete glimpses. Then you are interrogated —
hands-free, by voice, with no dialogue options — by an autonomous AI detective. You can tell
the truth, hide your uncertainty, protect someone, or lie. The detective reads two channels at
once: an LLM tracking meaning, timeline and consistency, and HuBERT-based speech
representations reading hesitation, tension, confidence, pauses and cadence. It never claims to
know that you are lying. It only knows that something moved you — and fear might be guilt,
faulty memory, or the pressure of being falsely accused.

Submission for the **Garena AI Build Challenge 2026** (theme: *Reimagine Digital Entertainment
Experiences*). Shortlisted; deliverables due **9 Aug 2026**.

---

## Documentation

Read these before writing any code or making any design call.

| Doc | What it is |
|---|---|
| [`docs/CHALLENGE_BRIEF.md`](docs/CHALLENGE_BRIEF.md) | The Garena case brief — deliverables, judging criteria and weights, rules, timeline. **Authoritative.** |
| [`docs/CONCEPT.md`](docs/CONCEPT.md) | The shortlisted pitch verbatim, the load-bearing ideas, the non-negotiables, and what is still undecided. |
| [`docs/DELIVERABLES.md`](docs/DELIVERABLES.md) | Live checklist for the three submission items and the submission mechanics. |
| [`AGENTS.md`](AGENTS.md) | Orientation for AI agents working in this repo. |
| [`docs/reference/`](docs/reference) | The original case brief PDF, as received. |

## Status

Pre-implementation. The repo currently holds documentation only — the stack is not chosen and
no prototype code exists yet. Open decisions are listed at the bottom of
[`docs/CONCEPT.md`](docs/CONCEPT.md); raise them rather than picking silently.

## Setup

Not applicable yet. Setup instructions are a **required** part of the submitted README —
write them here as soon as there is something to run.

## Architecture

Not applicable yet. An architecture overview is a **required** part of the submitted README.

## Third-party components

Disclosure of every third-party library, model, dataset and API is **required** at submission.
Record additions here as they are introduced.

| Component | Type | Used for | Licence / terms |
|---|---|---|---|
| _(none yet)_ | | | |

Known intent from the pitch: a goal-oriented LLM for the detective, and HuBERT-based speech
representations for prosody. Specific providers and model versions are not yet chosen.

## Credentials

No API keys, passwords, or `.env` files are ever committed — the brief prohibits it explicitly.
Use `.env.local` (git-ignored) and document the required variable *names* in `.env.example`.
