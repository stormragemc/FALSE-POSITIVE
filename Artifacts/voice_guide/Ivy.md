# Ivy — voice and script-driven delivery

**Status:** voice and four production WAVs **finalized in the working tree** 8 Aug 2026
(§4.1–4.3). Unity import and cutscene wiring are not yet implemented (§4.4). Supersedes the Lily
casting and legacy Ivy clip assumptions in `Assets/_Project/Art/Audio/VO/README.md` where they
conflict with this document.

---

## 1. Problem

Ivy's existing synthetic VO did not reflect the selected casting, and her emotional brief is easy
to overplay. She is frightened, but she is also protecting Aaron and herself. The important vocal
signal is not theatrical guilt; it is an alibi that arrives slightly too quickly and a confirmation
that is just controlled enough to feel rehearsed.

**Casting.** `Assets/_Project/Art/Audio/VO/README.md` currently names Lily
(`pFZP5JQG7iQjIQuC4Bku`). Four legacy Ivy MP3s exist under Unity's VO directory, but Laura was
selected in the new V3 audition and the approved production files now use ID-based WAV names.

**Delivery.** Ivy has only four canonical lines, but they cover shock, guarded explanation,
controlled confirmation, and quiet physical focus. A voice that succeeds on the first frightened
line can still sound synthetic or overtly suspicious on the alibi. The final choice therefore had
to survive a longer guarded passage rather than win on one short exclamation.

---

## 2. What is already true

Verified against the working tree on 8 Aug 2026.

| Source | Current reality |
|---|---|
| `docs/HUMAN_SCRIPT.md` | Canonical spoken script. Ivy owns `IVY-001` through `IVY-004`. |
| `Artifacts/voice-lines/ivy/` | Contains exactly four finalized Laura WAVs named after those IDs. |
| `Assets/_Project/Art/Audio/VO/README.md` | Still names Lily as Ivy's voice and is stale. |
| `Assets/_Project/Art/Audio/VO/` | Contains four legacy Ivy MP3s with descriptive stems. |
| `CutsceneRecipeBuilder.cs` | Uses old stems and non-canonical variants of the alibi; `IVY-004` is not present in the current sofa recipe. |

### 2.1 Canonical line set

| ID | Dialogue |
|---|---|
| `IVY-001` | Oh my God. What happened to him? What do we do now? |
| `IVY-002` | I don't know. I was upstairs with Aaron. |
| `IVY-003` | Yes. All night. |
| `IVY-004` | Careful. Careful. Easy. |

The legacy recipe text for the alibi reads "I don't know! I was with Aaron upstairs!!", which is
more emphatic and less natural than canon. The existing `ivy_careful_lift.mp3` file is not included
in the current `VoClipNames` map or the sofa recipe, so merely preserving that legacy file does not
make `IVY-004` play.

### 2.2 Audition evidence

Eight neutral female voices were auditioned on `IVY-001` using `eleven_v3` with Natural stability.
Laura was the favourite on shock; Jessica was the favourite on the guarded alibi. Both were carried
into a final decider using `IVY-003` and `IVY-004`.

Those lines were too short to expose enough of either voice, so both finalists read a longer
audition-only guarded passage. The passage tested sustained pacing, timbre, vulnerability, and the
transition from factual denial into a plea to slow down. **Laura** won that final comparison.

The longer passage is casting evidence only. It is not part of `HUMAN_SCRIPT.md`, has no line ID,
and is not a production asset.

### 2.3 Casting decision

Selected: **Laura**, ElevenLabs voice ID `FGY2WhTYpPnrIDTdsKH5`.

