# Ivy production voice lines

This directory contains Ivy's production dialogue, one 24 kHz mono 16-bit PCM
WAV per spoken line. Filenames match the stable IDs in `docs/HUMAN_SCRIPT.md`.

## Selected voice model

- Character: Ivy
- Casting reference: White, with a neutral English-language accent
- ElevenLabs voice: Laura
- ElevenLabs voice ID: `FGY2WhTYpPnrIDTdsKH5`
- Model: `eleven_v3`
- Stability: `0.50` (`Natural`)
- Similarity boost: `0.75`
- Style: `0.00`
- Speaker boost: enabled
- Speed: `1.00`
- Output: `pcm_24000`

## Clean-VO processing

Every production render is dry speech only. The generator applies conservative
FFmpeg broadband cleanup using exactly
`afftdn=nr=12:nf=-55:tn=1:gs=5`. After denoising, it fades the final 20 ms to
digital zero and appends 150 ms of exact digital silence. This order is
intentional: denoising can leave residual energy at the tail.

The generator stages source, denoised, and final WAVs beside the destination and
atomically replaces a production file only after processing succeeds. Existing
approved files are skipped unless `--force` is supplied.

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Line manifest

| ID | Dialogue | Performance direction |
|---|---|---|
| `IVY-001` | Oh my God. What happened to him? What do we do now? | Shock held in check, followed by two clear thought breaks and a quieter request for direction. |
| `IVY-002` | I don't know. I was upstairs with Aaron. | Guarded and slightly too quick; the alibi is ready before anyone directly accuses her. |
| `IVY-003` | Yes. All night. | Brief controlled confirmation after a fraction of hesitation. |
| `IVY-004` | Careful. Careful. Easy. | Hushed, practical concern; each word tracks the movement as Nick is lowered onto the sofa. |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `IVY-001`: `[shocked but restrained] Oh my God.\n\nWhat happened to him?\n\n[quieter, asking the others for direction] What do we do now?` (`\n\n` denotes a paragraph break.)
- `IVY-002`: `[guarded, answering quickly] I don’t know. I was upstairs with Aaron.`
- `IVY-003`: `[guarded] Yes. All night.`
- `IVY-004`: `[hushed, practical, concerned] Careful... careful... easy... [short pause]`

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\generate_ivy_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\generate_ivy_voice_lines.py --force
```

Regenerate selected lines intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\generate_ivy_voice_lines.py IVY-001 IVY-004 --force
```

Review one line without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\play_ivy_voice_line.py IVY-002
```

## Review status

All four production WAVs have passed offline format, sample-clipping, exact-zero
tail, stable-ID resolution, and user listening review after clean-VO processing.
Final in-game mix review remains pending.

`IVY-001` uses user-selected retake 4 with longer thought pauses and a more
restrained final question. `IVY-002` and `IVY-003` passed user listening review.
`IVY-004` uses user-selected take 22; its supported trailing `[short pause]`
produced 630 ms of low-energy raw tail before cleanup, preventing the earlier
clipped “easy.” The selected takes remain pending final in-game mix review.
