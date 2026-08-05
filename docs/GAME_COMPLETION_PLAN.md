# FALSE POSITIVE — plan to finish the game

**Written:** 6 Aug 2026 · **Freeze:** 8 Aug 23:59 · **Submission:** 9 Aug 2026
**Scope owner:** the game (Unity) team — 2 people. Backend is a separate team on `main`.

---

## Context

The repo has a working voice loop and no game. `docs/ROADMAP.md` states it plainly: *"the
plumbing is good and the game is missing."* Mic → STT → HuBERT affect → Gemini → ElevenLabs →
playback with lip sync runs end to end; 47 sidecar tests pass. What does not exist is the
experience around it — a main menu, mic consent and calibration, the playable memory scenes, the
cutscenes, the phase structure of the interrogation, and the endings.

Two things landed since the docs were last accurate:

1. `Assets/_Project/Scenes/NobodyWentOut_CabinNight.unity` exists (commit `2edccea`) — a blocked-out
   snow-cabin interior with fireplace, sofa, door, door bolt, landing/stairs, snow terrain, storm
   sky, memory anchors, and a **built cast** (`Scripts/Editor/CabinNightCharacterBuilder.cs` places
   Player, Nico, Aaron, Ivy, Priya from o3n UMA bodies with per-character idle profiles).
2. The backend team is mid-migration to **Cloud Run** (`origin/main` →
   `docs/superpowers/plans/2026-08-04-cloud-backend-work-split.md`): Google STT, Gemini via Vertex,
   `X-FP-Client-Key` header, pinned single instance. Their 7–8 Aug is reserved for the consistency
   tracker and `DetectiveAction`.

This plan takes the game from that state to a complete, playable, submittable build by the freeze,
built on the story the user specified. It is written for **two Unity generalists working in
parallel** with a hard interface between them.

### Decisions taken (from clarification, 6 Aug)

| Decision | Answer |
|---|---|
| Target | **9 Aug submission. Full scope retained.** No two-phase split. |
| Canon | **`idea/bong.md` is fully superseded.** New ground truth authored below (§3), flagged for approval. |
| Cutscenes | **In-engine Unity Timeline** + procedural/simple animation. No prerecorded video. |
| Team | **Two Unity generalists.** Split by system, not by discipline. |

### Concern stated once, then proceeding

Three days for ~18 cutscenes, two playable memory scenes, a menu, calibration, a four-phase
interrogation state machine, and four endings is aggressive for two people. The plan is built so
that **§10's cut ladder degrades gracefully** — every cutscene has a defined cheap form, and the
build is playable end to end from Day 1 evening with placeholders in every slot. Nothing is
sequenced so that a missed item leaves a hole the player can fall through.

---

## 1. What already exists (reuse, do not rebuild)

| Need | Already there | Path |
|---|---|---|
| Mic capture, ring buffer, VAD, utterance recorder | ☑ | `Scripts/Audio/{MicrophoneService,VoiceActivityDetector,UtteranceRecorder}.cs` |
| Turn state machine (Idle/Speaking/Listening/Uploading) + events | ☑ | `Scripts/Dialogue/DialogueManager.cs` |
| HTTP client + DTOs | ☑ | `Scripts/Net/{InterrogationSidecarClient,SidecarDtos}.cs` |
| Officer voice playback + jaw lip sync + procedural idle | ☑ | `Scripts/Cop/*`, `Scripts/Audio/CopVoicePlayback.cs` |
| Boot gate on `/health`, sidecar auto-launch | ☑ | `Scripts/Core/{GameBootstrap,SidecarProcessLauncher}.cs` |
| Screen fader, mic level meter, F1 debug overlay | ☑ | `Scripts/UI/{ScreenFader,MicLevelMeterUI,DebugOverlayUI}.cs` |
| Tunables in one ScriptableObject | ☑ | `Scripts/Core/InterrogationConfig.cs`, `Config/InterrogationConfig.asset` |
| Seated/standing camera handoff, seat anchors | ☑ | `Scripts/Player/{PlayerStateController,SeatedCameraRig,FreeLookCameraRig,SeatAnchor}.cs` |
| Free-roam FPS controller for the cabin | ☑ | `Scripts/Player/CabinFirstPersonController.cs` |
| Cabin interior blockout + snow terrain + storm sky + fire flicker | ☑ | `Scenes/NobodyWentOut_CabinNight.unity`, `Scripts/CabinNight/*` |
| Cast placement + idle profiles | ☑ | `Scripts/Editor/CabinNightCharacterBuilder.cs` |
| Environment art packs | ☑ | `Assets/Cabin`, `Assets/IL3DN`, `Assets/TDG Storage Solutions`, `Materials Collection/4 Snow Materials` |
| Character concept sheets | ☑ | `Art/Characters/NobodyWentOut/Concepts/*.png` |

