# Radio announcer — voice and delivery guide

**Status:** voice **selected** 9 Aug 2026. The `RADIO-001` and `RADIO-003`
audition takes are approved. Production packaging and the `RADIO-002` and
`RADIO-004` renders are pending.

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
| `RADIO-001` | A snowstorm is moving through the area. Please stay indoors until conditions improve. | Audition take approved |
| `RADIO-002` | …snow storm… | Not yet rendered with Roger |
| `RADIO-003` | …please stay indoors… | Audition take approved |
| `RADIO-004` | …during these times. | Not yet rendered with Roger |

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

Approved audition sources:

```text
Artifacts/voice-auditions/radio-announcer/v3-natural-round/line-01-storm-warning/01-roger.wav
Artifacts/voice-auditions/radio-announcer/v3-natural-round/line-03-stay-indoors/01-roger.wav
```

These WAVs are audition assets and are ignored by Git. They have not been copied
into an ID-based production directory or integrated into Unity.

When production rendering is requested:

1. Preserve the approved `RADIO-001` and `RADIO-003` takes.
2. Render `RADIO-002` and `RADIO-004` with Roger and the settings above.
3. Package the approved set as `RADIO-001.wav` through `RADIO-004.wav`.
4. Apply radio static and filtering non-destructively during Unity or sound-design
   integration.
