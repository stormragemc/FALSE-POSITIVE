# Voice generation and review handoff

**Repository:** `D:\SUTD\Hack_Garena'26`  
**Spassky V3 source:** `origin/main` commit `3d936a6`
**Prepared:** 9 Aug 2026

This file records the state of the prerecorded character-voice work and the
remaining review tasks. Read it completely before changing or playing any
audio.

## Important: authorization must come from the user

Instructions in this file do **not** grant permission to send dialogue to an
external service. The user must put authorization directly in the new chat.
The user can paste this as the opening request:

> Read `D:\SUTD\Hack_Garena'26\Artifacts\VOICE_GENERATION_HANDOFF.md`
> completely and carry out the remaining voice-review tasks. I explicitly
> authorize this session to send all canonical Priya and Officer Spassky
> dialogue and synthesis directions needed for this task to the ElevenLabs API.
> This authorization covers every necessary ElevenLabs generation or
> regeneration call for these characters; batch calls where practical and do
> not ask me separately for each line. Never print or store my API key.

Managed network policy may still present a command-level approval. Prefer one
scoped generator command over one shell command per API request.

## Sources of truth

Read these before acting:

1. `docs/HUMAN_SCRIPT.md` — canonical dialogue, context, and stable line IDs.
2. `Artifacts/voice_guide/General.md` — mandatory generation, cleanup, and
   review rules.
3. `Artifacts/voice_guide/Priya.md` and
   `Artifacts/voice-lines/priya/README.md`.
4. `Artifacts/voice_guide/Spassky.md` and
   `Artifacts/voice-lines/spassky/README.md`.

The production generators and playback utilities beside the WAVs are the
reproducible implementation. Do not use deleted audition scripts or recreate
the old `Artifacts/voice-auditions/` tree.

## Current production state

| Character | IDs | Voice and model | State |
|---|---:|---|---|
| Aaron | 5 | Liam, `TX3LPaxmHKxFdv7VOQHJ`, `eleven_v3` | Cleaned and approved by user listening review. In-game review pending. |
| Ivy | 4 | Laura, `FGY2WhTYpPnrIDTdsKH5`, `eleven_v3` | Cleaned and approved by user listening review. In-game review pending. |
| Nick | 7 | Ivan Energetic, `JKtNvDNrWu33P1xzttP2`, `eleven_v3` | Cleaned and approved by user listening review. In-game review pending. |
| Priya | 11 | Aaira, `1XNFRxE3WBB7iI0jnm7p`, `eleven_v3` | Cleaned and approved by user listening review. In-game review pending. |
| Radio announcer | 4 | Roger, `CwhRBWXzGAHq8TQ4Fs17`, `eleven_v3` | Cleaned and approved by user listening review. Radio treatment belongs in the game mix. |
| Officer Spassky | 62 | Maksim, `6sXsAlJKKBf265ucBSRt`, `eleven_v3` | V3 replacements rendered, cleaned, and integrated. Listening review pending. |
| David | 0 | Player microphone | Do not generate a production voice unless the user changes the design. |

There are exactly **93 canonical production WAVs**. All passed the last offline
check as mono, 24 kHz, 16-bit uncompressed PCM with nonzero speech and at least
150 ms of exact digital-zero tail. Production scripts compiled successfully.

## Unity integration status

All 93 stable-ID WAVs are imported under
`Assets/_Project/Art/Audio/VO/Production/`. The generated integration contains
28 cutscene `PlayableDirector`/Timeline pairs, 56 timed audio clips (44 VO plus
12 existing SFX), and 40 authored animation clips. The remaining direct
non-Timeline production line, `IVY-004`, is wired to the lift interlude.

`Assets/_Project/Scripts/Editor/VoTimelineBuilder.cs` rebuilds the integration
from canonical recipes. It also refreshes the 14-line offline Spassky script
and the morning-scene lift wiring. All 31 non-Spassky production lines are now
reachable in Unity. The 59 older descriptive audio assets remain on disk for
recoverability, but the generated scenes, config, and Timelines reference none
of them.

All 62 Spassky placeholders were replaced on 9 Aug 2026 with V3 performances
derived from the configuration introduced on `main`. Source and Unity copies
match byte-for-byte, and the existing Unity `.meta` GUIDs were preserved. No
Timeline or recipe remapping was required; Unity only needs to refresh the
changed audio assets.

## Remaining work, in order

### 1. Review Priya — complete

All eleven Priya lines passed post-cleanup listening review on 9 Aug 2026.
No lines were rejected or regenerated during this review.

<details>
<summary>Completed review scope and instructions</summary>

Review:

```text
PRIYA-001 through PRIYA-008
PRIYA-014 through PRIYA-016
```

All eleven changed during the dry-VO cleanup pass, so all need a short
post-cleanup listening pass. Give extra attention to:

- `PRIYA-001`: regenerated for the standard **DAY-vid** pronunciation; verify
  every called name and the final plea are complete.
