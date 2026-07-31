# FALSE POSITIVE — the concept

> **An AI-powered psychological mystery game.**
> *The detective can hear your fear. It cannot tell why you are afraid.*

This is the shortlisted pitch, recorded verbatim so that every agent and team member works
from the same text. Do not paraphrase it into something new; if the concept changes, edit
this file and say what changed.

---

## Canonical pitch (as submitted)

> We wanna build a new way to interact with digital entertainment, a game experience that
> doesn't wait for players to select from scripted dialogue options, but listens, interprets
> and responds to them in real time. False Positive is a voice-driven psychological mystery
> game that places the player not in the role of the detective, but as an eyewitness and
> potential suspect. The player first witnesses a sudden crime through brief, chaotic and
> incomplete glimpses, forcing them to rely on their actual memory during a hands-free
> interrogation. They can tell the truth, conceal uncertainty, protect someone else or
> deliberately mislead the investigator, with every spoken response shaping how the case
> unfolds.
>
> The interrogation is led by an autonomous AI detective powered by a goal-oriented LLM and
> HuBERT-based speech representations. While the LLM analyses meaning, timeline and
> CONSISTENCY (biggest determining factor of investigation outcome) of the player's testimony,
> HuBERT extracts acoustic and prosodic signals such as hesitation, vocal tension, confidence,
> pauses and changes in cadence. The detective uses both layers to adapt its strategy, pressing
> harder when it detects uncertainty, revisiting contradictions, changing tone, setting verbal
> traps or becoming unexpectedly sympathetic. Rather than claiming to know whether the player
> is lying, the system only knows when something affects them emotionally, creating the central
> tension: fear may signal guilt, faulty memory or the pressure of being falsely accused.
>
> By making multimodal AI the game's primary opponent, False Positive creates unique,
> emotionally responsive interrogations while helping players practice composure, clear
> thinking and consistent communication under pressure. These skills are useful in interviews,
> presentations and other high-stakes situations.

---

## The load-bearing ideas

Everything below is a restatement of the pitch, in the form the build has to respect.

1. **The player is the witness, not the detective.** The genre inversion is the originality
   claim. Any design decision that quietly turns the player back into an investigator (giving
   them evidence panels, deduction boards, accusation menus) destroys the pitch.
2. **Memory is the real mechanic.** The crime is shown in brief, chaotic, incomplete glimpses
   on purpose. The player's imperfect recall is the game's source of difficulty — not a bug to
   be smoothed over with a replay button.
3. **Voice is the only input.** No scripted dialogue options. Hands-free. The moment we add a
   list of things to click, we are a different game.
4. **Consistency is the biggest determining factor of the outcome.** Stated explicitly in the
   pitch. The scoring must reflect this, and the detective must be seen to catch
   contradictions.
5. **Two channels, deliberately separated.**
   - **LLM** — meaning, timeline, consistency of the testimony.
   - **HuBERT** — acoustic/prosodic signal: hesitation, vocal tension, confidence, pauses,
     cadence change.
6. **The system never claims to detect lies.** It detects *affect*. This is the ethical spine
   and the central dramatic tension at once: fear may mean guilt, faulty memory, or the
   pressure of being falsely accused. The name is the thesis — a false positive.
7. **The detective is an opponent, not a narrator.** Goal-oriented: it presses on uncertainty,
   revisits contradictions, changes tone, sets verbal traps, or turns sympathetic.
8. **The secondary claim is transferable skill** — composure, clear thinking, and consistent
   communication under pressure (interviews, presentations, high-stakes conversation). Useful
   for the impact slide; must not be oversold into an ed-tech pitch.

## Non-negotiables (violating these breaks the pitch, not just the build)

- No dialogue-option menus, ever.
- No "lie detected" UI, verdict, or percentage-of-truth meter presented as truth detection.
- No perfect replay of the crime on demand.
- The detective's behaviour must come from model output, not a scripted branch table
  (Build Quality explicitly scores "genuine use of AI-driven behaviour").
- Voice data handling is stated plainly to the player and in the README.

## Open decisions

These are **not decided yet**. Do not treat any of them as settled, and do not silently pick
one — raise it.

- Platform and stack (web vs native; ASR choice; TTS voice for the detective).
- How HuBERT features are consumed: raw representations vs a trained probe vs
  hand-derived prosodic features, and what the detective actually receives.
- Whether prosody arrives as a signal the LLM sees, or as a separate policy that steers it.
- The case itself: crime, cast, ground truth, and what "the case unfolds" resolves into.
- How consistency is tracked and scored across the interrogation.
- Endings / outcome model.
- Latency budget and the fallback when a response is slow or ASR fails.

## Honesty rule

Any claim in the deck, README, or demo must be true of the code that exists at the time of
submission. If something is a mock, a pre-recorded sample, or a fallback path, label it as one
in the same breath. Judges will run the prototype in a live setting; a claim that dies on stage
costs more than the feature was worth.
