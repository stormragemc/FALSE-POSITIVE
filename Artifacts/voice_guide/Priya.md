# Priya — voice and script-driven delivery

**Status:** voice finalized. Eleven production WAVs exist in the working tree
(§4.1–4.3). `PRIYA-002` through `PRIYA-008` are approved; `PRIYA-001` is pending
re-review after its 9 Aug **DAY-vid** pronunciation correction, and the three
good-years memory WAVs remain pending review. Unity import and cutscene wiring
are not yet implemented (§4.4). Supersedes the
Jessica casting and six legacy Priya clip names in `Assets/_Project/Art/Audio/VO/README.md` where
they conflict with this document.

---

## 1. Problem

Priya's voice had two connected problems: the existing game assets did not match the intended
casting, and a single broad emotion setting could not carry all eight of her lines naturally.

**Casting.** `Assets/_Project/Art/Audio/VO/README.md` casts Priya as Jessica
(`cgSgspJ2msm6clMCkdW9`), a general neutral female voice. The current character brief in
`docs/HUMAN_SCRIPT.md` calls for an Indian woman with a natural, easy-to-understand Indian accent.
Six Jessica MP3s already exist under Unity's `Art/Audio/VO` directory, but they are legacy assets,
not the selected production voice.

**Delivery.** Priya moves through immediate alarm, helpless panic, stunned disbelief, suspicion,
close concern, a panicked police report, and shaken confusion. Early auditions used one setting
and emphatic punctuation for every line. The result was recognisably synthetic: names were read as
an evenly spaced list, repeated questions had identical contours, and a `[breathless]` direction
introduced conspicuous gasps. Priya therefore needs line-specific thought beats, not one global
"panicked" preset.

---

## 2. What is already true

Verified against the working tree on 8 Aug 2026.

| Source | Current reality |
|---|---|
| `docs/HUMAN_SCRIPT.md` | Canonical spoken script. Priya owns `PRIYA-001` through `PRIYA-008` and the good-years memory lines `PRIYA-014` through `PRIYA-016`. |
| `Artifacts/voice-lines/priya/` | Contains eleven Aaira WAVs named after those IDs: seven approved and four pending review. |
| `Assets/_Project/Art/Audio/VO/README.md` | Still names Jessica as Priya's voice and is stale. |
| `Assets/_Project/Art/Audio/VO/` | Contains six legacy Priya MP3s with descriptive stems, not the new ID filenames. |
| `CutsceneRecipeBuilder.cs` | Refers to old stems such as `priya_screams` and `priya_door_locked`; several Priya beats are still plain subtitle `Beat` entries. |

### 2.1 Canonical line set

Priya has eleven lines in the current human script:

| ID | Dialogue |
|---|---|
| `PRIYA-001` | Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here! |
| `PRIYA-002` | What do we do? What do we do? |
| `PRIYA-003` | How did this happen? |
| `PRIYA-004` | All night? |
| `PRIYA-005` | The door was locked. Who locked it? |
| `PRIYA-006` | Nick? Nick, can you hear me? |
| `PRIYA-007` | Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry. |
| `PRIYA-008` | What happened? Why won't anyone tell me what happened? |
| `PRIYA-014` | Fifteen years and you two still act exactly the same. |
| `PRIYA-015` | And two years for these two. |
| `PRIYA-016` | To us. Somehow. |

The original legacy Unity set covered only six approximate equivalents. It had no current clip for
the police call (`PRIYA-007`) or Priya's spoken ending line (`PRIYA-008`). Two legacy strings also
differ from canon: "What could have happened here?" became "How did this happen?", and "Who locked
the door?" became "Who locked it?". The pulled Unity directory now also contains descriptive MP3s
for `PRIYA-014` through `PRIYA-016`; the approved Aaira WAVs remain the production source of truth.

### 2.2 Audition evidence