The name and voice ID are authoritative. Laura was candidate 3 in the original eight-voice round
and finalist 2 in the Jessica-versus-Laura decider.

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Ivy's production voice is **Laura** (`FGY2WhTYpPnrIDTdsKH5`) | Stayed natural across shock, guarded denial, controlled confirmation, and a longer emotional passage. |
| D2 | Model is **`eleven_v3`** with Natural stability | Supports restrained emotional direction without the rigid cadence heard in older V2 auditions. Runtime latency is irrelevant for pre-rendered clips. |
| D3 | Files use the `IVY-###` IDs from the human script | Removes ambiguity between legacy descriptive stems and canonical dialogue. |
| D4 | Ivy's guardedness is delivery, not rewritten dialogue | Canonical wording stays human and non-accusatory; pace and restraint carry the subtext. |
| D5 | Similarity and style settings stay fixed across all four lines | Voice identity remains stable while tags and punctuation shape the thought beat. |
| D6 | The longer decider passage remains audition-only | Prevents a useful casting test from silently entering the game script. |
| D7 | Approved WAVs are preserved; regeneration skips existing files | ElevenLabs output is nondeterministic and a rerun can change an approved take. |
| D8 | `Artifacts/voice-lines/ivy/` is the production source of truth | Unity's Lily MP3s remain legacy assets until the explicit integration work in §4.4. |

**Rejected:**

- **Lily as a binding cast choice.** The Unity README documents an earlier intention, not the new
  audition result.
- **Selecting from `IVY-001` alone.** Laura won shock but Jessica won the alibi; one line was not
  sufficient evidence.
- **Selecting from only `IVY-003` and `IVY-004`.** The lines were too short to compare sustained
  naturalness.
- **Overplaying the alibi.** Strong exclamation marks and overt guilt make Ivy reveal too much;
  the canon calls for guarded speed, not confession.
- **Adding the long decider to the script.** It exists only to expose voice quality over a longer
  sample.

---

## 4. Design

### 4.1 Voice and synthesis — FINALIZED

| Setting | Value |
|---|---|
| Voice | Laura |
| Voice ID | `FGY2WhTYpPnrIDTdsKH5` |
| `model_id` | `eleven_v3` |
| `stability` | `0.50` (`Natural`) |
| `similarity_boost` | `0.75` |
| `style` | `0.00` |
| `use_speaker_boost` | `True` |
| `speed` | `1.00` |
| Output format | `pcm_24000` |

The final assets are mono, 24 kHz, 16-bit PCM WAV. Style remains zero to avoid theatrical
exaggeration. Similarity is fixed at 0.75 so the selected timbre stays consistent across lines.

### 4.2 Script-driven delivery

| ID | Performance beat | Exact synthesis prompt |
|---|---|---|
| `IVY-001` | Immediate shock and vulnerability; frightened, not melodramatic | `[shocked] Oh my God. What happened to him? What do we do now?` |
| `IVY-002` | Guarded and slightly too quick | `[guarded, answering quickly] I don’t know. I was upstairs with Aaron.` |
| `IVY-003` | Controlled confirmation after a fraction of hesitation | `[guarded] Yes. All night.` |
| `IVY-004` | Quiet focus while lowering Nick | `[quietly, focused] Careful. Careful... easy.` |

Audio tags and punctuation are generation directions and are not additional spoken words. The
ellipsis in `IVY-004` softens the final instruction; it does not indicate a dramatic pause.

### 4.3 Assets and reproducibility — FINALIZED

```
Artifacts/voice-lines/ivy/IVY-001.wav
Artifacts/voice-lines/ivy/IVY-002.wav
Artifacts/voice-lines/ivy/IVY-003.wav
Artifacts/voice-lines/ivy/IVY-004.wav
```

`Artifacts/voice-lines/ivy/README.md` is the compact production manifest.
`generate_ivy_voice_lines.py` stores the public voice ID, model, settings, and exact prompts and
skips existing files unless `--force` is supplied. `play_ivy_voice_line.py IVY-002` plays one line
without opening a media-player window.

Only `ELEVENLABS_API_KEY` is external. It is read from the environment and is never written to a
source or audio file.

