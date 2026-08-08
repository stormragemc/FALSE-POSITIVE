# Nick — voice and script-driven delivery

**Status:** voice **selected** 8 Aug 2026. Production WAV generation, review, and
Unity integration are pending. This guide supersedes the Artem Lebedev casting
in `Assets/_Project/Art/Audio/VO/README.md` where the two conflict.

---

## 1. Problem

Nick needs to sound like the same person in two sharply different memories. In
the good-years fragment, he is warm, funny, and physically at ease with David.
Later, alcohol and the pressure of the hidden affair make him careless,
defensive, and humiliated. The performance should reveal that change without
turning him into an obvious villain.

**Casting.** The human script calls for a Russian man with a natural,
easy-to-understand Russian accent. The existing Unity README casts Artem
Lebedev (`rQOBu7YxCDxGiFdTm28w`), but the new audition selected Ivan Energetic.
Nick must also remain clearly distinct from Officer Spassky: younger, warmer,
quicker, and less controlled.

**Delivery.** Nick is described as warm and careless. He knows the conversation
with David matters, but he keeps putting it off. Even his angry lines come from
avoidance and embarrassment rather than menace.

---

## 2. What is already true

Verified against the working tree on 8 Aug 2026.

| Source | Current reality |
|---|---|
| `docs/HUMAN_SCRIPT.md` | Canonical spoken script. Nick owns `NICK-001` through `NICK-007`. |
| `Artifacts/voice-auditions/nick/` | Contains the audition metadata, playback utilities, and current audition renders. |
| `Artifacts/voice-lines/nick/` | Does not yet exist; no selected-voice production set has been rendered. |
| `Assets/_Project/Art/Audio/VO/README.md` | Still names Artem Lebedev as Nick and is stale. |
| `Assets/_Project/Art/Audio/VO/` | Contains six Artem-era MP3s for approximate equivalents of `NICK-002` through `NICK-007`. |
| `CutsceneRecipeBuilder.cs` | Uses the six descriptive Unity stems; `NICK-001` is not represented there. |

### 2.1 Canonical line set

| ID | Dialogue |
|---|---|
| `NICK-001` | Not tonight, David. I can't do this with you right now. I need some air. |
| `NICK-002` | He was worse at seventeen. |
| `NICK-003` | Unfortunately. |
| `NICK-004` | Here. You look fucking freezing. |
| `NICK-005` | You've been saying "after this trip" for two years. |
| `NICK-006` | He already knows. |
| `NICK-007` | I need some air. |

The first line belongs to the original argument scene. The remaining six are
split between the warm good-years memory and the colder memory showing when the
night went wrong. The descriptive Unity stems cover only the latter six.

### 2.2 Audition evidence

Eight Russian male candidates first read `NICK-001` using
`eleven_multilingual_v2`. Alexei was the provisional favourite on the longer
argument line.

The same candidates then read `NICK-005`, which tests drunken carelessness and
the moment Nick exposes the affair. Ivan Energetic was preferred on that line.
Those two voices advanced to a production-quality decider using `NICK-004`, a
warm line from before the group fractures. Both finalists used `eleven_v3` with
Natural stability. **Ivan Energetic**, presented as finalist 1, won the final
comparison.

The numbers are historical aids, not identity. Ivan Energetic was candidate 3
in the eight-voice rounds and finalist 1 in the decider.

### 2.3 Casting decision

Selected: **Ivan Energetic**, ElevenLabs voice ID
`JKtNvDNrWu33P1xzttP2`.

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Nick's production voice is **Ivan Energetic** (`JKtNvDNrWu33P1xzttP2`) | It carried the accidental revelation naturally and remained warm enough for the coat-swap joke. |
| D2 | Production model is **`eleven_v3`** with Natural stability | The final comparison used V3, and its direction handling was more useful than the historical V2 audition baseline. |
| D3 | Nick's accent stays natural and intelligible | Russian is a casting cue, not a reason to exaggerate pronunciation or reduce clarity. |
| D4 | Nick sounds younger and less controlled than Spassky | The two Russian male voices must remain immediately distinguishable in memory and interrogation scenes. |
| D5 | Warmth is the baseline; anger is situational | Nick is careless and avoidant, not threatening by default. The later lines should feel like the same friend under pressure. |
| D6 | Every production filename will use its `NICK-###` script ID | This avoids drift between descriptive Unity stems and canonical dialogue. |
| D7 | Performance direction may change punctuation but not spoken wording | Tags and pauses can shape delivery without silently rewriting the script. |
| D8 | Casting is final, but individual takes are not | Each of the seven production lines still needs generation, listening, and approval. |

**Rejected:**

- **Artem Lebedev as the binding cast choice.** It documents an earlier asset
  set, not the result of the current audition.
- **Alexei as the final voice.** He led on the first argument line, but Ivan
  Energetic handled the revelation and warm decider more convincingly overall.
- **Choosing from anger alone.** Nick appears at his warmest before the story
  reveals the affair; a voice that only sounds severe would flatten the memory.
- **Treating “Energetic” as a direction for every line.** It is the library
  voice name, not a requirement to make quiet or humiliated lines upbeat.
- **Reusing the old V2 settings as production defaults.** The decisive round
  used V3 Natural settings.

---

## 4. Design

### 4.1 Voice and synthesis — SELECTED

| Setting | Value |
|---|---|
| Voice | Ivan Energetic |
| Voice ID | `JKtNvDNrWu33P1xzttP2` |
| `model_id` | `eleven_v3` |
| `stability` | `0.50` (`Natural`) |
| `similarity_boost` | `0.75` |
| `style` | `0.00` |
| `use_speaker_boost` | `True` |
| `speed` | `1.00` |
| Output format | `pcm_24000` |

