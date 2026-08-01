# Case idea — *[title]*

**By:** Marcel · **Date:** [date]

> One or two sentences. What crime, who is the player, and why are they in the chair?

---

## 1. Ground truth — what actually happened

The real sequence of events, in order, including the parts the player never sees. This is the
answer key. Nobody but us ever reads it.

Say plainly: **did the player do it?** (Our default is no — the interesting case is an innocent
person under pressure. If you want them guilty, or ambiguous, argue for it.)

## 2. Why the player is there

Two things to establish:

- **How they witnessed it** — where they were standing, why they were there at all.
- **Why they are a suspect and not just a witness** — what makes the detective look twice at them.

## 3. The three glimpses

This is the entire crime cinematic: three fragments, ~8 seconds total, never replayed. Write what
the player actually perceives — including what is obscured, and what they hear but do not see.

| # | ~sec | What the player sees / hears | What they cannot tell |
|---|---|---|---|
| 1 | 3 s | | |
| 2 | 3 s | | |
| 3 | 2 s | | |

**What the glimpses deliberately withhold:** …

## 4. The cast

3–5 people, all off-screen. One line each: who they are, and their relationship to the player and
to the crime.

| Name | Who they are | Where they sit in this |
|---|---|---|
| | | |

## 5. Topics the detective wants covered

Six to ten. These are the spine — the detective works through them, and consistency is tracked
across them. Each one has to be something the player can answer differently twice.

| Topic | The question behind it | Where a contradiction can open |
|---|---|---|
| `example_topic` | | |

## 6. The traps

At least three. A trap is a detail the player **cannot possibly have seen** from where they were.
If they confidently describe it, they have fabricated — and the detective can catch that.

| Trap | Why it is unseeable | How the detective baits it |
|---|---|---|
| | | |

## 7. The false positive

The heart of it. **How does an innocent, honest player end up looking guilty?** What in this case
makes a truthful person hesitate, misremember, or protect someone — and read as evasive?

## 8. How the endings land

Four endings exist (`docs/IMPLEMENTATION_PLAN.md` §4.3). Write the last line the player hears in
each, in this case's voice.

| Ending | Condition | What it sounds like here |
|---|---|---|
| `released_clean` | High consistency, no fabrications | |
| `released_suspicious` | Consistent but the detective never got enough | |
| `held_for_questioning` | Contradictions unresolved | |
| `charged` | Hard contradictions or a caught fabrication | |

## 9. Production cost

Be honest — Giorgi has to build this in eight days.

- **Interrogation room:** …
- **The three glimpses:** …
- **Audio:** …
- **Anything unusual this needs that we do not already have:** …

## 10. Self-check

Tick these yourself before you call it done.

- [ ] Player is a witness/suspect, never the detective
- [ ] Nothing here needs a dialogue menu
- [ ] The crime is legible in three glimpses, ~8 s, from one vantage point
- [ ] No lie-detection framing anywhere in the fiction
- [ ] There are at least six topics a player can contradict themselves on
- [ ] At least three traps
- [ ] An innocent player can plausibly look guilty
- [ ] One room, cast of 3–5, 10–14 turns
- [ ] Entirely fictional — no real people, companies, crimes or locations
- [ ] Nothing that would be ugly to demo on a stage