### 4.4 Unity integration — NOT YET IMPLEMENTED

The finalized Laura WAVs are under `Artifacts`, not `Assets`, so Unity does not import them yet.
Current Unity references use these legacy stems:

- `ivy_oh_my_god`
- `ivy_i_dont_know`
- `ivy_yes_all_night`
- `ivy_careful_lift`

Integration needs to:

1. Import `IVY-001.wav` through `IVY-004.wav` without transcoding the approved takes.
2. Replace Ivy's Lily casting row in `Assets/_Project/Art/Audio/VO/README.md` with Laura and §4.1.
3. Replace the old stems in `CutsceneRecipeBuilder.VoClipNames` with the ID contract.
4. Restore the canonical alibi wording and punctuation for `IVY-002`.
5. Map `IVY-001` to `OutIntoTheSnow`, `IVY-002` and `IVY-003` to `TheCarry`, and `IVY-004` to
   `TheSofa` before Priya asks whether Nick can hear her.
6. Ensure each Ivy spoken beat resolves a non-null clip after serialized recipes are rebuilt.
7. Remove the tracked Lily MP3s only after no scene or recipe references their legacy stems.

### 4.5 Loudness and in-game mix

No destructive post-TTS normalization was applied. Ivy's quieter `IVY-003` and `IVY-004` should
remain quieter than her initial shock. Route all four through one Unity character-VO mixer group
and test them against wind, body movement, and fireplace SFX before changing individual files.

---

## 5. Testing and verification

Completed offline checks:

- `HUMAN_SCRIPT.md` contains exactly four Ivy IDs: `IVY-001` through `IVY-004`.
- The production folder contains one matching WAV for every Ivy ID.
- Each selected source take exists before audition cleanup.
- Every production WAV opens as mono, 24 kHz, 16-bit PCM.
- Generation and individual-playback utilities compile under the Sidecar Python environment.
- The generic `.gitignore` exception includes production WAVs under
  `Artifacts/voice-lines/<character>/` while leaving audition audio ignored.

Required Unity-side checks after §4.4:

- Every Ivy subtitle and clip pair uses the same `IVY-###` ID.
- `IVY-002` uses canonical wording rather than the legacy emphatic rewrite.
- `IVY-004` plays during the sofa-lowering beat and is not a silent orphaned asset.
- No serialized recipe references `ivy_oh_my_god`, `ivy_i_dont_know`, `ivy_yes_all_night`, or
  `ivy_careful_lift` after migration.
- All four lines remain intelligible in the final cabin mix without flattening their dynamics.

---

## 6. Out of scope

- **Importing or wiring the files in Unity.** §4.4 documents that separate implementation change.
- **Deleting tracked Lily VO before migration.** Existing scenes may still reference it.
- **Adding the long decider passage to the human script.** It is audition-only.
- **Generating other characters.** Each character requires an independent casting decision.
- **Live Ivy TTS.** Her current lines are pre-rendered.
- **Per-line mastering.** Preserve performance dynamics and balance through Unity's mixer.

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Unity continues playing Lily while Laura's WAVs remain only under `Artifacts` | **High** | Complete §4.4 as one migration and update casting, stems, text, and serialized recipes together |
| `IVY-004` remains orphaned and never plays | High | Add it explicitly to `TheSofa` and test the full lowering sequence |
| Regeneration changes an approved Laura performance | Medium | Generator skips existing files; preserve committed WAVs and require `--force` for replacement |
| Guarded delivery reads as guilt rather than fear | Medium | Keep the canonical neutral wording and validate the line in full scene context |
| Native levels disappear under cabin SFX | Medium | Test through the Unity VO mixer and use non-destructive mixer gain |
| Laura becomes unavailable in the Voice Library | Low | Public voice ID and settings are documented; approved WAVs do not require runtime access |
| Legacy stems and ID filenames coexist and drift | Low | Make `IVY-###` the only integration key and remove old references after migration |
