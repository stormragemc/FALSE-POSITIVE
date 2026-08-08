# Aaron — voice and script-driven delivery

**Status:** voice **selected** 9 Aug 2026. Production WAV generation, review,
and Unity integration are pending. This guide supersedes the Eric casting in
`Assets/_Project/Art/Audio/VO/README.md` where the two conflict.

---

## 1. Problem

Aaron has to read as useful before he reads as dangerous. In the morning scene,
he takes control because everyone else is panicking: he identifies what needs
doing, redirects Priya, and gives a lifting command. That practical confidence
must feel natural enough that the player can initially accept it as leadership.

The user refined the casting toward a **grounded jock**: athletic, confident,
physically decisive, and accustomed to taking charge. The target is a team
captain, not a loud frat-bro caricature. Aaron should sound capable without
sounding cheerful at the wrong moment.

His quiet “Two years?” is the other half of the role. It is the instant he
learns about Nick and Ivy. He does not shout. The stillness is more important
than anger because the later violence remains offscreen.

---

## 2. What is already true

Verified against the working tree on 9 Aug 2026.

| Source | Current reality |
|---|---|
| `docs/HUMAN_SCRIPT.md` | Canonical spoken script. Aaron owns `AARON-001` through `AARON-005`. |
| `Artifacts/voice-auditions/aaron/` | Contains the historical neutral round and a revised grounded-jock V3 round. |
| `Artifacts/voice-lines/aaron/` | Does not yet exist; no selected-voice production set has been rendered. |
| `Assets/_Project/Art/Audio/VO/README.md` | Still names Eric as Aaron and is stale. |
| `Assets/_Project/Art/Audio/VO/` | Contains five Eric-era MP3s with descriptive filenames. |
| `CutsceneRecipeBuilder.cs` | Uses descriptive stems rather than the stable `AARON-###` contract. |

### 2.1 Canonical line set

| ID | Dialogue |
|---|---|
| `AARON-001` | He's freezing. Let's get him inside, onto the sofa by the fire. |
| `AARON-002` | Priya. Not now. |
| `AARON-003` | Lift on three. |
| `AARON-004` | Barely survived it. |
| `AARON-005` | …Two years? |

The first three lines occur while the group recovers Nick's body. `AARON-004`
is a relaxed joke in the good-years memory. `AARON-005` is the moment Aaron
quietly realizes the affair has lasted for two years.

### 2.2 Audition evidence

The historical round used eight general male voices with
`eleven_multilingual_v2`. After the first line was played, the user asked for a
more recognizably jock-like Aaron.

The same eight candidates were regenerated using `eleven_v3`, Natural
stability, and a grounded athletic direction. The user selected **Liam**, who
was candidate 5, after hearing `AARON-001`. Liam gave the instruction a confident
physical presence without turning it into a performance of panic.

Additional revised audition files exist for `AARON-002`, `AARON-003`, and
`AARON-005`, but they had not been played when the casting was locked. They are
preparation assets, not approved production takes.

### 2.3 Casting decision

Selected: **Liam**, ElevenLabs voice ID `TX3LPaxmHKxFdv7VOQHJ`.

The voice name and ID are authoritative. Liam was candidate 5 in both the
historical pool and the revised grounded-jock round.

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Aaron's production voice is **Liam** (`TX3LPaxmHKxFdv7VOQHJ`) | Best fit for the user's grounded-jock direction on Aaron's longest practical command. |
| D2 | Production model is **`eleven_v3`** with Natural stability | The selected performance came from the revised V3 round, not the historical V2 round. |
| D3 | Aaron sounds like a team captain, not a frat-bro stereotype | Physical confidence and concise leadership fit the scene without introducing comic swagger. |
| D4 | Control is the baseline | Aaron redirects attention and gives tasks instead of reacting emotionally in public. |
| D5 | `AARON-005` stays quiet, flat, and slow | The script explicitly makes one restrained question his entire visible reaction to the affair. |
| D6 | Warmth is allowed in `AARON-004` | The first memory must establish a believable friend group before the second memory breaks it. |
| D7 | Production filenames use the `AARON-###` IDs | Stable script IDs prevent drift from Unity's descriptive stems. |
| D8 | Casting is final, but individual takes are not | All five production lines still require generation, listening, and approval. |

**Rejected:**

- **Eric as the binding cast choice.** It represents the existing Unity assets,
  not the current audition result.
- **The original generic delivery.** It did not make Aaron's athletic,
  take-charge quality clear enough.
- **An exaggerated jock or frat-bro performance.** Aaron is controlled and
  socially credible; broad swagger would weaken the mystery.
- **Open anger on “Two years?”** The story depends on Aaron becoming still, not
  visibly explosive.
- **Treating the prepared audition files as final production.** They establish
  casting range but have not been approved line by line.

---

## 4. Design

### 4.1 Voice and synthesis — SELECTED

| Setting | Value |
|---|---|
| Voice | Liam |
| Voice ID | `TX3LPaxmHKxFdv7VOQHJ` |
| `model_id` | `eleven_v3` |
| `stability` | `0.50` (`Natural`) |
| `similarity_boost` | `0.75` |
| `style` | `0.00` |
| `use_speaker_boost` | `True` |
| `speed` | `1.00` |
| Output format | `pcm_24000` |