**Nothing in the above list gets rewritten.** Every new system in §4 attaches to it.

---

## 2. Assumptions baked in (do not spend time re-deciding)

- **Mic permission is not an OS dialog on Windows standalone.** `Application.RequestUserAuthorization`
  is effectively a no-op on desktop. What we build is a **diegetic in-game consent card** —
  which is also ROADMAP **S8**'s required "in-game notice before the first recording." One card,
  session-scoped, plus a persistent mic-active indicator. Do not promise an OS prompt anywhere.
- **VAD calibration ≠ HuBERT baseline.** The "speak normally" calibration step sets the client-side
  noise floor and `vadEnterMultiplier`/`vadExitMultiplier`. HuBERT's reference centroid is computed
  **backend-side** from early turns in `prosody.py`'s `ProsodyTracker`. Never wire one to the other.
- **The yell-for-Nick loudness gate is 100% client-side** — RMS vs. the calibrated floor via
  `VoiceActivityDetector` + `MicLevelMeterUI`. Zero backend dependency.
- **G8 ("no replay of the crime") is deliberately superseded.** The pitch's imperfect-memory
  mechanic now lives in *what the memory scenes withhold* (no clock unless you look, no view of
  who left, nothing outside the window at night) rather than in brevity. Say this in the deck.
- **G6 stands and constrains UI copy.** The internal suspicion score is **never** labelled truth,
  lie, or deception in any visible string. See §8.
- **All character VO is ElevenLabs-generated** and committed as audio assets (Spassky, Priya, Ivy,
  Aaron, Nick). This is a G10 labelling item, not a blocker.

---

## 3. Story script

The full story script — ground truth, cast, scene/phase map, beat-by-beat script, cutscene list,
story marks, traps, ending selection, and clue ledger — lives in its own file:
**[`docs/STORY_SCRIPT.md`](STORY_SCRIPT.md)**. It supersedes `idea/bong.md` entirely; `bong.md`
moves to `idea/superseded/bong.md` with a one-line header pointing at the replacement.

Read `STORY_SCRIPT.md` before starting any task in §5 that touches dialogue, cutscene content, or
scene dressing — it is the single source of truth for what the game says and shows.

---

## 4. Client architecture — what gets built

All new code under `Assets/_Project/Scripts/`. Namespaces follow the existing `FalsePositive.*`
convention.

```
Scripts/
  Flow/          NEW — GameFlowDirector, GamePhase, SceneLoader, MemoryFlags, SessionScore
  Cutscene/      NEW — CutsceneDirector, CutsceneId, FuzzyTransition, SubtitlePlayer
  Interaction/   NEW — Interactable (base), InteractionRaycaster, RadioTuner, KeyPickup,
                       DoorInteractable, InspectPoint, ObjectiveTracker
  Voice/         NEW — MicConsentFlow, MicCalibration, LoudnessGate, SpeechPrompt
  Menu/          NEW — MainMenuController, SettingsPanel, SettingsStore
  Dialogue/      EXTEND — PhaseDialogueController, StoryMarkTracker, SuspectNameDetector,
                          EndingSelector, OutputGuard
  UI/            EXTEND — MicIndicator, ObjectiveHUD, SubtitleUI, OutcomeScreen
```

### 4.1 The interface between the two tracks — freeze this in hour 1

