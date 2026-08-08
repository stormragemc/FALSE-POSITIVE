# Track A — Unity Editor setup checklist

All of Track A's Day-1 code (A0–A8) is written and lives on the `systems` branch,
merged with `main`. It cannot compile-check or run itself: this checklist is
the remaining work, and it has to happen in the Unity Editor by a human — scene
files, Canvas layout, and ScriptableObject asset creation are not safe to
hand-author blindly from a text tool with no way to open the Editor and verify
the result. Follow this top to bottom; each step names the exact objects and
component wiring needed.

**Do this before anything else:** open the project once and confirm it
compiles with zero console errors. If `MicLevelMeterUI` or the old
`GameBootstrap` component show up as "missing script" anywhere, that is
expected — see step 3.

---

## 1. Create `_Persistent.unity`

`Assets/_Project/Scenes/_Persistent.unity`. This is scene index 0 in Build
Settings, the only scene ever loaded directly (everything else loads
additively through it).

Hierarchy and components (create empty GameObjects, add these components,
wire the serialized fields as noted — most wiring is "point it at the sibling
object with the matching name"):

```
_Persistent
├── Flow
│   ├── GameFlowDirector          <- GameFlowDirector.cs
│   └── SceneRouter                <- SceneRouter.cs
├── BackendHealthProbe
│   ├── BackendHealthProbe.cs
│   └── SidecarProcessLauncher.cs + InterrogationSidecarClient.cs
├── VoiceSystem
│   ├── MicrophoneService.cs
│   ├── VoiceActivityDetector.cs   (wires mic -> MicrophoneService above)
│   ├── UtteranceRecorder.cs       (wires vad -> VoiceActivityDetector above)
│   ├── MicCalibration.cs          (wires mic/vad above)
│   └── LoudnessGate.cs            (wires recorder above)
├── HUD  (Canvas, Render Mode: Screen Space - Overlay, Sort Order 100 — NOT
│         Screen Space - Camera; _Persistent must carry no Camera and no
│         AudioListener, see the note below)
│   ├── ScreenFader                 <- CanvasGroup (full-screen black Image,
│   │                                   alpha 0, Block Raycasts off) + ScreenFader.cs
│   │                                   Canvas sort order 200 (above everything else)
│   ├── MicIndicator                <- MicIndicator.cs (wire mic + vad)
│   ├── SpeechPrompt                <- SpeechPrompt.cs
│   ├── SubtitleUI                  <- SubtitleUI.cs
│   ├── ObjectiveHud                <- ObjectiveHud.cs
│   ├── ConsentPanel                <- MicConsentFlow.cs (wire mic)
│   ├── CalibrationPanel            <- CalibrationPanelUI.cs (wire calibration/mic/vad)
│   ├── SettingsPanelRoot           <- SettingsPanel.cs (wire mic -> VoiceSystem/
│   │                                   MicrophoneService above — same scene,
│   │                                   plain Inspector reference is fine;
│   │                                   this panel is reused as the Day-2
│   │                                   pause menu too, A15)
│   ├── DebugOverlayPanel           <- DebugOverlayUI.cs (F1 toggles it)
│   └── FaultCard, OutcomeScreen    <- empty placeholders, A13/A11 (Day 2)
└── EventSystem                     <- standard Unity EventSystem prefab
```

Then, on the `GameFlowDirector` component, wire every serialized field to its
sibling above: `config` -> `Config/InterrogationConfig.asset`, `mic`/`vad`/
`recorder`/`sidecar`/`loudnessGate`/`fader`/`subtitles`/`prompt`/`objectives`/
`sceneRouter`/`consentFlow`/`calibration`/`calibrationPanel`/`debugOverlay`/
`settingsPanel` -> the matching objects above, `memoryFlagCatalog` -> the
asset from step 4. Leave `mainMenuSceneName`/`interrogationSceneName`/
`nightSceneName`/`morningSceneName` at their defaults (`MainMenu`,
`Interrogation`, `Memory_CabinNight`, `Memory_CabinMorning`) — they must
match the scene file names exactly.

Every consumer of `DebugOverlayUI` or `SettingsPanel` that lives outside
`_Persistent` (`PhaseDialogueController` in `Interrogation.unity`,
`MainMenuController` in `MainMenu.unity`) reaches them through
`GameFlowDirector.DebugOverlay` / `GameFlowDirector.SettingsPanel` at
runtime, not a scene-local reference — nothing to wire on those two
components for this.

**Why no Camera/AudioListener here:** Interrogation and both memory scenes
each carry their own camera + listener; SceneRouter deactivates every
inactive scene's roots so only one is ever live, but `_Persistent`'s own
objects are never deactivated. A camera or listener living here would be a
second one active at all times.

## 2. Modify `Interrogation.unity`

- Add a new GameObject `SceneBinder` with `InterrogationSceneBinder.cs`. Wire
  `dialogueManager` -> the existing `DialogueManager` object, `playerState`
  -> the existing `Player` object's `PlayerStateController`. Its
  `debugOverlay` field is scene-local (`_Persistent`'s panel — wired there
  instead, see step 1); leave it empty if the component ever exposes one.
- Add `PhaseDialogueController.cs` next to `DialogueManager`. Wire `prompts`
  -> the `PhasePromptSet` asset (step 5), `storyMarksSource` ->
  `Assets/_Project/Prompts/story_marks.txt`, `binder` -> the `SceneBinder`
  object above. It reads `DebugOverlayUI` through
  `GameFlowDirector.DebugOverlay` at runtime — nothing else to wire.
- On the existing `GameSystems` GameObject: the old `GameBootstrap` component
  is now a broken "missing script" reference (its file was deleted and
  replaced by `Core/BackendHealthProbe.cs`, which lives in `_Persistent`
  instead per the split in step 1). Delete this GameObject's `GameBootstrap`
  component. Also remove `InterrogationSidecarClient` and
  `SidecarProcessLauncher` from this scene — they moved to `_Persistent`'s
  `BackendHealthProbe` object in step 1.
- On `DialogueManager`: its `sidecarClient`/`vad`/`recorder` serialized
  fields are gone (replaced by `BindServices`, called at runtime by
  `InterrogationSceneBinder`) — nothing to wire here now except the
  scene-local `copVoice`/`copMouth`/`fillerSource`/`fillerClips`, which are
  unchanged.
- On `PlayerStateController`: its `fader` field is gone (replaced by
  `SetFader`, called at runtime by `InterrogationSceneBinder`) — nothing to
  wire here now.
- Delete the old `MicMeterFill`/`MicMeterBackground`/`MicLevelMeterUI`
  objects if still present — superseded by `_Persistent`'s `MicIndicator`.

## 3. `MainMenu.unity` (generated by ProjectBootstrapBuilder)

`Assets/_Project/Scenes/MainMenu.unity` is generated wholesale by
`FalsePositive.Editor.ProjectBootstrapBuilder.BuildMainMenuScene()` — nothing
below is hand-built, and hand edits are wiped on every regeneration
(`Tools ▸ False Positive ▸ Bootstrap ▸ Build Everything`, or the
`2 - Build Main Menu Scene` item alone). Read this section as a description
of what the builder produces, not a set of manual steps.

```
MainMenu
├── Backdrop                  <- 3D diegetic scene, its own perspective camera
│   ├── Main Camera             (MenuCameraDrift — very slow, never-repeating
│   │                            drift), Cold Moonlight, WindowGlow, WindZone
│   ├── Cabin                   Cabin_v2 prefab instance, colliders stripped
│   └── Snow                    ParticleSystem, SnowParticle_Transparent.mat
│                                (everything forced onto the MenuBackdrop
│                                layer, which is the camera's exclusive
│                                culling mask)
├── Canvas                    <- TextMeshPro title/menu, vignette, BuildStamp
│   └── Begin / Offline Demo / How to Play / Settings / Credits / Quit
├── MenuOverlay                <- sortOrder 50, above Canvas
│   ├── QuitConfirmWindow       (Cancel is the default selection)
│   ├── Credits window          scrollable, copy sourced from docs/CONCEPT.md
│   │                            and docs/PRIVACY.md
│   └── Controls window         "How to play"
└── MainMenuController.cs     <- routes buttons to the windows above and to
                                 GameFlowDirector.SettingsPanel (which still
                                 lives in _Persistent — see step 1 — so there
                                 is no settingsPanel field to wire here)
```

TMP Essentials must be imported once before this scene builds — see
`FalsePositive.Editor.TmpEssentialsBootstrap` (`Tools ▸ False Positive ▸
Bootstrap ▸ 0 - Import TMP Essentials`); `BuildMainMenuScene()` throws if the
default font isn't set.

Storm ambience: a looping `AudioSource` on `Backdrop`, wind/snow bed
(`Assets/_Project/Art/Audio/SFX/menu_storm_bed.mp3` — logs a warning and
plays silent if the clip isn't present yet).

## 4. Create the `MemoryFlagCatalog` asset

`Assets > Create > False Positive > Memory Flag Catalog`, save under
`Assets/_Project/Config/`. Set its `source` field to
`Assets/_Project/Prompts/memory_flags.txt`. Wire it to `GameFlowDirector.
memoryFlagCatalog` (step 1).

## 5. Create the `PhasePromptSet` asset

`Assets > Create > False Positive > Phase Prompt Set`, save under
`Assets/_Project/Config/`. Set `caseFile` -> `Prompts/case_file.txt`,
`p2Recall` -> `Prompts/phase_p2_recall.txt`, `p3Verdict` ->
`Prompts/phase_p3_verdict.txt`. Wire it to `PhaseDialogueController.prompts`
(step 2).

## 6. Build Settings

`File > Build Settings`, replace the scene list (remove
`Assets/Scenes/SampleScene.unity`) with, in order:

```
0  Assets/_Project/Scenes/_Persistent.unity
1  Assets/_Project/Scenes/MainMenu.unity
2  Assets/_Project/Scenes/Interrogation.unity
3  Assets/_Project/Scenes/NobodyWentOut_CabinNight.unity   (Person B renames/
                                                              duplicates this
                                                              into the two
                                                              memory scenes
                                                              per T04 — add
                                                              Memory_CabinNight
                                                              and
                                                              Memory_CabinMorning
                                                              here once T04 lands)
```

## 7. Play-mode verification (the actual A0 exit criteria)

Press Play from `_Persistent`. In order:

1. Zero console errors on load.
2. `MainMenu` appears automatically (GameFlowDirector.Start() calls
   `GoToPhase(Menu)` itself — no manual scene load needed).
3. Click Play -> the consent card appears with real device names in the
   dropdown.
4. Click Enable -> calibration card appears, level meter moves when you
   speak, and it completes on its own in a normal room.
5. `Interrogation` scene fades in, camera is seated, "Who are you? Where am
   I?" prompt appears.
6. Say anything -> a screen-fader blink plays (no cutscene built yet) and the
   game is now sitting in `M1_Night` with dialogue suspended (expected —
   Person B hasn't built this scene's gameplay yet; this is exactly Day-1
   exit criterion #12's "walkable with placeholders").
7. Press **F2** repeatedly (Editor/Development builds only) — the phase
   advances every press with a full fade cycle each time, reaching
   `Outcome` from `Menu`. Watch the Console: `SessionId` printed on every
   press must be identical throughout.
8. Open the F1 debug overlay once in `P2_Recall` (reach it via F2, or play
   through naturally) — the seven story marks and turn counter should be
   visible and update as the officer's turns complete.
9. `git status Assets/_Project/Config/` must be clean after all of the
   above, including after moving the mouse during a seated turn (confirms
   the ScriptableObject-write trap from A2/A4 was actually avoided).

If any of 1–9 fails, that is a real bug in the code delivered here, not a
wiring mistake — file it against the corresponding script.
