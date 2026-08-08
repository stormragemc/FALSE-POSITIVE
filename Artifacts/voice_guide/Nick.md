# Nick — voice and script-driven delivery

**Status:** voice cast and six production MP3s **finalized in the working tree** 8 Aug 2026
(§4.1–4.3), imported and wired into CS-16A/CS-16B (§4.4). Adds Nick to
`Assets/_Project/Art/Audio/VO/README.md`, which had no row for him at all.

---

## 1. Problem

Nick had **no voice and no audio**. `docs/STORY_SCRIPT.md` §2 promised him "one pre-rendered line
(fire argument)" and no `nick_*.mp3` existed anywhere in the project. He was the single hard
blocker on the P3 memory pair: he is the centre of both fragments, and neither could be built
without him.

**Casting.** `docs/HUMAN_SCRIPT.md`'s accent table lists Nick as **Russian, with a Russian
accent** — the same family as Officer Spassky. That constraint was missed on the first audition
pass, which auditioned three American voices (Chris, Will, Josh) against §2's personality line
alone. All three were discarded.

**Separation.** Nick's problem is not just sounding right, it is sounding *distinct*. He shares
CS-16A and CS-16B with Aaron (Eric) and hard-cuts back to Spassky (Maksim, Russian) within
seconds. Two Russian male voices in adjacent shots will blur if they are close in age or depth,
and the player would lose track of who is speaking during the fire argument — the one beat where
following the speaker actually matters.

**Delivery.** Nick spans warm best-friend teasing and the careless line that ends Aaron's
marriage, then anger and humiliation, inside about twenty seconds of screen time. One preset
cannot carry all three.

---

## 2. What is already true

Verified against the working tree on 8 Aug 2026.

| Source | Current reality |
|---|---|
| `docs/HUMAN_SCRIPT.md` | Canonical spoken script. Nick owns `NICK-001` through `NICK-007`. |
| `Assets/_Project/Art/Audio/VO/` | Six finalized Artem MP3s with descriptive stems (see §2.1). |
| `Assets/_Project/Art/Audio/VO/README.md` | Now carries Nick's row and render settings. |
| `CutsceneRecipeBuilder.cs` | All six wired as `VoBeat` entries in `GoodYears` / `WhenItWentWrong`. |
| `docs/STORY_SCRIPT.md` §2 | Still reads "pre-rendered memory lines" — accurate but no longer a promise. |

### 2.1 Canonical line set

| ID | Line | Beat | Asset |
|---|---|---|---|
| `NICK-001` | Not tonight, David. I can't do this with you right now. I need some air. | superseded by `NICK-007` in the split-memory rewrite | — not rendered |
| `NICK-002` | He was worse at seventeen. | CS-16A, school photo | `nick_worse_at_seventeen.mp3` |
| `NICK-003` | Unfortunately. | CS-16A, the toast | `nick_unfortunately.mp3` |
| `NICK-004` | Here. You look fucking freezing. | CS-16A, the coat swap | `nick_you_look_freezing.mp3` |
| `NICK-005` | You've been saying "after this trip" for two years. | CS-16B, Aaron learns | `nick_after_this_trip.mp3` |
| `NICK-006` | He already knows. | CS-16B, the fire argument | `nick_he_already_knows.mp3` |
| `NICK-007` | I need some air. | CS-16B, he walks out | `nick_i_need_some_air.mp3` |

`NICK-001` predates the split-memory rewrite and overlaps `NICK-007`. It is deliberately not
rendered; if it is ever restored, it needs its own asset rather than reusing `NICK-007`.

### 2.2 Audition evidence

Two rounds, both auditioned on the same pair of lines so the warm and the damaging register could
be judged together: *"Here. You look freezing. … You've been saying 'after this trip' for two
years."*

| Round | Candidates | Outcome |
|---|---|---|
| 1 | Chris, Will, Josh (all American) | **Discarded** — cast against §2's personality note without checking `HUMAN_SCRIPT.md`'s accent table. Liam was also attempted and 404'd: not in the account's library. |
| 2 | Oleg, Denis, Guy, Artem — all Russian or Moscow-labelled | **Artem selected.** |

Round 2 was rendered alongside a Maksim reference clip of the same lines, so candidates could be
judged for family separation against Spassky rather than in isolation.

Note the account's voice labelling: only **one** voice is tagged `russian` (Maksim). The rest of
the Russian bench is tagged `moscow` or `standard`, so an accent filter alone will not surface
them.

### 2.3 Casting decision

**Artem Lebedev** ("Podcast Pro"), `rQOBu7YxCDxGiFdTm28w`. Middle-aged, casual register.

Chosen over the younger candidates (Oleg, Guy) because Nick has to be believable as David's friend
of fifteen years and as a man having a two-year affair — not as a student. Chosen over Denis
because the casual register carries the careless line better than a straight read.

Provisional in one respect: Artem sits nearer Maksim in age than the younger candidates did, so
separation from Spassky relies on depth and pace rather than age. If the fire argument reads as
muddy against Spassky in a full playthrough, re-audition Oleg before changing anything else.

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Russian accent, non-negotiable | `docs/HUMAN_SCRIPT.md` accent table |
| D2 | Auditioned against Maksim, not in isolation | Two Russian males in adjacent shots must be separable |
| D3 | Middle-aged over young | Fifteen-year friendship, two-year affair |
| D4 | One voice identity, per-line settings | Same approach as Priya: identity fixed, emotion from text and settings |
| D5 | `eleven_multilingual_v2`, not `v3` | Matches every other clip in the project and `Sidecar/tts.py`'s live model; mixing models inside one scene risks an audible seam |
| D6 | Descriptive stems, not `NICK-00n` filenames | `CutsceneRecipeBuilder.VoBeat` resolves by stem; renaming would mean touching every recipe |

