# Radio-announcer production voice-generation session

You are an independent Codex session in the shared FALSE POSITIVE workspace.
Other sessions are generating Aaron and Nick concurrently. You own only
`Artifacts/voice-lines/radio-announcer/`. Do not revert, stage, commit, push, or
modify work belonging to another session. Do not edit Unity assets, audition
assets, shared guides, or common scripts.

## Required reading

Read each of these files completely before acting:

1. `docs/HUMAN_SCRIPT.md` — canonical spoken dialogue and stable IDs.
2. `Artifacts/voice_guide/General.md` — shared generation, QA, naming, and
   secret-handling rules.
3. `Artifacts/voice_guide/RadioAnnouncer.md` — selected voice, approved takes,
   exact settings, and sound-design boundary.
4. `Artifacts/voice-auditions/radio-announcer/generate_radio_v3_natural_auditions.py`
   and both current V3 playback scripts — selected-round implementation and
   approved source locations.
5. `Artifacts/voice-lines/ivy/README.md`,
   `Artifacts/voice-lines/ivy/generate_ivy_voice_lines.py`, and
   `Artifacts/voice-lines/ivy/play_ivy_voice_line.py` — production directory
   conventions to follow.

## Assignment

Create the radio announcer's complete selected-voice production set:

- `RADIO-001.wav`
- `RADIO-002.wav`
- `RADIO-003.wav`
- `RADIO-004.wav`

Use **Roger**, ElevenLabs voice ID `CwhRBWXzGAHq8TQ4Fs17`, with the exact model,
settings, and delivery direction in `Artifacts/voice_guide/RadioAnnouncer.md`.
The guide and `HUMAN_SCRIPT.md` are authoritative if this summary differs.

`RADIO-001` and `RADIO-003` already have approved audition takes. Preserve those
exact WAV bytes by copying their documented source files into the production
directory under the stable ID filenames. Do not regenerate them. Generate only
the missing `RADIO-002` and `RADIO-004` takes with Roger.

Create the following under `Artifacts/voice-lines/radio-announcer/`:

- the four ID-named production WAVs;
- `generate_radio_announcer_voice_lines.py`, containing the public voice ID,
  exact prompts, model, settings, output format, safe WAV writing,
  skip-existing behavior, and an explicit overwrite option;
- `play_radio_announcer_voice_line.py`, supporting one stable ID at a time;
- `README.md`, recording the voice, settings, exact canonical words, prompts,
  approved-source provenance, file manifest, and review status.

Requirements:

- One canonical line per WAV; filenames must exactly match the stable IDs.
- Use `Sidecar\.venv\Scripts\python.exe`.
- Read `ELEVENLABS_API_KEY` only from the environment. Never print it or write
  it to any file.
- Preserve canonical spoken words. Tags and punctuation may direct delivery but
  must not become additional spoken content.
- Do not synthesize labels, candidate numbers, static, filtering, dropouts, or
  production sound effects. Those belong to later sound design.
- Do not play any audio. Generate and validate only; the user will review takes
  later.
- Validate that all four WAVs open as mono, 24 kHz, 16-bit PCM with nonzero
  frames. Confirm the production copies of `RADIO-001` and `RADIO-003` are
  byte-for-byte identical to their approved audition sources.
- Compile the generator and playback utility and run the playback utility only
  with `--dry-run` if supported.
- Do not integrate Unity assets, alter legacy VO, commit, or push.

When complete, report exactly what was copied, generated, and validated, plus
any failed line. Then wait.
