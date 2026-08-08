# General voice-generation guide

This document defines the shared voice-generation standard for every character
in **FALSE POSITIVE**. It applies to auditions, production rendering, review,
and handoff. Character-specific guides may override the baseline settings or
delivery advice when a documented performance requires it.

The goal is not merely clean speech. Each clip should sound like a person having
a thought in a specific moment, speaking to someone for a reason.

---

## 1. Source-of-truth order

Use project sources in this order:

1. `docs/HUMAN_SCRIPT.md` owns line IDs and canonical spoken words.
2. `Artifacts/voice_guide/<Character>.md` owns the selected voice, synthesis
   settings, character intent, and line-specific delivery.
3. This guide supplies the general generation and review rules.
4. Audition scripts and legacy Unity assets are historical evidence, not canon.

Do not silently rewrite dialogue to improve a generated take. If the canonical
wording genuinely needs revision, update the human script first and regenerate
the associated clip using the same stable ID.

Audio tags, punctuation changes, capitalization, and paragraph breaks may be
used as performance instructions as long as they do not change the words the
audience hears.

---

## 2. What makes a generated line sound human

### 2.1 Start with the thought, not the emotion label

Broad tags such as `[sad]`, `[angry]`, or `[panicked]` often produce generic
acting. First identify what the character is doing with the line:

- trying to stop someone from asking another question;
- buying time before admitting something;
- turning panic into a practical task;
- checking whether an injured friend can respond;
- joking to make an uncomfortable moment feel ordinary;
- asking a question whose answer they already suspect.

Prefer a concise playable direction such as `[guarded, answering quickly]` or
`[tense, trying to end the argument]`. Add an emotion only when it sharpens that
intent.

### 2.2 Write the internal beat before generating

For every line, establish:

| Question | Purpose |
|---|---|
| What has just happened? | Prevents the line from sounding detached from the scene. |
| Who is being addressed? | Changes warmth, distance, volume, and eye-line. |
| What does the character want right now? | Gives the line forward motion. |
| What are they hiding or avoiding? | Creates useful subtext without rewriting dialogue. |
| What changes during the line? | Determines stress, pace, and where a pause belongs. |

If none of these answers changes across a long line, the prompt is probably too
flat.

### 2.3 Use thought groups

Natural speech arrives in meaningful chunks rather than evenly spaced words.
Break a line where the thought changes, not wherever the text happens to contain
a comma.

Example:

> `[panicked but clear] Police? Our friend is hurt! / We found him outside—in
> the snow. / Please send someone. Please hurry!`

The slashes above illustrate thought groups; they are not synthesis text. The
speaker first confirms the connection, reports the emergency, gives the key
location, and then pleads for action. Each group should have its own contour.

### 2.4 Let sentence stress carry meaning

Not every word deserves equal weight. Decide which word contains the new or
dangerous information.

- “The door was **locked**” establishes the discovery.
- “Who **locked** it?” turns the discovery toward suspicion.
- “...**Two years**?” is about the duration, not the existence of the affair.

Use punctuation and direction to encourage the intended stress. Avoid capital
letters unless a genuinely raised call is required; excessive capitalization
usually creates theatrical shouting.

### 2.5 Vary repeated words and questions

People rarely repeat a phrase with the exact same pitch, rhythm, and intention.
The second repetition usually escalates, collapses, clarifies, or changes its
target.

For “What do we do? What do we do?”, the first question can be an attempt to
think and the second a sharper plea. If both repetitions have identical rising
contours, regenerate or adjust the prompt.

### 2.6 Names are actions, not a list

When a character calls several names, each name is an attempt to reach a person.
Avoid uniform gaps and identical rising pitch, which make the names sound like a
synthetic roll call.

- Use short, connected calls when urgency is high.
- Give a longer pause only when the character is listening for an answer.
- Do not put a question mark after every name unless each is genuinely a
  separate uncertain call.
- Paragraph breaks can separate the alarm, the names, and the final plea without
  forcing a breath between every word.

---

## 3. Pauses, pace, and silence

### 3.1 Every pause needs a reason

A useful pause represents one of the following:

- a new realization;
- listening for a response;
- choosing whether to reveal something;
- absorbing unexpected information;
- changing from facts to a plea or accusation;
- losing the words briefly under real pressure.

Do not add pauses merely to make a line sound dramatic. Unmotivated silence is
one of the clearest signs of synthetic performance.

### 3.2 Use punctuation deliberately

