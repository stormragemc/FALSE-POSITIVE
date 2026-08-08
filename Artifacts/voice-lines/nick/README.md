# Nick production voice lines

This directory contains Nick's selected-voice production dialogue, one 24 kHz
mono 16-bit PCM WAV per spoken line. Filenames match the stable IDs in
`docs/HUMAN_SCRIPT.md`. The voice is synthetic.

## Selected voice model

- Character: Nick
- Casting reference: Russian, with a natural and intelligible Russian accent
- ElevenLabs voice: Ivan Energetic
- ElevenLabs voice ID: `JKtNvDNrWu33P1xzttP2`
- Model: `eleven_v3`
- Stability: `0.50` (`Natural`)
- Similarity boost: `0.75`
- Style: `0.00`
- Speaker boost: enabled
- Speed: `1.00`
- Output: `pcm_24000`

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

`NICK-001` uses the synthesis-only alias `Day-vid` for the name `David`,
enforcing the project-wide **DAY-vid** pronunciation without changing the
canonical words.

## Line manifest

| ID | Exact canonical words | Performance direction |
|---|---|---|
| `NICK-001` | Not tonight, David. I can't do this with you right now. I need some air. | Frustrated avoidance; tired of the subject, not hostile toward David. |
| `NICK-002` | He was worse at seventeen. | Fond teasing while remembering their school years. |
| `NICK-003` | Unfortunately. | Dry joke during the toast, with affection underneath. |
| `NICK-004` | Here. You look fucking freezing. | Casual warmth while throwing David the warmer coat. |
| `NICK-005` | You've been saying "after this trip" for two years. | Slightly drunk and careless; the secret escapes before he recognizes the danger. |
| `NICK-006` | He already knows. | Angry and humiliated after David presses him. |
| `NICK-007` | I need some air. | Tight and final; end the conversation and get out of the room. |

## Exact synthesis prompts

Audio tags and punctuation are generation directions; they are not additional
spoken dialogue.

- `NICK-001`: `[frustrated, avoiding the conversation] Not tonight, David. I can’t do this with you right now. I need some air.`
- `NICK-002`: `[fondly teasing] He was worse at seventeen.`
- `NICK-003`: `[dryly, joking] Unfortunately.`
- `NICK-004`: `[warm, teasing] Here. You look fucking freezing.`
- `NICK-005`: `[slightly drunk, careless] You’ve been saying “after this trip” for two years.`
- `NICK-006`: `[angry, humiliated] He already knows.`
- `NICK-007`: `[tense, trying to end the argument] I need some air.`

## File manifest and review status

| File | Generation | Technical validation | Performance review |
|---|---|---|---|
| `NICK-001.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-002.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-003.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-004.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-005.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-006.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |
| `NICK-007.wav` | Generated | Passed: mono, 24 kHz, 16-bit PCM, nonzero frames | Pending user review |

## Utilities

Generate only missing lines:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\nick\generate_nick_voice_lines.py
```

Regenerate every line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\nick\generate_nick_voice_lines.py --force
```

Regenerate one line intentionally:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\nick\generate_nick_voice_lines.py --force --only NICK-001
```

Resolve one stable ID without playing audio:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\nick\play_nick_voice_line.py NICK-002 --dry-run
```

Review one line without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\nick\play_nick_voice_line.py NICK-002
```
