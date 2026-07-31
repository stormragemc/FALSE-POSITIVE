# Deliverables tracker — due 9 Aug 2026

Nine days from the case brief (31 Jul) to submission (9 Aug). Source of truth for *what* is
required: [`CHALLENGE_BRIEF.md`](./CHALLENGE_BRIEF.md). This file tracks *where we are*.

Status key: ☐ not started · ◐ in progress · ☑ done

---

## 1. Slide Deck Proposal — PDF, max 15 slides

☐ Not started.

Required sections (from the brief), each mapped to the judging weight it serves:

| ☐ | Section | Serves |
|---|---|---|
| ☐ | Problem statement | Problem–Solution Fit (40%) |
| ☐ | Target users / stakeholders | Problem–Solution Fit |
| ☐ | Proposed solution | Problem–Solution Fit + Originality |
| ☐ | How AI contributes to the solution | Build Quality + Originality |
| ☐ | Expected impact | Problem–Solution Fit |
| ☐ | Technical decisions and implementation details | Build Quality |

Notes:
- The differentiator slide has to make the genre inversion legible fast: the player is the
  *witness*, and the AI is the opponent that hears affect, not lies.
- Trade-off prioritisation is named explicitly in the 40% criterion — include what we chose
  *not* to build and why.

## 2. Source code of a working end-to-end prototype

◐ Repo initialised; documentation only so far. No prototype code yet.

Repo requirements checklist:

| ☐ | Requirement |
|---|---|
| ☐ | Complete source code |
| ☐ | README with **setup instructions** |
| ☐ | **Architecture overview** |
| ☐ | **Relevant prompts / agent configurations** committed (the detective's system prompt, goal policy, and any HuBERT-side config) |
| ☐ | **Disclosure of third-party libraries, models, datasets, APIs** |
| ☑ | **No credentials committed** — no keys, passwords, or `.env` files in git history |
| ☐ | Repo set to **private** on GitHub |
| ☐ | **`garena-ai-build-challenge`** added as a collaborator |
| ☐ | Repo link placed in the Google Drive submission (Google Doc or text file) |

The prototype must demonstrate the *complete core experience*: witness the crime → hands-free
interrogation → the detective adapting on both channels → an outcome shaped by what was said.
A vertical slice of the full loop beats a polished fragment of one stage.

Exception handling is scored — decide and implement what happens when ASR returns nothing, the
player stays silent, the network stalls, or the model returns something unusable.

## 3. Demo video — max 5 minutes

☐ Not started.

Must show the complete core experience and expected features of the *working prototype*. Plan
for a real playthrough capture; static mock-ups are explicitly called insufficient for the
prototype, and the video is judged against what the prototype actually does.

---

## Submission mechanics

- **Email:** `outreachsg@garena.com`
- **Body:** link to a **Google Drive folder** whose name accurately reflects the **team name**
  — *team name TBD/confirm before submitting*.
- Folder contains: the deck PDF, the demo video, and the repo link (or a code folder with the
  same required contents).
- **Nothing may be modified after submission.** Freeze the repo — no "one last fix" pushes
  after the email goes out.

## Dates

| Date | Milestone |
|---|---|
| 31 Jul 2026 | Case brief received |
| **9 Aug 2026** | **Deliverables due** |
| 13 Aug 2026 | Finalists announced |
| 23 Aug 2026 | Final presentations |
