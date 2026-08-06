# Voice-over — synthetic

Every audio file in this folder is synthetic text-to-speech generated via the
ElevenLabs API (`/elevenlabs-dialog`). No human voice actor recorded any line
in this game. Guardrail #11 (`docs/GAME_COMPLETION_PLAN.md` §8): all character
VO must be labelled synthetic in the README and the deck — this file is that
label for this folder.

Voice casting (ElevenLabs premade voices, not custom clones):

| Character | Voice |
|---|---|
| Officer Spassky | Brian |
| Radio | Daniel |
| Priya | Jessica |
| Ivy | Lily |
| Aaron | Eric |
| "David" (wake calls) | River |

Spassky's *live* in-game dialogue is generated turn-by-turn by the sidecar at
runtime (`Sidecar/tts.py`), not from a file here — the non-offline Spassky
clips in this folder are only the pre-rendered cutscene lines (P1's answer
and the four ending lines). `Sidecar/config.py`'s `ELEVENLABS_VOICE_ID` still
needs to be pointed at a male voice on the hosted account to match; that is a
deployment change outside this repo (see `docs/GAME_COMPLETION_PLAN.md` §7, B0).

The 14 `spassky_offline_p2_*`/`spassky_offline_p3_*` clips are the **Offline
demo** mode's fixed script (see the README's "Offline demo" section) — same
Brian voice, same synthetic-VO disclosure, but a canned line per turn instead
of a live reply, since Offline demo has no backend to generate one from.
