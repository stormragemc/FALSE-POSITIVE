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
| Radio | Daniel | `onwK4e9ZLuTAKqWW03F9` |
| Priya | Jessica | `cgSgspJ2msm6clMCkdW9` |
| Ivy | Lily | `pFZP5JQG7iQjIQuC4Bku` |
| Aaron | Eric | `cjVigY5qzO86Huf0OWal` |
| "David" (wake calls) | River | `SAz9YHcvj6GT2YYXdXww` |

**Render Spassky's clips with these settings**, or the pre-rendered lines will
not match his live voice — model `eleven_multilingual_v2`, stability `0.15`,
similarity_boost `1.00`, style `0.85`, speed `0.85`, then a `−1.5 dB` trim.
Rationale in `docs/superpowers/specs/2026-08-07-spassky-voice-and-delivery-design.md`.

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