Eight Indian-accented candidates were first auditioned on `PRIYA-001` using
`eleven_multilingual_v2`. Monika Sogam was the initial favourite, but the performance remained
robotic. One candidate rendered as male and another was about 25 dB quieter than the rest; both
were rejected.

The six viable voices were regenerated with `eleven_v3` at Natural stability. **Aaira** was chosen
from that round. Seven additional Aaira takes were then used to solve the name-calling cadence in
`PRIYA-001`. The selected take calls "Ivy, Aaron, David" quickly, uses `[worried]` rather than
`[breathless]`, and avoids audible gasping.

Four other script lines were used to validate Aaira's emotional range. The first police-call read
was too controlled, so two urgency variants were auditioned; "panicked but clear" was selected.
`PRIYA-002` also required a second pass. The final version uses two broken `?!` pleas rather than
two evenly read questions.

### 2.3 Casting decision

Selected: **Aaira**, ElevenLabs voice ID `1XNFRxE3WBB7iI0jnm7p`.

The name and ID, not an audition candidate number, are authoritative. Candidate numbers changed
between the original eight-voice round and the six-voice V3 round.

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Priya's production voice is **Aaira** (`1XNFRxE3WBB7iI0jnm7p`) | Best balance of Indian accent, warmth, urgency, and intelligibility across the full line set. |
| D2 | Model is **`eleven_v3`** with Natural stability | V3 handled line-specific delivery directions and punctuation more naturally than `eleven_multilingual_v2`. These clips are pre-rendered, so latency is irrelevant. |
| D3 | Every production file is named after its `HUMAN_SCRIPT.md` ID | `PRIYA-005.wav` maps directly to `[PRIYA-005]`; no separate stem lookup is required. |
| D4 | Spoken words remain canonical; tags, casing, and punctuation carry direction | Prevents performance tuning from silently rewriting the story. |
| D5 | Delivery is authored per line, not selected from a global register | Priya's eleven lines cover materially different thought changes across the morning and memory scenes. |
| D6 | Do not use `[breathless]` | Auditioning showed that it adds conspicuous inhalations. Panic comes from pace, contour, and punctuation instead. |
| D7 | Approved WAVs are preserved and generation skips existing files by default | ElevenLabs output is nondeterministic; an approved take must not be replaced accidentally. |
| D8 | The production source of truth is `Artifacts/voice-lines/priya/` | Unity still contains stale Jessica clips. Integration is an explicit later step, not an implicit overwrite. |

**Rejected:**

- **Monika Sogam as final voice.** Best of the first V2 round, but still sounded synthetic across
  the panic line.
- **One broad `[panicked]` direction for every line.** Flattened distinct thought beats into the
  same performance.
- **Question marks between every called name.** Produced repeated rising contours and uniform
  pauses, making the names sound like a list of questions.
- **Ellipses between every called name.** Solved the contour but created implausibly long gaps.
- **A `[breathless]` opening.** Added too much audible breathing.
- **Regenerating approved lines during cleanup.** New output could differ despite identical input;
  approved files were copied into the ID set instead.

---

## 4. Design

### 4.1 Voice and synthesis — FINALIZED

All eight production lines share one voice identity and baseline:

| Setting | Value |
|---|---|
| Voice | Aaira |
| Voice ID | `1XNFRxE3WBB7iI0jnm7p` |
| `model_id` | `eleven_v3` |
| `stability` | `0.50` (`Natural`) |
| `similarity_boost` | `0.75` |
| `style` | `0.00` |
| `use_speaker_boost` | `True` |
| `speed` | `1.00` |
| Output format | `pcm_24000` |

The final assets are mono, 24 kHz, 16-bit PCM WAV. `similarity_boost` is fixed so the voice and
accent remain consistent; emotional differences come from the text prompt, not identity drift.
Style is held at zero because early higher-style renders sounded performed rather than immediate.

### 4.2 Script-driven delivery

Audio tags and punctuation below are synthesis instructions. They are not additional spoken words.