Everything below is a **contract**. Person A implements the left column; person B calls it and
implements the right. Neither edits the other's files.

```csharp
// Flow/GameFlowDirector.cs — Person A owns
public enum GamePhase { Menu, P1_Tutorial, M1_Night, P2_Recall, M2_Morning, P3_Verdict, P4_Ending }

public sealed class GameFlowDirector : MonoBehaviour   // lives in _Persistent, never unloaded
{
    public static GameFlowDirector Instance { get; }
    public GamePhase Phase { get; }
    public MemoryFlags Flags { get; }        // Set(string), Has(string), Describe() -> prompt text
    public SessionScore Score { get; }       // credibility, composure, accusation

    // ONE session id for the entire playthrough. DialogueManager no longer mints its own.
    public string SessionId { get; }

    public event Action<GamePhase> PhaseChanged;

    public void AdvancePhase();                          // scene activate/deactivate + fade inside
    public void RequestCutscene(CutsceneId id);          // -> CutsceneDirector
    public void RequestSpokenPrompt(string prompt, bool requireLoud, Action onSatisfied);
}

// Cutscene/CutsceneDirector.cs — Person B owns
public sealed class CutsceneDirector : MonoBehaviour
{
    public bool IsPlaying { get; }
    public event Action<CutsceneId> Finished;
    public void Play(CutsceneId id);        // binds the PlayableDirector, locks input,
                                            // raises Finished on stop
}

// Interaction/Interactable.cs — Person B owns
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected string lookPrompt;    // "Hold E to tune"
    [SerializeField] protected string memoryFlag;    // written to GameFlowDirector.Flags on use
    public abstract void OnInteract();
    public event Action<Interactable> Completed;
}
```

**Rule:** `CutsceneDirector` never reads game state and `GameFlowDirector` never touches a
`PlayableDirector`. One raises, the other listens. This is what makes the two tracks independent.

**The `_Persistent` scene.** Loaded additively at boot, never unloaded, owned entirely by Person A.
It holds `GameFlowDirector`, `MicIndicator`, `SubtitleUI`, `SpeechPrompt`, `ObjectiveHUD`,
`OutcomeScreen`, the fault cards, and `ScreenFader`. Without it, A's UI work would have to live
inside B's three scenes and §6's clean split would be a fiction — every one of A5, A11, A13 needs
objects that outlive a scene change. Create it in T02, before B dresses anything.

`GameBootstrap` is refactored in the same task: today it hard-wires health-gate → `BeginSeated()`
→ `BeginConversation()`, which is the wrong boot order now that menu → consent → calibration comes
first. It becomes a health probe that reports to `GameFlowDirector` and nothing else.

### 4.2 Phase-scoped dialogue

`DialogueManager` stays as-is. A new **`PhaseDialogueController`** sits above it and owns:

- the **phase system prompt** — sent as a `scene_instruction` history turn (the backend already
  supports `HISTORY_KIND_SCENE`, `llm.py:71`), or as a `phase` form field once B1 lands;
- **the memory-flag briefing** — `MemoryFlags.Describe()` serialized into that same
  `scene_instruction` on entry to P2 and P3. **This is what makes the story marks, traps and clue
  ledger in `STORY_SCRIPT.md` function rather than decorate:** B's interactables write
  `saw_clock`, `saw_grille_intact`, `heard_ivy_alibi` and the rest, and without this line they
  reach nothing. The officer needs to know *"the witness looked at the mantel clock and could know
  the time was 00:52"* versus *"the witness never looked at a clock; any specific time is
  invented"* — otherwise the clock trap cannot be baited, fabrication cannot be detected, and
  `E_AARON`'s "cited ≥ 2 clues" condition has nothing to check against. Zero backend dependency;
- **`StoryMarkTracker`** — folds each turn's `transcript` into the seven marks;
- **`SuspectNameDetector`** — P3 only, single unambiguous name from {Aaron, Ivy, Priya};
- **turn caps** — hard per phase (P2: 14, P3: 8) so a stuck phase cannot burn the session;
- **`EndingSelector`** — run once at P3 exit, per `STORY_SCRIPT.md`'s ending-selection rules.

