# `idea/` — storyline submissions

One case ships. This folder is where the five of us pitch it, and where we record which one won.

The case is listed as an **open decision** in [`docs/CONCEPT.md`](../docs/CONCEPT.md) — *"the case
itself: crime, cast, ground truth, and what 'the case unfolds' resolves into."* Nobody resolves it
alone. Write your idea here, we read all five, we pick one, and the decision gets recorded back in
`CONCEPT.md` with the date.

## Who writes what

| File | Person |
|---|---|
| [`vinay.md`](vinay.md) | Vinay |
| [`bong.md`](bong.md) | Bong |
| [`giorgi.md`](giorgi.md) | Giorgi |
| [`marcel.md`](marcel.md) | Marcel |
| [`ado.md`](ado.md) | Ado |

Everyone writes in their own file, nobody edits anyone else's. Copy
[`TEMPLATE.md`](TEMPLATE.md) into yours and fill it in. This is the one folder outside the
ownership table in [`AGENTS.md`](../AGENTS.md) — all five of us write here.

## Proposed timing

Derived from the schedule in `docs/IMPLEMENTATION_PLAN.md` §7 — case content has to be in by
**D5, Wed 5 Aug**, and Ado has to author it as YAML before that. So:

- **End of D2, Sun 2 Aug** — five ideas in.
- **D3, Mon 3 Aug** — team picks one, records it in `CONCEPT.md`.

Confirm this in chat; it is a proposal, not a decree.

---

## The premise you are writing inside

**The player is an eyewitness who is also a suspect.** That is straight out of the pitch — *"not
in the role of the detective, but as an eyewitness and potential suspect."* They saw something.
They are now in a room being questioned about it. They may be entirely innocent; the title is the
thesis.

The player answers **out loud, in their own words**. An autonomous AI detective listens to two
things at once: what they say (meaning, timeline, consistency) and how they say it (hesitation,
tension, cadence). It never claims to catch a lie. It only knows something moved them — and fear
reads the same whether it comes from guilt, bad memory, or being wrongly accused.

## Hard constraints — an idea that breaks one of these cannot be built

These come from `docs/CONCEPT.md` and are not preferences.

1. **The player is never the detective.** No evidence boards, no deductions, no accusations by
   the player. They are answering questions, not solving anything.
2. **Voice only.** No dialogue menus. If your story needs the player to pick from a list, it is a
   different game.
3. **The crime is seen in three glimpses, ~8 seconds total, and never replayed.** Whatever your
   crime is, it has to be legible in three fragments from one person's vantage point. Imperfect
   memory is the mechanic, not a bug.
4. **No lie detection.** No truth meter, no "deception detected." The system reads affect.
5. **Consistency decides the outcome.** Your case has to give the player enough to contradict
   themselves about — times, colours, order of events, who was where.
6. **The detective's tactics come from the model**, not a branch table. Do not script the
   interrogation beat by beat; give the detective goals and material.

## Practical limits — respect these or the idea will not fit in eight days

- **One interrogation room.** Whatever set the questioning happens in, we build one of it.
- **Three glimpses is the entire crime cinematic.** Giorgi builds them. Stills with sound and
  camera movement is realistic; a car chase is not.
- **10–14 turns, roughly 10 minutes.** One sitting, no acts, no scene changes.
- **Cast of 3–5**, all off-screen. Nobody but the detective has a voice.

## What "makes sense" means here

The crime has to hold up if a judge thinks about it for thirty seconds.

- **The player has to plausibly be there.** Why was this person at that place at that time, close
  enough to see it, and not close enough to see all of it?
- **The suspicion has to be plausible too.** Why is a witness being questioned like a suspect?
  Proximity, a prior connection, a coincidence that looks bad — give the detective a real reason.
- **The gaps have to be natural.** The player misses things because of where they stood, how fast
  it happened, what was in the way — not because the game withheld it.
- **An innocent player must be able to look guilty.** That is the whole game. If your case only
  works when the player is actually guilty, it is not this game.

## Repo rules that apply to your idea

**This repo is public.** Everything you invent is fiction: no real people, no real companies, no
real crimes, no recognisable real locations. Do not base a case on a news story.

Keep it grounded. No gore for its own sake, no sexual violence, no real-world hate targets — a
judge is going to watch this on a stage. The tension we want comes from being disbelieved, not
from the crime being horrible.

---

## How we pick

Score each idea against [`DECISION.md`](DECISION.md). Short version — the winner is whichever
case produces the most *contradictions worth catching* per minute of interrogation, at the lowest
art cost. Cleverness of the twist matters less than whether a nervous player is going to trip over
their own testimony in front of a judge.
