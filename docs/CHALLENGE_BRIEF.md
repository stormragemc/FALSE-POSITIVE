# Garena AI Build Challenge 2026 — Case Brief

> Transcribed from `Garena AI Build Challenge 2026 - Case Brief.pdf`, received 31 Jul 2026.
> This is the authoritative statement of what we are being judged on. When a decision is
> contested, this document wins over anything else in the repo.

**Theme: Reimagine Digital Entertainment Experiences**

---

## The Challenge

> Design and build an AI-powered product, tool, experience, or workflow that reimagines
> digital entertainment. Your functional prototype should demonstrate the complete core
> experience or workflow, and expected features.

Artificial intelligence is creating new possibilities across digital entertainment: from how
content and experiences are created and discovered to how communities are supported and
operations are managed. The challenge is to identify a meaningful problem within this
ecosystem and build an AI-powered solution that creates clear value.

Digital entertainment encompasses the content, platforms, communities, creators, and
technologies that shape how people play, watch, listen, create, express themselves, and
connect with others. Solutions may explore gaming, streaming, music, video, social platforms
and communities, creator tools, virtual worlds, emerging forms of digital entertainment, and
beyond.

Solutions may be consumer-facing or built for the people and teams behind these experiences,
including audiences, creators, developers, moderators, publishers, operations teams,
customer-support teams, and other stakeholders.

There is no single correct solution. Teams are free to define the problem, target audience,
solution format, AI approach, and technology stack. The brief explicitly encourages **bold
ideas, thoughtful problem-solving, and innovative uses of AI that push the boundaries of
digital entertainment.**

---

## Expected Deliverables

Submit **three items** by email to **outreachsg@garena.com** with a link to a **Google Drive
folder** containing the deliverables. The Drive folder name must accurately reflect the team
name. **Do not modify any part of the deliverables after submission.**

### 1. Slide Deck Proposal

- Format: **PDF, maximum 15 slides.**
- Must minimally include:
  - Problem statement
  - Target users or stakeholders
  - Proposed solution
  - How AI contributes to the solution
  - Expected impact
  - Technical decisions and implementation details

### 2. Source Code of Working End-to-End Prototype

- Build a functional prototype that demonstrates the **complete core experience or workflow,
  and expected features.**
  - For user-facing products: show how users interact with the solution, how AI contributes,
    and what outcome is created.
  - For tools or workflows: show the initial trigger or input, key workflow steps, use of
    models/agents/tools/APIs, human review points (if any), exception handling, and the final
    output or action.
  - **Production readiness is not expected, but static mock-ups or conceptual presentations
    alone are insufficient.**
- Submit a **GitHub repository or equivalent** containing:
  - Complete source code
  - A **README with setup instructions**
  - An **architecture overview**
  - **Relevant prompts or agent configurations**
  - **Disclosure of third-party libraries, models, datasets, and APIs used**
  - **No passwords, API keys, or other confidential credentials**
- If submitting via GitHub: set the repository to **private** and add
  **`garena-ai-build-challenge`** as a collaborator, then include the repository link in the
  Google Drive submission (e.g. in a Google Doc or text file).
- If submitting as a folder inside the Drive submission instead, it must contain the same
  required contents.
- **Build quality of prototypes is assessed as part of the judging criteria.**

### 3. Demo Video

- **Maximum 5 minutes.**
- Must demonstrate the complete core experience or workflow, and expected features of the
  working prototype.

---

## Judging Criteria

| Weight | Criterion | What it means |
|---|---|---|
| **40%** | **Problem–Solution Fit** | Importance and clarity of the problem, understanding of users or stakeholders, quality of the product or operational insight, strength of the solution, suitability of AI, expected impact, and prioritisation of trade-offs. |
| **30%** | **Build Quality** | Whether the prototype functions end-to-end and clearly demonstrates the value of the solution **in a live setting**. Key considerations: reliability, genuine use of AI-driven behaviour, soundness of model/tool/data integration for the stated use case, and appropriate human review or exception handling where relevant. |
| **30%** | **Originality** | Originality of the problem insight, creativity of the solution, differentiation from existing products or workflows, and uniqueness of execution. |

---

## Technology Guidelines

- **No prescribed technology restrictions.** Any programming language, AI model, framework,
  cloud platform, third-party API, open-source software, or low-code/no-code tool is allowed.
- Teams are responsible for:
  - Ensuring their solution **can be evaluated**
  - **Disclosing third-party components**
  - Complying with applicable **licences and usage terms**
  - Protecting **confidential or personal information**

---

## Timeline

| Date | Milestone |
|---|---|
| 31 Jul 2026 | Case brief received (today, at time of writing) |
| **9 Aug 2026** | **Deliverables submission** |
| 13 Aug 2026 | Finalists announced |
| 23 Aug 2026 | Final presentations |

---

## Reading of the brief (our interpretation, not the brief's words)

- **"Live setting" is in the Build Quality text.** The prototype has to survive being run in
  front of someone, not just play back a recording. Anything that can only work as a video is
  worth 0 of the 30%.
- **"Genuine use of AI-driven behaviour"** rules out a scripted decision tree wearing an LLM
  costume. The detective's adaptation must actually be driven by model output.
- **"Appropriate human review or exception handling where relevant"** — for us this maps to
  what happens when speech recognition fails, the player says nothing, or the model produces
  something incoherent. Handling those visibly is scored, not a distraction.
- **Problem–Solution Fit is the single biggest block at 40%.** The deck must argue *why this
  problem matters* before it argues that the tech is cool.
- **Privacy is a licence-and-personal-information obligation**, and we process voice. Say out
  loud in the deck and README what is recorded, where it goes, and what is deleted.