### 4.3 Officer VO — two paths, both needed

| Path | Used for | How |
|---|---|---|
| **Live** | P1 opening, all of P2, all of P3 | Existing `/turn` → `audio_b64` → `CopVoicePlayback` |
| **Pre-rendered** | The wake/answer cutscene, all of Priya/Ivy/Aaron/Nick, the four endings | ElevenLabs-generated `.wav` committed under `Art/Audio/VO/`, played from Timeline audio tracks |

Both paths render subtitles through the same `SubtitleUI` so the two never look different.

> `.gitignore:90-145` currently blocks `.wav`/`.mp3` (the "no recordings of real people" rule,
> `IMPLEMENTATION_PLAN.md` §8). **Add a scoped un-ignore for `Assets/_Project/Art/Audio/VO/**`**
> and note in the README that every file there is synthetic TTS, not a recording of a person.
> Do not disable the rule wholesale.

---

## 5. Task list — start to finish

**Legend:** `A` = Person A (systems/voice/flow) · `B` = Person B (scenes/cutscenes/art) ·
`⊕` = both, sync point. Dependencies in the last column. IDs are stable — use them in commits.

### Day 0 (6 Aug, remainder) — unblock everything

| ID | Own | Task | Dep |
|---|---|---|---|
| T00 | ⊕ | **Open the project in Unity, confirm it compiles, and confirm Play mode actually ticks.** `ROADMAP.md` §2 says nobody has proven the compile; worse, `docs/UNITY_CLIENT.md` reports `Time.frameCount` stuck at **1** through a full Play session on the machine that wrote the HuBERT work. **The entire cutscene plan is Timeline, and Timeline needs the player loop.** Press Play, log `Time.frameCount` for 3 s, confirm it advances — on *both* machines. If it doesn't, that is a Day-0 emergency, not a Day-2 discovery. | — |
| T01 | ⊕ | Write `docs/STORY_SCRIPT.md` (§3). Get the ground truth signed off. | — |
| T02 | ⊕ | **The seam commit.** Land on `main`, in one go: §4.1's contracts as empty compiling stubs; the **`_Persistent` scene** with the UI/flow objects (§4.1); `GameFlowDirector.SessionId` with `DialogueManager` taking it by injection instead of `Guid.NewGuid()` in `Awake`; `Interrogation` loaded additively and deactivated rather than unloaded; `GameBootstrap` reduced to a health probe. Both people branch from this commit. **Nothing in §5 is independent until this lands.** | T00 |
| T03 | A | Branch `game/systems`. Branch `game/scenes` for B. | T02 |
| T04 | B | Duplicate `NobodyWentOut_CabinNight.unity` → `Memory_CabinNight.unity` and `Memory_CabinMorning.unity`. Re-run `Tools ▸ False Positive ▸ Rebuild Cabin Night Cast` against the new names, **renaming Nico → Nick** in `CabinNightCharacterBuilder.cs`. | T02 |
| T05 | ⊕ | Send the **backend asks** (§7) to the other team **today** — B0 (male voice probe) and B1 (phase prompt) are the two with real lead time. | T01 |
| T06 | ⊕ | **Confirm the ElevenLabs plan tier before Day 2.** Live TTS is ~200 chars/turn × ~23 turns ≈ 4.6k chars per playthrough. Z1's two clean-machine runs + four scripted ending playthroughs + the Z5 capture ≈ **32k chars**, plus ~20 pre-rendered VO lines. The backend team's own doc puts the free tier at ~10k chars/month. Verification stalls on `402` if this is not settled early. | — |

### Day 1 (7 Aug) — playable end to end with placeholders

**Track A — flow, voice, menu**

