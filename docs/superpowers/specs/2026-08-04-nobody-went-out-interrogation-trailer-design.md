# Nobody Went Out Interrogation Trailer Design

## Objective

Replace the current seven-second narrated teaser with a ten-second psychological-horror cut that presents the player as the person being interrogated. The result must feel like a scary game trailer, use a threatening male ElevenLabs voice, and preserve the strongest cabin footage from the existing teaser.

## Approved Direction

The trailer uses the **Interrogation** concept. A Russian-accented male detective speaks directly to the player while the edit moves from the cabin murder context into an interrogation room. The threat comes from being judged through speech rather than from graphic violence.

## Timeline

| Time | Picture | Dialogue and sound |
| --- | --- | --- |
| 0.0-2.0 s | Existing group shot outside the cabin with a restrained digital push-in. | Detective: “You remember the murder.” Low room tone begins. |
| 2.0-4.0 s | Existing close-up followed by a brief evidence-photograph insert. | Detective: “Your voice remembers more.” A muted camera-shutter impact marks the insert. |
| 4.0-7.0 s | Fast sequence of three new inserts: interrogation room, red voice waveform, locked cabin door. | Detective: “Every pause. Every tremor. Every lie.” A slow heartbeat and restrained radio texture build underneath. |
| 7.0-9.0 s | Detective silhouette across a metal table with a red recording light. | Detective: “I will hear it.” The bed drops toward silence on “hear.” |
| 9.0-10.0 s | Hard cut to black title card: **FALSE POSITIVE**. | Single bass impact, short door-slam tail, and radio-static cutoff. |

## Narration

The approved script is:

> You remember the murder. Your voice remembers more. Every pause. Every tremor. Every lie. I will hear it.

The delivery must be male, deep, threatening, deliberate, and Russian-accented. Generate it with ElevenLabs v3. Use expressive direction and a low-stability performance, then apply only subtle pitch lowering in post-processing. The words must remain intelligible on laptop speakers.

## New Visual Inserts

Create four 16:9 still images that match the teaser’s dark, cold, cinematic realism:

1. A crime-scene evidence photograph showing the cabin’s locked wooden door, with no readable case text.
2. A dark police interrogation room viewed from the player’s seated position.
3. A red voice-analysis waveform on a dim monitor, with no legible interface copy.
4. A broad-shouldered male detective in silhouette across a metal table, with a small red recording light.

The new inserts may use slow push-ins, shallow parallax, flash frames, and crossfades. They must not introduce visible gore, unrelated characters, logos, watermarks, or a different visual era.

## Sound Mix

- Retain the current teaser ambience where it supports the cabin shots.
- Add a low heartbeat, subdued police-radio texture, camera-shutter hit, door slam, and final bass impact.
- Keep narration dominant and centered.
- Target approximately -14 to -16 LUFS integrated with true peak at or below -1 dBTP.
- Use abrupt silence immediately before the title impact to increase contrast.

## Deliverables

- One ten-second 1280x720 H.264/AAC trailer in the existing `NobodyWentOut/Video` folder.
- One mastered narration file in the existing `NobodyWentOut/Audio` folder.
- Four generated 16:9 insert images stored under `NobodyWentOut/TrailerInserts`.
- Preserve every existing trailer and narration file; use new filenames.

## Validation

- Confirm the final duration is between 9.9 and 10.1 seconds.
- Confirm the voice is recognizably male and the full script is audible without clipping.
- Confirm all new frames match the cabin footage’s lighting and horror tone.
- Confirm the title remains readable for at least 0.8 seconds.
- Decode the completed MP4 with FFmpeg and verify audio true peak does not exceed -1 dBTP.
