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
16-bit PCM WAVs named by the stable IDs in `docs/HUMAN_SCRIPT.md`. The current
62 Spassky files are placeholders while replacement performances are prepared;
keeping the `SPASSKY-###` filenames lets those replacements drop in without
changing any recipe, Timeline, or subtitle mapping.

`Editor/VoTimelineBuilder.cs` creates one `PlayableDirector` and Timeline per
authored cutscene. Each Timeline places its VO and SFX at cumulative recipe-beat
times and binds them to separate audio sources. It also layers existing authored
character-state animation clips over spoken beats; `CutsceneStage` continues to
own scene-specific movement and blocking.

If a new Spassky line does need rendering, use `eleven_multilingual_v2` and the
delivery register that matches it — `Artifacts/voice_guide/Spassky.md` §4.3 has
the table, and `Artifacts/voice-lines/spassky/generate_spassky_voice_lines.py`
applies it. The settings quoted here previously (stability `0.15`,
similarity_boost `1.00`, style `0.85`, speed `0.85`, `−1.5 dB` trim) are the
`LOW` register specifically, which is right for the verdict and ending lines but
reads too heavy on a short press like "Then what?".

Exact production prompts and settings for every character are recorded beside
their source WAVs under `Artifacts/voice-lines/<character>/`.

Spassky's *live* in-game dialogue is generated turn-by-turn by the sidecar at
runtime (`Sidecar/tts.py`). Offline-demo and scripted cutscene paths use the
stable-ID WAVs from `Production/`.

Pre-rendering P1's answer matters for more than consistency. It is the longest
line Spassky has, and `eleven_multilingual_v2` costs 950–2100 ms scaling with
length — rendering it offline keeps that cost off the live turn budget.

Offline demo uses 14 canonical Spassky IDs selected by
`Editor/OfflineScriptBuilder.cs`, rather than the superseded
`spassky_offline_*` descriptive stems.