| ID | Task | Dep |
|---|---|---|
| A1 | `GameFlowDirector` + `GamePhase` + `MemoryFlags` (incl. `Describe()`) + `SessionScore`, phase transitions as **activate/deactivate over additive scenes** with `ScreenFader` on both sides, single `SessionId` for the whole playthrough. | T02 |
| A2 | `MainMenu` scene: Play / Settings / Quit, `SettingsStore` (PlayerPrefs), storm bed audio, cabin exterior still or slow camera. | A1 |
| A3 | `MicConsentFlow` — the diegetic card (`STORY_SCRIPT.md` §3.4 copy verbatim), device enumeration, Back. **This is ROADMAP S8.** | A2 |
| A4 | `MicCalibration` — 5 s sample, noise floor → `vadEnterMultiplier`/`vadExitMultiplier`, store `loudReferenceRms`. Failure path with device dropdown + Retry. Persist to `InterrogationConfig` at runtime (not to the asset). | A3 |
| A5 | `MicIndicator` (active/inactive, never "recording") + `SpeechPrompt` UI + `SubtitleUI`. | A3 |
| A6 | `LoudnessGate` — peak RMS vs `loudReferenceRms × yellFactor`, "Louder" retry copy, no cap on attempts. Unit-testable with synthetic buffers. | A4 |
| A7 | `PhaseDialogueController` — phase prompts as `scene_instruction`, turn caps, wires `DialogueManager` events. Phase prompts live in **`prompts/` as text assets, never string literals** (G3 — this is a *graded* deliverable currently in violation). | A1 |
| A7b | **`MemoryFlags.Describe()` → the P2/P3 `scene_instruction`.** §4.2. Small task, and the story marks/traps/clue ledger are inert without it. | A7, B2 |
| A8 | `StoryMarkTracker` (client-side keyword fallback) + debug readout on the F1 overlay. | A7 |

**Track B — scenes, interaction, cutscenes**

| ID | Task | Dep |
|---|---|---|
| B1 | `CutsceneDirector` + `CutsceneId` enum + `FuzzyTransition` (one reusable Timeline + volume-override ramp: chromatic aberration, radial blur, pitch-bend audio). The four fuzzy-transition beats are all **the same asset**, parameterised. | T02 |
| B2 | `Interactable` base + `InteractionRaycaster` (crosshair, look prompt, hold-E) + `ObjectiveHUD`. | T02 |
| B3 | `Memory_CabinNight` dress: table with **5 cups** + bottles, radio on the mantel, **mantel clock reading 00:52**, coat on the chair, blocked stairs, curtained window. Pull furniture from `Assets/Cabin` and `IL3DN`. | T04 |
| B4 | `RadioTuner` (one-axis snap minigame), `DoorInteractable`, `InspectPoint`, `KeyPickup`. | B2 |
| B5 | Stand-from-chair, radio-clears, door-closing (forced head turn + whiteout beyond the open door), call-for-Nick shell (mic prompt hook, door onto storm). | B1, B3, B4 |
| B6 | `Memory_CabinMorning` dress: morning lighting, curtains open, **broken pane + intact grille + shards inside**, body prop in the snow outside, locked door, key on the hook. | T04, B3 |
| B7 | Wake (eyelid open, focus pull) and Spassky's answer, in `Interrogation`. | B1 |

**⊕ Sync, end of Day 1:** `MainMenu → consent → calibration → P1 → M1 (radio, door, yell) → P2 →
M2 → P3 → placeholder ending`. Every cutscene may be a fade and a subtitle. **The whole game must
be walkable tonight.** If it is not, cut per §10 tomorrow morning, not on the 8th.

### Day 2 (8 Aug) — content, then freeze

**Track A**

| ID | Task | Dep |
|---|---|---|
| A9 | `SuspectNameDetector` — single unambiguous name, rejects "maybe X or Y", handles possessives and mid-sentence. | A7 |
| A10 | `EndingSelector` per `STORY_SCRIPT.md` + `SessionScore` accumulation from `emotion`/`prosody` fields already in `SidecarTurnResponse`. | A8, A9 |
| A11 | `OutcomeScreen` — quotes 2–3 verbatim player lines with turn numbers, then the closing card. **Grep the whole build for lie/deception/truth-meter copy** (G6). | A10 |
| A12 | `OutputGuard` — client-side second layer before any reply reaches TTS or subtitles: length cap, strip markdown/stage directions, block persona-leak phrases. **This is ROADMAP S1**, which the never-cut list names explicitly. | A7 |
| A13 | Fault UX: F1 empty transcript → officer re-asks, turn not consumed; F5 mic lost → blocking in-fiction card with Retry; F6 backend unreachable → menu-level "Interrogation service offline" with the exact command. All in-fiction, all labelled. | A7 |
| A14 | Backend integration: swap client fallbacks for real fields **if** §7's B1–B4 landed; `X-FP-Client-Key` header + `backendBaseUrl` on `InterrogationConfig` for the cloud migration. Keep both paths working. | §7 |
| A15 | Settings persistence, pause menu (Esc), quit-to-menu, full session reset (`POST /session/reset`). | A2 |