| ID | Performance beat | Exact synthesis prompt |
|---|---|---|
| `PRIYA-001` | Immediate alarm; quick clear name calls; no gasping | `[worried] Guys—help! Something’s happened to Nick.` then `IVY! AARON! DAVID!` then `Please—come here!`, with paragraph breaks between the three beats |
| `PRIYA-002` | Two sharp panic pleas as composure breaks | `[panicked] What do we do?! What do we do?!` |
| `PRIYA-003` | Quieter stunned disbelief | `[stunned] How did this happen?` |
| `PRIYA-004` | Brief, pointed suspicion after Ivy hesitates | `[skeptical] All night?` |
| `PRIYA-005` | Realisation, half-beat, direct suspicion | `[realizing] The door was locked... who locked it?` |
| `PRIYA-006` | Tentative call, listening pause, close concern | `[softly] Nick? ... Nick, can you hear me?` |
| `PRIYA-007` | Panicked but intelligible report; urgent final plea | `[panicked but clear] Police? Our friend is hurt! We found him outside—in the snow. Please send someone. Please hurry!` |
| `PRIYA-008` | Disorientation, longer pause, hurt frustration | `[shaken] What happened...? Why won’t anyone tell me what happened?` |
| `PRIYA-014` | Warmly tease two old friends over their school photograph | `[warmly amused] Fifteen years—and you two still act exactly the same.` |
| `PRIYA-015` | Playfully redirect the toast toward Aaron and Ivy | `[playfully] And two years for these two.` |
| `PRIYA-016` | Warm toast with a lightly wry finish | `[warm, lightly wistful] To us... somehow.` |

The repeated words in `PRIYA-002` are deliberately separated by `?!`, unlike the connected em-dash
take. The broken contour is the selected panic performance, not a typographical change to the
canonical script.

### 4.3 Assets and reproducibility — PARTIALLY REVIEWED

Production files:

```
Artifacts/voice-lines/priya/PRIYA-001.wav
Artifacts/voice-lines/priya/PRIYA-008.wav
Artifacts/voice-lines/priya/PRIYA-014.wav
Artifacts/voice-lines/priya/PRIYA-015.wav
Artifacts/voice-lines/priya/PRIYA-016.wav
```

`PRIYA-002` through `PRIYA-008` are approved takes. `PRIYA-001` was regenerated
with the project-wide synthesis alias `DAY-VID` and is pending re-review.
`PRIYA-014` through `PRIYA-016` have the selected voice and documented prompts
but remain pending audition approval.

`Artifacts/voice-lines/priya/README.md` is the compact production manifest.
`generate_priya_voice_lines.py` stores the public voice ID, model, settings, and exact prompts. It
generates only missing files unless `--force` is supplied. `play_priya_voice_line.py PRIYA-007`
plays one selected file without opening a media-player window.

Only `ELEVENLABS_API_KEY` is external. It is read from the environment at runtime and is never
written into a source or audio file.

All rejected audition WAVs and the standalone ElevenLabs test WAV were deleted after selection.
The production directory contains exactly the eight approved Priya WAVs; the historical audition
scripts remain as process documentation but no longer have their old audio payloads.

### 4.4 Unity integration — NOT YET IMPLEMENTED

The finalized WAVs are not under `Assets/`, so Unity does not import or play them yet. Current
integration still assumes six old descriptive stems:

- `priya_screams`
- `priya_what_do_we_do`
- `priya_what_could_have_happened`
- `priya_all_night`
- `priya_door_locked`
- `priya_can_you_hear_me`

`CutsceneRecipeBuilder.VoClipNames` and its recipes must be updated to the ID contract. The target
shape is either to import files using the ID stems directly or to maintain one explicit ID-to-clip
mapping. Direct stems are preferred because they preserve the invariant established in D3.

Integration also needs to:

1. Copy or import the eleven WAVs into Unity's authored VO directory without transcoding the
   approved takes.
2. Replace the Jessica casting row in `Assets/_Project/Art/Audio/VO/README.md` with Aaira and the
   V3 settings from §4.1.
