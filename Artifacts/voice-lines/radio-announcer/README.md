# Radio announcer production voice lines

This directory contains the radio announcer's synthetic production dialogue,
one 24 kHz mono 16-bit PCM WAV per spoken line. Filenames match the stable IDs
in `docs/HUMAN_SCRIPT.md`. The clean source clips contain no radio static,
filtering, dropouts, masking, labels, or candidate numbers; those belong to
later, non-destructive sound design.

## Selected voice and settings

- Character: Radio announcer
- Casting reference: White; neutral, impersonal, and easy to understand
- ElevenLabs voice: Roger
- ElevenLabs voice ID: `CwhRBWXzGAHq8TQ4Fs17`
- Model: `eleven_v3`
- Stability: `0.50` (`Natural`)
- Similarity boost: `0.75`
- Style: `0.00`
- Speaker boost: enabled
- Speed: `1.00`
- Output: `pcm_24000`

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Canonical line manifest and review status

| ID | Exact canonical words | Production provenance | Review status |
|---|---|---|---|
| `RADIO-001` | A snowstorm is moving through the area. Please stay indoors until conditions improve. | Exact copy of approved V3 Natural Roger audition `v3-natural-round/line-01-storm-warning/01-roger.wav` | Approved take |
| `RADIO-002` | …snow storm… | Production render generated with Roger | Pending listening review |
| `RADIO-003` | …please stay indoors… | Exact copy of approved V3 Natural Roger audition `v3-natural-round/line-03-stay-indoors/01-roger.wav` | Approved take |
| `RADIO-004` | …during these times. | Production render generated with Roger | Pending listening review |

The approved audition source paths are relative to
`Artifacts/voice-auditions/radio-announcer/`. The ellipses on the short memory
fragments indicate clipped surrounding radio context; they are not extra spoken
content.

## Exact synthesis prompts

The bracketed direction and punctuation guide delivery and must not be spoken.

- `RADIO-001`: `[neutral, impersonal, calm public safety announcement] A snowstorm is moving through the area. Please stay indoors until conditions improve.`
- `RADIO-002`: `[neutral, impersonal, calm public safety announcement] ...snow storm...`
- `RADIO-003`: `[neutral, impersonal, calm public safety announcement] ...please stay indoors...`
- `RADIO-004`: `[neutral, impersonal, calm public safety announcement] ...during these times.`

## Utilities

Generate missing lines while preserving every existing WAV:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\radio-announcer\generate_radio_announcer_voice_lines.py
```

Generate selected missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\radio-announcer\generate_radio_announcer_voice_lines.py --line RADIO-002 --line RADIO-004
```

Overwrite selected lines only when regeneration is intentional:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\radio-announcer\generate_radio_announcer_voice_lines.py --line RADIO-002 --overwrite
```

Resolve one stable ID without playing audio:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\radio-announcer\play_radio_announcer_voice_line.py RADIO-002 --dry-run
```
