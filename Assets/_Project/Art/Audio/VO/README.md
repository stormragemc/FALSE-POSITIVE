# Voice-over — synthetic

Every audio file in this folder is synthetic text-to-speech generated via the
ElevenLabs API (`/elevenlabs-dialog`). No human voice actor recorded any line
in this game. Guardrail #11 (`docs/GAME_COMPLETION_PLAN.md` §8): all character
VO must be labelled synthetic in the README and the deck — this file is that
label for this folder.

Voice casting (ElevenLabs Voice Library / premade voices, not custom clones):

| Character | Voice | Voice ID |
|---|---|---|
| Officer Spassky | Maksim — "Raw, unpolished, deep", Russian | `6sXsAlJKKBf265ucBSRt` |
| Nick | Ivan Energetic, Russian | `JKtNvDNrWu33P1xzttP2` |
| Radio | Roger | `CwhRBWXzGAHq8TQ4Fs17` |
| Priya | Aaira, Indian | `1XNFRxE3WBB7iI0jnm7p` |
| Ivy | Laura | `FGY2WhTYpPnrIDTdsKH5` |
| Aaron | Liam | `TX3LPaxmHKxFdv7VOQHJ` |

The canonical Unity imports live under `Production/`: exactly 93 mono 24 kHz
16-bit PCM WAVs named by the stable IDs in `docs/HUMAN_SCRIPT.md`. On 9 Aug
2026, all 62 Spassky placeholders were replaced by the completed ElevenLabs V3
performances. Their stable filenames and existing `.meta` GUIDs were preserved,
so no recipe, Timeline, or subtitle mapping changed.

`Editor/VoTimelineBuilder.cs` creates one `PlayableDirector` and Timeline per
authored cutscene. Each Timeline places its VO and SFX at cumulative recipe-beat
times and binds them to separate audio sources. It also layers existing authored
character-state animation clips over spoken beats; `CutsceneStage` continues to
own scene-specific movement and blocking.

If a new Spassky line needs rendering, use `eleven_v3` with stability `1.0`,
similarity boost `1.0`, style `0.0`, speaker boost enabled, and the explicit
`strong Russian accent` audio tag. The production generator maps `PRESS` to
`impatient`, `RAISED` to `shouting`, and `LOW` to `quietly menacing`; `FLAT`
uses the accent tag alone.

Exact production prompts and settings for every character are recorded beside
their source WAVs under `Artifacts/voice-lines/<character>/`.

Spassky's *live* in-game dialogue is generated turn-by-turn by the sidecar at
runtime (`Sidecar/tts.py`). Offline-demo and scripted cutscene paths use the
stable-ID WAVs from `Production/`.

Pre-rendering P1's answer keeps the longest scripted Spassky line off the live
turn budget and ensures it uses the same reviewed V3 performance every time.

Offline demo uses 14 canonical Spassky IDs selected by
`Editor/OfflineScriptBuilder.cs`, rather than the superseded
`spassky_offline_*` descriptive stems.