**Track B**

| ID | Task | Dep |
|---|---|---|
| B8 | Priya screams / body reveal + Ivy and Aaron down the stairs. | B6 |
| B9 | Out into the snow. | B6, B8 |
| B10 | **The carry** — the expensive one. Backward-walking camera at carry height, sway curve, Nick's face in frame, 7 VO lines on the audio track. Budget the whole morning. | B9 |
| B11 | The sofa / Priya dials. | B10 |
| B12 | Accusation flashbacks ×3 — heavily degraded, no dialogue, ~6 s each. Reuse the `FuzzyTransition` volume profile at full strength. | B1 |
| B13 | The four endings. Observation-glass framing means **one camera setup reused four times** with a different character behind the glass — this is why it is affordable. | B1, B7 |
| B14 | Audio pass: fire, wind, storm, door, glass, radio static, phone dial, room tone per scene. Pull from `IL3DN/Audio` first, generate the gaps. | B3, B6 |
| B15 | Lighting + post pass on all three scenes; make M1 night and M2 morning read as unmistakably different times. | B3, B6 |

**⊕ Sync, 8 Aug afternoon**

| ID | Own | Task |
|---|---|---|
| Z1 | ⊕ | **Full playthrough on a machine that is not the developer's.** Twice. Time it. |
| Z2 | ⊕ | Standalone Windows build. Confirm mic works in the build, not just the Editor. |
| Z3 | A | `docs/PRIVACY.md` — what is captured, what leaves the machine (**this inverts after the cloud migration**), what is never written to disk, how to revoke. Never-cut list item. |
| Z4 | B | README third-party table: o3n UMA races, IL3DN, TDG, Cabin pack, Materials Collection, ElevenLabs-generated VO. **G4 is graded.** |
| Z5 | ⊕ | Demo video — real playthrough capture, ≤5 min. **Route it in `docs/DEMO_SCRIPT.md` first:** a straight playthrough at the P2:14 + P3:8 caps, ~3 s latency per turn, plus exploration and ~2 min of cutscenes, overruns 5 minutes before you start. Either script a reduced-turn route or plan the edit deliberately — do not discover this during capture. |
| Z6 | ⊕ | Deck — 6 required sections, ≤15 slides. |
| Z7 | ⊕ | **Freeze 23:59.** Tag it. |

---

## 6. Parallelism — why these two tracks do not collide

| | Person A | Person B |
|---|---|---|
| **Scenes edited** | `MainMenu`, `_Persistent` | `Memory_CabinNight`, `Memory_CabinMorning`, `Interrogation` |
| **Script folders** | `Flow/`, `Voice/`, `Menu/`, `Dialogue/`, `UI/` | `Cutscene/`, `Interaction/`, `CabinNight/` |
| **Assets** | `prompts/`, `Config/`, A's UI prefabs | `Art/`, `CabinNight/`, VO `.wav`s |
| **Talks to** | The backend team | Nobody outside the repo |

Unity `.unity` and `.prefab` files merge terribly. **The scene split is the whole trick** — it is
the same principle the backend team used in their work-split doc, and it is why they are not in
merge hell.

`_Persistent` is what makes the split real rather than aspirational. A's mic indicator, subtitles,
speech prompt, objective HUD, outcome screen and fault cards all have to outlive a scene change and
appear over *B's* scenes; without a persistent scene A would be editing `Interrogation` all week
alongside B. A owns `_Persistent` and every prefab in it; B never opens it. A touches
`Interrogation` exactly once — **in T02, before B starts dressing it** — and never again.

