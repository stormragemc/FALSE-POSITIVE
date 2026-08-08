# Ivy production voice lines

This directory contains Ivy's finalized dialogue, one 24 kHz mono 16-bit PCM
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

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Line manifest

| ID | Dialogue | Performance direction |
|---|---|---|
| `IVY-001` | Oh my God. What happened to him? What do we do now? | Immediate shock and vulnerability. Frightened, not melodramatic. |
| `IVY-002` | I don't know. I was upstairs with Aaron. | Guarded and slightly too quick; the alibi is ready before anyone directly accuses her. |
| `IVY-003` | Yes. All night. | Brief controlled confirmation after a fraction of hesitation. |
| `IVY-004` | Careful. Careful. Easy. | Quiet physical focus while the group lowers Nick onto the sofa. |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `IVY-001`: `[shocked] Oh my God. What happened to him? What do we do now?`
- `IVY-002`: `[guarded, answering quickly] I don’t know. I was upstairs with Aaron.`
- `IVY-003`: `[guarded] Yes. All night.`
- `IVY-004`: `[quietly, focused] Careful. Careful... easy.`

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\generate_ivy_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\generate_ivy_voice_lines.py --force
```

Review one line without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\ivy\play_ivy_voice_line.py IVY-002
```
