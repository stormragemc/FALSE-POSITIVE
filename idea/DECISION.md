# Picking the case

Five ideas, one ships. This is how we choose and where the choice gets written down.

## Scoring

Each of us scores every idea — including our own — 1–5 on each row. Highest total wins; a tie
goes to whichever is cheaper to build.

| # | Criterion | Why it is weighted this way |
|---|---|---|
| 1 | **Contradiction surface** | How many things can a player say two different ways across 10–14 turns? Consistency is the biggest determining factor of the outcome — a case with nothing to contradict has no game in it. Score this highest. |
| 2 | **The false positive works** | Can an innocent, honest player look guilty? If the case only bites when the player is actually guilty, it is not this game. |
| 3 | **Legible in three glimpses** | ~8 seconds, one vantage point, no replay. If the crime needs more than that to make sense, it does not fit. |
| 4 | **Traps** | Details the player cannot have seen. Cheapest sharp mechanic we have — count them and check they are genuinely unseeable. |
| 5 | **Plausibility** | Does it survive thirty seconds of a judge thinking about it? Why was the player there, why are they a suspect, why are the gaps natural? |
| 6 | **Build cost** | One room, three glimpses, cast of 3–5, eight days, and Giorgi already has a client to finish. Cheap scores high. |
| 7 | **Demos well** | It gets played live on a stage. Grips fast, no explanation needed, nothing ugly. |

## Grid

Fill in once the ideas are in.

| Idea | Contradiction | False positive | Glimpses | Traps | Plausibility | Cost | Demo | Total |
|---|---|---|---|---|---|---|---|---|
| Vinay | | | | | | | | |
| Bong | | | | | | | | |
| Giorgi | | | | | | | | |
| Marcel | | | | | | | | |
| Ado | | | | | | | | |

## Grafting

The winner does not have to win alone. If another idea has a better trap, a sharper reason the
player is a suspect, or a cleaner ending, take it. Record what got grafted in and from whom.

## Recording the decision

The case is an open decision in [`docs/CONCEPT.md`](../docs/CONCEPT.md). Once we have picked:

1. Move the line **"The case itself: crime, cast, ground truth…"** out of *Open decisions* and
   into the *Decisions made* table, with the date and who took it.
2. Ado authors the winner as `Sidecar/cases/case_01_<slug>.yaml` (plan §8, item A5) — ground
   truth, glimpses, cast, topics, traps. It lives as data, never inside the detective's prompt.
3. Bong's prompt reads the case from that file, not from a hardcoded premise string.
4. Rename `case_01_the_stairwell` throughout the docs to match whatever we actually picked.

## Outcome

> _Written here when decided — date, winner, what was grafted in, who dissented._