The target production format is mono, 24 kHz, 16-bit PCM WAV. Keep these
baseline settings fixed across the character. Use line direction rather than
identity drift to move from casual confidence to shock.

### 4.2 Script-driven delivery

Audio tags and punctuation below are synthesis directions, not extra dialogue.

| ID | Performance beat | Exact synthesis prompt |
|---|---|---|
| `AARON-001` | Grounded urgency; immediately turn panic into a physical plan | `[confident, athletic, urgent but controlled] He’s freezing. Let’s get him inside—onto the sofa, by the fire.` |
| `AARON-002` | Firm redirection without raising his voice | `[controlled, firm, redirecting] Priya. Not now.` |
| `AARON-003` | Short team-lifting command with clean timing | `[terse, physically decisive] Lift on three.` |
| `AARON-004` | Easy, dry anniversary joke before the night turns | `[relaxed, dryly joking] Barely survived it.` |
| `AARON-005` | Flat, slow disbelief; no anger on the surface | `[quiet, flat, stunned] ...Two years?` |

The em dash in `AARON-001` joins the plan into one decisive thought. The leading
ellipsis in `AARON-005` allows a short beat of realization, but it should not
become a theatrical pause.

### 4.3 Assets and reproducibility — NOT YET IMPLEMENTED

The intended production set is:

```text
Artifacts/voice-lines/aaron/AARON-001.wav
Artifacts/voice-lines/aaron/AARON-002.wav
Artifacts/voice-lines/aaron/AARON-003.wav
Artifacts/voice-lines/aaron/AARON-004.wav
Artifacts/voice-lines/aaron/AARON-005.wav
```

The future generator should store the public voice ID, settings, and exact
prompts from §4.1–4.2. It must read `ELEVENLABS_API_KEY` from the environment,
skip approved files by default, and require an explicit overwrite flag.

### 4.4 Unity integration — NOT YET IMPLEMENTED

Current Unity audio uses these descriptive stems:

- `aaron_bring_him_in`
- `aaron_priya_not_now`
- `aaron_lift_on_three`
- `aaron_barely_survived`
- `aaron_two_years`

Integration needs to:

1. Generate and approve all five Liam production WAVs.
2. Import the approved files without transcoding them.
3. Replace Aaron's Eric casting row in the Unity VO README with §4.1.
4. Replace descriptive stems with the `AARON-###` ID contract.
5. Map `AARON-001` through `AARON-003` into the morning recovery sequence.
6. Map `AARON-004` into the good-years memory.
7. Map `AARON-005` into the when-it-went-wrong memory.
8. Rebuild serialized recipes and verify every subtitle resolves its matching
   clip.

### 4.5 Loudness and in-game mix

Aaron's practical commands must remain clear over wind, body movement, and the
fire, but `AARON-005` should feel quieter than the morning lines. Preserve that
contrast and balance all five through one Unity character-VO mixer group rather
than normalizing each file independently.

---

## 5. Testing and verification

Completed:

- `HUMAN_SCRIPT.md` contains exactly five Aaron IDs, `AARON-001` through
  `AARON-005`.
- Revised audition scripts document all eight candidate names and public voice
  IDs.
- Liam is consistently candidate 5 and maps to `TX3LPaxmHKxFdv7VOQHJ`.
- The selected round uses `eleven_v3` and the settings in §4.1.
- No API key is stored in the audition or guide files.

Required after production generation:

- Each WAV opens as mono, 24 kHz, 16-bit PCM.
- Spoken words match the canonical dialogue paired with each ID.
- `AARON-001` through `AARON-003` sound practical and physically assured, not
  melodramatic.
- `AARON-004` sounds comfortably friendly in the warm memory.
- `AARON-005` stays flat and restrained without sounding robotic.
- Aaron remains intelligible in the final cabin mix.

---

## 6. Out of scope

- Generating or approving Aaron's production WAVs.
- Importing or wiring Aaron's files in Unity.
- Deleting tracked Eric-era MP3s before migration.
- Rewriting canonical dialogue around a generated performance.
- Live Aaron TTS; his dialogue is pre-rendered.
- Destructive per-line mastering.

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Unity continues playing Eric while the guide names Liam | **High** | Treat production generation, import, casting update, and recipe migration as one explicit follow-up change |
| The jock direction becomes a caricature | Medium | Anchor delivery in concise physical confidence and avoid exaggerated swagger |
| `AARON-005` reveals anger too openly | Medium | Keep it quiet, flat, and slow as required by the scene direction |
| Short commands produce unstable or theatrical takes | Medium | Generate multiple takes and approve each line individually |
| Prepared audition files are mistaken for approved production | Medium | Keep production under the ID-based `Artifacts/voice-lines/aaron/` directory only |
| Aaron's quieter lines disappear under ambience | Medium | Preserve dynamics and adjust the shared Unity VO mixer non-destructively |
| Liam becomes unavailable in the Voice Library | Low | Preserve the public voice ID, settings, prompts, and approved WAVs in source control |
