# Deliverables tracker — due 9 Aug 2026

Nine days from the case brief (31 Jul) to submission (9 Aug). Source of truth for *what* is
required: [`CHALLENGE_BRIEF.md`](./CHALLENGE_BRIEF.md). This file tracks the **three submitted
artefacts**.

> **For engineering status — what is built and what is not — read
> [`ROADMAP.md`](./ROADMAP.md).** That is the live tracker and the file you update when you
> finish work. This file covers only the deck, the video, and the repo's submission
> requirements.

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

◐ **A working vertical slice exists** as of 1 Aug 2026. Giorgi's Unity branch is merged into
trunk: Unity client + local Python sidecar running mic → STT → speech emotion → LLM → TTS →
playback with lip sync. The parts that carry the *pitch* rather than the plumbing are not built —
consistency tracking, structured detective tactics, the case as data, and the outcome model.

Repo requirements checklist:

| ☐ | Requirement |
|---|---|
| ◐ | Complete source code — the voice loop runs; the game around it does not exist yet |
| ☑ | README with **setup instructions** — written 1 Aug; ⚠ not yet clean-machine tested by someone who did not write it |
| ☑ | **Architecture overview** — README + `IMPLEMENTATION_PLAN.md` §2, both redrawn to match the code |
| ☐ | **Relevant prompts / agent configurations** committed — ⚠ **currently violated.** The detective's persona and the crime premise are inline string literals in `Sidecar/llm.py`. This is a *graded* deliverable. **Bong extracts them to files** |
| ☑ | **Disclosure of third-party libraries, models, datasets, APIs** — table filled in 1 Aug; ⚠ several licences still marked unconfirmed, including **IEMOCAP** (restrictive academic licence) and **Avaturn** redistribution rights for a public repo |
| ☑ | **No credentials committed** — no keys, passwords, or `.env` files in git history |
| ☑ | Repo visibility — **staying public** (see decision below) |
| ☐ | Repo link placed in the Google Drive submission (Google Doc or text file) |

**Repo visibility decision (31 Jul 2026):** the brief's wording is *"set your repository to
private and add `garena-ai-build-challenge` as a collaborator"* — the collaborator step exists
to grant the judges access to a private repo. We were advised to keep the repo **public**, so
neither step applies; judges can read it directly. The submitted Drive folder still needs the
repo link. Revisit only if Garena asks for private.

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
