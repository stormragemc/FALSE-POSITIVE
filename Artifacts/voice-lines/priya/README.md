# Priya production voice lines

This directory contains Priya's synthetic selected-voice dialogue, one 24 kHz
mono 16-bit PCM WAV per spoken line. Filenames match the stable IDs in
`docs/HUMAN_SCRIPT.md`. The performances in `PRIYA-002` through `PRIYA-008`
were approved before the dry-VO cleanup pass. `PRIYA-001` was regenerated on
9 Aug 2026 for the standardized **DAY-vid** pronunciation and was already
pending re-review; `PRIYA-014` through `PRIYA-016` were also pending review.
The cleaned outputs `PRIYA-001` through `PRIYA-008` passed post-cleanup
listening review on 9 Aug 2026. `PRIYA-014` through `PRIYA-016` passed the same
review later that day, completing post-cleanup approval for all eleven lines.

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

`PRIYA-001` uses the synthesis-only alias `DAY-VID` for the name `David`,
enforcing the project-wide **DAY-vid** pronunciation without changing the
canonical words.

## Dry-VO cleanup and tail treatment

Every production render is processed in this mandatory order:

1. Conservative FFmpeg broadband cleanup with
   `afftdn=nr=12:nf=-55:tn=1:gs=5`.
2. A 20 ms fade over the final source samples to reach digital zero.
3. Exactly 150 ms of appended digital silence.

The generator performs this processing before an atomic WAV replacement, skips
existing files by default, and overwrites only when `--force` is explicit.
The cleanup pass preserves native line-to-line dynamics; it does not normalize,
compress, or add ambience.

On 9 Aug 2026, all eleven production WAVs received this cleanup and tail
treatment. The original `PRIYA-014` ended during sustained speech with no
natural decay, so it was regenerated after explicit user authorization. The
replacement passed technical validation and human listening confirmed the
complete final word and performance on 9 Aug 2026.

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
| `PRIYA-014` | Fifteen years and you two still act exactly the same. | Warmly teasing two old friends while holding up their school photograph. |
| `PRIYA-015` | And two years for these two. | Playful affection as she shifts the group's attention to Aaron and Ivy. |
| `PRIYA-016` | To us. Somehow. | A warm toast with a faintly wry, reflective turn on “somehow.” |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `PRIYA-001`: `[worried] Guys—help! Something’s happened to Nick.` then
  `IVY! AARON! DAVID!` then `Please—come here!`, with paragraph breaks between
  all three beats. The generator substitutes `DAY-VID` only at synthesis time.
- `PRIYA-002`: `[panicked] What do we do?! What do we do?!`
- `PRIYA-003`: `[stunned] How did this happen?`
- `PRIYA-004`: `[skeptical] All night?`
- `PRIYA-005`: `[realizing] The door was locked... who locked it?`
- `PRIYA-006`: `[softly] Nick? ... Nick, can you hear me?`
- `PRIYA-007`: `[panicked but clear] Police? Our friend is hurt! We found him outside—in the snow. Please send someone. Please hurry!`
- `PRIYA-008`: `[shaken] What happened...? Why won’t anyone tell me what happened?`
- `PRIYA-014`: `[warmly amused] Fifteen years—and you two still act exactly the same.`
- `PRIYA-015`: `[playfully] And two years for these two.`
- `PRIYA-016`: `[warm, lightly wistful] To us... somehow.`

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py --force
```

Regenerate one line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py --force --only PRIYA-001
```

Review one line without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\play_priya_voice_line.py PRIYA-007
```