Sync points: **T02** (contracts), **end of Day 1** (walkable game), **8 Aug afternoon** (Z1–Z7).
Between them, no coordination is required.

---

## 7. Asks of the backend team — each with a client fallback

Send these on Day 0. **Every one has a client-side fallback that we build first**, so nothing in
§5 is blocked on their schedule. Their 7–8 Aug is already committed to the consistency tracker.

| ID | Ask | Client fallback (build this now) |
|---|---|---|
| **B0** | Re-run `tools/probe_tts.py` for an API-usable **male** voice for Officer Spassky. Rename the persona in `llm.py`. | Keep Matilda; rename the character to a female officer. Fifteen minutes either way. |
| **B1** | A `phase` form field on `/turn` selecting one of five phase system prompts. | Send the phase prompt as a `scene_instruction` history turn — **`llm.py:71` already supports this shape.** |
| **B2** | `topics_covered: []` and `all_marks_answered: bool` on the turn response. | `StoryMarkTracker` keyword matcher over `transcript`. Cruder, works. |
| **B3** | `named_suspect: "aaron"\|"ivy"\|"priya"\|null` in P3. | `SuspectNameDetector` string match over `transcript`. |
| **B4** | `consistency_score` on the response (their A6 produces it anyway). | `credibility` from marks covered, turn count, and client-detected contradictions on the seven marks. Weaker, and honest about it. |
| **B5** | Output filter between the LLM and TTS (**their S1**). | `OutputGuard` client-side (A12). Belt and braces — build ours regardless. |
| **B6** | Confirm the Cloud Run URL + `X-FP-Client-Key` shape so A14 can wire it. | Local sidecar via `SidecarProcessLauncher`, which already works. |

**Tell them one thing explicitly:** the ending is chosen **client-side**. They do not need to build
an ending endpoint, and we do not want them to.

---

## 8. Security and safety guardrails — where each one lives

Every row maps to a ROADMAP §5 item or a global constraint.

| # | Guardrail | Where it lives | Maps to |
|---|---|---|---|
| 1 | **Mic consent before the first byte is captured.** No capture starts before the card is accepted. | `Voice/MicConsentFlow` (A3) | S8, brief's personal-info requirement |
| 2 | **Persistent mic-active indicator**, whole session. | `UI/MicIndicator` (A5) | S8 |
| 3 | **No audio to disk, ever.** Buffers are in-memory; nothing is written even on error. | `Audio/UtteranceRecorder` — verify, do not add | S8 |
| 4 | **No transcripts logged by default.** F1 overlay shows the last turn in memory only. | `UI/DebugOverlayUI` (verify) | A10 / S8 |
| 5 | **Output filter before anything is spoken or shown.** Length cap, markdown/stage-direction strip, persona-leak block. | `Dialogue/OutputGuard` (A12) | **S1 — never-cut** |
| 6 | **Player speech is data, never instruction.** The client must never route transcript text into a prompt slot; only into the backend's `WITNESS_TRANSCRIPT` block. Name/mark detection reads `transcript` **as a string**. | `PhaseDialogueController`, `SuspectNameDetector` (A7/A9) | **S2 — never-cut** |
| 7 | **No lie/deception/truth language in any visible string.** Includes the internal suspicion score's UI label — call it *pressure* or *scrutiny*, never *truth*. | Grep gate in A11; review every UI string | **G6 — non-negotiable** |
| 8 | **Turn caps per phase** (P2: 14, P3: 8) and a session cap. A stuck phase cannot burn paid credits. | `PhaseDialogueController` (A7) | S6 |
| 9 | **No key in the client build.** `X-FP-Client-Key` comes from `InterrogationConfig` at runtime; the asset ships with the field **empty** and the real value is set on the demo machine. | `InterrogationConfig`, A14 | **G2, S4, S5** |
| 10 | **Every fault is visible and in-fiction.** No silent fallback anywhere. | A13 | G10, graded exception handling |
| 11 | **All character VO is labelled synthetic** in the README and the deck. | Z4 | G10, G4 |
| 12 | **Prompts live in `prompts/` as files.** Currently violated — the persona is a literal at `llm.py:35`. Our phase prompts must not repeat the mistake. | A7 | **G3 — graded deliverable** |