| Tool | Typical effect | Use with care |
|---|---|---|
| Comma | Light continuation | Too many create a measured, narrated cadence. |
| Period | Completed thought and reset | Several short sentences can sound clipped. |
| Em dash | Interruption, pivot, or tightly connected emphasis | Overuse makes every line sound written. |
| Ellipsis | Hesitation, listening, or a longer thought beat | Too many produce sluggish, melodramatic speech. |
| Question mark | Genuine question contour | Repeated marks can create an artificial list of rises. |
| Exclamation mark | Increased attack or urgency | Multiple marks often cause shouting or overacting. |
| Paragraph break | Strong separation between performance beats | May introduce too much silence or an audible reset. |

Punctuation is model guidance, not a precise timing system. Always listen to the
result rather than assuming a mark created the intended pause.

### 3.3 Match pace to cognition

- Panic can be fast while still intelligible.
- Shock is often slower and quieter than panic.
- A rehearsed answer may arrive slightly too quickly.
- A practical command should be concise, not rushed.
- Contained anger often becomes flatter and more deliberate.
- Confusion may contain a brief search for meaning, but should not become random
  hesitation.

Do not apply one speed to an entire character's emotional range. Keep the voice
setting stable where possible and shape local pace through the prompt and thought
structure.

### 3.4 Protect the beginning and end

Listen for clipped consonants, swallowed first words, and endings that fall away
before the sentence finishes. Leave enough clean head and tail for editing and
in-game triggering. Do not solve a cutoff by adding spoken filler.

---

## 4. Breathing and vocal effort

Breathing should follow the thought and physical condition of the character.
It should not be added as a generic shortcut for emotion.

- Avoid `[breathless]` unless audible breathing is specifically wanted. It can
  introduce large gasps between phrases.
- Use pace, sentence attack, broken contours, and tighter thought groups to
  create panic before requesting breath sounds.
- A quiet inhalation may help before a difficult admission; repeated gasps will
  usually distract from the words.
- Do not let every sentence begin with a sigh, laugh, grunt, or inhale.
- Reject takes with unexplained mouth noises, breaths in unnatural positions, or
  an effort level that does not match the character's physical action.

If breathing is important to the scene, treat it as an intentional performance
or separate sound-design element, not accidental model output.

---

## 5. Emotional restraint and scene truth

### 5.1 Do not announce subtext

A guilty character does not need to sound “guilty.” A killer does not need to
sound sinister. A frightened character can still answer clearly. Let wording,
timing, and scene context expose the subtext.

Overtly signalling the mystery's answer weakens both the performance and the
game.

### 5.2 Prefer specific restraint

Useful directions include:

- `[quiet, flat, stunned]`
- `[guarded, answering quickly]`
- `[warmly amused]`
- `[firm, redirecting]`
- `[angry, humiliated]`
- `[panicked but clear]`

Directions such as `[extremely emotional]`, `[cinematic]`, or `[dramatic]` are
too broad to guide a believable thought.

### 5.3 Keep emotional transitions audible

When a line contains more than one beat, the voice should change for a reason.
A police call may move from factual reporting into a personal plea. A question
may begin as confusion and sharpen into suspicion. Preserve that movement rather
than rendering the entire line at one intensity.

### 5.4 Short lines need more scrutiny

One- to four-word lines give the model little context and can vary wildly
between renders. Supply clear scene direction, generate more than one take, and
judge them beside adjacent dialogue. A technically clean “Yes. All night.” can
still fail if it sounds unrelated to the preceding question.

---

## 6. Casting, identity, and accents

### 6.1 Cast across multiple kinds of line

Do not finalize a voice from one emotional register when the character has a
wider range. A useful audition sequence is:

1. a representative longer line for timbre and sustained naturalness;
2. a contrasting line for range;
3. a final decider between the strongest two voices;
4. a longer audition-only passage if the canonical lines are too short to make
   a reliable choice.

An audition-only passage must be labelled clearly and must never enter the
canonical script or production set by accident.

### 6.2 Keep candidate numbers local

Candidate numbers are convenient during playback but can change between rounds.
Record all of the following in comments and documentation:

- candidate or finalist number;
- voice display name;
- ElevenLabs voice ID;
- model and settings used for that round.

The voice name and ID are authoritative after selection, not the number.

### 6.3 Treat accents as identity, not performance effects

- Follow the accent reference in `HUMAN_SCRIPT.md`.
- Prefer a natural, intelligible accent over an exaggerated one.
- Do not use phonetic misspellings to imitate an ethnicity.
- Check difficult names and story terms explicitly.
- Keep accent consistency across emotional states and line lengths.
- Avoid stereotypes in rhythm, vocabulary, or emotional direction.

