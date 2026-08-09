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

## Clean-VO processing

Every production render is processed as dry dialogue before it is installed:

1. Conservative FFmpeg broadband cleanup:
   `afftdn=nr=12:nf=-55:tn=1:gs=5`.
2. A 20 ms fade to digital zero, applied after denoising at the end of the
   denoised render.
3. Exactly 150 ms of appended digital silence, applied after the fade.

The order is intentional: denoising can leave residual tail energy, so the
fade and silence padding must remain after it. Scene ambience, room tone,
static, wind, and fire belong in the game mix and are not baked into these
voice clips. FFmpeg must be available on `PATH` when the generator renders a
new or intentionally overwritten line.

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Line manifest

| ID | Canonical spoken words | Performance direction | File |
|---|---|---|---|
| `AARON-001` | He's freezing. Let's get him inside, onto the sofa by the fire. | Controlled urgency; appear calm while immediately turning panic into a physical plan. | `AARON-001.wav` |
| `AARON-002` | Priya. Not now. | Firm redirection without raising his voice. | `AARON-002.wav` |
| `AARON-003` | Lift on three. One, two, three. | Team-lifting command followed by a steady count that cues the movement. | `AARON-003.wav` |
| `AARON-004` | Barely survived it. | Easy, dry anniversary joke before the night turns. | `AARON-004.wav` |
| `AARON-005` | …Two years? | Flat, slow disbelief; no anger on the surface. | `AARON-005.wav` |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `AARON-001`: `[calm on the surface, suppressing urgency, taking charge] He’s freezing.\n\n[steady and decisive] Let’s get him inside.\n\nOnto the sofa, by the fire.` (`\n\n` denotes a paragraph break.)
- `AARON-002`: `[controlled, firm, redirecting] Priya. Not now.`
- `AARON-003`: `[firm, coordinating the group before a heavy lift] Lift on three. One, two, three.`
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
- Clean-VO remediation: complete on 9 Aug 2026. All five files use the
  documented denoising, fade, and tail treatment. `AARON-001` was replaced by
  user-selected calm-under-pressure retake 8. `AARON-003` was expanded in the
  canonical script to include “One, two, three” and replaced by user-selected
  retake 3. `AARON-004` was replaced by user-selected retake 2 to correct its
  clipped ending. `AARON-002` and `AARON-005` were retained after listening
  review.
- Technical WAV validation: passed for all five files. Each opens as mono,
  24 kHz, 16-bit uncompressed PCM with nonzero frames, no full-scale sample
  clipping, and at least 150 ms of exact digital-zero tail.
- Stable-ID resolution: passed for `AARON-001` through `AARON-005` using the
  playback utility's `--dry-run` mode; no audio was played.
- Spoken-content and performance review: complete for all five files.
  User-selected retakes are installed for `AARON-001`, `AARON-003`, and
  `AARON-004`; all five remain pending final in-game mix review.
- Unity integration: not started and outside this production task.

| ID | Frames | Duration | Clean-VO technical status | Human review |
|---|---:|---:|---|---|
| `AARON-001` | 130,320 | 5.43 s | Passed | Retake 8 selected; in-game review pending |
| `AARON-002` | 47,760 | 1.99 s | Passed | Passed |
| `AARON-003` | 101,520 | 4.23 s | Passed | Retake 3 selected; in-game review pending |
| `AARON-004` | 43,920 | 1.83 s | Passed | Retake 2 selected; in-game review pending |
| `AARON-005` | 53,520 | 2.23 s | Passed | Passed |