### Two things flagged for the user's decision, not blocked on

- **The "sus meter."** If it is ever shown, it must be labelled as the officer's *pressure* or
  *scrutiny*, not as truth or suspicion-of-lying. Recommendation: **do not show it at all.** It
  reads as a lie detector on a stage, which is the one thing `CONCEPT.md` says the game must never
  look like. The endings communicate it better.
- **The four endings are executions.** An officer deciding who dies off a voice read is a sharper
  scene than `bong.md`'s arrest endings — and it collides with G6 (*"the system never claims to
  detect lies"*) and `bong.md` §10's *"nothing ugly to demo on a stage."* `STORY_SCRIPT.md` is
  written with softer staging (**"taken"**, through observation glass, framing left open) which
  keeps the weight and removes the on-stage risk. **Say if you want the harder version** and it is
  a copy change in four Timeline audio tracks, nothing structural.

---

## 9. Verification

| Check | How |
|---|---|
| Compiles **and Play mode ticks** | T00 — Editor opens, zero console errors, and `Time.frameCount` advances during Play. Both machines. |
| Session survives phase changes | Play through P1 → M1 → P2 and confirm the officer references something said in P1. If he doesn't, T02's session-ID fix regressed. |
| Backend alive | `curl http://127.0.0.1:8765/health` before touching Unity |
| Backend loop intact | `cd Sidecar && python -m unittest discover -s tests -v` → 47 pass |
| Loudness gate | Unit test `LoudnessGate` against synthetic quiet/loud buffers; then in-Editor with a real yell |
| Calibration failure path | Unplug the mic mid-calibration; the device dropdown + Retry must appear |
| Story marks | F1 overlay shows the seven marks filling as you speak; a deliberately evasive playthrough leaves gaps |
| Name detection | Say "maybe Aaron, or maybe Ivy" → must **not** fire. Say "it was Aaron" → must fire |
| All four endings | Four scripted playthroughs from `docs/DEMO_SCRIPT.md` (write it during Z1), one per ending |
| Faults F1/F5/F6 | Stay silent; unplug the mic; kill the sidecar. Each must produce an in-fiction, labelled response |
| G6 grep | `rg -i "lie|lying|deception|truth ?meter|honest" Assets/_Project` returns nothing user-visible |
| No secrets | `git log -p` for `.env`; `InterrogationConfig.asset` ships with an empty client key |
| Clean machine | Z1 — full playthrough on the machine that did not build it, twice |
| Standalone build | Z2 — mic works in the **build**, not only the Editor |

---

## 10. Cut ladder — if the day runs out, cut from the bottom

Each cut is defined so the game stays complete and honest, never broken.

1. **Accusation flashbacks** → a 2 s black frame with the SFX. Cheapest real loss.
2. **Ivy and Aaron down the stairs** → they are already downstairs when control returns.
3. **The carry** → fade to black, VO plays over it, fade up at the sofa. **Keeps all seven
   lines, which are the actual content.**
4. **The radio tuning minigame** → a single E press. The cutscene is the point, not the puzzle.
5. **The mantel clock** as an inspectable → still visible, just not flagged. The time trap weakens.
6. **The four endings** → collapse to still frame + VO + card. `ROADMAP.md` §8 already sanctions
   this exact degradation, labelled.
7. **`Memory_CabinMorning` art polish** → keep the blockout. The clues (door, key, window, grille,
   shards) are non-negotiable — they are what the endings adjudicate against.

**Never cut:** the consent card, the output guard, prompts-as-files, `PRIVACY.md`, the seven story
marks, or the ten-clue ledger. Four of those are graded and two are the game.

**Never fake:** if a cutscene is a still, the deck and video say "still". If the ending selection
runs on the client fallback rather than the backend's consistency score, say that too.
`CONCEPT.md`'s honesty rule is not decoration — the judges run this live.
