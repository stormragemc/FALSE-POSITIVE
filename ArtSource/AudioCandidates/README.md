# Audio candidates — auditioning only

Nothing in this folder ships. It is outside `Assets/`, so `.gitignore` keeps the MP3s
out of the repo (`.gitignore:146-150` exempts only `Assets/**`). When a candidate wins it
gets moved to `Assets/_Project/Art/Audio/` and committed there.

Regenerate with:

```bash
python3 Tools/generate_music_candidates.py --list   # styles, no API calls
python3 Tools/generate_music_candidates.py          # writes here; skips what exists
python3 Tools/generate_sfx_candidates.py            # blocked, see "Radio" below
```

Both scripts skip files that already exist, so a re-run costs nothing unless you delete
a take first.

## Music — the interrogation-loop bed

All five score the same moment, at 45s, so they can be judged against each other.

| File | Style | Idea |
|---|---|---|
| `music_A_pressure.mp3` | Sub-bass drone | Barely music. Near-motionless low drone, slow 20s swells, no melody. |
| `music_B_testimony.mp3` | Solo cello | Close-miked and dry, audible rosin. Scandinavian noir. |
| `music_C_interview.mp3` | Felt piano + tape | Single notes, long decay, analogue hiss. Procedural slow-burn. |
| `music_D_machine.mp3` | Granular | Clinical shimmer, data-like ticks, no warmth. Scores the detective as an AI. |
| `music_E_storm.mp3` | Ambience-led | Wind on timber, one barely-there drone. The storm that killed Nick. |

### The constraint these were written against

The mic is open. VAD drives the turn loop, and Officer Spassky's TTS has to stay
intelligible, so anything sitting in the 300 Hz - 4 kHz speech band is a functional
problem and not just a mix preference. Measured energy in that band relative to each
take's own full-band level:

```
style          full   speech    delta
pressure      -12.6    -31.7    -19.1   <- speech band essentially empty
testimony     -17.0    -22.8     -5.8
interview     -23.6    -27.9     -4.3
machine       -20.6    -28.8     -8.2
storm         -17.4    -23.0     -5.6
```

`A` is almost entirely below 300 Hz, so the officer's voice sits clear of it. `C` is the
most crowded relative to its own level *and* the quietest overall, which is the worst
combination: you would push it up to hear it and crowd the voice further.

Judge these quiet. In game they sit 12-15 dB below the level they audition at, and `B`
and `C` are the two most likely to vanish entirely down there.

## Radio SFX — M1, "Fix the radio"

Covers the beat in `docs/STORY_SCRIPT.md` ss.159-176: free-roam hiss -> dial minigame ->
lock -> the storm-warning line.

| File | Length | Beat |
|---|---|---|
| `sfx_radio_bed_synth.mp3` | 20s, loops | Free-roam bed. Pink noise through a 300-3200 Hz speaker band, drifting 1080 Hz carrier, crackle layer. |
| `sfx_radio_tuning_synth.mp3` | 12s | Dial minigame. Heterodyne whistle across the band, hiss swelling and thinning. |
| `sfx_radio_lock_synth.mp3` | 6s | CS-05. Static collapses into a clean carrier at ~2.6s. |

**These are synthesised with ffmpeg, not generated.** The ElevenLabs key carries the
`music` permission but not `sound_generation`, so `generate_sfx_candidates.py` fails with
a 401 until that scope is enabled on the key in the ElevenLabs dashboard. Its takes are
named `*_11l.mp3` and will land beside these for A/B.

Synthesis is arguably the better answer here regardless: static *is* filtered noise plus a
carrier, so it loops with no seam, costs nothing to re-roll, and raises no AI-content
licensing question. The one thing it cannot produce is the **fragments of distant speech**
that `STORY_SCRIPT.md:159` asks for in the free-roam bed — that needs generation, layered
over the synthesised bed.

## Before any of this ships

- **Licensing.** Eleven Music commercial rights depend on plan tier. Worth confirming
  before an itch.io release.
- **Labelling.** Guardrail #11 (`docs/GAME_COMPLETION_PLAN.md` ss.8) requires synthetic VO
  to be labelled as such. Generated score and SFX plausibly fall under the same honesty
  rule; `Assets/_Project/Art/Audio/VO/README.md` is the precedent to copy.
- **Cost.** These billed to the ElevenLabs key, which `docs/CONCEPT.md` flags as a second
  vendor off team credits — not the GCP credits Vinay owns.
