# Task §7 — the traps (fabrication detection)

**Owner: Vinay.** Depends on nothing that is still in flight; §8's `EndingSelector`
depends on this.

---

## 1. What this is

`STORY_SCRIPT.md` §7 lists six details David **cannot** know, because he was
outside, unconscious, or blackout drunk. The officer baits each one. A confident
answer to any of them is a **fabrication** — the witness stating as memory
something he never witnessed.

This is the game's thesis, not a side feature. §7 says of one of them:

> naming Aaron for the bolt is a *fabrication that happens to be correct.* The
> officer catches the fabrication. Being right is not the same as having seen it
> — and the game punishes the gap. This is the thesis in one mechanic.

Right now the game cannot catch any of it.

---

## 2. What already exists

| Piece | Where | State |
|---|---|---|
| `RecordFabrication(string trapId)` | `Scripts/Flow/SessionScore.cs:49` | defined, **called from nowhere** |
| `CaughtFabrications` counter | `SessionScore.cs:34` | never incremented |
| `SetCredibility(float)` | `SessionScore.cs:61` | never called |
| `Credibility` | `SessionScore.cs:30` | hardcoded `1f`, comment says nothing reads it |
| Witness knowledge → officer | `MemoryFlags.Describe()` → `scene_instruction` | ✅ wired (A7b) |
| Per-flag knowledge sentences | `Assets/_Project/Prompts/memory_flags.txt` | ✅ 16 rows |
| Transcript of what the player said | `SidecarTurnResponse.transcript` | ✅ every turn |

So the plumbing exists. **Nothing populates it.**

---

## 3. The six traps, and the flag each one tests

`memory_flags.txt` already carries a present/absent sentence per flag. The
"absent" sentence is what makes a trap catchable — it tells the officer what the
witness demonstrably did *not* observe.

| Trap (§7) | Flag | Absent sentence says |
|---|---|---|
| Who went through the door | `saw_door_close` | saw it shut, **never saw who** |
| The time of anything | `saw_clock` | never looked at a clock — any specific time is invented |
| Whether Nick answered | `called_for_nick` | called out, **got no answer** |
| Whether Nick was outside yet | *(none)* | inherently unseeable — needs a new flag or prompt-only handling |
| Who locked the door | `left_door_unlocked` | does not know what state he left it in |
| The window breaking | `saw_glass_inside`, `saw_grille_intact` | morning-only observations; at night, unseeable |

Note `saw_door_close`'s present sentence **already** encodes the trap: *"saw the
door swinging shut, but never saw who went through it."* Most of the authoring
is done; it is the detection that is missing.

---

## 4. Suggested split

### Backend (`Sidecar/`) — the judgement

Deciding whether a given answer is a confident claim or an admission of not
knowing is a language problem, not a string-matching one. Put it where the model
already has the transcript, the case file and the witness-knowledge block.

1. Extend the officer's phase prompt so he **baits** the traps rather than only
   answering. §7 has the bait lines verbatim — e.g. *"You said you looked up. Who
   was it?"*, *"You're very precise about one o'clock. How do you know?"*
2. Add a structured field to the turn response — suggested
   `fabrications: string[]` on `SidecarTurnResponse`, carrying trap ids
   (`trap_door`, `trap_time`, `trap_answer`, `trap_outside`, `trap_lock`,
   `trap_window`).
3. Populate it when the reply is asserting a detail the `scene_instruction`'s
   witness-knowledge block says the witness never observed.

**Two hard rules:**

- **G6 — no lie/deception/truth language, anywhere.** Not in the field name, not
  in the prompt, not in any visible string. `GAME_COMPLETION_PLAN.md` §8 calls
  this non-negotiable and it is graded. The officer notes an *unsupported
  detail*; he never says the witness is lying, and the game never claims to
  detect dishonesty.
- **Uncertainty is not fabrication.** "I don't remember", "I think", "maybe" must
  not count. The mechanic is about confident false memory, not hedging.

### Client (`Assets/`) — the bookkeeping

1. In `PhaseDialogueController.OnTurnCompleted`, for each returned trap id call
   `_flow.Score.RecordFabrication(id)`.
2. Derive credibility and call `SetCredibility` — §8 uses thresholds of `0.45`
   and `0.6`, so the scale matters more than the exact formula.
3. Contract test: `Sidecar/tests/test_unity_contract.py` asserts the C# DTO and
   the Python response agree. Add the new field there or CI will not protect it.

---

## 5. Definition of done

- [ ] The officer baits all six traps during P2/P3
- [ ] A confident answer to a trap increments `CaughtFabrications`
- [ ] Hedged or "I don't know" answers do **not**
- [ ] `Credibility` moves in response to the session rather than sitting at `1f`
- [ ] No lie/deception/truth wording in any prompt, field name or visible string
- [ ] `test_unity_contract.py` covers the new response field
- [ ] Naming Aaron with no cited evidence is caught, **even though Aaron did it**

That last one is the acceptance test for the whole feature. If a player can guess
Aaron and sail through, §7 is not working.

---

## 6. Gotchas

- **The memory pair changed the odds.** `CS-16A`/`CS-16B` (P3) now hand the
  player Aaron's motive directly. Far more players will name him, and most will
  be doing it from the flashback — *not* from anything David witnessed about the
  door. The pair made this trap far more common and left it unarmed.
- **`saw_aaron_learn` is always set.** It comes from a cutscene that always
  plays, so it is guaranteed context rather than a variable. The affair can never
  itself be a fabrication — that trade was made deliberately, for comprehension.
- **`memory_flags.txt` and `MemoryFlagIds.cs` must stay in step.**
  `MemoryFlagCatalog` errors on a flag with no row. If you add a flag for
  "whether Nick was outside yet", add both.
- **Deploy after backend changes.** `./Sidecar/deploy.sh` — the running container
  is not rebuilt by a merge, and a prompt change is invisible until it is.
