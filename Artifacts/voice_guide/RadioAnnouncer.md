# Radio announcer — voice and delivery guide

**Status:** voice selected and all four cleaned production WAVs approved by user
listening review 9 Aug 2026. Unity integration and radio sound design are
pending.

---

## 1. Casting decision

Selected: **Roger**, ElevenLabs voice ID `CwhRBWXzGAHq8TQ4Fs17`.

Roger was candidate 1 in the historical and V3 Natural radio-announcer rounds.
He was approved on both the full storm warning and the short "please stay
indoors" radio-bleed fragment. The voice should remain neutral, impersonal, and
easy to understand rather than sounding like a dramatic character performance.

## 2. Canonical line set

`docs/HUMAN_SCRIPT.md` owns the spoken wording and stable IDs.

| ID | Dialogue | State |
|---|---|---|
| `RADIO-001` | A snowstorm is moving through the area. Please stay indoors until conditions improve. | Production take approved |
| `RADIO-002` | …snow storm… | Production take approved in sequence context |
| `RADIO-003` | …please stay indoors… | Production take approved in sequence context |
| `RADIO-004` | …during these times. | Regenerated production take approved in sequence context |

The three short lines are broken radio bleed during the later memory. Ellipses
represent clipped context; no labels, candidate numbers, static, or production
sound effects are spoken.

## 3. Synthesis settings

| Setting | Value |
|---|---|
| Voice | Roger |
| Voice ID | `CwhRBWXzGAHq8TQ4Fs17` |
| `model_id` | `eleven_v3` |
| `stability` | `0.50` (`Natural`) |
| `similarity_boost` | `0.75` |
| `style` | `0.00` |
| `use_speaker_boost` | `True` |
| `speed` | `1.00` |
| Output format | `pcm_24000` |

Approved takes use the direction
`[neutral, impersonal, calm public safety announcement]`. The direction is a V3
synthesis instruction and is not spoken dialogue.

Static, bandwidth filtering, dropouts, and masking belong to later sound design.
They must not be baked into the voice source because the same identity needs to
remain consistent across the clean warning and the three memory fragments.

## 4. Current assets and next work

The authoritative production set is `Artifacts/voice-lines/radio-announcer/`
and contains `RADIO-001.wav` through `RADIO-004.wav`, the reproducible generator,
single-line playback utility, and review manifest. Historical audition assets
were removed after the production set was locked; casting provenance remains
documented here and in the production README.

Unity integration must import the dry WAVs without baking in static. Apply radio
bandwidth filtering, dropouts, masking, and static non-destructively during
sound-design integration.
