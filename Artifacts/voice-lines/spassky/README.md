# Officer Spassky production voice lines

This directory contains Spassky's finalized dialogue, one 24 kHz mono 16-bit PCM
WAV per spoken line. Filenames match the stable IDs in `docs/HUMAN_SCRIPT.md`.

All 62 lines are synthetic text-to-speech generated via the ElevenLabs API. No
human voice actor recorded any line — the same synthetic-VO disclosure that
`Assets/_Project/Art/Audio/VO/README.md` carries applies here.

## Selected voice model

- Character: Officer Spassky
- Casting reference: Russian, with a Russian accent — semi-deep, snarly, raspy,
  and angry in a contained way rather than shouting
- ElevenLabs voice: Maksim — "Raw, unpolished, deep"
- ElevenLabs voice ID: `6sXsAlJKKBf265ucBSRt`
- Model: `eleven_multilingual_v2`
- Output: `pcm_24000`

This is the same voice and model the sidecar uses for Spassky's live turns
(`Sidecar/config.py`, `Sidecar/tts.py`), so pre-rendered and live dialogue are
the same man. The casting rationale, the rejected alternatives, and the sharing
terms checked before committing to a Voice Library voice are in
`Artifacts/voice_guide/Spassky.md`.

The API key is read from `ELEVENLABS_API_KEY` at runtime and is never stored in
this directory.

## Delivery registers

Per-line delivery follows the register table in `Artifacts/voice_guide/Spassky.md`
§4.3 rather than one uniform setting, so a short press does not read the same as
a verdict. `similarity_boost` is held at `1.00` everywhere — it is the accent
carrier, and only delivery is allowed to vary, never voice identity.

| Register | stability | similarity_boost | style | speed | gain_db | Lines |
|---|---|---|---|---|---|---|
| `FLAT` | 0.28 | 1.00 | 0.62 | 0.92 | 0.0 | 21 |
| `PRESS` | 0.20 | 1.00 | 0.78 | 0.90 | +1.0 | 12 |
| `RAISED` | 0.15 | 1.00 | 0.92 | 0.95 | +2.5 | 1 |
| `LOW` | 0.15 | 1.00 | 0.85 | 0.85 | −1.5 | 28 |

The register for each line is derived, not hand-assigned — first match wins:

1. `RAISED` if the line contains `!`, or a token of two or more letters that is
   entirely uppercase.
2. `LOW` if the phase is `P3_VERDICT` or `P4_ENDING`, or the line contains no `?`
   and runs to 18 or more words.
3. `PRESS` if the line ends in `?` and runs to 5 or fewer words.
4. `FLAT` otherwise.

Phase comes from the scene the line sits in: scene 1 is `P1_TUTORIAL`, scene 3
`P2_RECALL`, scene 5 `P3_VERDICT`, scene 6 `P4_ENDING`.

The `gain_db` trim is applied after synthesis and is peak-limited at `0.97`, so a
loud line loses some of its nominal boost rather than clipping.

## Post-processing owed at mix time

`SPASSKY-001`, `-002` and `-003` are the black-screen wake calls, which the
script directs as *"muffled and distant, each call clearer than the last."* That
is a mix effect, not a synthesis setting, and is **not** baked into these files —
they are rendered clean. Apply the progressive low-pass and distance in Unity.

`SPASSKY-042`, `-045`, `-048` and `-052` are the same sentence on four mutually
exclusive accusation routes. They are rendered as four separate takes rather than
one shared file: at `stability` `0.15` each take differs audibly, so a player
replaying into a different route does not hear a bit-identical line.

## Line manifest

