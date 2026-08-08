# Priya voice auditions

All candidates use the same numbering for every line. Each file is 24 kHz, mono, 16-bit PCM WAV.

## Final voice and delivery

**Aaira, Natural V3 candidate 3, variation take 7**

- ElevenLabs voice ID: `1XNFRxE3WBB7iI0jnm7p`
- Finalized after the Natural V3 voice audition and seven Aaira delivery tests
- Delivery: worried opening, quick consecutive name calls, no breathless tag
- Production set: `../../voice-lines/priya/PRIYA-001.wav` through `PRIYA-008.wav`
- Earlier reference: original candidate 2, Monika Sogam (`2zRM7PkgwBPiau2jvVXc`)

## Aaira final-validation lines

Run `play_priya_aaira_v3_final_validation_lines.py` to test Aaira across four
additional lines from the current human script. Pass a number from 1 to 4 to
play only that line.

1. Locked-door suspicion
2. Concern for Nick
3. Urgent police call
4. Ending confusion

These use `eleven_v3` with Natural stability and restrained line-specific
directions. Aaira was finalized after this validation round and the revised
police-call audition.

## Aaira context-directed validation V2

Run `play_priya_aaira_v3_directed_validation_v2.py` to hear the same four
script lines with delivery shaped around each line's internal thought changes:

1. Quiet realization followed by suspicion
2. Tentative call, listening pause, then concern
3. Controlled factual report followed by an urgent plea
4. Disorientation followed by hurt frustration

The dialogue words are unchanged; only tags, casing, and punctuation guide the
intonation and pauses.

Lines 1, 2, and 4 were approved from this round. Line 3 needed more panic and
urgency.

## Police-call urgency variations

Run `play_priya_aaira_police_call_urgency_variations.py` to compare two revised
deliveries of the police call:

1. Panicked but clear
2. Alarmed and faster

Neither version uses a breathless direction.

## Rejected original candidates

- Original candidate 3, Mahi: reject because the rendered voice sounded male
- Original candidate 4, Aisha: reject because the rendered audio was abnormally muted
- Do not include either candidate in future Priya audition rounds

## Refined line 1 audition

Run `play_priya_line_01_panic_refined.py` to hear the six retained female
voices. Monika remains candidate 2.

The refined render uses more natural punctuation and reduces style exaggeration:

- Model: `eleven_multilingual_v2`
- Stability: `0.40`
- Similarity boost: `0.80`
- Style: `0.25`
- Speaker boost: enabled
- Speed: `1.00`

## Natural V3 line 1 audition

Run `play_priya_line_01_panic_v3_natural.py` for the less robotic audition
round. It retains the same six candidates and numbering as the refined round.

The spoken words remain unchanged. Delivery uses restrained `[worried]` and
`[urgent]` directions with natural punctuation.

- Model: `eleven_v3`
- Stability: `0.50` (`Natural`)
- Similarity boost: `0.75`
- Style: `0.00`
- Speaker boost: enabled
- Speed: `1.00`

## Aaira name-calling variations

Run `play_priya_aaira_name_call_variations.py` to compare seven deliveries of
Priya's first line using Aaira only. All four use `eleven_v3` with Natural
stability and preserve the spoken words.

1. Balanced calls
2. Breathless calls
3. Across-cabin calls
4. Punctuation only
5. Breathless/punctuation hybrid, combining takes 2 and 4
6. Hybrid with quicker name calls and no ellipses between names
7. Worried quick calls with the breathless direction removed

## Candidates

| Number | Voice | ElevenLabs voice ID | Intended quality |
|---|---|---|---|
| 1 | Anika | `90ipbRoKi4CpHXvKVtl0` | Clear, friendly Indian female voice |
| 2 | Monika Sogam | `2zRM7PkgwBPiau2jvVXc` | Deeper, grounded Indian female voice |
| 3 | Mahi | `yD0Zg2jxgfQLY8I2MEHO` | Warm, conversational Indian female voice |
| 4 | Aisha | `MjJrIRgwH0lZCuxcakAW` | Medium-pitched, warm, confident Indian female voice |
| 5 | Aaira | `1XNFRxE3WBB7iI0jnm7p` | Calm, conversational Indian female voice |
| 6 | Aaliyah | `aUTn6mevnrM9pqtesisb` | Polished, warm Indian female voice |
| 7 | Aasha | `rxvktZTNrsQlsGIpOQGz` | Clear, empathetic Indian female voice |
| 8 | Saavi | `a4BpQNxKFbuzzTj2JRQc` | Soft, composed Indian female voice |

## Audition lines

### Line 1: panic

> Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here!

Files: `line-01-panic/01-anika.wav` through `line-01-panic/08-saavi.wav`.

### Line 2: suspicion

> All night?

Files: `line-02-suspicion/01-anika.wav` through `line-02-suspicion/08-saavi.wav`.

### Line 3: concern

> Nick? Nick, can you hear me?

Files: `line-03-concern/01-anika.wav` through `line-03-concern/08-saavi.wav`.

## Generation settings

- Model: `eleven_multilingual_v2`
- Output: `pcm_24000`
- Stability: `0.40`
- Similarity boost: `0.75`
- Style: `0.55`
- Speaker boost: enabled
- Speed: `1.00`

The ElevenLabs API key was loaded from the Windows user environment for each generation request. It is not stored in this directory.

## Playback workflow

Ask to play a Priya line by number or description, such as "play Priya line 2." The eight candidates should be played in numerical order using direct Windows audio playback, without opening a media-player window. Choose a candidate by replying with its number from 1 to 8.
