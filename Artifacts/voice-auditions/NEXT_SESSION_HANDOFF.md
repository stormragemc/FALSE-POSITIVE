# Independent voice-audition preparation handoff

> **Casting update, 9 Aug 2026:** the radio announcer is finalized as Roger
> (`CwhRBWXzGAHq8TQ4Fs17`). `RADIO-001` and `RADIO-003` audition takes are
> approved; `RADIO-002` and `RADIO-004` still need production renders. Do not
> reopen the radio casting. See `../voice_guide/RadioAnnouncer.md`.

You are an independent Codex session working in the shared FALSE POSITIVE
workspace. Prepare audition assets, but do **not** play any audio and do **not**
start asking the user to select voices. The user will return later and direct
the auditions one character at a time.

## Required task

Regenerate a few representative ElevenLabs audition lines for the remaining
unfinalized prerecorded cast. Prioritize:

1. **Aaron** — his casting is still open. The user wants a grounded jock voice:
   athletic, confident, physically decisive, and used to taking charge. Aim for
   a team-captain quality, not a broad frat-bro caricature. A revised V3 round
   already exists for his first line; prepare useful additional lines in the
   same direction.
2. **Radio announcer** — completed after this handoff was written. Roger was
   selected; do not prepare more casting candidates. `RADIO-002` and `RADIO-004`
   remain production-rendering work, not audition work.

Do not prepare a David production voice: David is normally supplied through the
player's microphone. Do not reopen finalized casting for Spassky, Priya, Ivy, or
Nick.

## Current casting state

| Character | State | Selected voice |
|---|---|---|
| Officer Spassky | Finalized, full production set exists | Maksim — `6sXsAlJKKBf265ucBSRt` |
| Priya | Aaira selected; `PRIYA-001`–`008` approved and `014`–`016` pending review | Aaira — `1XNFRxE3WBB7iI0jnm7p` |
| Ivy | Finalized, full production set exists | Laura — `FGY2WhTYpPnrIDTdsKH5` |
| Nick | Casting finalized; production rendering pending | Ivan Energetic — `JKtNvDNrWu33P1xzttP2` |
| Aaron | Audition in progress | Not selected |
| Radio announcer | Finalized; `RADIO-001` and `RADIO-003` approved | Roger — `CwhRBWXzGAHq8TQ4Fs17` |
| David | Player microphone/reference-only | No production voice needed |

## Aaron audition state

- Historical eight-voice pool and three V2 lines are documented under
  `Artifacts/voice-auditions/aaron/`.
- Candidate order is Brian, Daniel, George, Eric, Liam, Will, Callum, Roger.
- The user asked for a more jock-like quality after hearing the original first
  round.
- `generate_aaron_jock_auditions.py` and
  `play_aaron_jock_line_01_body.py` implement the revised direction for:
  “He's freezing. Let's get him inside, onto the sofa by the fire.”
- Those revised files use `eleven_v3`, Natural stability `0.50`, similarity
  `0.75`, style `0.00`, speaker boost enabled, speed `1.00`, and `pcm_24000`.
- Extend the revised jock round with two or more canonical Aaron lines that test
  controlled deflection, terse physical command, and the quiet reaction to the
  affair. Preserve stable candidate numbering and create clearly named playback
  scripts with comments recording every public voice ID.

## Radio audition state

- Casting was completed on 9 Aug 2026. Roger (`CwhRBWXzGAHq8TQ4Fs17`) was
  selected from the V3 Natural round.
- `RADIO-001` and `RADIO-003` are approved audition takes. `RADIO-002` and
  `RADIO-004` remain unrendered production lines.
- Do not reopen radio casting unless the user explicitly asks.
- Historical metadata is under
  `Artifacts/voice-auditions/radio-announcer/` and in
  `generate_all_auditions.py`.
- Historical candidate order is Roger, Sarah, Daniel, Matilda, George, Jessica,
  Chris, Alice.
- Regenerate several useful candidates using Eleven V3 Natural settings unless
  listening evidence or the repository gives a concrete reason to retain V2.
- Keep the spoken warning canonical. Do not synthesize labels, candidate
  numbers, or production-only sound effects into the voice.

## Source of truth and workflow rules

- Read `docs/HUMAN_SCRIPT.md` before generating. It owns canonical dialogue and
  stable IDs.
- Read `Artifacts/voice-auditions/README.md`, the relevant character scripts,
  and existing guides under `Artifacts/voice_guide/` before editing.
- One candidate speaking one line must be one separate WAV file.
- Keep filenames and playback entry points obvious enough that a later Codex
  session can find them without the user supplying exact paths.
- Playback scripts must document number-to-name and number-to-voice-ID mappings
  in comments.
- Generate audition files only. Do not play them, pick winners, write final
  voice guides, create production line sets, integrate Unity assets, commit, or
  push.
- Preserve all existing approved audio and unrelated working-tree changes. Do
  not delete or overwrite finalized Priya, Ivy, Spassky, or Nick material.
- The ElevenLabs key is stored in the Windows **User** environment variable
  `ELEVENLABS_API_KEY`. Load it into the process environment without printing
  it. Never write the key to any source, log, Markdown, audio, or non-env file.
- Python environment: `Sidecar\.venv\Scripts\python.exe`.
- Default to four spaces for indentation.

When preparation is complete, summarize what was generated and then wait for
the user. Do not begin playback.