| ID | Phase | Register | Length | Dialogue |
|---|---|---|---|---|
| `SPASSKY-001` | P1_TUTORIAL | `FLAT` | 0.93 s | David. |
| `SPASSKY-002` | P1_TUTORIAL | `FLAT` | 0.93 s | David. |
| `SPASSKY-003` | P1_TUTORIAL | `RAISED` | 0.88 s | David! |
| `SPASSKY-004` | P1_TUTORIAL | `PRESS` | 1.02 s | You with me? |
| `SPASSKY-005` | P1_TUTORIAL | `LOW` | 12.68 s | I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night. |
| `SPASSKY-006` | P2_RECALL | `FLAT` | 2.32 s | So. What's the last thing you remember? |
| `SPASSKY-007` | P2_RECALL | `FLAT` | 1.67 s | Who else was drinking with you? |
| `SPASSKY-008` | P2_RECALL | `PRESS` | 1.53 s | How much did you have? |
| `SPASSKY-009` | P2_RECALL | `FLAT` | 4.78 s | Tell me about the argument with Nick. What was it really about? |
| `SPASSKY-010` | P2_RECALL | `FLAT` | 2.88 s | You kept that from your best friend for two years? |
| `SPASSKY-011` | P2_RECALL | `FLAT` | 2.37 s | What did Nick do after the argument? |
| `SPASSKY-012` | P2_RECALL | `PRESS` | 0.98 s | Thought? |
| `SPASSKY-013` | P2_RECALL | `PRESS` | 1.39 s | What did you do next? |
| `SPASSKY-014` | P2_RECALL | `PRESS` | 1.16 s | Did he answer? |
| `SPASSKY-015` | P2_RECALL | `FLAT` | 2.18 s | Was the door locked when you opened it? |
| `SPASSKY-016` | P2_RECALL | `FLAT` | 2.00 s | What did you do with it afterward? |
| `SPASSKY-017` | P2_RECALL | `PRESS` | 1.16 s | Then what? |
| `SPASSKY-018` | P2_RECALL | `PRESS` | 1.58 s | How long were you out? |
| `SPASSKY-019` | P2_RECALL | `PRESS` | 1.53 s | What time did Nick leave? |
| `SPASSKY-020` | P2_RECALL | `FLAT` | 3.72 s | Walk me through the morning. Start with Priya's scream. |
| `SPASSKY-021` | P2_RECALL | `PRESS` | 1.81 s | What happened at the door? |
| `SPASSKY-022` | P2_RECALL | `PRESS` | 0.93 s | Then? |
| `SPASSKY-023` | P2_RECALL | `FLAT` | 1.53 s | You moved the body. |
| `SPASSKY-024` | P2_RECALL | `PRESS` | 1.49 s | What happened to Nick? |
| `SPASSKY-025` | P2_RECALL | `FLAT` | 2.00 s | You said you looked up. Who was it? |
| `SPASSKY-026` | P2_RECALL | `FLAT` | 4.37 s | You couldn't see the doorway clearly. Don't give me a face you never saw. |
| `SPASSKY-027` | P2_RECALL | `FLAT` | 3.25 s | You're very precise about one o'clock. How do you know? |
| `SPASSKY-028` | P2_RECALL | `FLAT` | 2.69 s | If you didn't look at the clock, don't give me a time. |
| `SPASSKY-029` | P2_RECALL | `FLAT` | 2.79 s | Did you hear him out there, or did you assume? |
| `SPASSKY-030` | P2_RECALL | `FLAT` | 5.43 s | You told me the storm swallowed your voice. Be precise about what you heard back. |
| `SPASSKY-031` | P2_RECALL | `FLAT` | 3.02 s | How do you know he was even outside yet? |
| `SPASSKY-032` | P2_RECALL | `FLAT` | 5.11 s | You never saw Nick outside that night. Tell me what you know, not what fits afterward. |
| `SPASSKY-033` | P2_RECALL | `FLAT` | 2.97 s | So the door was locked before you fell asleep, or after? |
| `SPASSKY-034` | P2_RECALL | `LOW` | 7.80 s | You were unconscious when someone turned that key. You can draw a conclusion, but you cannot say you saw it happen. |
| `SPASSKY-035` | P2_RECALL | `PRESS` | 1.49 s | Did you hear glass? |
| `SPASSKY-036` | P2_RECALL | `FLAT` | 4.60 s | You said you were unconscious. How would you know what the window sounded like? |
| `SPASSKY-037` | P3_VERDICT | `LOW` | 2.46 s | Tell me why I should spare your life. |
| `SPASSKY-057` | P3_VERDICT | `LOW` | 2.28 s | That's what I don't understand. |
| `SPASSKY-058` | P3_VERDICT | `LOW` | 5.94 s | Old friends. An anniversary. Drinks. From the way they tell it, things were going well. |
| `SPASSKY-059` | P3_VERDICT | `LOW` | 1.81 s | So when did it all go wrong? |
| `SPASSKY-060` | P3_VERDICT | `LOW` | 7.43 s | And somewhere between that photograph and sunrise, Nick ended up dead. |
| `SPASSKY-061` | P3_VERDICT | `LOW` | 2.93 s | If it wasn't you, David — who killed Nick? |
| `SPASSKY-062` | P3_VERDICT | `LOW` | 1.30 s | Who, David? |
| `SPASSKY-038` | P3_VERDICT | `LOW` | 13.00 s | You were wearing Nick's coat. Your hands were on the body, the door, and the key. You knew what Nick had done, and you kept it quiet. Why should I trust the part that clears you? |
| `SPASSKY-039` | P3_VERDICT | `LOW` | 2.69 s | If it's not you, then tell me who did it. |
| `SPASSKY-040` | P3_VERDICT | `LOW` | 2.93 s | So you think it's Aaron, huh? Tell me why. |
| `SPASSKY-041` | P3_VERDICT | `LOW` | 2.55 s | Did you see Aaron lock the door? |
| `SPASSKY-042` | P3_VERDICT | `LOW` | 3.02 s | That's enough from you. I've heard enough. |
| `SPASSKY-043` | P3_VERDICT | `LOW` | 2.65 s | So you think it's Ivy, huh? Tell me why. |
| `SPASSKY-044` | P3_VERDICT | `LOW` | 5.90 s | Knowing something happened is not the same as causing it. What puts her at the door? |
| `SPASSKY-045` | P3_VERDICT | `LOW` | 3.11 s | That's enough from you. I've heard enough. |
| `SPASSKY-046` | P3_VERDICT | `LOW` | 3.07 s | So you think it's Priya, huh? Tell me why. |
| `SPASSKY-047` | P3_VERDICT | `LOW` | 6.18 s | She was asleep in the armchair, and she's the one who called us. Is that all you have? |
| `SPASSKY-048` | P3_VERDICT | `LOW` | 2.69 s | That's enough from you. I've heard enough. |
| `SPASSKY-049` | P3_VERDICT | `LOW` | 2.51 s | That's not an answer. Try again. |
| `SPASSKY-050` | P3_VERDICT | `LOW` | 2.60 s | You understand how that sounds, don't you? |
| `SPASSKY-051` | P3_VERDICT | `LOW` | 3.16 s | Last chance. Who do you think did this? |
| `SPASSKY-052` | P3_VERDICT | `LOW` | 2.74 s | That's enough from you. I've heard enough. |
| `SPASSKY-053` | P4_ENDING | `LOW` | 3.67 s | You were the only one who couldn't tell me where you were. |
| `SPASSKY-054` | P4_ENDING | `LOW` | 5.80 s | He locked it. You unlocked it. Only one of those was a decision. |
| `SPASSKY-055` | P4_ENDING | `LOW` | 3.81 s | She agreed with you. That's not the same as it being true. |
| `SPASSKY-056` | P4_ENDING | `LOW` | 2.69 s | She's the one who called us. Sit with that. |

