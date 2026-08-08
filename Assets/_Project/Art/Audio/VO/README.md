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
| Nick | Artem Lebedev — "Podcast Pro", Russian | `rQOBu7YxCDxGiFdTm28w` |
| Radio | Daniel | `onwK4e9ZLuTAKqWW03F9` |
| Priya | Jessica | `cgSgspJ2msm6clMCkdW9` |
| Ivy | Lily | `pFZP5JQG7iQjIQuC4Bku` |
| Aaron | Eric | `cjVigY5qzO86Huf0OWal` |
| "David" (wake calls) | River | `SAz9YHcvj6GT2YYXdXww` |

**Spassky's full line set is already rendered** — all 62 IDs from
`docs/HUMAN_SCRIPT.md` are in `Artifacts/voice-lines/spassky/`, as 24 kHz mono
WAVs named after their script IDs. Import from there rather than re-rendering.

If a new Spassky line does need rendering, use `eleven_multilingual_v2` and the
delivery register that matches it — `Artifacts/voice_guide/Spassky.md` §4.3 has
the table, and `Artifacts/voice-lines/spassky/generate_spassky_voice_lines.py`
applies it. The settings quoted here previously (stability `0.15`,
similarity_boost `1.00`, style `0.85`, speed `0.85`, `−1.5 dB` trim) are the
`LOW` register specifically, which is right for the verdict and ending lines but
reads too heavy on a short press like "Then what?".

**Render Nick's clips with these settings** — model `eleven_multilingual_v2`,
stability `0.35`, similarity_boost `0.85`, style `0.55`, speed `0.95`. Looser
than Spassky so he reads drunk and careless rather than composed, but not so
loose that the accent wanders. Cast Russian per `docs/HUMAN_SCRIPT.md`'s accent
table, and auditioned against Maksim so the two Russian voices stay clearly
distinct — Nick is younger and warmer, Spassky deeper and slower.

Spassky's *live* in-game dialogue is generated turn-by-turn by the sidecar at
runtime (`Sidecar/tts.py`), not from a file here — the non-offline Spassky
clips in this folder are only the pre-rendered cutscene lines (P1's answer
and the four ending lines). No deployment step is needed to match: the voice,
model and settings are committed in `Sidecar/config.py` and `Sidecar/tts.py`,
and `GAME_COMPLETION_PLAN.md` §7 B0 is closed.

Pre-rendering P1's answer matters for more than consistency. It is the longest
line Spassky has, and `eleven_multilingual_v2` costs 950–2100 ms scaling with
length — rendering it offline keeps that cost off the live turn budget.

The 14 `spassky_offline_p2_*`/`spassky_offline_p3_*` clips are the **Offline
demo** mode's fixed script (see the README's "Offline demo" section) — same
Maksim voice and settings, same synthetic-VO disclosure, but a canned line per
turn instead of a live reply, since Offline demo has no backend to generate
one from.