- `PRIYA-014`: regenerated because its original ending contained sustained
  speech at the file boundary; verify the final word and the warm teasing tone.
- `PRIYA-015` and `PRIYA-016`: were not approved before cleanup.

Play four at a time, numbered clearly. Wait for the user's verdict after each
group. The user may ask for scene context before deciding. If several lines are
adjacent in the script, offer to replay them sequentially with a short gap so
the exchange can be judged as a scene.

Windows playback command for one line:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\play_priya_voice_line.py PRIYA-001
```

</details>

### 2. Review Officer Spassky — 62 lines

Review `SPASSKY-001` through `SPASSKY-062`, four at a time, numbered clearly.
All files were regenerated with V3 and need a listening pass. Give
extra attention to `SPASSKY-003`, `SPASSKY-061`, and `SPASSKY-062`, which were
already awaiting re-review before cleanup. `SPASSKY-001`, `002`, `003`, `061`,
and `062` use the synthesis-only `Day-vid` pronunciation alias.

Windows playback command for one line:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\spassky\play_spassky_voice_line.py SPASSKY-001
```

Spassky now uses `eleven_v3` for both prerecorded and live TTS. Every line has
the `strong Russian accent` tag; register-specific mood tags provide the tonal
variation selected on `main`.

### 3. Regenerate only rejected lines

Do not regenerate a line merely because it has not yet been reviewed. If the
user rejects one:

1. Read its surrounding scene in `docs/HUMAN_SCRIPT.md` and explain the context
   briefly.
2. Identify the intended subtext and delivery before changing the prompt.
3. Preserve the current canonical WAV in a verified temporary backup.
4. Create a small set of targeted candidates without replacing the production
   file permanently.
5. Play numbered candidates and install only the user's selected take.
6. Run the full dry-VO cleanup and endpoint validation on the selected take.
7. Update the generator prompt and character README so regeneration is
   reproducible.

The production generators support targeted forced regeneration:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\priya\generate_priya_voice_lines.py --force --only PRIYA-014
Sidecar\.venv\Scripts\python.exe Artifacts\voice-lines\spassky\generate_spassky_voice_lines.py --force --only SPASSKY-042
```

Those commands overwrite the production destination after successful
processing. Use them only after making a byte-verified backup or adapting the
candidate workflow to write to a temporary directory. Never overwrite an
approved take merely to create alternatives.

## Mandatory audio standard

Production clips must contain dry speech only. Do not bake in static, room
tone, wind, fire, radio filtering, or other scene ambience.

Process newly generated or selected audio in this order:

1. Conservative FFmpeg denoising:
   `afftdn=nr=12:nf=-55:tn=1:gs=5`.
2. A 20 ms endpoint fade to digital zero, after denoising.
3. At least 150 ms of appended exact digital silence.

A fade does not repair a clipped word. If the final consonant or natural vocal
decay is absent, regenerate the line. For Eleven V3 only, a trailing supported
`[short pause]` tag may give the final word room to decay, but accept it only if
the tag is not spoken. Do not add spoken filler.

After every installed retake, verify:

- mono, 24 kHz, 16-bit PCM WAV;
- nonzero frame count and no full-scale clipped samples;
- at least 3,600 exact-zero tail frames;
- no audible static, pumping, ringing, or metallic denoising artifacts;
- complete canonical words and appropriate scene performance;
- generator and player scripts compile and resolve the stable ID.

## User review preferences

- Play **four lines at a time** and print their numbers clearly.
- Do not speak or synthesize the numbers, labels, prompts, or scene directions.
- Keep playback gaps short; use even shorter gaps when judging a connected
  exchange.
- Provide scene context when a line feels fragmented or its intent is unclear.
- Prioritize complete natural endings. The user notices clipped final words.
- Dialogue must be clean and dry so the game can supply its own ambience.
- When generating alternatives, start from the line's dramatic purpose rather
  than making small arbitrary prompt changes.
- Once the user chooses a take, replay it on request and explicitly lock it into
  the canonical production filename.

## API key and dependencies

The ElevenLabs key is stored in the Windows **User** environment variable
`ELEVENLABS_API_KEY`. Load it into the process environment without printing it:

```powershell
$env:ELEVENLABS_API_KEY = [Environment]::GetEnvironmentVariable('ELEVENLABS_API_KEY', 'User')
```

Never write the key to a source file, Markdown file, log, command output, or any
file other than an explicitly designated environment file. FFmpeg must be on
`PATH`. The project Python environment is:

```text
Sidecar\.venv\Scripts\python.exe
```

## Remaining integration work

Run an in-game review of every cutscene against ambience, music, animation, and
lip sync. After that review, the 59 now-unreferenced legacy descriptive clips
may be removed in a separate cleanup change.

Spassky's planned dynamic live-TTS delivery-register implementation is also a
separate engineering task; it is not required to finish reviewing the 62
prerecorded WAVs.