Russian and Indian in this project indicate intended accent families. They do
not override the character's individual age, warmth, education, energy, or
relationship to the listener.

### 6.4 Keep characters distinct

Compare characters who may appear close together. Distinction can come from
timbre, age, pace, vocal weight, and social energy. Do not rely only on volume or
accent. In particular, voices sharing an accent must still have separate
identities.

### 6.5 Project name pronunciation

The character name **David** is always pronounced **DAY-vid** (`/ˈdeɪvɪd/`).
Production generators may substitute the synthesis-only spelling `Day-vid` (or
`DAY-VID` in an already-capitalized call) to enforce that pronunciation. The
canonical script, subtitles, manifests, stable IDs, and filenames must continue
to use `David`.

---

## 7. Prompt construction

Use the smallest prompt that reliably produces the intended performance.

Recommended shape:

```text
[specific intent, useful emotional state] Canonical spoken words with deliberate punctuation.
```

For a longer line with a genuine internal turn:

```text
[initial intent] First thought.

[changed intent] Second thought.
```

Only use multiple tags if the character's intent actually changes. Too many
directions can make the read self-conscious, unstable, or theatrical.

Avoid:

- prose descriptions that the model may speak aloud;
- contradictory tags such as `[quietly, shouting]`;
- directing every individual word;
- excessive capitalization or repeated punctuation;
- vague quality requests such as “make it more human” without a playable beat;
- embedding sound effects, candidate numbers, or filenames in synthesis text.

Keep the exact synthesis prompt beside the line ID in the character guide and
production generator.

---

## 8. Model and setting policy

### 8.1 Baseline for new prerecorded dialogue

Unless a character guide documents an exception, begin with:

| Setting | Baseline |
|---|---|
| Model | `eleven_v3` |
| Stability | `0.50` (`Natural`) |
| Similarity boost | `0.75` |
| Style | `0.00` |
| Speaker boost | enabled |
| Speed | `1.00` |
| Output | `pcm_24000` |

This is a starting point, not a reason to overwrite a finalized character's
documented configuration. Officer Spassky, for example, uses a deliberately
different model and delivery-register system. The character guide wins.

### 8.2 Change one variable for a reason

When diagnosing a weak take, identify the problem before changing settings:

- unnatural rhythm: revise thought groups or punctuation first;
- emotional flatness: improve the playable direction;
- identity drift: raise similarity or stability cautiously;
- stiff delivery: lower stability cautiously or use a more suitable model;
- excessive acting: reduce style and simplify tags;
- rushed or dragging output: adjust local phrasing before changing global
  speed.

Do not change voice, model, stability, style, speed, punctuation, and direction
at once. A comparison is only useful when the cause of improvement is visible.

### 8.3 Generated output is nondeterministic

The same prompt and settings can produce a different performance on another
run. Preserve an approved take. Production generators must skip existing files
by default and require an explicit force or overwrite option.

---

## 9. Audition workflow

### 9.1 Prepare

1. Read the full scene around the line.
2. Confirm the canonical ID and words.
3. Choose a line long enough to expose timbre and cadence.
4. Document every candidate's number, name, and voice ID.
5. Render the same prompt and settings for every candidate in that round.

### 9.2 Listen

Play one line across all candidates in numbered order. Do not open external
media-player windows. Leave a short consistent gap between candidates.

Judge:

- natural thought and sentence stress;
- character fit and age;
- intelligibility and accent;
- emotional truth without overacting;
- breathing and mouth noise;
- consistency of volume and recording quality;
- distinction from the rest of the cast.

### 9.3 Narrow and validate

Carry the strongest voices into a contrasting second line. If different voices
win different lines, run a decider using production-quality settings. Do not
force a choice from a passage too short to expose meaningful differences.

### 9.4 Lock casting separately from takes

“Voice selected” means the voice identity and baseline settings are chosen. It
does not mean every generated audition WAV is approved production.

Track these states separately:

1. audition prepared;
2. voice selected;
3. production lines generated;
4. individual takes reviewed and approved;
5. Unity assets integrated and tested.

---

## 10. Production asset conventions

- One script line per audio file.
- Filename: `<CHARACTER>-<NNN>.wav`, matching `HUMAN_SCRIPT.md` exactly.
- Character directory:
  `Artifacts/voice-lines/<lowercase-character>/`.
- Target format: mono, 24 kHz, 16-bit PCM WAV unless a character guide records
  an exception.
- Keep a `README.md` manifest with voice name, voice ID, settings, canonical
  words, delivery direction, and exact prompts.