D5 and D6 both differ from `Priya.md` deliberately. See §7.

---

## 4. Design

### 4.1 Voice and synthesis — FINALIZED

All six production lines share one voice identity and baseline:

| Setting | Value |
|---|---|
| Voice | Artem Lebedev — "Podcast Pro" |
| Voice ID | `rQOBu7YxCDxGiFdTm28w` |
| `model_id` | `eleven_multilingual_v2` |
| `stability` | `0.35` |
| `similarity_boost` | `0.85` |
| `style` | `0.55` |
| `speed` | `0.95` |
| Output format | default MP3 |

Looser than Spassky (`stability 0.15` is his expressive setting, but he is *composed*; Nick is
drunk and careless) and tighter than a free-form read, because below about `0.3` the Russian
accent begins to wander between takes.

`similarity_boost 0.85` holds the accent stable across all six renders. Emotional range comes from
the line text and the direction in §4.2, not from changing identity per line.

### 4.2 Script-driven delivery

Three registers across six lines. They are not interchangeable.

**Warm — `NICK-002`, `NICK-003`, `NICK-004`.** Teasing an old friend. `NICK-004` ("Here. You look
fucking freezing.") must land as generosity, not aggression — the profanity is affectionate. This
line is also load-bearing: it is why Nick is underdressed when he dies, and §9's clue 3 now cites
this memory as a source. If it reads as a sneer, the coat swap stops looking like a kindness.

**Careless — `NICK-005`.** The line that ends Aaron's marriage. Nick is not being cruel and is not
making a point; he is drunk and has stopped tracking who is in the room. Any hint of deliberate
malice makes Aaron's restraint incoherent and turns Nick into a villain, which the story does not
support.

**Humiliated — `NICK-006`, `NICK-007`.** Angry, clipped, defensive. `NICK-007` is his last line in
the game; it should sound like leaving a room, not like a threat.

The current renders were tuned on the warm register. The two humiliated lines are the most likely
to need a second pass — check them before locking.

### 4.3 Assets and reproducibility — FINALIZED

Six MP3s under `Assets/_Project/Art/Audio/VO/`, named by descriptive stem (§2.1). Rendered at the
settings in §4.1; 156 characters billed total.

Reproducing them needs only the voice ID, the six line texts verbatim from `docs/HUMAN_SCRIPT.md`,
and the §4.1 settings. Any re-render must take the text from the human script rather than from
this table, so the spoken lines and the subtitles cannot drift apart.

### 4.4 Unity integration — IMPLEMENTED

All six are wired as `VoBeat` entries in `CutsceneRecipeBuilder.BuildRecipes`, three in
`CutsceneId.GoodYears` and three in `CutsceneId.WhenItWentWrong`. `VoBeat` resolves the clip by
stem at recipe-build time, so the assets are picked up by
`Tools ▸ False Positive ▸ Bootstrap ▸ 6 - Populate Cutscene Recipes` with no per-clip wiring.

They are **not** in `CutsceneRecipeBuilder.VoClipNames`, and must not be added: that path assigns
clips to beats *positionally*, and `WhenItWentWrong` contains two subtitle-only `DAVID` beats at
indices 4 and 6. A positional mapping would write Nick's audio onto David's silent lines.

### 4.5 Loudness and in-game mix

Not yet normalised. Spassky's clips carry a documented −1.5 dB trim; Nick's have no equivalent
pass and may sit hotter than the rest of the cast. Do a level check across CS-16A once Priya's and
Aaron's final assets are in, and normalise the scene together rather than per character.

---

## 5. Testing and verification

- [x] Six MP3s exist under `Art/Audio/VO/` with the §2.1 stems
- [x] `Bootstrap/6` reports 27 recipes with no "Missing VO nick_*" warnings
- [ ] CS-16A and CS-16B play with all six audible, in a full playthrough
- [ ] Nick is distinguishable from Aaron (Eric) within CS-16A
- [ ] Nick is distinguishable from Spassky (Maksim) across the CS-16B → interrogation hard cut
- [ ] `NICK-004` reads as generosity, not aggression
- [ ] `NICK-005` reads as careless, not cruel
- [ ] Level check against Priya, Aaron and Spassky in the same scene

The two separation checks are the ones that would send this back to casting. Everything else is
tuning.

---

## 6. Out of scope

- Nick's on-screen model, animation and lip-sync
- `NICK-001`, superseded by the split-memory rewrite
- Loudness normalisation across the whole cast (§4.5 flags it; it is a scene-level job)
- Any live TTS for Nick — he is pre-rendered only, and never speaks in the present tense

---

## 7. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Artem and Maksim blur across the CS-16B hard cut | Player loses the speaker in the fire argument | Separation check in §5; Oleg is the fallback |
| `NICK-005` reads as cruel | Aaron's restraint becomes incoherent; Nick becomes a villain | §4.2 direction; re-render at lower `style` if needed |
| Model divergence from `Priya.md` | Audible seam within CS-16A | D5 is deliberate — but if Priya ships on `eleven_v3`, A/B the two in the same scene before locking |
| Casting recorded in three places | Drift, as already happened with Priya | `Art/Audio/VO/README.md`, `docs/HUMAN_SCRIPT.md` and this file must agree; this file is the detailed record, the README table is the index |
| Nick's stems differ from Priya's `NICK-00n` convention | Confusion when both guides are read together | D6 — descriptive stems are what `VoBeat` resolves; changing them means touching every recipe |
