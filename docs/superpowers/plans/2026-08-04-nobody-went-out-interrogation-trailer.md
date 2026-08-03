# Nobody Went Out Interrogation Trailer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a ten-second psychological-horror trailer that combines the existing cabin footage with four new inserts and a threatening male ElevenLabs narration.

**Architecture:** Treat the existing MP4 as immutable source footage. Generate four project-local 16:9 insert images and one deterministic title card, synthesize and master the narration independently, then assemble picture and sound with FFmpeg into a new sibling MP4. Validate picture, duration, codec, loudness, and title readability without modifying any existing media.

**Tech Stack:** OpenAI built-in image generation, ElevenLabs v3, FFmpeg/FFprobe, gstack browse offline HTML rendering

## Global Constraints

- Final duration must be between 9.9 and 10.1 seconds.
- Final frame size must be 1280x720 with H.264 video and AAC audio.
- Narration must be male, deep, threatening, deliberate, Russian-accented, and fully intelligible.
- Preserve every existing trailer and narration file; create new filenames only.
- Use no visible gore, unrelated characters, logos, watermarks, or readable fake case text.
- Keep narration centered and dominant; target -14 to -16 LUFS integrated and no more than -1 dBTP true peak.
- Keep the title readable for at least 0.8 seconds.

---

## File Map

- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/evidence-door.png`: cabin-door evidence insert.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/interrogation-room.png`: seated player-view interrogation room insert.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/voice-waveform.png`: red voice-analysis insert.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/detective-silhouette.png`: threatening male detective insert.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.html`: exact, editable title-card source.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.png`: rendered title card.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice-raw.mp3`: unprocessed ElevenLabs take.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav`: fitted and mastered narration.
- Create `Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4`: final ten-second trailer.
- Read only `Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4`: existing source footage and ambience.

### Task 1: Generate and validate the four horror inserts

**Files:**
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/evidence-door.png`
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/interrogation-room.png`
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/voice-waveform.png`
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/detective-silhouette.png`
- Reference: `Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4`

**Interfaces:**
- Consumes: existing teaser frames as mood, lighting, palette, and lens references.
- Produces: four opaque 16:9 PNG files that Task 4 can scale and animate at 1280x720.

- [ ] **Step 1: Extract a source reference sheet**

Run:

```bash
ffmpeg -hide_banner -loglevel error -y \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4 \
  -vf "fps=1,scale=480:-1,tile=4x2:padding=2:margin=2" \
  -frames:v 1 /tmp/nobody-went-out-trailer-reference.jpg
```

Expected: `/tmp/nobody-went-out-trailer-reference.jpg` shows the cabin group, close-up, and locked-door lighting.

- [ ] **Step 2: Generate the evidence-door insert**

Use the built-in image generation tool with the reference sheet visible and this prompt:

```text
Use case: stylized-concept
Asset type: 16:9 psychological-horror trailer insert
Primary request: a police evidence photograph of the same heavy locked wooden cabin door seen in the reference footage, photographed at night moments after a murder
Input images: the extracted teaser contact sheet is a mood, lighting, palette, cabin-door, and cinematic-grain reference
Composition/framing: tight oblique close-up of the iron bolt and splintered dark wood, photographic evidence framing, no paper border
Lighting/mood: cold blue-black moonlight with a faint dying amber fire reflection, oppressive and realistic
Constraints: no visible body, no gore, no readable labels, no text, no logos, no watermark; preserve the source trailer's dark cinematic realism
```

Save the selected output as `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/evidence-door.png`.

- [ ] **Step 3: Generate the interrogation-room insert**

Use the built-in image generation tool with this prompt:

```text
Use case: stylized-concept
Asset type: 16:9 psychological-horror trailer insert
Primary request: a dark police interrogation room viewed in first person from the suspect's seated position, an empty metal chair across a scratched steel table
Composition/framing: symmetrical wide shot at seated eye level, table edge in foreground, one-way glass behind the empty chair
Lighting/mood: one hard overhead fluorescent light, deep black corners, cold desaturated blue-gray palette, realistic cinematic horror
Constraints: no person, no readable text, no logos, no watermark, no futuristic technology, no visible gore
```

