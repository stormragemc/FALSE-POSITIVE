# Aaron production voice-generation session

You are an independent Codex session in the shared FALSE POSITIVE workspace.
Other sessions are generating Nick and the radio announcer concurrently. You
own only `Artifacts/voice-lines/aaron/`. Do not revert, stage, commit, push, or
modify work belonging to another session. Do not edit Unity assets, audition
assets, shared guides, or common scripts.

## Required reading

Read each of these files completely before acting:

1. `docs/HUMAN_SCRIPT.md` — canonical spoken dialogue and stable IDs.
2. `Artifacts/voice_guide/General.md` — shared generation, QA, naming, and
   secret-handling rules.
3. `Artifacts/voice_guide/Aaron.md` — selected voice, exact settings, intent,
   and line-specific prompts.
4. `Artifacts/voice-auditions/aaron/generate_aaron_jock_auditions.py` — public
   voice ID and selected-round implementation reference.
5. `Artifacts/voice-lines/ivy/README.md`,
   `Artifacts/voice-lines/ivy/generate_ivy_voice_lines.py`, and
   `Artifacts/voice-lines/ivy/play_ivy_voice_line.py` — production directory
   conventions to follow.

## Assignment

Generate Aaron's complete selected-voice production set:

- `AARON-001.wav`
- `AARON-002.wav`
- `AARON-003.wav`
- `AARON-004.wav`
- `AARON-005.wav`

Use **Liam**, ElevenLabs voice ID `TX3LPaxmHKxFdv7VOQHJ`, with the exact model,
settings, and synthesis prompts in `Artifacts/voice_guide/Aaron.md`. The guide
and `HUMAN_SCRIPT.md` are authoritative if this summary differs.

Create the following under `Artifacts/voice-lines/aaron/`:

- the five ID-named production WAVs;
- `generate_aaron_voice_lines.py`, containing the public voice ID, exact
  prompts, model, settings, output format, safe WAV writing, skip-existing
  behavior, and an explicit overwrite option;
- `play_aaron_voice_line.py`, supporting one stable ID at a time;
- `README.md`, recording the voice, settings, exact canonical words, prompts,
  file manifest, and review status.

Requirements:

- One canonical line per WAV; filenames must exactly match the stable IDs.
- Use `Sidecar\.venv\Scripts\python.exe`.
- Read `ELEVENLABS_API_KEY` only from the environment. Never print it or write
  it to any file.
- Preserve canonical spoken words. Tags and punctuation may direct delivery but
  must not become additional spoken content.
- Do not play any audio. Generate and validate only; the user will review takes
  later.
- Do not treat audition WAVs as approved production takes. Generate all five
  production lines using the selected Liam configuration.
- Validate that every WAV opens as mono, 24 kHz, 16-bit PCM with nonzero frames.
- Compile the generator and playback utility and run the playback utility only
  with `--dry-run` if supported.
- Do not integrate Unity assets, alter legacy VO, commit, or push.

When complete, report exactly what was generated, validation results, and any
lines that failed. Then wait.
