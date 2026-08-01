# A6 — consistency tracker: design proposal

**Author:** Ado · **Date:** 1 Aug 2026 · **Status:** ⚠ **proposal — needs a team call**

`CONCEPT.md` lists *"How consistency is tracked and scored across the interrogation"* under **Open
decisions**, and `AGENTS.md` forbids resolving one of those alone. So this is a proposal, not a
decision. It is written to be pasted into the group chat and argued with.

**Why this is urgent.** The pitch says consistency is *"the biggest determining factor of
investigation outcome"* — those are our words, submitted. Nothing in the build tracks it today.
Of everything on the board, this is the largest gap between what we claimed and what exists.

---

## 1. What has to be true

| | Requirement | Source |
|---|---|---|
| R1 | Detects that turn 9 contradicts turn 3, across a whole interrogation | Pitch |
| R2 | The detective can **act** on a specific contradiction — "you said the door was locked" | Pitch: *revisiting contradictions* |
| R3 | It is **visible** to the player that the detective caught it | Build Quality: genuine AI-driven behaviour |
| R4 | Never framed as lie detection. A contradiction is a fact about the *testimony*, not about the player | G6, non-negotiable |
| R5 | Off the critical path — the player must not wait on it | §2.2 latency |
| R6 | Testable without a mic, Unity, or a human | A6 acceptance criteria |

R4 is the one that is easy to get wrong. Memory contradicts itself constantly under stress; that
is the *subject* of the game, not a defect in the player. No field in this system may be named
`deception`, `lying`, `truthfulness`, or `credibility`.

## 2. Options

**A — LLM judge over the full transcript, every turn.** Ask Gemini "list contradictions in this
transcript" each turn. Simplest possible thing. But it re-derives everything each time, the answer
drifts turn to turn, cost grows quadratically, and it hands back prose the detective can't
reference precisely.

**B — Claim ledger (recommended).** After each answer, an async call extracts *atomic claims* into
a structured ledger. A second cheap pass checks each new claim against the existing ledger. The
ledger is durable, and contradictions are addressable objects with IDs.

**C — Embedding similarity between turns.** Cheap and fast, but it measures *topical* similarity.
"The door was locked" and "the door was open" are near-identical embeddings and opposite claims.
Rejected — it does not do the one job.

## 3. Recommendation — B, the claim ledger

Four reasons, in order of weight:

1. **The ledger *is* the detective's notebook.** We already want that panel. A structured ledger
   renders directly into it — the player watches claims accumulate and a contradiction get
   flagged. That is R3 solved by the data structure rather than by extra UI work.
2. **Bong gets something to write tactics against.** `revisit(contradiction_id="c_003")` is a
   tactic. A scalar `consistency: 0.62` is not — you cannot build an interrogation move out of it.
3. **It is honest about what it knows.** Each contradiction carries the two turns it came from, so
   any claim the detective makes is grounded in something the player actually said. No hallucinated
   accusations.
4. **It tests.** Fixture transcript in, known contradiction set out. R6 for free.

### Shape

```jsonc
// Claim — one per assertion the witness makes
{ "id": "cl_014", "turn": 3, "subject": "back door", "predicate": "state",
  "value": "locked", "verbatim": "the back door was locked, I'm sure of it",
  "hedged": false }

// Contradiction — produced when a new claim collides with an existing one
{ "id": "c_003", "claims": ["cl_014", "cl_031"], "subject": "back door",
  "kind": "direct" | "temporal" | "omission",
  "note": "turn 3: locked. turn 9: 'I pushed it open'." }
```

`hedged` matters: *"I think it was locked"* contradicting *"it was open"* is a weaker collision
than two confident statements. It also gives the detective a sympathetic move as well as a
pressing one — which is the character we described.

### Where it runs

In the turn handler, **after** the response goes back to the player, not before. The player never
waits on it; the result lands in time for the *next* turn. That satisfies R5 and matches how a
real interrogator works — they notice the discrepancy a beat late, then circle back.

## 4. What the team has to decide

I can build all of the above. These four are not mine to pick:

1. **Scalar or ledger to the outcome model?** I propose the ledger is the truth and any scalar is
   derived from it for display only. Anyone who wants a single number owns defining it.
2. **How heavily does consistency weigh against affect in the ending?** The pitch says consistency
   is the *biggest* factor. "Biggest" needs a number before the outcome model is written. — **Bong
   and whoever owns endings.**
3. **Second Gemini call per turn — who pays, and what is the cap?** Ties into A11. — **Vinay.**
4. **Does the notebook panel show the ledger live, or only at the end?** Live is far more
   dramatic and far more likely to expose a wrong flag on stage. — **Giorgi.**

## 5. Not in scope

No scoring of whether the witness is *telling the truth*. The system has no ground truth about the
player's intent and never will. It knows what was said, that two things said cannot both hold, and
how it was delivered. The gap between those and *guilt* is the game.