Save the selected output as `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/interrogation-room.png`.

- [ ] **Step 4: Generate the voice-waveform insert**

Use the built-in image generation tool with this prompt:

```text
Use case: stylized-concept
Asset type: 16:9 psychological-horror trailer insert
Primary request: a dim police interview-room monitor showing an ominous red voice waveform reacting to a frightened suspect
Composition/framing: extreme close-up of an old dark monitor, red waveform centered, subtle scan lines and glass reflections
Lighting/mood: almost black frame with restrained crimson light, analog procedural technology, realistic and threatening
Constraints: waveform only, no readable interface labels, no numbers, no logos, no watermark, no neon cyberpunk styling
```

Save the selected output as `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/voice-waveform.png`.

- [ ] **Step 5: Generate the detective-silhouette insert**

Use the built-in image generation tool with this prompt:

```text
Use case: stylized-concept
Asset type: 16:9 psychological-horror trailer climax insert
Primary request: a broad-shouldered male Russian detective sitting motionless across a metal interrogation table, seen from the suspect's first-person viewpoint
Composition/framing: symmetrical medium-wide silhouette, face mostly hidden, hands resting on the table, small red recording light between detective and camera
Lighting/mood: harsh overhead fluorescent rim light, cold blue-black shadows, grounded police realism, menacing psychological horror
Constraints: clearly male silhouette, no uniform insignia, no weapon, no readable text, no logos, no watermark, no gore, no supernatural anatomy
```

Save the selected output as `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/detective-silhouette.png`.

- [ ] **Step 6: Normalize every insert to the delivery frame**

Run once per PNG, replacing the source file through a temporary sibling:

```bash
for trailer_insert in evidence-door interrogation-room voice-waveform detective-silhouette; do
  ffmpeg -hide_banner -loglevel error -y \
    -i "Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/${trailer_insert}.png" \
    -vf "scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,format=yuv420p" \
    "/tmp/${trailer_insert}-1280x720.png"
  cp "/tmp/${trailer_insert}-1280x720.png" \
    "Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/${trailer_insert}.png"
done
```

Expected: every output is exactly 1280x720 and remains visually consistent after cropping.

- [ ] **Step 7: Validate and visually inspect all inserts**

Run:

```bash
for trailer_insert in Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/*.png; do
  ffprobe -v error -select_streams v:0 \
    -show_entries stream=width,height,pix_fmt \
    -of default=noprint_wrappers=1 "$trailer_insert"
done
```

Expected for each generated insert: `width=1280`, `height=720`, and a decodable pixel format. Open all four with the image viewer and reject any output containing readable fake text, visible gore, a female detective, or a palette that does not match the source footage.

- [ ] **Step 8: Commit the approved insert assets**

```bash
git add \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/evidence-door.png \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/interrogation-room.png \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/voice-waveform.png \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/detective-silhouette.png
git commit -m "feat(trailer): add interrogation horror inserts"
```

### Task 2: Render the exact title card

**Files:**
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.html`
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.png`

**Interfaces:**
- Consumes: exact title text `FALSE POSITIVE` and the project's cold black/crimson palette.
- Produces: a deterministic 1280x720 PNG displayed by Task 4 from 9.0 to 10.0 seconds.

- [ ] **Step 1: Create the title-card HTML**