3. Replace legacy dialogue text in `CutsceneRecipeBuilder` with the canonical `PRIYA-###` lines.
4. Add `PRIYA-007` to the morning police-call sequence.
5. Add `PRIYA-008` before Spassky's line in `EndingPriya`.
6. Ensure each spoken Priya beat uses `VoBeat` or otherwise receives the correct clip; several
   current beats are plain `Beat` entries.
7. Map `PRIYA-014` through `PRIYA-016` into the `GoodYears` memory recipe.
8. Rebuild and verify serialized cutscene recipes after the mapping changes.

### 4.5 Loudness and in-game mix

No post-TTS gain, compression, or denoising was applied. The chosen takes were selected by ear at
their native ElevenLabs levels. Unity should apply one character VO mixer group consistently
rather than destructively rewriting individual WAVs.

Before final integration, audition all eight against the snowstorm, fire, movement SFX, and music.
If a clip needs correction, prefer mixer automation or a documented non-destructive import setting.
Do not normalize each line independently: that would make Priya's soft `PRIYA-006` as loud as her
panic calls and erase intentional dynamics.

---

## 5. Testing and verification

Completed offline checks:

- Priya owns exactly `PRIYA-001` through `PRIYA-008` and `PRIYA-014` through `PRIYA-016`.
- The production directory contains one WAV for every Priya ID, with no missing or extra ID.
- Every production WAV opens as mono, 24 kHz, 16-bit PCM.
- The generation and individual-playback utilities compile under the project's Sidecar Python.
- `.gitignore` includes production WAVs under `Artifacts/voice-lines/<character>/` while keeping
  Python caches and unrelated generated audio ignored.
- After cleanup, no non-final workflow voice audio remains under `Artifacts`; cabin ambience is
  intentionally outside the voice-line scope.

Required Unity-side tests after §4.4 ships:

- Every Priya `VoBeat` resolves a non-null clip by its `PRIYA-###` ID.
- Subtitle text equals the canonical text paired with that ID.
- `PRIYA-007` plays during the police call and `PRIYA-008` plays in the Priya ending.
- No recipe references a removed `priya_*` legacy stem.
- A full morning-memory playthrough has no silent holds or clipped line endings.
- VO remains intelligible in the final cabin mix without flattening the intended dynamic range.

---

## 6. Out of scope

- **Importing or wiring the files in Unity.** §4.4 describes the work but does not perform it.
- **Deleting tracked legacy Unity VO.** Those files may still be referenced by serialized scenes;
  remove them only as part of the integration change.
- **Generating additional characters beyond Priya and Ivy.** Those casting and delivery decisions
  remain separate work.
- **Live or dynamic Priya TTS.** All current Priya dialogue is pre-rendered.
- **Per-line mastering.** Preserve the selected dynamics and handle level in Unity's mixer.

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Unity continues playing the six Jessica clips while the Aaira files sit only under `Artifacts` | **High** | Complete §4.4 as one integration change; update casting, stems, text, and serialized recipes together |
| The missing police-call or ending mapping produces a silent beat | High | Add explicit `PRIYA-007` and `PRIYA-008` recipe coverage and test both routes |
| Regenerating with identical settings changes an approved performance | Medium | Generator skips existing files by default; preserve committed approved WAVs and require `--force` for replacement |
| Broad V3 tags introduce gasps or theatrical delivery on future lines | Medium | Use the line-specific thought-beat method in §4.2; do not use `[breathless]` without auditioning |
| Native clip levels disappear under the storm or differ too much in the cabin mix | Medium | Validate through the Unity VO mixer; use non-destructive mixer gain rather than per-file normalization |
| Aaira is a Voice Library dependency and could become unavailable | Low | Voice ID and exact settings are documented; approved WAVs are source-controlled and do not require runtime access |
| Legacy descriptive stems and new ID stems coexist and drift | Low | Make `PRIYA-###` the only integration key and remove old references once serialized recipes are rebuilt |
