# Officer Spassky filler voice lines

This directory contains the 10 canonical latency-cover acknowledgements defined in
`Artifacts/filler_responses.md`. Each file is synthetic speech generated through
ElevenLabs; no human voice actor recorded these lines.

All 10 WAVs were generated and passed automated technical validation on
9 Aug 2026. Their durations range from 0.800 s to 1.961 s, including the final
150 ms playback-safe silence, and their highest measured peak is 0.9009 of full
scale.

## Voice and delivery

- Character: Officer Spassky
- Voice: Maksim — "Raw, unpolished, deep"
- Voice ID: `6sXsAlJKKBf265ucBSRt`
- Model: `eleven_multilingual_v2`
- Register: `FLAT`
- Stability: `0.28`
- Similarity boost: `1.00`
- Style: `0.62`
- Speed: `0.92`
- Output: mono 24 kHz 16-bit PCM WAV
- Direction: terse, watchful acknowledgement; heard but neither believed nor
  disbelieved

The exact synthesis prompt for every line is identical to the dialogue in the
manifest. No performance tags are included because Spassky's selected model is
not the Eleven V3 audio-tag model.

## Cleanup and endpoint safety

Every generated file receives the same conservative dry-VO cleanup as the main
Spassky production set:

1. FFmpeg `afftdn=nr=12:nf=-55:tn=1:gs=5` broadband denoising.
2. A 20 ms fade of the existing endpoint to digital zero.
3. Exactly 150 ms (3,600 frames) of appended digital silence.
4. Automated checks for mono 24 kHz 16-bit PCM format, full-scale samples,
   peak headroom, and the exact zero tail.

No room tone, ambience, static, or other background texture is intentionally
baked into the clips. Human listening is still required for final performance
approval and to catch perceptual artifacts that numeric QA cannot identify.

## Line manifest

| ID | Dialogue and exact prompt |
|---|---|
| `SPASSKY-FILLER-001` | Hm. |
| `SPASSKY-FILLER-003` | I see. |
| `SPASSKY-FILLER-004` | Interesting. |
| `SPASSKY-FILLER-005` | All right. |
| `SPASSKY-FILLER-006` | Very well. |
| `SPASSKY-FILLER-007` | Noted. |
| `SPASSKY-FILLER-009` | That's noted. |
| `SPASSKY-FILLER-010` | Hm. I see. |
| `SPASSKY-FILLER-013` | Interesting. All right. |
| `SPASSKY-FILLER-023` | Hm... interesting. |

## Utilities

Generate only missing WAVs:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\spassky_filler\generate_spassky_filler_voice_lines.py
```

Validate every existing WAV without contacting ElevenLabs:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\spassky_filler\generate_spassky_filler_voice_lines.py --validate-only
```

Review selected clips without opening a media-player window:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\spassky_filler\play_spassky_filler_voice_line.py SPASSKY-FILLER-001 SPASSKY-FILLER-003
```

The generator skips existing WAVs unless `--force` is supplied. It reads
`ELEVENLABS_API_KEY` from the process environment and never stores or prints it.