Create `title-card.html` with this exact self-contained content:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=1280, initial-scale=1">
  <style>
    * { box-sizing: border-box; }
    html, body {
      width: 1280px;
      height: 720px;
      margin: 0;
      overflow: hidden;
      background: #020304;
    }
    body {
      display: grid;
      place-items: center;
      font-family: "Arial Narrow", "Helvetica Neue", Arial, sans-serif;
    }
    .frame {
      position: relative;
      width: 1280px;
      height: 720px;
      display: grid;
      place-items: center;
      background:
        radial-gradient(circle at 50% 48%, rgba(88, 0, 6, 0.15), transparent 36%),
        linear-gradient(#020304, #000);
    }
    .frame::before {
      content: "";
      position: absolute;
      inset: 0;
      background: repeating-linear-gradient(
        to bottom,
        rgba(255,255,255,0.015) 0,
        rgba(255,255,255,0.015) 1px,
        transparent 1px,
        transparent 4px
      );
    }
    h1 {
      position: relative;
      margin: 0;
      color: #f0ece6;
      font-size: 82px;
      font-weight: 800;
      line-height: 1;
      letter-spacing: 0.24em;
      text-indent: 0.24em;
      text-shadow:
        0 0 2px rgba(255,255,255,0.7),
        0 0 18px rgba(145,0,10,0.42),
        3px 0 0 rgba(95,0,7,0.45);
    }
  </style>
</head>
<body>
  <main class="frame">
    <h1>FALSE POSITIVE</h1>
  </main>
</body>
</html>
```

- [ ] **Step 2: Render with gstack browse offline mode**

Run the browse skill setup check, then:

```bash
$B viewport 1280x720 --scale 1
$B goto file://./Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.html
$B screenshot \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.png \
  --viewport
```

Expected: exact 1280x720 PNG with `FALSE POSITIVE` centered and no browser chrome.

- [ ] **Step 3: Inspect and commit the title card**

Open `title-card.png` with the image viewer. Confirm exact spelling, high contrast, and safe margins, then run:

```bash
git add \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.html \
  Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.png
git commit -m "feat(trailer): add false positive title card"
```

### Task 3: Generate and master the male ElevenLabs narration

**Files:**
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice-raw.mp3`
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav`
- Read: `Sidecar/tts.py`

**Interfaces:**
- Consumes: the approved script and the existing private ElevenLabs key loaded by `Sidecar/config.py`.
- Produces: mono PCM narration no longer than 8.85 seconds for Task 4.

- [ ] **Step 1: Generate one expressive male take**

Install no project dependencies. Reuse the temporary ElevenLabs SDK target already created at `/tmp/false-positive-elevenlabs`. Call ElevenLabs v3 through `Sidecar/tts.py` with the public Clyde male voice ID `2EiwWnXFnvU5JabPnv8n`, seed `48621`, speed `0.95`, stability `0.22`, similarity boost `0.78`, style `0.92`, and this exact text:

```text
[low, threatening, strong Russian accent] You remember the murder. Your voice remembers more. Every pause. Every tremor. Every lie. [quietly] I will hear it.
```

Write the returned MP3 bytes to `Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice-raw.mp3`. If ElevenLabs returns a 402 for Clyde, retry once with the public Brian male voice ID `nPczCjzI2devNBz1zQrb` using the same settings. Do not use Matilda or another female voice.

- [ ] **Step 2: Tighten pauses, lower pitch subtly, and fit the take**

First trim long silence while retaining 70 ms phrase gaps:

```bash
ffmpeg -hide_banner -loglevel error -y \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice-raw.mp3 \
  -af "silenceremove=start_periods=1:start_threshold=-42dB:start_silence=0.03:stop_periods=-1:stop_duration=0.16:stop_threshold=-42dB:stop_silence=0.07" \
  /tmp/nobody-went-out-interrogation-voice-tight.wav
```

Measure the tight take, compute the tempo needed to fit it into 8.80 seconds, and master it:

```bash
trailer_voice_seconds=$(ffprobe -v error -show_entries format=duration \
  -of default=noprint_wrappers=1:nokey=1 \
  /tmp/nobody-went-out-interrogation-voice-tight.wav)
trailer_voice_tempo=$(awk -v duration="$trailer_voice_seconds" \
  'BEGIN { value=duration/8.80; if (value < 1.0) value=1.0; printf "%.6f", value }')
ffmpeg -hide_banner -loglevel error -y \
  -i /tmp/nobody-went-out-interrogation-voice-tight.wav \
  -af "atempo=${trailer_voice_tempo},asetrate=44100*0.94,aresample=44100,atempo=1.063830,highpass=f=65,lowpass=f=10500,acompressor=threshold=-18dB:ratio=2.5:attack=8:release=90,loudnorm=I=-15:TP=-1.5:LRA=7,apad=whole_dur=8.85,atrim=duration=8.85" \
  -ac 1 -ar 44100 \
  Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav
```

Expected: a deepened but intelligible male voice ending by 8.85 seconds.

- [ ] **Step 3: Validate narration duration and peaks**

Run:

```bash
ffprobe -v error -show_entries format=duration \
  -of default=noprint_wrappers=1 \
  Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav
ffmpeg -hide_banner \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav \
  -af volumedetect -f null - 2>&1 | rg "mean_volume|max_volume"
```

Expected: `duration=8.850000` within codec rounding and `max_volume` no higher than `-1.0 dB`.

- [ ] **Step 4: Commit the narration assets**

```bash
git add \
  Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice-raw.mp3 \
  Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav
git commit -m "feat(trailer): add male interrogation narration"
```

### Task 4: Assemble and validate the ten-second trailer

**Files:**
- Create: `Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4`
- Read: `Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4`
- Read: all Task 1-3 outputs.

**Interfaces:**
- Consumes: four 1280x720 inserts, one 1280x720 title card, one 8.85-second narration, and the original teaser video/audio.
- Produces: final ten-second H.264/AAC trailer.

- [ ] **Step 1: Build the picture track with the approved timeline**

Construct `/tmp/nobody-went-out-interrogation-picture.mp4` at 24 fps with hard cuts and subtle push-ins. The frame counts below total exactly 240 frames:

- 0.0-1.8 s: source group footage.
- 1.8-3.3 s: source close-up footage.
- 3.3-4.0 s: `evidence-door.png`.
- 4.0-5.0 s: `interrogation-room.png`.
- 5.0-5.8 s: `voice-waveform.png`.
- 5.8-7.0 s: `evidence-door.png` with a tighter crop.
- 7.0-9.0 s: `detective-silhouette.png`.
- 9.0-10.0 s: `title-card.png`.

Run:

```bash
ffmpeg -hide_banner -loglevel error -y \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4 \
  -loop 1 -framerate 24 -t 2 -i Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/evidence-door.png \
  -loop 1 -framerate 24 -t 1 -i Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/interrogation-room.png \
  -loop 1 -framerate 24 -t 1 -i Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/voice-waveform.png \
  -loop 1 -framerate 24 -t 2 -i Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/detective-silhouette.png \
  -loop 1 -framerate 24 -t 1 -i Assets/_Project/Art/Characters/NobodyWentOut/TrailerInserts/title-card.png \
  -filter_complex "
    [0:v]fps=24,scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,split=2[src_a][src_b];
    [src_a]trim=start_frame=0:end_frame=43,setpts=N/(24*TB)[group];
    [src_b]trim=start_frame=43:end_frame=79,setpts=N/(24*TB)[close];
    [1:v]scale=1280:720,split=2[door_a][door_b];
    [door_a]trim=end_frame=17,setpts=N/(24*TB),zoompan=z='min(pzoom+0.0008,1.03)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1280x720:fps=24[evidence];
    [2:v]scale=1280:720,trim=end_frame=24,setpts=N/(24*TB),zoompan=z='min(pzoom+0.0005,1.02)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1280x720:fps=24[room];
    [3:v]scale=1280:720,trim=end_frame=19,setpts=N/(24*TB)[wave];
    [door_b]trim=end_frame=29,setpts=N/(24*TB),zoompan=z='min(pzoom+0.0012,1.05)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1280x720:fps=24[door_tight];
    [4:v]scale=1280:720,trim=end_frame=48,setpts=N/(24*TB),zoompan=z='min(pzoom+0.00045,1.02)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1280x720:fps=24[detective];
    [5:v]scale=1280:720,trim=end_frame=24,setpts=N/(24*TB)[title];
    [group][close][evidence][room][wave][door_tight][detective][title]
    concat=n=8:v=1:a=0,fps=24,format=yuv420p[picture]
  " \
  -map "[picture]" -an -t 10 \
  -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p \
  /tmp/nobody-went-out-interrogation-picture.mp4
```

Expected: exactly 240 frames at 24 fps. Apply no dissolves; the hard cuts and the 5.0-second waveform flash provide the horror rhythm.

- [ ] **Step 2: Build the sound design and final mix**

Mix these sources into the picture track:

- original teaser ambience, extended to ten seconds at -24 dB under narration;
- mastered narration from 0.05 to 8.90 seconds;
- 48 Hz synthesized heartbeat pulses at 4.0, 5.35, 6.70, and 8.05 seconds;
- filtered pink-noise radio texture from 4.0 to 8.75 seconds;
- a 55 Hz exponential bass/door impact beginning at 9.0 seconds;
- 150 ms of near-silence from 8.85 to 9.0 seconds before the title impact.

Run:

```bash
ffmpeg -hide_banner -loglevel error -y \
  -i /tmp/nobody-went-out-interrogation-picture.mp4 \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-teaser-final.mp4 \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Audio/nobody-went-out-interrogation-voice.wav \
  -f lavfi -t 4.75 -i "anoisesrc=color=pink:amplitude=0.04:r=48000" \
  -f lavfi -t 0.18 -i "sine=frequency=48:sample_rate=48000" \
  -f lavfi -t 0.90 -i "sine=frequency=55:sample_rate=48000" \
  -filter_complex "
    [1:a]aresample=48000,volume=10dB,apad=whole_dur=10,atrim=duration=10,
      volume='if(between(t,8.85,9.0),0,1)':eval=frame[bed];
    [2:a]aresample=48000,volume=-0.5dB,adelay=50|50,atrim=duration=8.90[voice];
    [3:a]highpass=f=1200,lowpass=f=4200,volume=0.025,adelay=4000|4000,
      atrim=duration=8.75[radio];
    [4:a]afade=t=out:st=0:d=0.18,volume=0.55,asplit=4[h1][h2][h3][h4];
    [h1]adelay=4000|4000[hb1];
    [h2]adelay=5350|5350[hb2];
    [h3]adelay=6700|6700[hb3];
    [h4]adelay=8050|8050[hb4];
    [5:a]afade=t=out:st=0.05:d=0.85,volume=0.68,adelay=9000|9000[impact];
    [bed][voice][radio][hb1][hb2][hb3][hb4][impact]
      amix=inputs=8:duration=longest:normalize=0,
      acompressor=threshold=-16dB:ratio=2:attack=8:release=100,
      loudnorm=I=-15:TP=-1.2:LRA=7,
      alimiter=limit=0.89:attack=5:release=50,
      atrim=duration=10[mix]
  " \
  -map 0:v:0 -map "[mix]" \
  -c:v copy -c:a aac -b:a 192k -ar 48000 \
  -t 10 -movflags +faststart \
  Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4
```

Expected: ten-second final output with the voice ending before the title-card impact and no sound between 8.85 and 9.0 seconds except codec noise floor.

- [ ] **Step 3: Validate codecs, dimensions, duration, loudness, and decode**

Run:

```bash
ffprobe -v error \
  -show_entries format=duration:stream=index,codec_type,codec_name,width,height,r_frame_rate,sample_rate,channels \
  -of json \
  Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4
ffmpeg -v error \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4 \
  -f null -
ffmpeg -hide_banner \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4 \
  -map 0:a:0 -af loudnorm=I=-15:TP=-1:LRA=7:print_format=summary \
  -f null - 2>&1 | tail -n 12
```

Expected: duration between 9.9 and 10.1 seconds; H.264 video at 1280x720 and 24 fps; AAC mono or stereo audio at 44.1 or 48 kHz; clean decode; integrated loudness between -16 and -14 LUFS; true peak at or below -1 dBTP.

- [ ] **Step 4: Inspect picture checkpoints**

Run:

```bash
ffmpeg -hide_banner -loglevel error -y \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4 \
  -vf "fps=1,scale=320:-1,tile=5x2:padding=2:margin=2" \
  -frames:v 1 /tmp/nobody-went-out-interrogation-contact-sheet.jpg
ffmpeg -hide_banner -loglevel error -y \
  -ss 9.50 \
  -i Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4 \
  -frames:v 1 /tmp/nobody-went-out-title-check.png
```

Open both images with the image viewer. Confirm sequence continuity, matching palette, a clearly male detective silhouette, and exact title spelling.

- [ ] **Step 5: Commit the final trailer**

```bash
git add Assets/_Project/Art/Characters/NobodyWentOut/Video/nobody-went-out-interrogation-trailer.mp4
git commit -m "feat(trailer): add interrogation horror cut"
```
