# Priya production voice lines

This directory contains Priya's finalized dialogue, one 24 kHz mono 16-bit PCM
WAV per spoken line. Filenames match the stable IDs in `docs/HUMAN_SCRIPT.md`.

## Selected voice model

- Character: Priya
- Casting reference: Indian, with a natural and easy-to-understand Indian accent
- ElevenLabs voice: Aaira
- ElevenLabs voice ID: `1XNFRxE3WBB7iI0jnm7p`
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
| `PRIYA-001` | Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here! | Immediate alarm. Call the names quickly and clearly without audible gasping. |
| `PRIYA-002` | What do we do? What do we do? | Two sharp panic pleas as Priya loses composure. Strong urgency without a breathless direction. |
| `PRIYA-003` | How did this happen? | Stunned disbelief while looking at Nick, quieter than the surrounding panic. |
| `PRIYA-004` | All night? | Short, skeptical, and pointed. Priya notices Ivy's hesitation. |
| `PRIYA-005` | The door was locked. Who locked it? | Quiet realization, a half-beat, then direct suspicion. |
| `PRIYA-006` | Nick? Nick, can you hear me? | Tentative first call, a short listening pause, then close concern. |
| `PRIYA-007` | Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry. | Panicked but intelligible. Report the facts quickly, then make the final plea urgent. |
| `PRIYA-008` | What happened? Why won't anyone tell me what happened? | Shaken disorientation, a longer pause, then hurt frustration rather than anger. |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `PRIYA-001`: `[worried] Guys—help! Something’s happened to Nick. IVY! AARON! DAVID! Please—come here!`
- `PRIYA-002`: `[panicked] What do we do?! What do we do?!`
- `PRIYA-003`: `[stunned] How did this happen?`
- `PRIYA-004`: `[skeptical] All night?`
- `PRIYA-005`: `[realizing] The door was locked... who locked it?`
- `PRIYA-006`: `[softly] Nick? ... Nick, can you hear me?`
- `PRIYA-007`: `[panicked but clear] Police? Our friend is hurt! We found him outside—in the snow. Please send someone. Please hurry!`
- `PRIYA-008`: `[shaken] What happened...? Why won’t anyone tell me what happened?`

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py --force
```

Review one line without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\play_priya_voice_line.py PRIYA-007
```
