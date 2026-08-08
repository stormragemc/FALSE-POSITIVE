# Aaron production voice lines

This directory contains Aaron's synthetic production dialogue, one 24 kHz mono
16-bit PCM WAV per spoken line. Filenames match the stable IDs in
`docs/HUMAN_SCRIPT.md`.

## Selected voice model

- Character: Aaron
- Casting reference: White, with a neutral English-language accent
- ElevenLabs voice: Liam
- ElevenLabs voice ID: `TX3LPaxmHKxFdv7VOQHJ`
- Model: `eleven_v3`
- Stability: `0.50` (`Natural`)
- Similarity boost: `0.75`
- Style: `0.00`
- Speaker boost: enabled
- Speed: `1.00`
- Output: `pcm_24000`

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Line manifest

| ID | Canonical spoken words | Performance direction | File |
|---|---|---|---|
| `AARON-001` | He's freezing. Let's get him inside, onto the sofa by the fire. | Grounded urgency; immediately turn panic into a physical plan. | `AARON-001.wav` |
| `AARON-002` | Priya. Not now. | Firm redirection without raising his voice. | `AARON-002.wav` |
| `AARON-003` | Lift on three. | Short team-lifting command with clean timing. | `AARON-003.wav` |
| `AARON-004` | Barely survived it. | Easy, dry anniversary joke before the night turns. | `AARON-004.wav` |
| `AARON-005` | …Two years? | Flat, slow disbelief; no anger on the surface. | `AARON-005.wav` |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `AARON-001`: `[confident, athletic, urgent but controlled] He’s freezing. Let’s get him inside—onto the sofa, by the fire.`
- `AARON-002`: `[controlled, firm, redirecting] Priya. Not now.`
- `AARON-003`: `[terse, physically decisive] Lift on three.`
- `AARON-004`: `[relaxed, dryly joking] Barely survived it.`
- `AARON-005`: `[quiet, flat, stunned] ...Two years?`

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\aaron\generate_aaron_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\aaron\generate_aaron_voice_lines.py --overwrite
```

Resolve one stable ID without playing audio:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\aaron\play_aaron_voice_line.py AARON-002 --dry-run
```

Omit `--dry-run` only during an authorized listening review.

## Review status

- Voice identity and synthesis configuration: selected and locked.
- Production generation: complete on 9 Aug 2026; all five Liam takes were
  rendered directly as new production assets.
- Technical WAV validation: passed for all five files. Each opens as mono,
  24 kHz, 16-bit uncompressed PCM with nonzero frames.
- Stable-ID resolution: passed for `AARON-001` through `AARON-005` using the
  playback utility's `--dry-run` mode; no audio was played.
- Spoken-content and performance review: pending user listening review.
- Unity integration: not started and outside this production task.

| ID | Frames | Duration |
|---|---:|---:|
| `AARON-001` | 90,240 | 3.76 s |
| `AARON-002` | 44,160 | 1.84 s |
| `AARON-003` | 44,160 | 1.84 s |
| `AARON-004` | 34,560 | 1.44 s |
| `AARON-005` | 49,920 | 2.08 s |