- Keep a clearly named generator and single-line playback utility beside the
  production files.
- Never rename a line to describe its current wording. Stable IDs are the
  association contract.
- Do not overwrite legacy Unity assets until integration work explicitly maps
  and verifies every replacement.

Audition WAVs and production WAVs are different classes of asset. Do not copy an
audition into production merely because its voice won; approve the individual
performance first.

---

## 11. Secrets and reproducibility

- Read `ELEVENLABS_API_KEY` from the process environment at runtime.
- Never print the key.
- Never write the key into Python, Markdown, logs, command transcripts, audio
  metadata, or any non-env file.
- Store public voice IDs, model IDs, settings, prompts, and output format in the
  generator so another teammate can reproduce missing lines.
- Use the project's Sidecar Python environment:
  `Sidecar\.venv\Scripts\python.exe`.
- Do not commit rejected audition audio unless the project explicitly needs it
  as review evidence.
- Preserve approved production WAVs in source control because the upstream
  library voice or exact nondeterministic take may later be unavailable.

All character VO is synthetic and must remain labelled as synthetic in project
documentation and presentation material.

---

## 12. Technical and performance QA

Review every production candidate before approval.

### Spoken-content check

- Correct character and line ID.
- Every canonical word is present and in order.
- No direction tag, punctuation cue, or instruction is spoken aloud.
- Names and story-specific terms are pronounced correctly.
- No invented words, duplicated fragments, or missing endings.

### Performance check

- The line responds to what happened immediately before it.
- Sentence stress lands on the meaningful information.
- Pauses represent thought, listening, or a real change of intent.
- Repeated words do not use identical synthetic contours.
- Emotion supports the action without announcing subtext.
- Accent remains natural, consistent, and intelligible.
- Breathing and vocal effort fit the physical scene.
- The beginning and ending are not clipped.

### Technical check

- File opens successfully.
- Expected channel count, sample rate, and bit depth.
- No digital clipping, corruption, unexpected long silence, or truncated tail.
- Native loudness is reasonable beside the same character's other lines.
- Playback utility resolves the file from its stable ID.

### Scene check

- Subtitle text and clip use the same ID.
- The line fits between adjacent voices without an unnatural dead gap.
- Quiet dialogue remains audible under ambience and effects.
- Panic and commands remain intelligible in the busiest mix.
- Mixer adjustments preserve intentional differences between quiet and loud
  performances.

---

## 13. Common failure modes

| Symptom | Likely cause | First correction |
|---|---|---|
| Robotic list cadence | Equal stress and equal pauses | Rebuild the line into meaningful thought groups. |
| Names sound like questions | Repeated question contours | Connect the calls and reserve a listening pause for where it belongs. |
| Panic sounds calm | Direction describes emotion without urgency | Tighten pace, sharpen sentence attacks, and give the speaker a concrete goal. |
| Panic contains distracting gasps | Broad `[breathless]` tag | Remove it and create urgency through rhythm and punctuation. |
| Line sounds melodramatic | Too many tags, ellipses, or style | Simplify to one playable intention and reduce style. |
| Short reply sounds random | Too little context | Add precise scene direction and render several takes. |
| Repetition sounds copied | Same contour on both phrases | Give the second repetition a different intention. |
| Accent becomes caricature | Exaggerated direction or phonetic spelling | Return to the natural voice identity and canonical spelling. |
| Character changes identity between lines | Settings or voice changed | Restore the documented baseline and vary delivery through the prompt. |
| Approved line changes after rerun | Nondeterministic regeneration | Preserve approved files and skip existing production assets by default. |

---

## 14. Final approval checklist

Before calling a voice line final, confirm:

- [ ] Canonical ID and spoken words match `HUMAN_SCRIPT.md`.
- [ ] Selected voice name and public voice ID match the character guide.
- [ ] Model and settings match the documented baseline or exception.
- [ ] Intonation follows the character's thought rather than generic narration.
- [ ] Pauses are motivated and not excessively long.
- [ ] Breathing is intentional and unobtrusive.
- [ ] Emotion is clear without becoming theatrical or revealing hidden plot.
- [ ] Accent is natural, consistent, and intelligible.
- [ ] The take has no spoken tags, artifacts, clipping, or truncated words.
- [ ] File format and filename meet the production convention.
- [ ] The take has been heard beside adjacent dialogue and representative scene
  audio.
- [ ] The approved file is preserved against accidental regeneration.

When a line fails, describe the audible problem precisely before generating the
next take. “More human” is the goal; the useful instruction is why the current
performance does not sound human yet.