The target production format is mono, 24 kHz, 16-bit PCM WAV. Keep the baseline
settings fixed across the character so emotional direction changes the
performance without changing Nick's identity.

### 4.2 Script-driven delivery

Audio tags and punctuation below are synthesis directions. They are not extra
spoken dialogue.

| ID | Performance beat | Exact synthesis prompt |
|---|---|---|
| `NICK-001` | Frustrated avoidance; tired of the subject, not hostile toward David | `[frustrated, avoiding the conversation] Not tonight, David. I can’t do this with you right now. I need some air.` |
| `NICK-002` | Fond teasing while remembering their school years | `[fondly teasing] He was worse at seventeen.` |
| `NICK-003` | Dry joke during the toast, with affection underneath | `[dryly, joking] Unfortunately.` |
| `NICK-004` | Casual warmth while throwing David the warmer coat | `[warm, teasing] Here. You look fucking freezing.` |
| `NICK-005` | Slightly drunk and careless; the secret escapes before he recognizes the danger | `[slightly drunk, careless] You’ve been saying “after this trip” for two years.` |
| `NICK-006` | Angry and humiliated after David presses him | `[angry, humiliated] He already knows.` |
| `NICK-007` | Tight and final; end the conversation and get out of the room | `[tense, trying to end the argument] I need some air.` |

`NICK-001` and `NICK-007` both end with the same thought, but they occur in
different presentations of the night. The longer version carries exhausted
avoidance; the short memory fragment should land as a clipped exit.

### 4.3 Assets and reproducibility — NOT YET IMPLEMENTED

The intended production set is:

```text
Artifacts/voice-lines/nick/NICK-001.wav
Artifacts/voice-lines/nick/NICK-002.wav
Artifacts/voice-lines/nick/NICK-003.wav
Artifacts/voice-lines/nick/NICK-004.wav
Artifacts/voice-lines/nick/NICK-005.wav
Artifacts/voice-lines/nick/NICK-006.wav
Artifacts/voice-lines/nick/NICK-007.wav
```

The future generator should store the public voice ID, settings, and exact
prompts from §4.1–4.2. It must read `ELEVENLABS_API_KEY` from the environment,
skip existing approved WAVs by default, and require an explicit overwrite flag
for regeneration.

### 4.4 Unity integration — NOT YET IMPLEMENTED

Current Unity references use these descriptive stems:

- `nick_worse_at_seventeen`
- `nick_unfortunately`
- `nick_you_look_freezing`
- `nick_after_this_trip`
- `nick_he_already_knows`
- `nick_i_need_some_air`

Integration needs to:

1. Generate and approve all seven Ivan Energetic WAVs.
2. Import the approved files without transcoding them.
3. Replace Nick's Artem casting row in the Unity VO README with §4.1.
4. Replace descriptive stems with the `NICK-###` ID contract.
5. Add `NICK-001` to the fire-argument sequence.
6. Map `NICK-002` through `NICK-004` to the good-years memory.
7. Map `NICK-005` through `NICK-007` to the when-it-went-wrong memory.
8. Rebuild serialized recipes and verify every subtitle resolves the matching
   clip.

### 4.5 Loudness and in-game mix

Nick's warm jokes, accidental revelation, and angry exit should not all be
normalized to the same apparent intensity. Preserve the natural dynamics of
approved takes and balance them through one Unity character-VO mixer group.
Check intelligibility against fire, glass, radio bleed, wind, and the door slam.

---

## 5. Testing and verification

Completed:

- `HUMAN_SCRIPT.md` contains exactly seven Nick IDs, `NICK-001` through
  `NICK-007`.
- Audition scripts preserve every candidate name, number, and public voice ID.
- The final-decider script records Ivan Energetic as original candidate 3 and
  finalist 1.
- The final-decider files use `eleven_v3` with the settings in §4.1.
- No API key is stored in the audition or guide files.

Required after production generation:

- Each production WAV opens as mono, 24 kHz, 16-bit PCM.
- Spoken words match the canonical line paired with each ID.
- The Russian accent remains natural and easy to understand.
- `NICK-002` through `NICK-004` sound warm enough to establish the friendship.
- `NICK-005` sounds careless rather than deliberately cruel.
- `NICK-006` and `NICK-007` sound humiliated and avoidant rather than
  villainous.
- Nick remains clearly distinct from Spassky in the final mix.

---

## 6. Out of scope

- Generating or approving Nick's seven production WAVs.
- Importing or wiring Nick's files in Unity.
- Deleting the tracked Artem-era MP3s before migration.
- Changing canonical dialogue to suit a generated take.
- Live or dynamic Nick TTS; his lines are pre-rendered memories.
- Per-line destructive mastering.

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Unity continues playing Artem while the guide names Ivan Energetic | **High** | Treat casting, production generation, import, and recipe migration as one explicit follow-up change |
| `NICK-001` remains silent because no legacy stem exists | High | Add the ID-based clip explicitly to the fire-argument sequence |
| “Ivan Energetic” is directed too brightly in the later scene | Medium | Treat the name as voice identity only and follow the per-line beats in §4.2 |
| Nick sounds too similar to Spassky | Medium | Compare both in the cabin/interrogation transition and preserve Nick's quicker, warmer delivery |
| Drunken delivery becomes slurred or comic | Medium | Suggest carelessness through timing; keep every word intelligible |
| Short lines produce unstable or theatrical takes | Medium | Audition multiple renders and approve files individually before locking them |
| The Voice Library entry becomes unavailable | Low | Preserve the public voice ID, settings, prompts, and approved WAVs in source control |