Total runtime 198 s across 62 lines. Manifest order is story order — the
photograph scene (`SPASSKY-057`–`062`) plays between `SPASSKY-037` and the
truthful-defense route, and carries high IDs only because it was written after
the verdict lines were numbered.

## Utilities

Unlike Priya's set, the spoken text is not duplicated into the generator. It is
parsed out of `docs/HUMAN_SCRIPT.md` at render time, so a script edit cannot
silently leave stale audio behind — rerun with `--force` after changing a line.
The generator also refuses to run if two lines share an ID, since IDs are
filenames.

Generate only missing lines:

```bash
Sidecar/.venv/bin/python Artifacts/voice-lines/spassky/generate_spassky_voice_lines.py
```

Preview which register every line resolves to, without calling the API:

```bash
Sidecar/.venv/bin/python Artifacts/voice-lines/spassky/generate_spassky_voice_lines.py --dry-run
```

Re-render one line after a script edit:

```bash
Sidecar/.venv/bin/python Artifacts/voice-lines/spassky/generate_spassky_voice_lines.py --force --only SPASSKY-042
```

Review a line without opening a media-player window:

```bash
Sidecar/.venv/bin/python Artifacts/voice-lines/spassky/play_spassky_voice_line.py SPASSKY-037
```

On Windows, substitute `Sidecar\.venv\Scripts\python.exe` for the interpreter
path. The player dispatches to `winsound`, `afplay` or ALSA/PulseAudio depending
on the host.
